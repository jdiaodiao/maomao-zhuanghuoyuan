using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CatMart
{
    public class CustomerOrder
    {
        public int Ticket;
        public string CustomerName;
        public Color Fur;
        public List<ItemDefinition> Items = new List<ItemDefinition>();
        public BagMaterial Material;
        /// <summary>要求的袋子：尺寸 → 个数。</summary>
        public Dictionary<BagSize, int> Bags = new Dictionary<BagSize, int>();
        public float PatienceSeconds;
        public int Seed;
        /// <summary>记忆单：没有光边和属性说明，商品都是今天前几单出现过的。</summary>
        public bool Memory;

        public float ItemsTotal => Items.Sum(i => i.Price);
        public int BagCount => Bags.Values.Sum();

        public string BagRequestText
        {
            get
            {
                var parts = new List<string>();
                foreach (BagSize s in new[] { BagSize.Large, BagSize.Medium, BagSize.Small })
                    if (Bags.TryGetValue(s, out int n) && n > 0)
                        parts.Add($"{n}个{BagSpec.Get(Material, s).Name}");
                return string.Join(" + ", parts);
            }
        }

        public string Speech
        {
            get
            {
                string s = $"请用 {BagRequestText} 装喵~";
                if (Items.Any(i => i.Bulky)) s += "\n大件直接放推车就好";
                return s;
            }
        }
    }

    public static class OrderGenerator
    {
        static readonly string[] Names =
        {
            "橘座", "咪咪", "煤球", "雪糕", "花卷", "布丁", "年糕", "芝麻", "豆包", "奶茶",
            "可乐", "汤圆", "大福", "饭团", "糯米", "黑豆", "三花", "狸花", "虎斑", "奶牛"
        };

        static readonly Color[] Furs =
        {
            U.Hex("#E8923A"), U.Hex("#8A8A8A"), U.Hex("#2B2B2B"), U.Hex("#F4F1EA"), U.Hex("#C9A27A"),
            U.Hex("#6E5A4A"), U.Hex("#D9B38C"), U.Hex("#B0B7BF")
        };

        /// <summary>按难度生成一张订单。customerIndex 从 0 开始。</summary>
        /// <param name="pool">不为空时只从这些商品里挑（记忆单：只出今天前几单出现过的商品）。</param>
        public static CustomerOrder Generate(int ticket, int customerIndex, int seed, ICollection<ItemDefinition> pool = null)
        {
            var rng = new System.Random(seed);
            var o = new CustomerOrder
            {
                Ticket = ticket,
                Seed = seed,
                CustomerName = Names[rng.Next(Names.Length)],
                Fur = Furs[rng.Next(Furs.Length)],
                Material = rng.NextDouble() < 0.55 ? BagMaterial.Plastic : BagMaterial.Paper
            };

            int count = Mathf.Clamp(4 + customerIndex + rng.Next(0, 3), 4, 14);
            bool bulkyUsed = false;
            var counts = new Dictionary<string, int>();
            while (o.Items.Count < count)
            {
                System.Func<ItemDefinition, bool> ok = x =>
                    (pool == null || pool.Contains(x)) &&
                    (!x.Bulky || (!bulkyUsed && customerIndex >= 1)) &&
                    (!counts.TryGetValue(x.Id, out int c) || c < (x.Category == ItemCategory.Produce && x.Mass < 0.3f ? 3 : 2));
                if (!ItemCatalog.All.Any(ok)) break; // 记忆单的候选用完了
                var d = ItemFactory.RandomDef(rng, ok);
                if (d.Bulky) bulkyUsed = true;
                counts[d.Id] = counts.TryGetValue(d.Id, out int cc) ? cc + 1 : 1;
                o.Items.Add(d);
            }

            // 计算需要的袋子；装不下就去掉最重的非大件商品
            while (!ChooseBags(o) && o.Items.Count > 2)
            {
                var heavy = o.Items.Where(i => !i.Bulky).OrderByDescending(i => i.Mass).First();
                o.Items.Remove(heavy);
            }

            // 打乱顺序（上游扫描是随机的）
            o.Items = o.Items.OrderBy(_ => rng.Next()).ToList();
            o.PatienceSeconds = 50f + o.Items.Count * 9f;
            return o;
        }

        /// <summary>指定商品的订单（测试关用）。装不下就用 4 个大袋兜底。</summary>
        public static CustomerOrder Custom(int ticket, IEnumerable<ItemDefinition> items, BagMaterial material, int seed, float patience = -1)
        {
            var rng = new System.Random(seed);
            var o = new CustomerOrder
            {
                Ticket = ticket,
                Seed = seed,
                CustomerName = Names[rng.Next(Names.Length)],
                Fur = Furs[rng.Next(Furs.Length)],
                Material = material,
            };
            o.Items.AddRange(items.Where(d => d != null));
            if (!ChooseBags(o)) { o.Bags.Clear(); o.Bags[BagSize.Large] = Mathf.Clamp(o.Items.Count(i => !i.Bulky), 1, 4); }
            o.Items = o.Items.OrderBy(_ => rng.Next()).ToList();
            o.PatienceSeconds = patience > 0 ? patience : 50f + o.Items.Count * 9f;
            return o;
        }

        /// <summary>选出满足体积/重量的最少袋子组合。</summary>
        static bool ChooseBags(CustomerOrder o)
        {
            var bagItems = o.Items.Where(i => !i.Bulky).ToList();
            float vol = bagItems.Sum(i => i.Volume);
            float kg = bagItems.Sum(i => i.Mass);
            float maxDim = 0;
            foreach (var i in bagItems) maxDim = Mathf.Max(maxDim, MinFitDim(i));

            const float fill = 0.5f;       // 不规则物品的装填率
            const float weightMargin = 1.15f;
            BagSize[] sizes = { BagSize.Small, BagSize.Medium, BagSize.Large };

            List<BagSize> best = null;
            float bestCap = float.MaxValue;
            // 枚举 1~4 个袋子的组合（多重集合）
            for (int n = 1; n <= 4 && best == null; n++)
            {
                foreach (var combo in Combos(sizes, n))
                {
                    float cv = 0, ck = 0;
                    bool fits = false;
                    foreach (var s in combo)
                    {
                        var spec = BagSpec.Get(o.Material, s);
                        cv += spec.Volume * fill;
                        ck += spec.CapacityKg;
                        if (MaxBagFitDim(spec) >= maxDim) fits = true;
                    }
                    if (!fits || cv < vol || ck < kg * weightMargin) continue;
                    // 每个袋子都至少能分到点东西
                    if (n > bagItems.Count) continue;
                    if (cv < bestCap) { bestCap = cv; best = combo; }
                }
            }
            if (best == null) return false;
            o.Bags.Clear();
            foreach (var s in best) o.Bags[s] = o.Bags.TryGetValue(s, out int c) ? c + 1 : 1;
            return true;
        }

        /// <summary>商品放进袋子至少需要的口径（第二长边）。</summary>
        static float MinFitDim(ItemDefinition d)
        {
            var s = d.Size;
            var a = new[] { s.x, s.y, s.z }.OrderBy(x => x).ToArray();
            return a[1];
        }

        static float MaxBagFitDim(BagSpec spec) => Mathf.Max(spec.Inner.x, spec.Inner.z) * 1.05f;

        static IEnumerable<List<BagSize>> Combos(BagSize[] sizes, int n, int start = 0)
        {
            if (n == 0) { yield return new List<BagSize>(); yield break; }
            for (int i = start; i < sizes.Length; i++)
                foreach (var rest in Combos(sizes, n - 1, i))
                {
                    var l = new List<BagSize> { sizes[i] };
                    l.AddRange(rest);
                    yield return l;
                }
        }
    }
}
