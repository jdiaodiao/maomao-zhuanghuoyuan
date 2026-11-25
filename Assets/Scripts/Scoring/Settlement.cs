using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CatMart
{
    public class BagResult
    {
        public Bag Bag;
        public BagSpec Spec;
        public int ItemCount;
        public float Mass;
        public float Imbalance;
        public bool Overweight;
        public bool Torn;
        public float Factor;
        public string Note;
    }

    public class OrderResult
    {
        public float ItemsTotal, BasePay;
        public float ReqFactor, BalanceFactor, PatienceFactor, MissingFactor, Multiplier;
        public float Pay, DamagePenalty, Net;
        public int Stars;
        public bool ReqMatch;
        public string ReqNote;
        public int Missing;
        public List<string> MissingNames = new List<string>();
        public List<string> DamagedNames = new List<string>();
        public List<BagResult> Bags = new List<BagResult>();
    }

    /// <summary>结算规则（详见 Docs/GDD_Chapter1.md）。</summary>
    public static class Settlement
    {
        public const float Commission = 0.10f;

        public static float BalanceFactor(float r)
        {
            if (r <= 0.15f) return 1.3f;
            if (r <= 0.3f) return Mathf.Lerp(1.3f, 1.0f, (r - 0.15f) / 0.15f);
            if (r <= 0.6f) return Mathf.Lerp(1.0f, 0.6f, (r - 0.3f) / 0.3f);
            return 0.5f;
        }

        public static OrderResult Evaluate(CustomerOrder order, IList<GroceryItem> items, IList<Bag> bags,
            System.Func<GroceryItem, bool> inCart, float patience01)
        {
            var r = new OrderResult { ItemsTotal = order.ItemsTotal };
            r.BasePay = r.ItemsTotal * Commission;

            // 1) 归属
            var assigned = new Dictionary<Bag, List<GroceryItem>>();
            foreach (var b in bags) assigned[b] = new List<GroceryItem>();
            foreach (var it in items)
            {
                if (it == null) continue;
                if (it.IsDamaged) { r.DamagePenalty += it.Def.Price; r.DamagedNames.Add(it.Def.Name); }
                if (it.IsDestroyed) continue;

                Bag home = bags.FirstOrDefault(b => b.Contains(it));
                if (home != null) { assigned[home].Add(it); continue; }
                if (it.IsDamaged) continue; // 已经赔过了
                if (it.Def.Bulky && inCart(it)) continue;
                r.Missing++;
                r.MissingNames.Add(it.Def.Name + (inCart(it) ? "（非大件不能直接放推车）" : ""));
            }

            // 2) 每个袋子
            float massSum = 0, weighted = 0;
            foreach (var kv in assigned)
            {
                if (kv.Value.Count == 0) continue;
                var b = kv.Key;
                var st = b.Compute();
                var br = new BagResult
                {
                    Bag = b, Spec = b.Spec, ItemCount = kv.Value.Count, Mass = st.Mass,
                    Imbalance = st.Imbalance, Overweight = st.Overweight, Torn = b.Torn
                };
                br.Factor = BalanceFactor(st.Imbalance);
                br.Note = st.Imbalance <= 0.15f ? "重心完美" : st.Imbalance <= 0.3f ? "略有偏斜" : st.Imbalance <= 0.6f ? "一边重一边轻" : "严重偏重";
                if (b.Torn) { br.Factor = Mathf.Min(br.Factor, 0.5f); br.Note = "袋子被刺破"; }
                if (st.Overweight) { br.Factor = Mathf.Min(br.Factor, 0.4f); br.Note = $"超重 ({st.Mass:0.0}/{b.Spec.CapacityKg}kg) 提起时破了"; }
                r.Bags.Add(br);
                massSum += Mathf.Max(0.05f, st.Mass);
                weighted += br.Factor * Mathf.Max(0.05f, st.Mass);
            }
            r.BalanceFactor = massSum > 0 ? weighted / massSum : 0.5f;

            // 3) 规格
            var used = r.Bags.Select(b => b.Spec).ToList();
            if (used.Count == 0)
            {
                r.ReqFactor = 0.6f; r.ReqNote = "一个袋子都没用";
            }
            else if (used.Any(s => s.Material != order.Material))
            {
                r.ReqFactor = 0.6f; r.ReqNote = $"顾客要的是{(order.Material == BagMaterial.Paper ? "纸袋" : "塑料袋")}";
            }
            else
            {
                bool match = true;
                foreach (BagSize s in new[] { BagSize.Small, BagSize.Medium, BagSize.Large })
                {
                    int want = order.Bags.TryGetValue(s, out int w) ? w : 0;
                    int got = used.Count(u => u.Size == s);
                    if (want != got) match = false;
                }
                r.ReqMatch = match;
                r.ReqFactor = match ? 1.2f : 0.8f;
                string usedText = string.Join(" + ", used.GroupBy(u => u.Size).OrderByDescending(g => g.Key)
                    .Select(g => $"{g.Count()}个{g.First().Name}"));
                r.ReqNote = match ? "完全符合要求" : $"要求 {order.BagRequestText}，实际 {usedText}";
            }

            // 4) 耐心 & 漏装
            r.PatienceFactor = patience01 >= 0.5f ? 1f : Mathf.Lerp(0.7f, 1f, patience01 / 0.5f);
            r.MissingFactor = Mathf.Pow(0.9f, r.Missing);

            r.Multiplier = r.ReqFactor * r.BalanceFactor * r.PatienceFactor * r.MissingFactor;
            r.Pay = r.BasePay * r.Multiplier;
            r.Net = r.Pay - r.DamagePenalty;
            r.Stars = r.Multiplier >= 1.4f ? 3 : r.Multiplier >= 1.0f ? 2 : r.Multiplier >= 0.6f ? 1 : 0;
            if (r.DamagedNames.Count > 0) r.Stars = Mathf.Max(0, r.Stars - 1);
            return r;
        }
    }
}
