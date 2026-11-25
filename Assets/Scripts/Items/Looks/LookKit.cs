using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 构建一件商品外观时的上下文。每个实体部件 i 有自己的 VisualSet，
    /// 坐标系 = 部件局部坐标（中心为原点，轴向与部件一致，尺寸 = 部件 Size）。
    /// </summary>
    public class LookCtx
    {
        public readonly ItemDefinition Def;
        public readonly List<PartDef> Solids = new List<PartDef>();
        public readonly List<PartDef> Decos = new List<PartDef>();
        public readonly VisualSet[] Sets;
        public LabelStyle Style;

        public LookCtx(ItemDefinition def)
        {
            Def = def;
            foreach (var p in def.Parts) (p.Deco ? Decos : Solids).Add(p);
            Sets = new VisualSet[Solids.Count];
            for (int i = 0; i < Sets.Length; i++) Sets[i] = new VisualSet();
            Style = LookKit.AutoStyle(def);
        }

        public Vector3 S(int i) => Solids[i].Size;
        public VisualSet V(int i) => Sets[i];
        public Color C(int i) => Solids[i].Color;
        public Color DecoColor(int k, Color fallback) => k < Decos.Count ? Decos[k].Color : fallback;
        public int Seed => Def.Id.GetHashCode() & 0x7fffffff;

        /// <summary>部件 j 的中心在部件 i 局部坐标中的位置。</summary>
        public Vector3 Rel(int i, int j)
        {
            var pi = Solids[i]; var pj = Solids[j];
            return Quaternion.Inverse(Quaternion.Euler(pi.Rot)) * (pj.Pos - pi.Pos);
        }
    }

    /// <summary>通用建模积木。</summary>
    public static class LookKit
    {
        // ---------------------------------------------------------------- 材质
        public static Material Plastic(Color c, float smooth = 0.55f) => Mats.Lit(c, smooth, 0f);
        public static Material Matte(Color c) => Mats.Lit(c, 0.15f, 0f);
        public static Material Metal(Color c, float smooth = 0.62f) => Mats.Lit(c, smooth, 0.85f, TexGen.BrushedSteel, TexGen.BrushedSteelN, 0.25f, new Vector2(1, 1));
        public static Material Film => Mats.Glass(new Color(0.96f, 0.97f, 1f), 0.14f, 0.92f, TexGen.PlasticWrinkleN);
        public static Material ClearPlastic => Mats.Glass(new Color(0.95f, 0.98f, 1f), 0.18f, 0.95f);
        public static Material Glass(Color tint, float a = 0.3f) => Mats.Glass(tint, a, 0.96f);
        public static Material Cardboard(Color c) => Mats.Lit(c, 0.12f, 0f, TexGen.Cardboard, null, 1, new Vector2(2, 2));
        public static Material KraftMat => Mats.Lit(U.Hex("#C49A6C"), 0.12f, 0f, TexGen.Kraft, TexGen.KraftN, 0.6f, new Vector2(3, 3));
        public static Material Pulp(Color c) => Mats.Lit(c, 0.08f, 0f, TexGen.Pulp, TexGen.PulpN, 0.8f, new Vector2(4, 4));
        public static Material Foam => Mats.Lit(U.Hex("#F4F4F0"), 0.25f, 0f, TexGen.Foam, null, 1, new Vector2(3, 3));

        // ---------------------------------------------------------------- 自动包装设计
        static readonly Dictionary<ItemCategory, string[]> Brands = new Dictionary<ItemCategory, string[]>
        {
            { ItemCategory.Dairy, new[] { "喵农场", "奶牛猫", "晨光牧场" } },
            { ItemCategory.Bakery, new[] { "面包喵坊", "麦香猫", "烤箱猫" } },
            { ItemCategory.Produce, new[] { "鲜果喵", "绿爪农园" } },
            { ItemCategory.Meat, new[] { "肉球屋", "大口喵" } },
            { ItemCategory.Frozen, new[] { "冰爪", "极地喵" } },
            { ItemCategory.Snacks, new[] { "咔滋喵", "嘎嘣猫", "小鱼零食" } },
            { ItemCategory.Drinks, new[] { "喵泉", "咕噜咕噜", "气泡猫" } },
            { ItemCategory.Pantry, new[] { "猫厨房", "CatMart 精选", "老橘家" } },
            { ItemCategory.Household, new[] { "净爪", "CatMart 好物", "毛毛家居" } },
            { ItemCategory.Pet, new[] { "主子专享", "喵星人", "铲屎官之选" } },
        };

        static readonly Dictionary<string, LabelIcon> Icons = new Dictionary<string, LabelIcon>
        {
            { "eggs", LabelIcon.Egg }, { "milk_gallon", LabelIcon.Milk }, { "milk_half", LabelIcon.Milk }, { "yogurt_cup", LabelIcon.Fruit },
            { "yogurt_4pack", LabelIcon.Drop }, { "cheese", LabelIcon.Milk }, { "butter", LabelIcon.Milk }, { "ice_cream", LabelIcon.Snow },
            { "bread", LabelIcon.Wheat }, { "bagels", LabelIcon.Wheat }, { "donuts", LabelIcon.Heart }, { "croissants", LabelIcon.Wheat },
            { "buns", LabelIcon.Wheat }, { "cake", LabelIcon.Star }, { "frozen_pizza", LabelIcon.Pizza }, { "frozen_shrimp", LabelIcon.Fish },
            { "frozen_peas", LabelIcon.Leaf }, { "fish_fillet", LabelIcon.Fish }, { "frozen_dinner", LabelIcon.Flame }, { "chips", LabelIcon.Sun },
            { "tortilla_chips", LabelIcon.Sun }, { "crackers", LabelIcon.Wheat }, { "cookies", LabelIcon.Cookie }, { "cereal", LabelIcon.Cat },
            { "candy_bar", LabelIcon.Heart }, { "popcorn", LabelIcon.Star }, { "peanut_butter", LabelIcon.Bean }, { "gummies", LabelIcon.Heart },
            { "soda_12", LabelIcon.Bubble }, { "water_case", LabelIcon.Drop }, { "orange_juice", LabelIcon.Fruit }, { "wine", LabelIcon.Fruit },
            { "soda_2l", LabelIcon.Bubble }, { "coffee", LabelIcon.Bean }, { "energy_drink", LabelIcon.Bolt }, { "milk_tea", LabelIcon.Drop },
            { "canned_soup", LabelIcon.Fruit }, { "canned_tuna", LabelIcon.Fish }, { "canned_beans", LabelIcon.Bean }, { "pasta", LabelIcon.Wheat },
            { "rice", LabelIcon.Wheat }, { "flour", LabelIcon.Wheat }, { "sugar", LabelIcon.Star }, { "pasta_sauce", LabelIcon.Fruit },
            { "olive_oil", LabelIcon.Drop }, { "honey", LabelIcon.Drop }, { "ketchup", LabelIcon.Fruit }, { "toilet_paper", LabelIcon.Bubble },
            { "paper_towels", LabelIcon.Leaf }, { "detergent", LabelIcon.Bubble }, { "bleach", LabelIcon.Drop }, { "light_bulbs", LabelIcon.Sun },
            { "forks", LabelIcon.Star }, { "skewers", LabelIcon.Flame }, { "knife", LabelIcon.Star }, { "sponges", LabelIcon.Bubble },
            { "foil", LabelIcon.Star }, { "tissues", LabelIcon.Leaf }, { "batteries", LabelIcon.Bolt }, { "cat_can", LabelIcon.Fish },
            { "cat_litter", LabelIcon.Paw }, { "catnip", LabelIcon.Leaf }, { "cat_kibble", LabelIcon.Cat }, { "cat_wand", LabelIcon.Paw },
            { "fish_treats", LabelIcon.Fish }, { "steak", LabelIcon.Flame }, { "ground_beef", LabelIcon.Flame }, { "chicken", LabelIcon.Flame },
            { "carrots", LabelIcon.Leaf }, { "potatoes", LabelIcon.Leaf }, { "onions", LabelIcon.Leaf }, { "grapes", LabelIcon.Fruit },
            { "strawberries", LabelIcon.Fruit }, { "apples", LabelIcon.Fruit },
        };

        static readonly string[] Badges = { "", "", "新品", "有机", "特惠", "", "人气", "" };

        public static LabelStyle AutoStyle(ItemDefinition d)
        {
            int seed = d.Id.GetHashCode() & 0x7fffffff;
            Color main = d.Parts.Count > 0 ? d.Parts[0].Color : Color.white;
            Color accent = Complement(main);
            foreach (var p in d.Parts) if (p.Deco) { accent = p.Color; break; }
            if (ColorDist(accent, main) < 0.25f) accent = Complement(main);

            string title = d.Name, sub = "";
            int paren = d.Name.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) { title = d.Name.Substring(0, paren); sub = d.Name.Substring(paren + 2).TrimEnd(')'); }
            if (sub.Length == 0) sub = d.Mass >= 1 ? $"净含量 {d.Mass:0.#}kg" : $"净含量 {d.Mass * 1000:0}g";

            var brands = Brands[d.Category];
            return new LabelStyle
            {
                Brand = brands[seed % brands.Length],
                Title = title,
                Sub = sub,
                Badge = Badges[seed % Badges.Length],
                Bg = main,
                Bg2 = Color.Lerp(main, Color.black, 0.18f),
                Accent = accent,
                Ink = Lum(main) > 0.6f ? new Color(0.12f, 0.1f, 0.12f) : Color.white,
                BrandInk = Lum(accent) > 0.62f ? new Color(0.12f, 0.1f, 0.12f) : Color.white,
                Icon = Icons.TryGetValue(d.Id, out var ic) ? ic : LabelIcon.Cat,
                IconColor = Lum(main) > 0.6f ? accent : Color.Lerp(Color.white, accent, 0.15f),
                Pattern = (LabelPattern)(seed % 8),
                Seed = seed % 100000,
            };
        }

        public static float Lum(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
        static float ColorDist(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        static Color Complement(Color c)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB((h + 0.5f) % 1f, Mathf.Max(0.55f, s), v < 0.4f ? 0.9f : Mathf.Max(0.55f, v * 0.85f));
        }

        // ================================================================ 包装积木

        /// <summary>印刷纸盒。</summary>
        public static void Carton(LookCtx c, int i, float bevel = 0.003f, float gloss = 0.3f, LabelStyle s = null)
        {
            var size = c.S(i);
            var tex = LabelBaker.Box(size, s ?? c.Style);
            var mb = Shapes.RoundedBox(size, bevel, 2, 3, LabelBaker.BoxUv);
            c.V(i).Add(mb, Mats.Label(tex, gloss));
        }

        /// <summary>枕形包装袋（薯片、冷冻食品、零食）。</summary>
        public static void Pouch(LookCtx c, int i, bool sideSeals = false, float gloss = 0.6f, float metallic = 0.15f, float puff = 1f,
            Vector3? sizeOverride = null, Quaternion? rot = null)
        {
            var size = sizeOverride ?? c.S(i);
            var tex = LabelBaker.Pouch(new Vector2(size.x, size.y), c.Style);
            var mb = Shapes.Pillow(new Vector3(size.x, size.y, size.z * puff), Mathf.Min(0.012f, size.y * 0.06f), sideSeals, 20, 26, c.Seed);
            c.V(i).Add(mb, Vector3.zero, rot ?? Quaternion.identity, Mats.Label(tex, gloss, metallic));
        }

        /// <summary>透明袋子装着东西：袋身 + 腰封标签。</summary>
        public static void ClearBag(LookCtx c, int i, float bandFrac = 0.28f, float bandAt = 0.78f, bool sideSeals = false)
        {
            var s = c.S(i);
            var mb = Shapes.Pillow(new Vector3(s.x * 1.01f, s.y * 1.01f, s.z * 1.04f), 0.01f, sideSeals, 16, 20, c.Seed);
            c.V(i).Add(mb, Film);
            // 印刷腰封（贴在袋子前后）
            float bh = s.y * bandFrac, by = -s.y / 2 + s.y * bandAt;
            var tex = LabelBaker.Sticker(new Vector2(s.x * 0.9f, bh), c.Style);
            var q = Shapes.Quad(s.x * 0.9f, bh);
            c.V(i).Add(q, new Vector3(0, by, -s.z * 0.5f - 0.0015f), Quaternion.identity, Mats.Label(tex, 0.5f));
            var back = Shapes.Quad(s.x * 0.9f, bh);
            c.V(i).Add(back, new Vector3(0, by, s.z * 0.5f + 0.0015f), Quaternion.Euler(0, 180, 0), Mats.Label(tex, 0.5f));
        }

        /// <summary>平贴的标签（朝 -z）。</summary>
        public static void Sticker(LookCtx c, int i, Vector3 pos, Vector2 size, Quaternion? rot = null, LabelStyle s = null, bool simple = false, float gloss = 0.4f)
        {
            var tex = LabelBaker.Sticker(size, s ?? c.Style, simple);
            c.V(i).Add(Shapes.Quad(size.x, size.y), pos, rot ?? Quaternion.identity, Mats.Label(tex, gloss));
        }

        /// <summary>朝上的标签（贴在顶面）。</summary>
        public static Quaternion FaceUp => Quaternion.Euler(90, 0, 0);

        /// <summary>环绕标签带。</summary>
        public static void WrapLabel(LookCtx c, int i, float radius, float y0, float y1, LabelStyle s = null, float gloss = 0.45f, float radiusTop = -1)
        {
            float rt = radiusTop < 0 ? radius : radiusTop;
            var tex = LabelBaker.Wrap(Mathf.PI * (radius + rt), y1 - y0, s ?? c.Style);
            c.V(i).Add(Shapes.Band(radius + 0.0005f, rt + 0.0005f, y0, y1, 48, 3), Mats.Label(tex, gloss));
        }

        // ---------------------------------------------------------------- 旋转体
        public static List<Vector2> P(params float[] xy)
        {
            var l = new List<Vector2>();
            for (int k = 0; k < xy.Length; k += 2) l.Add(new Vector2(xy[k], xy[k + 1]));
            return l;
        }

        /// <summary>金属罐头（带卷边、拉环）。部件为竖直圆柱。</summary>
        public static void Can(LookCtx c, int i, bool pullTab = true, float labelMargin = 0.006f)
        {
            var s = c.S(i);
            float r = s.x / 2, h = s.y / 2;
            var prof = Shapes.Fillet(P(0, -h + 0.003f, r * 0.82f, -h + 0.002f, r * 0.93f, -h, r, -h + 0.004f, r, h - 0.006f,
                r * 0.95f, h - 0.0015f, r * 0.97f, h, r * 0.9f, h - 0.0025f, 0, h - 0.0025f), 0.0015f, 2);
            c.V(i).Add(Shapes.Lathe(prof, 40), Metal(new Color(0.86f, 0.87f, 0.9f)));
            WrapLabel(c, i, r, -h + labelMargin, h - labelMargin - 0.002f);
            if (pullTab)
            {
                c.V(i).Add(Shapes.Torus(r * 0.82f, 0.0008f, 40, 6), new Vector3(0, h - 0.0024f, 0), Quaternion.identity, Metal(new Color(0.8f, 0.8f, 0.83f)));
                var tab = Shapes.RoundedBox(new Vector3(r * 0.5f, 0.0012f, r * 0.28f), 0.0005f, 1, 1);
                c.V(i).Add(tab, new Vector3(0, h - 0.0018f, -r * 0.35f), Quaternion.identity, Metal(new Color(0.75f, 0.76f, 0.8f)));
            }
        }

        /// <summary>
        /// 瓶身（玻璃或塑料）+ 内部液体 + 标签。shoulderTo = 顶部收口半径（0 表示平顶）。
        /// </summary>
        public static void BottleBody(LookCtx c, int i, Material shell, Color? liquid, float fill = 0.9f, float shoulderTo = 0f,
            float shoulderStart = 0.75f, float labelFrom = 0.15f, float labelTo = 0.62f, bool label = true)
        {
            var s = c.S(i);
            float r = s.x / 2, h = s.y / 2;
            float top = shoulderTo > 0 ? shoulderTo : r * 0.92f;
            float sy = -h + s.y * shoulderStart;
            var prof = Shapes.Fillet(P(0, -h + 0.002f, r * 0.7f, -h + 0.001f, r * 0.9f, -h, r, -h + 0.006f, r, sy,
                Mathf.Lerp(r, top, 0.65f), Mathf.Lerp(sy, h, 0.7f), top, h, 0, h), 0.004f, 3);
            c.V(i).Add(Shapes.Lathe(prof, 36), shell);
            if (liquid.HasValue)
            {
                float ly = Mathf.Lerp(-h, h, fill);
                var lp = new List<Vector2>();
                foreach (var p in prof) if (p.y <= ly && p.x > 0) lp.Add(new Vector2(p.x * 0.93f, Mathf.Max(p.y, -h + 0.002f)));
                lp.Insert(0, new Vector2(0, -h + 0.003f));
                float rTop = lp.Count > 1 ? lp[lp.Count - 1].x : r * 0.9f;
                lp.Add(new Vector2(rTop, ly));
                lp.Add(new Vector2(0, ly));
                c.V(i).Add(Shapes.Lathe(lp, 28), Mats.Lit(liquid.Value, 0.7f));
            }
            if (label) WrapLabel(c, i, r, -h + s.y * labelFrom, -h + s.y * labelTo);
        }

        /// <summary>瓶颈（细圆柱，带口沿）。</summary>
        public static void Neck(LookCtx c, int i, Material shell, Color? liquid = null, float lip = 0.0015f)
        {
            var s = c.S(i);
            float r = s.x / 2, h = s.y / 2;
            var prof = Shapes.Fillet(P(0, -h, r * 1.05f, -h, r, -h + s.y * 0.2f, r, h - 0.004f, r + lip, h - 0.003f, r + lip, h - 0.001f, r * 0.8f, h, 0, h), 0.001f, 2);
            c.V(i).Add(Shapes.Lathe(prof, 24), shell);
            if (liquid.HasValue)
                c.V(i).Add(Shapes.Lathe(P(0, -h, r * 0.85f, -h, r * 0.85f, h * 0.2f, 0, h * 0.2f), 16), Mats.Lit(liquid.Value, 0.7f));
        }

        /// <summary>带竖纹的瓶盖。</summary>
        public static void Cap(LookCtx c, int i, Color col, bool ribbed = true, float smooth = 0.4f)
        {
            var s = c.S(i);
            float r = s.x / 2, h = s.y / 2;
            var prof = Shapes.Fillet(P(0, -h, r, -h, r, h - 0.001f, r * 0.9f, h, 0, h), 0.0012f, 2);
            var mb = Shapes.Lathe(prof, 48);
            if (ribbed)
                mb.Displace((p, n) => Mathf.Abs(n.y) < 0.5f ? p + n * 0.00025f * Mathf.Sign(Mathf.Sin(Mathf.Atan2(p.z, p.x) * 24)) : p);
            c.V(i).Add(mb, Plastic(col, smooth));
        }

        /// <summary>杯子（酸奶）：锥形杯身 + 铝箔盖 + 标签。部件为竖直圆柱。</summary>
        public static void Cup(LookCtx c, int i, Vector3 offset = default, float scale = 1f, bool label = true)
        {
            var s = c.S(i) * scale;
            float r = s.x / 2, h = s.y / 2, rb = r * 0.78f;
            var prof = Shapes.Fillet(P(0, -h, rb, -h, r * 0.97f, h - 0.003f, r * 1.04f, h - 0.002f, r * 1.04f, h, 0, h), 0.0015f, 2);
            c.V(i).Add(Shapes.Lathe(prof, 32), Matrix4x4.Translate(offset), Plastic(Color.white, 0.5f));
            var lid = Shapes.Disc(r * 1.04f, 32);
            c.V(i).Add(lid, Matrix4x4.Translate(offset + new Vector3(0, h + 0.0004f, 0)), Metal(new Color(0.85f, 0.85f, 0.9f), 0.5f));
            if (label)
            {
                var tex = LabelBaker.Wrap(Mathf.PI * (r + rb), s.y * 0.75f, c.Style);
                c.V(i).Add(Shapes.Band(Mathf.Lerp(rb, r, 0.1f) + 0.0006f, Mathf.Lerp(rb, r, 0.85f) + 0.0006f, -h + s.y * 0.08f, -h + s.y * 0.83f, 40, 3),
                    Matrix4x4.Translate(offset), Mats.Label(tex, 0.4f));
            }
        }

        // ---------------------------------------------------------------- 托盘肉
        /// <summary>泡沫托盘 + 保鲜膜 + 价签。contents 在托盘里画内容。</summary>
        public static void MeatTray(LookCtx c, int i, Action<VisualSet, Vector3> contents, Color? trayColor = null)
        {
            var s = c.S(i);
            var tray = Shapes.OpenBox(new Vector3(s.x, s.y * 0.55f, s.z), 0.004f, 0.006f);
            Material trayMat = trayColor.HasValue ? Mats.Lit(trayColor.Value, 0.3f, 0, TexGen.Foam) : Foam;
            c.V(i).Add(tray, new Vector3(0, -s.y * 0.225f, 0), Quaternion.identity, trayMat);
            contents(c.V(i), new Vector3(s.x * 0.9f, s.y * 0.85f, s.z * 0.85f));
            // 保鲜膜（略鼓）
            var film = Shapes.Pillow(new Vector3(s.x * 0.98f, s.z * 0.98f, s.y * 0.55f), 0.004f, true, 14, 14, c.Seed);
            c.V(i).Add(film, new Vector3(0, s.y * 0.0f, 0), Quaternion.Euler(90, 0, 0), Film);
            Sticker(c, i, new Vector3(s.x * 0.22f, s.y * 0.5f + 0.0012f, -s.z * 0.12f), new Vector2(s.x * 0.36f, s.z * 0.42f), FaceUp, null, true);
        }

        // ---------------------------------------------------------------- 透明盒（烘焙/水果）
        public static void Clamshell(LookCtx c, int i, Action<VisualSet, Vector3> contents, Material baseMat = null)
        {
            var s = c.S(i);
            var bottom = Shapes.OpenBox(new Vector3(s.x, s.y * 0.45f, s.z), 0.0015f, 0.008f);
            c.V(i).Add(bottom, new Vector3(0, -s.y * 0.275f, 0), Quaternion.identity, baseMat ?? ClearPlastic);
            contents(c.V(i), new Vector3(s.x * 0.92f, s.y * 0.9f, s.z * 0.9f));
            var lid = Shapes.RoundedBox(new Vector3(s.x * 1.01f, s.y * 0.6f, s.z * 1.01f), 0.01f, 3, 2);
            // 只保留上半部分作为盖子
            var lidMb = new MB();
            for (int k = 0; k < lid.V.Count; k++) lidMb.Vert(lid.V[k], lid.N[k], lid.UV[k]);
            for (int k = 0; k < lid.T.Count; k += 3)
                if (lid.V[lid.T[k]].y > -s.y * 0.05f || lid.V[lid.T[k + 1]].y > -s.y * 0.05f)
                    lidMb.Tri(lid.T[k], lid.T[k + 1], lid.T[k + 2]);
            c.V(i).Add(lidMb, new Vector3(0, s.y * 0.2f, 0), Quaternion.identity, ClearPlastic);
            Sticker(c, i, new Vector3(0, s.y * 0.5f + 0.001f, 0), new Vector2(s.x * 0.5f, s.z * 0.5f), FaceUp);
        }

        // ---------------------------------------------------------------- 网袋
        public static void NetBag(LookCtx c, int i, Action<VisualSet, Vector3> contents, Color netColor)
        {
            var s = c.S(i);
            contents(c.V(i), s * 0.92f);
            var shell = Shapes.RoundedBox(s * 1.01f, Mathf.Min(s.x, Mathf.Min(s.y, s.z)) * 0.45f, 4, 3);
            c.V(i).Add(shell, Mats.Cutout(netColor, TexGen.Net, 0.5f, new Vector2(3, 3)));
            // 顶部卡扣 + 纸签
            c.V(i).Add(Shapes.RoundedBox(new Vector3(0.012f, 0.012f, 0.03f), 0.003f, 2, 1), new Vector3(s.x * 0.5f, 0, 0), Quaternion.identity, Plastic(netColor));
            Sticker(c, i, new Vector3(s.x * 0.5f + 0.004f, 0, -0.0f), new Vector2(s.z * 0.6f, s.y * 0.55f), Quaternion.Euler(0, -90, 0), null, true);
        }

        // ---------------------------------------------------------------- 吸塑卡
        /// <summary>纸卡 + 透明泡罩。faceAxis：卡片法线方向（-z 或 +y）。</summary>
        public static void Blister(LookCtx c, int i, bool faceUp, Action<VisualSet, Vector3, Quaternion> contents)
        {
            var s = c.S(i);
            Quaternion rot = faceUp ? Quaternion.Euler(90, 0, 0) : Quaternion.identity;
            // 在"卡片空间"里：卡片位于 xy 平面，朝 -z
            Vector2 card = faceUp ? new Vector2(s.x, s.z) : new Vector2(s.x, s.y);
            float depth = faceUp ? s.y : s.z;
            var cardMb = Shapes.RoundedBox(new Vector3(card.x, card.y, 0.0015f), 0.0006f, 1, 1);
            Vector3 cardPos = rot * new Vector3(0, 0, depth / 2 - 0.001f);
            c.V(i).Add(cardMb, cardPos, rot, Matte(Color.white));
            var tex = LabelBaker.Sticker(card, c.Style);
            c.V(i).Add(Shapes.Quad(card.x * 0.98f, card.y * 0.98f), rot * new Vector3(0, 0, depth / 2 - 0.0019f), rot, Mats.Label(tex, 0.3f));
            var bubble = Shapes.RoundedBox(new Vector3(card.x * 0.7f, card.y * 0.62f, depth * 0.85f), depth * 0.35f, 3, 2);
            c.V(i).Add(bubble, rot * new Vector3(0, -card.y * 0.12f, -depth * 0.07f), rot, ClearPlastic);
            contents(c.V(i), rot * new Vector3(0, -card.y * 0.12f, -depth * 0.05f), rot);
        }

        // ---------------------------------------------------------------- 有机形体
        public static MB Apple(float d)
        {
            float R = d / 2;
            return Shapes.Blob(dir =>
            {
                float top = Mathf.Exp(-Mathf.Pow((1 - dir.y) / 0.12f, 2)) * 0.22f;
                float bot = Mathf.Exp(-Mathf.Pow((1 + dir.y) / 0.1f, 2)) * 0.12f;
                float shoulder = 1 + 0.06f * dir.y;
                return R * (shoulder - top - bot) * (1 - 0.04f * dir.y * dir.y);
            }, 32, 22);
        }

        public static MB Tomato(Vector3 size)
        {
            return Shapes.Blob(dir =>
            {
                float th = Mathf.Atan2(dir.z, dir.x);
                float lobes = 1 + 0.035f * Mathf.Cos(th * 5) * (1 - Mathf.Abs(dir.y));
                float flat = 1 / Mathf.Sqrt(dir.x * dir.x / (size.x * size.x / 4) + dir.y * dir.y / (size.y * size.y / 4) + dir.z * dir.z / (size.z * size.z / 4));
                float top = Mathf.Exp(-Mathf.Pow((1 - dir.y) / 0.15f, 2)) * 0.12f;
                return flat * lobes * (1 - top);
            }, 32, 20);
        }

        public static MB Lemon(Vector3 size)
        {
            return Shapes.Blob(dir =>
            {
                float e = 1 / Mathf.Sqrt(dir.x * dir.x / (size.x * size.x / 4 * 0.75f) + dir.y * dir.y / (size.y * size.y / 4) + dir.z * dir.z / (size.z * size.z / 4));
                float tip = Mathf.Pow(Mathf.Abs(dir.x), 14) * 0.22f;
                return e * (1 + tip);
            }, 32, 18);
        }

        public static MB Potato(Vector3 size, int seed)
        {
            return Shapes.Blob(dir =>
            {
                float e = 1 / Mathf.Sqrt(dir.x * dir.x / (size.x * size.x / 4) + dir.y * dir.y / (size.y * size.y / 4) + dir.z * dir.z / (size.z * size.z / 4));
                return e * (0.88f + 0.24f * Noise.Fbm3(dir * 1.6f, 3, seed));
            }, 24, 16);
        }

        public static MB Onion(float d)
        {
            float R = d / 2;
            return Shapes.Blob(dir =>
            {
                float tip = Mathf.Pow(Mathf.Max(0, dir.y), 10) * 0.45f;
                return R * (0.92f + tip - 0.08f * Mathf.Max(0, -dir.y));
            }, 28, 20);
        }

        /// <summary>沿 y 轴弯曲的香蕉（局部 y 为长轴）。</summary>
        public static MB Banana(float len, float d, float bend)
        {
            var path = new List<Vector3>();
            var rad = new List<float>();
            int n = 18;
            for (int k = 0; k <= n; k++)
            {
                float t = k / (float)n;
                float y = (t - 0.5f) * len;
                float x = bend * (4 * t * (1 - t)) - bend * 0.5f;
                path.Add(new Vector3(x, y, 0));
                float taper = Mathf.Sin(Mathf.Clamp01(t * 1.15f) * Mathf.PI) * 0.85f + 0.15f;
                if (t > 0.92f) taper = Mathf.Lerp(0.35f, 0.25f, (t - 0.92f) / 0.08f);
                rad.Add(d / 2 * Mathf.Clamp(taper, 0.2f, 1f));
            }
            return Shapes.Tube(path, rad, 6, true);
        }

        /// <summary>叶片（双面），沿 +y 伸展，宽在 x。</summary>
        public static MB Leaf(float len, float width, float curl, int seed)
        {
            var mb = new MB();
            int ny = 8, nx = 4;
            for (int j = 0; j <= ny; j++)
            for (int i = 0; i <= nx; i++)
            {
                float t = j / (float)ny, u = i / (float)nx - 0.5f;
                float w = width * Mathf.Sin(Mathf.Clamp01(t * 1.1f) * Mathf.PI * 0.95f + 0.08f) * (1 - t * 0.6f);
                float z = -curl * t * t * len + Mathf.Abs(u) * w * 0.4f;
                mb.Vert(new Vector3(u * w, t * len, z), new Vector2(u + 0.5f, t));
            }
            for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, cc = a + nx + 2, d = a + nx + 1;
                mb.Quad(a, d, cc, b);
            }
            return mb.Normals(true).DoubleSided();
        }

        /// <summary>甜甜圈/百吉饼。</summary>
        public static void Donut(VisualSet v, Vector3 pos, float d, Color dough, Color? frosting)
        {
            float R = d * 0.3f, r = d * 0.2f;
            v.Add(Shapes.Torus(R, r, 28, 14), pos, Quaternion.identity, Mats.Lit(dough, 0.25f, 0, TexGen.Bread, TexGen.BreadN, 0.6f));
            if (frosting.HasValue)
            {
                var top = Shapes.Torus(R, r * 1.04f, 28, 14);
                var mb = new MB();
                for (int k = 0; k < top.V.Count; k++) mb.Vert(top.V[k], top.N[k], top.UV[k]);
                for (int k = 0; k < top.T.Count; k += 3)
                    if (top.V[top.T[k]].y > r * 0.1f && top.V[top.T[k + 1]].y > r * 0.1f && top.V[top.T[k + 2]].y > r * 0.1f) mb.Tri(top.T[k], top.T[k + 1], top.T[k + 2]);
                v.Add(mb, pos, Quaternion.identity, Mats.Lit(frosting.Value, 0.75f));
            }
        }
    }
}
