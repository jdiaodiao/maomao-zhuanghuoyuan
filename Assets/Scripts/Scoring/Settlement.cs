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
        public int Unstable;
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
        public float ToppleFactor = 1f;
        /// <summary>小费（按顾客剩余耐心分档；弄坏/漏装就没有）。</summary>
        public float Tip;
        public int TipTier;            // 0 = 没有，1..3 = 档次
        public string TipNote;
        /// <summary>记忆单（没有属性提示）的奖励倍率。</summary>
        public float MemoryFactor = 1f;
        public List<string> TippedNames = new List<string>();
        public List<string> MissingNames = new List<string>();
        public List<string> DamagedNames = new List<string>();
        public List<BagResult> Bags = new List<BagResult>();
    }

    /// <summary>结算规则（详见 Docs/GDD_Chapter1.md，游戏里开场「计分规则」页面也会显示）。</summary>
    public static class Settlement
    {
        public const float Commission = 0.10f;
        /// <summary>弄坏商品的赔偿 = 商品价格 × 这个比例。</summary>
        public const float DamageRate = 0.5f;
        public static float DamageCost(ItemDefinition d) => d.Price * DamageRate;

        // ---- 规格 ----
        public const float ReqMatch = 1.2f;          // 材质、尺寸、个数完全对
        public const float ReqSizeWrong = 0.9f;      // 个数或尺寸不对
        public const float ReqMaterialWrong = 0.95f; // 材质不对（两样都不对就相乘）

        // ---- 平衡（每个袋子算一个系数，多个袋子取平均）----
        public const float BalanceBest = 1.2f, BalanceWorst = 0.8f;
        /// <summary>每件质心高出袋口的商品，该袋系数再 ×0.95（最低 ×0.75）。</summary>
        public const float UnstablePerItem = 0.95f, UnstableFloor = 0.75f;
        /// <summary>袋子被扎破 / 超重提起时破了：该袋系数最多这么多。</summary>
        public const float BrokenBagCap = 0.8f;

        // ---- 漏装 ----
        public const float MissingPerItem = 0.9f, NoMissing = 1.2f;

        /// <summary>每件翻倒的"不能倒"商品，总倍率 ×0.9，最低 ×0.75。</summary>
        public const float TippedPerItem = 0.9f, TippedFloor = 0.75f;
        /// <summary>记忆单（每天最后一位顾客，没有光边和属性说明）的奖励倍率。</summary>
        public const float MemoryBonus = 1.2f;
        /// <summary>顾客等到没耐心了 → 工钱 ×0.85。</summary>
        public const float ImpatientFactor = 0.85f;

        /// <summary>星星：总倍率（不算记忆单加成）达到这些值。</summary>
        public const float Star3 = 1.2f, Star2 = 1.0f, Star1 = 0.7f;

        /// <summary>小费档次：剩余耐心 ≥ Min 时给 订单总价 × Rate 的小费。</summary>
        public static readonly (float Min, float Rate, string Name)[] TipTiers =
        {
            (0.5f, 0.05f, "超快！大方的小费"),
            (0.25f, 0.025f, "挺快的小费"),
            (0.0001f, 0.01f, "一点点小费"),
        };

        /// <summary>单个袋子的平衡系数。r = 重心水平偏移 / 半宽。</summary>
        public static float BalanceFactor(float r)
        {
            if (r <= 0.15f) return BalanceBest;
            if (r <= 0.3f) return Mathf.Lerp(BalanceBest, 1.0f, (r - 0.15f) / 0.15f);
            if (r <= 0.6f) return Mathf.Lerp(1.0f, BalanceWorst, (r - 0.3f) / 0.3f);
            return BalanceWorst;
        }

        public static OrderResult Evaluate(CustomerOrder order, IList<GroceryItem> items, IList<Bag> bags,
            System.Func<GroceryItem, bool> inCart, float patience01, bool memoryOrder = false)
        {
            var r = new OrderResult { ItemsTotal = order.ItemsTotal };
            r.BasePay = r.ItemsTotal * Commission;

            // 1) 归属：小件要在袋子里，大件要在推车里；放错地方都算漏装
            var assigned = new Dictionary<Bag, List<GroceryItem>>();
            foreach (var b in bags) assigned[b] = new List<GroceryItem>();
            foreach (var it in items)
            {
                if (it == null) continue;
                if (it.IsDamaged) { r.DamagePenalty += DamageCost(it.Def); r.DamagedNames.Add(it.Def.Name); }
                if (it.IsDestroyed) continue;
                if (it.Tipped && !it.IsDamaged) r.TippedNames.Add(it.Def.Name);

                Bag home = bags.FirstOrDefault(b => b.Contains(it));
                if (it.Def.Bulky)
                {
                    if (inCart(it)) continue;
                    if (it.IsDamaged) continue; // 已经赔过了
                    r.Missing++;
                    r.MissingNames.Add(it.Def.Name + (home != null ? "（大件要放推车，不能装袋）" : ""));
                    continue;
                }
                if (home != null) { assigned[home].Add(it); continue; }
                if (it.IsDamaged) continue; // 已经赔过了
                r.Missing++;
                r.MissingNames.Add(it.Def.Name + (inCart(it) ? "（小件要装袋，不能直接放推车）" : ""));
            }

            // 2) 每个袋子
            float factorSum = 0;
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
                // 质心高出袋口的商品 → 提起来会晃、会掉，不稳
                br.Unstable = st.Unstable;
                if (st.Unstable > 0)
                {
                    br.Factor *= Mathf.Max(UnstableFloor, Mathf.Pow(UnstablePerItem, st.Unstable));
                    br.Note += $"，{st.Unstable}件高出袋口不稳";
                }
                if (b.Torn) { br.Factor = Mathf.Min(br.Factor, BrokenBagCap); br.Note = "袋子被刺破"; }
                if (st.Overweight) { br.Factor = Mathf.Min(br.Factor, BrokenBagCap); br.Note = $"超重 ({st.Mass:0.0}/{b.Spec.CapacityKg}kg) 提起时破了"; }
                r.Bags.Add(br);
                factorSum += br.Factor;
            }
            // 多个袋子：每个袋子的系数取平均
            r.BalanceFactor = r.Bags.Count > 0 ? factorSum / r.Bags.Count : BalanceWorst;

            // 3) 规格：材质不对 ×0.95；个数/尺寸不对 ×0.9；都不对就相乘
            var used = r.Bags.Select(b => b.Spec).ToList();
            if (used.Count == 0)
            {
                r.ReqFactor = ReqSizeWrong; r.ReqNote = "一个袋子都没用";
            }
            else
            {
                bool materialOk = used.All(s => s.Material == order.Material);
                bool sizeOk = true;
                foreach (BagSize s in BagSpec.AllSizes)
                {
                    int want = order.Bags.TryGetValue(s, out int w) ? w : 0;
                    int got = used.Count(u => u.Size == s);
                    if (want != got) sizeOk = false;
                }
                r.ReqMatch = materialOk && sizeOk;
                string usedText = string.Join(" + ", used.GroupBy(u => u.Name).Select(g => $"{g.Count()}个{g.Key}"));
                if (r.ReqMatch) { r.ReqFactor = ReqMatch; r.ReqNote = "完全符合要求"; }
                else
                {
                    r.ReqFactor = (materialOk ? 1f : ReqMaterialWrong) * (sizeOk ? 1f : ReqSizeWrong);
                    string what = !materialOk && !sizeOk ? "材质和个数/尺寸都不对" : !materialOk ? "材质不对" : "个数/尺寸不对";
                    r.ReqNote = $"{what}：要求 {order.BagRequestText}，实际 {usedText}";
                }
            }

            // 4) 耐心、漏装、翻倒、记忆单
            r.PatienceFactor = patience01 > 0 ? 1f : ImpatientFactor;
            r.MissingFactor = r.Missing == 0 ? NoMissing : Mathf.Pow(MissingPerItem, r.Missing);
            r.ToppleFactor = r.TippedNames.Count == 0 ? 1f : Mathf.Max(TippedFloor, Mathf.Pow(TippedPerItem, r.TippedNames.Count));
            r.MemoryFactor = memoryOrder ? MemoryBonus : 1f;

            r.Multiplier = r.ReqFactor * r.BalanceFactor * r.PatienceFactor * r.MissingFactor * r.ToppleFactor * r.MemoryFactor;
            r.Pay = r.BasePay * r.Multiplier;

            // 5) 小费：越快越多；弄坏了、漏装了、等太久了就没有
            if (r.DamagedNames.Count > 0) r.TipNote = "弄坏了东西，没有小费";
            else if (r.Missing > 0) r.TipNote = "漏装了东西，没有小费";
            else if (patience01 <= 0) r.TipNote = "等太久了，没有小费";
            else
            {
                for (int i = 0; i < TipTiers.Length; i++)
                    if (patience01 >= TipTiers[i].Min)
                    {
                        r.TipTier = TipTiers.Length - i;
                        r.Tip = r.ItemsTotal * TipTiers[i].Rate;
                        r.TipNote = TipTiers[i].Name;
                        break;
                    }
            }

            r.Net = r.Pay + r.Tip - r.DamagePenalty;
            float q = r.Multiplier / r.MemoryFactor; // 星级只看装得好不好
            r.Stars = q >= Star3 ? 3 : q >= Star2 ? 2 : q >= Star1 ? 1 : 0;
            if (r.DamagedNames.Count > 0) r.Stars = Mathf.Max(0, r.Stars - 1);
            return r;
        }
    }
}
