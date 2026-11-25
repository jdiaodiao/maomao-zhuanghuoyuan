using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    public enum DamageStyle { None, Crumple, Dent, Squish, Bruise, Leak, Shatter }

    public enum ItemCategory { Dairy, Bakery, Produce, Meat, Frozen, Snacks, Drinks, Pantry, Household, Pet }

    public enum DamageCause { Crush, Impact, Pierce }

    /// <summary>商品的一个部件：形状 + 物理属性（承压、抗摔、尖锐）。</summary>
    public class PartDef
    {
        public string Name;
        public Shape Shape;
        public Vector3 Pos;      // 米，y 以底部为 0（工厂会重新居中）
        public Vector3 Size;     // 米
        public Vector3 Rot;      // 欧拉角
        public Color Color;
        public float Crush;      // 持续承压上限 N，0 = 不会压坏
        public float Impact;     // 单次冲量上限 N·s，0 = 摔不坏
        public float Sharp;      // 0~1
        public bool Deco;        // 纯装饰（无碰撞，跟随前一个实体部件形变）
        public DamageStyle? Style;
        public float Smooth = 0.25f;
    }

    public class ItemDefinition
    {
        public string Id;
        public string Name;
        public ItemCategory Category;
        public float Price;
        public float Mass;
        public DamageStyle Style = DamageStyle.Crumple;
        public Color LiquidColor = new Color(0.9f, 0.9f, 0.85f);
        public Vector3 ComOffset;   // 米，相对几何中心
        public bool Bulky;          // 大件：放购物车，不装袋
        public bool Upright;        // 不能倒：倾斜太多就算翻倒（蛋糕、插花…）
        public float Weight = 1f;   // 出现概率权重
        public readonly List<PartDef> Parts = new List<PartDef>();

        // ---------- 派生数据 ----------
        Bounds? _bounds;

        /// <summary>未居中前的包围盒（考虑部件旋转）。</summary>
        public Bounds RawBounds
        {
            get
            {
                if (_bounds.HasValue) return _bounds.Value;
                bool first = true;
                var b = new Bounds();
                foreach (var p in Parts)
                {
                    var r = Quaternion.Euler(p.Rot);
                    Vector3 h = p.Size * 0.5f;
                    Vector3 ext = new Vector3(
                        Mathf.Abs((r * Vector3.right).x) * h.x + Mathf.Abs((r * Vector3.up).x) * h.y + Mathf.Abs((r * Vector3.forward).x) * h.z,
                        Mathf.Abs((r * Vector3.right).y) * h.x + Mathf.Abs((r * Vector3.up).y) * h.y + Mathf.Abs((r * Vector3.forward).y) * h.z,
                        Mathf.Abs((r * Vector3.right).z) * h.x + Mathf.Abs((r * Vector3.up).z) * h.y + Mathf.Abs((r * Vector3.forward).z) * h.z);
                    var pb = new Bounds(p.Pos, ext * 2f);
                    if (first) { b = pb; first = false; } else b.Encapsulate(pb);
                }
                _bounds = b;
                return b;
            }
        }

        public Vector3 Size => RawBounds.size;
        public float Volume => Size.x * Size.y * Size.z;

        public float WeakestCrush
        {
            get
            {
                float m = float.MaxValue;
                foreach (var p in Parts) if (!p.Deco && p.Crush > 0) m = Mathf.Min(m, p.Crush);
                return m == float.MaxValue ? 0 : m;
            }
        }

        public float WeakestImpact
        {
            get
            {
                float m = float.MaxValue;
                foreach (var p in Parts) if (!p.Deco && p.Impact > 0) m = Mathf.Min(m, p.Impact);
                return m == float.MaxValue ? 0 : m;
            }
        }

        public float MaxSharp
        {
            get
            {
                float m = 0;
                foreach (var p in Parts) if (!p.Deco) m = Mathf.Max(m, p.Sharp);
                return m;
            }
        }

        public string CategoryName
        {
            get
            {
                switch (Category)
                {
                    case ItemCategory.Dairy: return "乳制品";
                    case ItemCategory.Bakery: return "烘焙";
                    case ItemCategory.Produce: return "果蔬";
                    case ItemCategory.Meat: return "肉类";
                    case ItemCategory.Frozen: return "冷冻";
                    case ItemCategory.Snacks: return "零食";
                    case ItemCategory.Drinks: return "饮料";
                    case ItemCategory.Pantry: return "粮油干货";
                    case ItemCategory.Household: return "日用";
                    case ItemCategory.Pet: return "宠物";
                }
                return "";
            }
        }

        /// <summary>给玩家看的承压描述。</summary>
        public string FragilityText
        {
            get
            {
                float c = WeakestCrush;
                string s;
                if (c <= 0 || c >= 1500) s = "非常结实";
                else if (c >= 400) s = "结实";
                else if (c >= 100) s = "一般";
                else if (c >= 30) s = "怕压";
                else s = "极易压坏";
                if (WeakestImpact > 0) s += " · 易摔碎";
                return s;
            }
        }
    }
}

namespace CatMart
{
    /// <summary>需要玩家特别注意的商品属性。光边颜色和悬停说明里的字用同一种颜色。</summary>
    public enum ItemTrait { Crushable, Breakable, Sharp, Bulky, Upright }

    public static class ItemTraits
    {
        public const float CrushableBelow = 100f;   // 承压低于此值 = 怕压
        public const float SharpFrom = 0.3f;

        public static readonly Color NormalColor = new Color(1f, 1f, 1f);

        public static System.Collections.Generic.IEnumerable<ItemTrait> Of(ItemDefinition d)
        {
            float c = d.WeakestCrush;
            if (c > 0 && c < CrushableBelow) yield return ItemTrait.Crushable;
            if (d.WeakestImpact > 0) yield return ItemTrait.Breakable;
            if (d.MaxSharp >= SharpFrom) yield return ItemTrait.Sharp;
            if (d.Bulky) yield return ItemTrait.Bulky;
            if (d.Upright) yield return ItemTrait.Upright;
        }

        public static string Hex(ItemTrait t)
        {
            switch (t)
            {
                case ItemTrait.Crushable: return "#FFD84D"; // 黄
                case ItemTrait.Breakable: return "#FF7FB6"; // 粉
                case ItemTrait.Sharp: return "#FF8A3D";     // 橙
                case ItemTrait.Bulky: return "#6FC3FF";     // 蓝
                case ItemTrait.Upright: return "#5FE08A";   // 绿
            }
            return "#FFFFFF";
        }

        public static Color Color(ItemTrait t) => U.Hex(Hex(t));

        public static string Name(ItemTrait t, ItemDefinition d)
        {
            switch (t)
            {
                case ItemTrait.Crushable: return d.WeakestCrush < 30 ? "极易压坏" : "怕压";
                case ItemTrait.Breakable: return "易摔碎";
                case ItemTrait.Sharp: return "尖锐";
                case ItemTrait.Bulky: return "大件→推车";
                case ItemTrait.Upright: return "不能倒";
            }
            return "";
        }

        /// <summary>带颜色的属性说明（富文本）。没有特殊属性时返回白色的结实程度。</summary>
        public static string RichText(ItemDefinition d)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var t in Of(d))
            {
                if (sb.Length > 0) sb.Append(" · ");
                sb.Append($"<color={Hex(t)}>{Name(t, d)}</color>");
            }
            if (sb.Length > 0) return sb.ToString();
            float c = d.WeakestCrush;
            return c <= 0 || c >= 1500 ? "普通 · 非常结实" : c >= 400 ? "普通 · 结实" : "普通";
        }
    }
}
