using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 第一章流程（关卡模式）：每天 = 一关。开店 → 顾客 1..4 → 当日结算。
    /// 今日收入达到目标金额才算过关（收入存进钱包，解锁下一天）；没达到就重来这一天。
    /// 每天最后一位顾客是“记忆单”：没有光边和属性说明，商品都是今天前几单出现过的。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public enum State { Intro, Serving, Settlement, DaySummary }

        public const int CustomersPerDay = 4;

        /// <summary>目标金额 = 今天所有订单的基础工钱 × 这个比例（越往后越高）。</summary>
        public static float GoalRatio(int day) => Mathf.Min(1.35f, 0.9f + 0.1f * (day - 1));
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
        /// <summary>属性提示（商品光边 + 悬停属性说明）。记忆单关掉，靠记忆。</summary>
        public bool TraitHints = true;

        /// <summary>今天的订单（开店时一次生成好，目标金额按它们算）。</summary>
        public readonly List<CustomerOrder> DayOrders = new List<CustomerOrder>();
        public float DayGoal { get; private set; }
        public float DayEarned { get; private set; }
        public bool DayPassed => DayEarned >= DayGoal - 0.001f;
        public bool IsMemoryOrder => Order != null && Order.Memory;
        /// <summary>下一位顾客是不是记忆单（结算界面提前告诉玩家）。</summary>
        public bool NextIsMemory => CustomerIndex < DayOrders.Count && DayOrders[CustomerIndex].Memory;

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
            IsTest = false;
            CustomerIndex = 0;
            DayResults.Clear();
            DayEarned = 0;
            GenerateDayOrders();
            NextCustomer();
        }

        /// <summary>测试关：订单来自 TestLevel（这次改动要测的东西），不存档、不影响钱包和关卡进度。</summary>
        public bool IsTest { get; private set; }

        public void StartTestLevel()
        {
            IsTest = true;
            CustomerIndex = 0;
            DayResults.Clear();
            DayEarned = 0;
            DayOrders.Clear();
            int seed = Random.Range(0, 100000);
            foreach (var spec in TestLevel.Orders)
            {
                var o = OrderGenerator.Custom(_ticket++, spec.Items.Select(ItemCatalog.Get), spec.Material, seed + DayOrders.Count * 131, spec.Patience);
                o.Memory = spec.Memory;
                DayOrders.Add(o);
            }
            ComputeGoal(1);
            NextCustomer();
        }

        /// <summary>这一单在测试关里要测什么（不是测试关就是 null）。</summary>
        public string TestNote => IsTest && CustomerIndex < TestLevel.Orders.Length && Phase == State.Serving ? TestLevel.Orders[CustomerIndex].Note : null;

        /// <summary>回到开场界面（测试关结束用）。</summary>
        public void BackToTitle()
        {
            ClearTable();
            IsTest = false;
            Order = null;
            DayOrders.Clear();
            DayResults.Clear();
            DayEarned = 0;
            Phase = State.Intro;
            Player.InputEnabled = false;
            PlayerController.LockCursor(false);
            Store.Customer.gameObject.SetActive(false);
        }

        void ComputeGoal(int day)
        {
            float basePay = DayOrders.Sum(o => o.ItemsTotal * Settlement.Commission * (o.Memory ? Settlement.MemoryBonus : 1f));
            DayGoal = Mathf.Max(1f, Mathf.Round(basePay * GoalRatio(day) * 2f) / 2f); // 取整到 $0.5
        }

        /// <summary>一次生成今天的 4 单；最后一单只从前几单出现过的商品里挑。</summary>
        void GenerateDayOrders()
        {
            DayOrders.Clear();
            var seen = new HashSet<ItemDefinition>();
            int daySeed = Day * 7919 + Random.Range(0, 100000);
            for (int i = 0; i < CustomersPerDay; i++)
            {
                bool memory = i == CustomersPerDay - 1;
                var o = OrderGenerator.Generate(_ticket++, i, daySeed + i * 131, memory ? seen : null);
                o.Memory = memory;
                foreach (var d in o.Items) seen.Add(d);
                DayOrders.Add(o);
            }
            ComputeGoal(Day);
        }

        public void NextCustomer()
        {
            ClearTable();
            if (CustomerIndex >= (DayOrders.Count > 0 ? DayOrders.Count : CustomersPerDay))
            {
                Phase = State.DaySummary;
                Player.InputEnabled = false;
                PlayerController.LockCursor(false);
                Store.Customer.gameObject.SetActive(false);
                return;
            }

            if (DayOrders.Count == 0) GenerateDayOrders();
            Order = DayOrders[CustomerIndex];
            int seed = Order.Seed;
            TraitHints = !Order.Memory;
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
            if (Order.Memory) ShowToast("记忆单！这一单没有光边和属性说明，商品都是今天见过的，靠记忆喵", 5f);
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
                it => StoreBuilder.InCart(Store.Cart, it), Patience01, Order.Memory);
            DayResults.Add(LastResult);
            DayEarned += LastResult.Net;

            Phase = State.Settlement;
            Player.InputEnabled = false;
            PlayerController.LockCursor(false);
            Store.Customer.SetMood(LastResult.Stars >= 2);
            Store.Customer.Speech.text = LastResult.Stars >= 2 ? "谢谢喵~ 装得真好!" : LastResult.Stars == 1 ? "嗯…还行吧喵" : "这装的什么呀喵!!";
            Sfx.Play(LastResult.Net >= 0 ? "cash" : "rip");
            Sfx.Play("ding", 0.7f);
            CustomerIndex++;
        }

        /// <summary>今日结算界面的按钮：过关 → 收入存进钱包，进入下一天；没过 → 重来这一天（这天的收入作废）。</summary>
        public void EndDayAndContinue()
        {
            if (IsTest) { StartTestLevel(); return; } // 测试关：再测一次，不存档
            if (DayPassed)
            {
                Money += DayEarned;
                Day++;
                PlayerPrefs.SetFloat(MoneyKey, Money);
                PlayerPrefs.SetInt(DayKey, Day);
                PlayerPrefs.Save();
            }
            StartDay();
        }

        // 自动测试用
        public void DebugJumpTo(int customerIndex) { CustomerIndex = customerIndex; NextCustomer(); }
        public void DebugAddEarned(float v) => DayEarned += v;

        public void ResetProgress()
        {
            Money = 0;
            Day = 1;
            DayEarned = 0;
            DayOrders.Clear();
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

        public void OnItemTipped(GroceryItem item)
        {
            ShowToast($"{item.Def.Name} 倒了！不能倒的东西要一直立着放（结算扣分）", 3f);
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
