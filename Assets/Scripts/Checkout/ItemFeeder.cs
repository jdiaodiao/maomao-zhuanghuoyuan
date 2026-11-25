using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 上游收银猫：按订单逐件"扫描"（嘀一声、小票加一行），把商品放上传送带。
    /// 入口被占住时会等待。
    /// </summary>
    public class ItemFeeder : MonoBehaviour
    {
        public Vector3 SpawnCenter;          // 传送带上表面的生成点
        public Vector3 BeltDirection = Vector3.right;
        public float Interval = 1.6f;
        public ReceiptPrinter Receipt;
        public Transform ScannerLight;

        readonly Queue<ItemDefinition> _queue = new Queue<ItemDefinition>();
        public readonly List<GroceryItem> Spawned = new List<GroceryItem>();
        float _timer;
        float _flash;
        System.Random _rng = new System.Random();

        public int Remaining => _queue.Count;
        public bool Done => _queue.Count == 0;

        public void Begin(IEnumerable<ItemDefinition> items, int seed)
        {
            _queue.Clear();
            Spawned.Clear();
            foreach (var d in items) _queue.Enqueue(d);
            _timer = 1.2f;
            _rng = new System.Random(seed);
        }

        public void Stop() { _queue.Clear(); }

        void Update()
        {
            if (ScannerLight != null)
            {
                _flash = Mathf.Max(0, _flash - Time.deltaTime * 4f);
                ScannerLight.localScale = new Vector3(ScannerLight.localScale.x, 0.004f + _flash * 0.01f, ScannerLight.localScale.z);
            }

            if (_queue.Count == 0) return;
            _timer -= Time.deltaTime;
            if (_timer > 0) return;

            var def = _queue.Peek();
            Vector3 size = def.Size;
            // 让长边沿传送带方向，扁平的东西平放
            Quaternion rot = Quaternion.identity;
            if (size.z > size.x * 1.2f) rot = Quaternion.Euler(0, 90, 0);
            rot = Quaternion.AngleAxis((float)(_rng.NextDouble() * 20 - 10), Vector3.up) * rot;
            Vector3 rotated = rot * size;
            Vector3 half = new Vector3(Mathf.Abs(rotated.x), size.y, Mathf.Abs(rotated.z)) * 0.5f;

            Vector3 pos = SpawnCenter + Vector3.up * (half.y + 0.004f) + BeltDirection * half.x;
            // 入口有东西就等
            if (Physics.CheckBox(pos + Vector3.up * 0.004f, half + new Vector3(0.03f, -0.002f, 0.0f), rot, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                _timer = 0.25f;
                return;
            }

            _queue.Dequeue();
            var item = ItemFactory.Create(def, pos, rot);
            Spawned.Add(item);
            Sfx.Play("beep", 0.8f);
            _flash = 1f;
            if (Receipt != null) Receipt.AddLine(def.Name, def.Price);
            _timer = Interval * (0.8f + (float)_rng.NextDouble() * 0.5f);
            if (_queue.Count == 0 && Receipt != null) Receipt.PrintTotal();
        }
    }
}
