using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CatMart
{
    /// <summary>第一章流程：开店 → 顾客1..N → 当日结算。</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public enum State { Intro, Serving, Settlement, DaySummary }

        public const int CustomersPerDay = 5;
        const string MoneyKey = "CatMart.Money";
        const string DayKey = "CatMart.Day";

        public StoreRefs Store;
        public PlayerController Player;

        public State Phase { get; private set; } = State.Intro;
        public CustomerOrder Order { get; private set; }
        public OrderResult LastResult { get; private set; }
        public readonly List<OrderResult> DayResults = new List<OrderResult>();

        public int Day { get; private set; } = 1;
        public int CustomerIndex { get; private set; }
        public float Money { get; private set; }
        public float Patience { get; private set; }
        public float PatienceMax { get; private set; } = 1;
        public float Patience01 => PatienceMax > 0 ? Mathf.Clamp01(Patience / PatienceMax) : 0;
        public bool ShowReceipt;
        public bool ShowHelp = true;

        public readonly Bag[] Slots = new Bag[4];
        int _ticket = 1;
        int _lastSpawnCount;

        string _toast;
        float _toastTime;
        public string Toast => _toastTime > 0 ? _toast : null;

        public void ShowToast(string s, float seconds = 2.5f) { _toast = s; _toastTime = seconds; }

        public void Init(StoreRefs store, PlayerController player)
        {
            I = this;
            Store = store;
            Player = player;
            Money = PlayerPrefs.GetFloat(MoneyKey, 0f);
            Day = PlayerPrefs.GetInt(DayKey, 1);
            _ticket = Random.Range(1000, 9000);
            Player.InputEnabled = false;
            Store.Customer.gameObject.SetActive(false);
        }

        public IList<GroceryItem> OrderItems => Store.Feeder.Spawned;

        // ------------------------------------------------------------ 流程
        public void StartDay()
        {
            CustomerIndex = 0;
            DayResults.Clear();
            NextCustomer();
        }

        public void NextCustomer()
        {
            ClearTable();
            if (CustomerIndex >= CustomersPerDay)
            {
                Phase = State.DaySummary;
                Player.InputEnabled = false;
                PlayerController.LockCursor(false);
                Store.Customer.gameObject.SetActive(false);
                return;
            }

            int seed = Day * 7919 + CustomerIndex * 131 + Random.Range(0, 100000);
            Order = OrderGenerator.Generate(_ticket++, CustomerIndex, seed);
            var c = Store.Customer;
            c.gameObject.SetActive(true);
            c.SetFur(Order.Fur);
            c.SetMood(true);
            c.Speech.text = $"{Order.CustomerName}: {Order.Speech}";
            Store.Receipt.Begin(Order.Ticket, Order.CustomerName);
            Store.Feeder.Begin(Order.Items, seed);
            _lastSpawnCount = 0;
            PatienceMax = Patience = Order.PatienceSeconds;
            Phase = State.Serving;
            Player.InputEnabled = true;
            PlayerController.LockCursor(true);
            Sfx.Play("meow", 0.8f);
        }

        public void FinishOrder()
        {
            if (Phase != State.Serving) return;
            if (!Store.Feeder.Done)
            {
                ShowToast($"商品还没传送完（还剩 {Store.Feeder.Remaining} 件）");
                return;
            }
            Player.Release();
            LastResult = Settlement.Evaluate(Order, OrderItems, Slots.Where(b => b != null).ToList(),
                it => StoreBuilder.InCart(Store.Cart, it), Patience01);
            DayResults.Add(LastResult);
            Money += LastResult.Net;
            PlayerPrefs.SetFloat(MoneyKey, Money);
            PlayerPrefs.Save();

            Phase = State.Settlement;
            Player.InputEnabled = false;
            PlayerController.LockCursor(false);
            Store.Customer.SetMood(LastResult.Stars >= 2);
            Store.Customer.Speech.text = LastResult.Stars >= 2 ? "谢谢喵~ 装得真好!" : LastResult.Stars == 1 ? "嗯…还行吧喵" : "这装的什么呀喵!!";
            Sfx.Play(LastResult.Net >= 0 ? "cash" : "rip");
            Sfx.Play("ding", 0.7f);
            CustomerIndex++;
        }

        public void EndDayAndContinue()
        {
            Day++;
            PlayerPrefs.SetInt(DayKey, Day);
            PlayerPrefs.Save();
            StartDay();
        }

        public void ResetProgress()
        {
            Money = 0;
            Day = 1;
            PlayerPrefs.DeleteKey(MoneyKey);
            PlayerPrefs.DeleteKey(DayKey);
        }

        void ClearTable()
        {
            Player.Release();
            foreach (var it in Store.Feeder.Spawned) if (it != null) Destroy(it.gameObject);
            Store.Feeder.Spawned.Clear();
            Store.Feeder.Stop();
            // 散落的其他商品（以防万一）
            foreach (var it in GroceryItem.Active.ToArray()) Destroy(it.gameObject);
            for (int i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] != null) Destroy(Slots[i].gameObject);
                Slots[i] = null;
            }
            DamageVisuals.ClearDebris();
        }

        // ------------------------------------------------------------ 袋子
        public void SpawnBag(BagSpec spec)
        {
            if (Phase != State.Serving) return;
            int slot = System.Array.IndexOf(Slots, null);
            if (slot < 0) { ShowToast("打包台放不下更多袋子了（看着空袋子按 X 收回）"); return; }
            var bag = BagFactory.Create(spec, Store.BagSlots[slot], Quaternion.identity);
            bag.Slot = slot;
            Slots[slot] = bag;
            Sfx.Play("bag");
        }

        public void RemoveBag(Bag bag)
        {
            if (bag == null || Phase != State.Serving) return;
            if (bag.Contents().Count > 0) { ShowToast("袋子里还有东西，不能收回"); return; }
            Slots[bag.Slot] = null;
            Destroy(bag.gameObject);
            Sfx.Play("bag", 0.6f);
        }

        public void OnItemDamaged(GroceryItem item)
        {
            ShowToast($"{item.Def.Name} 损坏！要赔 {U.Money(item.Def.Price)}", 3f);
        }

        // ------------------------------------------------------------ 每帧
        void Update()
        {
            if (_toastTime > 0) _toastTime -= Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.Tab)) ShowReceipt = !ShowReceipt;
            if (Input.GetKeyDown(KeyCode.H)) ShowHelp = !ShowHelp;

            if (Phase != State.Serving) return;

            Patience = Mathf.Max(0, Patience - Time.deltaTime);
            Store.Customer.SetPatience(Patience01);
            if (Patience <= 0 && Store.Customer.MouthHappy.gameObject.activeSelf)
            {
                Store.Customer.SetMood(false);
                Store.Customer.Speech.text = $"{Order.CustomerName}: 快一点喵！！";
                Sfx.Play("meow", 1f);
            }

            ReturnLostItems();

            if (Store.Feeder.Spawned.Count != _lastSpawnCount)
            {
                _lastSpawnCount = Store.Feeder.Spawned.Count;
                if (Store.Cashier != null) Store.Cashier.ScanAnim = 0.4f;
            }
        }

        // ------------------------------------------------------------ 掉到够不着的地方 → 顾客帮忙捡回来
        readonly Dictionary<GroceryItem, float> _lostTimers = new Dictionary<GroceryItem, float>();

        /// <summary>玩家走不过去的区域（打包台对面、两端外侧）的地面。</summary>
        static bool Unreachable(Vector3 p) =>
            p.y < 0.45f && (p.z > Layout.LaneZ + 0.33f || p.x > 2.4f || p.x < Layout.BeltStartX - 0.1f);

        void ReturnLostItems()
        {
            foreach (var it in Store.Feeder.Spawned)
            {
                if (it == null || it.IsDestroyed || it.Held) continue;
                if (!Unreachable(it.WorldCom) || StoreBuilder.InCart(Store.Cart, it)) { _lostTimers.Remove(it); continue; }
                _lostTimers.TryGetValue(it, out float t);
                t += Time.deltaTime;
                _lostTimers[it] = t;
                if (t < 1.2f) continue;

                // 放回传送带靠近末端的位置，由传送带送回来
                _lostTimers.Remove(it);
                var rb = it.Rb;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                Vector3 pos = new Vector3(Layout.BeltEndX - 0.5f, Layout.CounterTop + it.Def.Size.y / 2 + 0.03f, Layout.LaneZ);
                rb.position = pos;
                rb.rotation = Quaternion.identity;
                it.transform.SetPositionAndRotation(pos, Quaternion.identity);
                Sfx.Play("meow", 0.5f);
                ShowToast($"{Order.CustomerName} 帮你把掉下去的 {it.Def.Name} 捡回来了，放在传送带上");
                break; // 每帧最多一件，避免叠在一起
            }
        }

        // ------------------------------------------------------------ 统计（HUD 用）
        public int DeliveredCount => Store.Feeder.Spawned.Count;
        public int BaggedCount
        {
            get
            {
                int n = 0;
                foreach (var it in Store.Feeder.Spawned)
                {
                    if (it == null || it.IsDestroyed) continue;
                    if (Slots.Any(b => b != null && b.Contains(it))) n++;
                    else if (it.Def.Bulky && StoreBuilder.InCart(Store.Cart, it)) n++;
                }
                return n;
            }
        }
        public int DamagedCount => Store.Feeder.Spawned.Count(it => it != null && it.IsDamaged);
    }
}
