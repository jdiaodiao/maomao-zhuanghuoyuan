using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 第一章商品库。单位：尺寸/位置 cm（y 从底部算起），质量 kg，价格 $，承压 N，抗摔 N·s。
    /// 参考：1kg 物体压在上面 ≈ 9.8N。薯片 8N 意味着几乎任何东西压上去都会碎。
    /// </summary>
    public static class ItemCatalog
    {
        static List<ItemDefinition> _all;
        static Dictionary<string, ItemDefinition> _byId;

        public static IReadOnlyList<ItemDefinition> All { get { if (_all == null) Build(); return _all; } }

        public static ItemDefinition Get(string id)
        {
            if (_all == null) Build();
            return _byId.TryGetValue(id, out var d) ? d : null;
        }

        // ======================= 构建器 =======================
        class B
        {
            public readonly ItemDefinition D;
            PartDef _last, _lastSolid;
            public B(ItemDefinition d) { D = d; }

            B AddPart(PartDef p)
            {
                D.Parts.Add(p);
                _last = p;
                if (!p.Deco) _lastSolid = p;
                return this;
            }

            PartDef P(string name, Shape s, float px, float py, float pz, float sx, float sy, float sz, string hex,
                float crush, float impact, float sharp, bool deco = false) => new PartDef
            {
                Name = name, Shape = s, Pos = U.Cm(px, py, pz), Size = U.Cm(sx, sy, sz), Color = U.Hex(hex),
                Crush = crush, Impact = impact, Sharp = sharp, Deco = deco
            };

            public B Box(string n, float px, float py, float pz, float sx, float sy, float sz, string hex, float crush, float impact = 0, float sharp = 0)
                => AddPart(P(n, Shape.Box, px, py, pz, sx, sy, sz, hex, crush, impact, sharp));
            public B Cyl(string n, float px, float py, float pz, float d, float h, float d2, string hex, float crush, float impact = 0, float sharp = 0)
                => AddPart(P(n, Shape.Cylinder, px, py, pz, d, h, d2, hex, crush, impact, sharp));
            public B Ball(string n, float px, float py, float pz, float sx, float sy, float sz, string hex, float crush, float impact = 0, float sharp = 0)
                => AddPart(P(n, Shape.Sphere, px, py, pz, sx, sy, sz, hex, crush, impact, sharp));
            public B Cone(string n, float px, float py, float pz, float d, float h, float d2, string hex, float crush, float impact = 0, float sharp = 0)
                => AddPart(P(n, Shape.Cone, px, py, pz, d, h, d2, hex, crush, impact, sharp));

            /// <summary>以底部为原点的盒状主体。</summary>
            public B Body(float sx, float sy, float sz, string hex, float crush, float impact = 0, float sharp = 0, string n = "主体")
                => Box(n, 0, sy / 2, 0, sx, sy, sz, hex, crush, impact, sharp);
            /// <summary>以底部为原点的圆柱主体。</summary>
            public B CylBody(float d, float h, string hex, float crush, float impact = 0, float sharp = 0, string n = "瓶身")
                => Cyl(n, 0, h / 2, 0, d, h, d, hex, crush, impact, sharp);
            public B BallBody(float sx, float sy, float sz, string hex, float crush, float impact = 0, float sharp = 0, string n = "果实")
                => Ball(n, 0, sy / 2, 0, sx, sy, sz, hex, crush, impact, sharp);

            public B Deco(Shape s, float px, float py, float pz, float sx, float sy, float sz, string hex)
                => AddPart(P("装饰", s, px, py, pz, sx, sy, sz, hex, 0, 0, 0, true));

            /// <summary>在最近的实体部件上加一圈标签带。frac = 占高度比例，at = 中心高度比例。</summary>
            public B Label(string hex, float frac = 0.45f, float at = 0.5f)
            {
                var s = _lastSolid;
                Vector3 size = s.Size * 100f, pos = s.Pos * 100f;
                float y = pos.y - size.y / 2 + size.y * at;
                if (s.Shape == Shape.Box)
                    return Deco(Shape.Box, pos.x, y, pos.z, size.x + 0.25f, size.y * frac, size.z + 0.25f, hex);
                return Deco(Shape.Cylinder, pos.x, y, pos.z, size.x + 0.25f, size.y * frac, size.z + 0.25f, hex);
            }

            /// <summary>在最近实体部件正前方(+z)贴一块 logo。</summary>
            public B Logo(string hex, float w = 0.5f, float h = 0.3f, float at = 0.6f)
            {
                var s = _lastSolid;
                Vector3 size = s.Size * 100f, pos = s.Pos * 100f;
                float y = pos.y - size.y / 2 + size.y * at;
                return Deco(Shape.Box, pos.x, y, pos.z + size.z / 2 + 0.1f, size.x * w, size.y * h, 0.3f, hex);
            }

            /// <summary>在最近实体部件顶上加盖子（实体部件，有独立承压）。</summary>
            public B Cap(float d, float h, string hex, float crush, float impact = 0)
            {
                var s = _lastSolid;
                Vector3 size = s.Size * 100f, pos = s.Pos * 100f;
                return Cyl("盖子", pos.x, pos.y + size.y / 2 + h / 2, pos.z, d, h, d, hex, crush, impact);
            }

            public B Rot(float x, float y, float z) { _last.Rot = new Vector3(x, y, z); return this; }
            public B Style(DamageStyle s) { _last.Style = s; return this; }
            public B Gloss(float g) { _last.Smooth = g; return this; }
            public B Liquid(string hex) { D.LiquidColor = U.Hex(hex); return this; }
            public B Com(float x, float y, float z) { D.ComOffset = U.Cm(x, y, z); return this; }
            public B Bulky() { D.Bulky = true; return this; }
            public B Upright() { D.Upright = true; return this; }
            public B Freq(float w) { D.Weight = w; return this; }
        }

        static B Add(string id, string name, ItemCategory cat, float price, float kg, DamageStyle style = DamageStyle.Crumple)
        {
            var d = new ItemDefinition { Id = id, Name = name, Category = cat, Price = price, Mass = kg, Style = style };
            _all.Add(d);
            _byId[id] = d;
            return new B(d);
        }

        const ItemCategory Dairy = ItemCategory.Dairy, Bakery = ItemCategory.Bakery, Produce = ItemCategory.Produce,
            Meat = ItemCategory.Meat, Frozen = ItemCategory.Frozen, Snacks = ItemCategory.Snacks, Drinks = ItemCategory.Drinks,
            Pantry = ItemCategory.Pantry, Household = ItemCategory.Household, Pet = ItemCategory.Pet;
        const DamageStyle Crumple = DamageStyle.Crumple, Dent = DamageStyle.Dent, Squish = DamageStyle.Squish,
            Bruise = DamageStyle.Bruise, Leak = DamageStyle.Leak, Shatter = DamageStyle.Shatter;

        static void Build()
        {
            _all = new List<ItemDefinition>();
            _byId = new Dictionary<string, ItemDefinition>();

            // ============ 乳制品 & 蛋 ============
            Add("eggs", "鸡蛋 (12枚)", Dairy, 3.29f, 0.75f, Leak).Liquid("#F2B705")
                .Body(30, 7, 10, "#E6D7B5", crush: 25, impact: 1.8f, n: "蛋盒")
                .Deco(Shape.Box, 0, 7.2f, 0, 28, 0.6f, 8.6f, "#D4C29A")
                .Logo("#F28C28", 0.4f, 0.55f, 0.5f);
            Add("milk_gallon", "全脂牛奶 (1加仑)", Dairy, 3.98f, 3.95f, Leak).Liquid("#FAFAF5")
                .Body(15, 24, 15, "#F7F7F2", crush: 350, n: "奶桶").Gloss(0.5f)
                .Label("#1E5AA8", 0.35f, 0.45f)
                .Cap(4, 2, "#1E5AA8", 150).Com(0, -1.5f, 0);
            Add("milk_half", "低脂牛奶 (半加仑)", Dairy, 2.68f, 2.0f, Leak).Liquid("#FAFAF5")
                .Body(10, 20, 10, "#F7F7F2", crush: 260, n: "奶桶").Gloss(0.5f)
                .Label("#D33F49", 0.3f, 0.45f)
                .Cap(3.5f, 1.8f, "#D33F49", 120).Com(0, -1, 0);
            Add("yogurt_cup", "草莓酸奶", Dairy, 0.89f, 0.17f, Squish).Liquid("#F7C6D0")
                .CylBody(7, 8, "#FFFFFF", crush: 30, n: "杯身").Label("#E75480", 0.5f)
                .Deco(Shape.Cylinder, 0, 8.1f, 0, 7.4f, 0.3f, 7.4f, "#C0C0C8").Freq(1.3f);
            Add("yogurt_4pack", "希腊酸奶 4连杯", Dairy, 4.97f, 0.68f, Squish).Liquid("#F3F0E6")
                .Body(14, 8, 14, "#F5F5F0", crush: 45, n: "杯组").Label("#3F7FBF", 0.5f);
            Add("cheese", "切达奶酪块", Dairy, 2.48f, 0.23f, Squish)
                .Body(15, 3, 8, "#F2A900", crush: 200, n: "奶酪").Logo("#7B3F00", 0.5f, 0.6f, 0.5f);
            Add("butter", "黄油 (4条)", Dairy, 4.12f, 0.45f, Squish)
                .Body(12, 6.5f, 6.5f, "#FFF2B2", crush: 80, n: "黄油盒").Label("#2E7D32", 0.4f);
            Add("ice_cream", "香草冰淇淋", Frozen, 3.98f, 1.4f, Dent)
                .CylBody(15, 13, "#FFF8E1", crush: 150, sharp: 0.05f, n: "桶身").Label("#8D6E63", 0.5f)
                .Deco(Shape.Cylinder, 0, 13.2f, 0, 15.4f, 0.6f, 15.4f, "#6D4C41");

            // ============ 烘焙 ============
            Add("bread", "白吐司", Bakery, 1.98f, 0.57f, Crumple)
                .Body(28, 12, 12, "#E9C891", crush: 12, n: "面包").Label("#F0F4FF", 0.75f)
                .Deco(Shape.Cylinder, 14.5f, 6, 0, 1.5f, 3, 1.5f, "#D32F2F").Rot(0, 0, 90).Freq(1.4f);
            Add("bagels", "百吉饼 (6个)", Bakery, 3.48f, 0.6f, Crumple)
                .Body(20, 8, 14, "#C8935A", crush: 18, n: "饼袋").Label("#FFFFFF", 0.3f);
            Add("donuts", "甜甜圈 (盒)", Bakery, 5.98f, 0.5f, Squish)
                .Body(24, 8, 24, "#FFFFFF", crush: 15, n: "纸盒").Logo("#EC407A", 0.6f, 0.5f, 0.5f)
                .Deco(Shape.Box, 0, 8.1f, 0, 14, 0.2f, 14, "#BBDEFB").Upright();
            Add("croissants", "牛角包 (4个)", Bakery, 4.98f, 0.3f, Crumple)
                .Body(25, 9, 15, "#D89B4A", crush: 10, n: "透明盒").Gloss(0.6f);
            Add("buns", "汉堡面包 (8个)", Bakery, 1.48f, 0.34f, Crumple)
                .Body(22, 8, 16, "#E3B878", crush: 12, n: "面包袋").Label("#FFC107", 0.3f);
            Add("cake", "生日蛋糕", Bakery, 14.98f, 1.1f, Squish).Liquid("#FFFFFF")
                .Body(22, 12, 22, "#FFFFFF", crush: 20, impact: 2.75f, n: "蛋糕盒").Gloss(0.6f)
                .Deco(Shape.Cylinder, 0, 6, 0, 18, 9, 18, "#F8BBD0").Freq(0.4f).Upright();

            // ============ 果蔬 ============
            Add("bananas", "香蕉 (一串)", Produce, 1.37f, 1.2f, Bruise)
                .Cyl("香蕉", 0, 1.8f, -3.6f, 3.6f, 20, 3.6f, "#F5D547", 30).Rot(0, -8, 90)
                .Cyl("香蕉", 0, 1.8f, 0, 3.6f, 21, 3.6f, "#F2CF3A", 30).Rot(0, 0, 90)
                .Cyl("香蕉", 0, 1.8f, 3.6f, 3.6f, 20, 3.6f, "#F5D547", 30).Rot(0, 8, 90)
                .Deco(Shape.Cylinder, 11.5f, 1.8f, 0, 2, 3, 2, "#5D4037").Rot(0, 0, 90).Freq(1.5f);
            Add("apples", "红苹果", Produce, 0.68f, 0.2f, Bruise).Liquid("#FFF3C4").Freq(1.3f)
                .Ball("苹果", 0, 4, 0, 8, 8, 8, "#C62828", 60);
            Add("tomatoes", "番茄", Produce, 0.73f, 0.17f, Squish).Liquid("#C62828").Freq(1.3f)
                .Ball("番茄", 0, 3.25f, 0, 7, 6.5f, 7, "#E53935", 25);
            Add("strawberries", "草莓 (1磅盒)", Produce, 3.97f, 0.45f, Squish).Liquid("#B71C1C")
                .Body(17, 8, 12, "#E53950", crush: 14, n: "草莓盒").Gloss(0.7f)
                .Deco(Shape.Box, 0, 8.1f, 0, 17.2f, 0.2f, 12.2f, "#E8F5E9").Upright();
            Add("grapes", "无籽葡萄", Produce, 4.48f, 0.9f, Squish).Liquid("#4A148C")
                .Body(22, 10, 14, "#7B1FA2", crush: 20, n: "葡萄袋").Label("#E1BEE7", 0.25f, 0.8f);
            Add("watermelon", "迷你西瓜", Produce, 4.98f, 4.5f, Leak).Liquid("#E53935")
                .BallBody(24, 21, 21, "#2E7D32", crush: 700, impact: 18f, n: "西瓜")
                .Deco(Shape.Sphere, 0, 10.5f, 0, 24.3f, 14, 21.3f, "#1B5E20").Freq(0.5f);
            Add("lettuce", "生菜", Produce, 1.78f, 0.6f, Crumple)
                .BallBody(15, 14, 15, "#7CB342", crush: 25, n: "菜头")
                .Deco(Shape.Sphere, 0, 9, 0, 11, 9, 11, "#AED581");
            Add("avocados", "牛油果", Produce, 0.98f, 0.22f, Bruise).Freq(1.2f)
                .Ball("牛油果", 0, 3.5f, 0, 7, 9, 7, "#33691E", 70).Rot(0, 0, 90);
            Add("potatoes", "土豆 (5磅)", Produce, 3.27f, 2.27f, Bruise)
                .Body(30, 10, 20, "#A1887F", crush: 800, n: "网袋").Label("#FFEB3B", 0.3f);
            Add("onions", "黄洋葱 (3磅)", Produce, 2.97f, 1.36f, Bruise)
                .Body(24, 10, 16, "#D7A86E", crush: 500, n: "网袋").Label("#E53935", 0.3f);
            Add("pineapple", "菠萝", Produce, 2.48f, 1.6f, Bruise).Liquid("#FFEE58")
                .BallBody(13, 19, 13, "#C68A2B", crush: 400, sharp: 0.35f, n: "果身")
                .Cone("叶冠", 0, 25, 0, 10, 12, 10, "#558B2F", 300, sharp: 0.85f).Com(0, -2, 0);
            Add("lemons", "柠檬", Produce, 0.58f, 0.12f, Bruise).Freq(1.2f)
                .Ball("柠檬", 0, 3, 0, 7, 6, 6, "#FDD835", 80);
            Add("carrots", "胡萝卜 (2磅)", Produce, 1.68f, 0.9f, Bruise)
                .Body(26, 6, 14, "#FB8C00", crush: 300, n: "袋子").Label("#43A047", 0.4f, 0.9f);

            // ============ 肉类 & 冷冻 ============
            Add("chicken", "鸡胸肉托盘", Meat, 8.62f, 1.1f, Leak).Liquid("#E57373")
                .Body(23, 5, 16, "#F8BBD0", crush: 90, n: "托盘").Gloss(0.7f)
                .Deco(Shape.Box, 0, 5.1f, 0, 10, 0.2f, 6, "#FFFFFF");
            Add("ground_beef", "牛肉馅 (1磅)", Meat, 5.48f, 0.45f, Squish).Liquid("#B71C1C")
                .Body(15, 5, 10, "#C62828", crush: 120, n: "肉盒").Gloss(0.7f).Label("#FFFFFF", 0.3f, 0.8f);
            Add("steak", "西冷牛排", Meat, 12.97f, 0.6f, Leak).Liquid("#B71C1C")
                .Body(22, 3.5f, 15, "#D84343", crush: 150, n: "牛排盒").Gloss(0.7f).Freq(0.6f);
            Add("frozen_pizza", "冷冻披萨", Frozen, 4.98f, 0.65f, Crumple)
                .Body(30, 5, 30, "#D32F2F", crush: 70, sharp: 0.15f, n: "披萨盒").Logo("#FFC107", 0.6f, 0.6f, 0.5f);
            Add("frozen_shrimp", "冷冻大虾", Frozen, 8.97f, 0.9f, Crumple)
                .Body(25, 6, 18, "#4FC3F7", crush: 400, sharp: 0.6f, n: "冻虾袋").Logo("#FF7043", 0.5f, 0.5f, 0.5f).Freq(0.7f);
            Add("frozen_peas", "冷冻豌豆", Frozen, 1.12f, 0.45f, Crumple)
                .Body(18, 5, 24, "#2E7D32", crush: 300, sharp: 0.1f, n: "冻豌豆袋").Label("#A5D6A7", 0.3f);
            Add("fish_fillet", "三文鱼片", Meat, 6.48f, 0.45f, Leak).Liquid("#FFAB91")
                .Body(20, 3, 14, "#FF8A65", crush: 60, n: "鱼片盒").Gloss(0.7f);
            Add("frozen_dinner", "冷冻便当", Frozen, 2.98f, 0.35f, Crumple)
                .Body(20, 4, 15, "#3949AB", crush: 80, sharp: 0.1f, n: "便当盒").Logo("#FFFFFF", 0.6f, 0.6f, 0.5f);

            // ============ 零食 ============
            Add("chips", "薯片 (派对装)", Snacks, 3.98f, 0.28f, Crumple)
                .Body(22, 30, 8, "#FFD600", crush: 8, n: "薯片袋").Gloss(0.7f).Logo("#D50000", 0.7f, 0.3f, 0.6f).Freq(1.5f);
            Add("tortilla_chips", "玉米片", Snacks, 3.48f, 0.37f, Crumple)
                .Body(22, 30, 8, "#FF6F00", crush: 9, n: "玉米片袋").Gloss(0.7f).Logo("#FFFFFF", 0.6f, 0.25f, 0.6f);
            Add("crackers", "苏打饼干", Snacks, 3.28f, 0.4f, Crumple)
                .Body(20, 22, 6, "#1565C0", crush: 30, n: "饼干盒").Logo("#FFEB3B", 0.7f, 0.3f, 0.6f);
            Add("cookies", "巧克力曲奇", Snacks, 3.98f, 0.38f, Crumple)
                .Body(20, 6, 15, "#3E2723", crush: 35, n: "曲奇袋").Logo("#FFFFFF", 0.5f, 0.5f, 0.5f);
            Add("cereal", "猫猫谷物麦片", Snacks, 4.28f, 0.55f, Crumple)
                .Body(20, 30, 7, "#FF8F00", crush: 60, sharp: 0.15f, n: "麦片盒").Logo("#FFFFFF", 0.7f, 0.35f, 0.62f).Freq(1.3f);
            Add("candy_bar", "巧克力棒", Snacks, 1.25f, 0.05f, Crumple)
                .Body(14, 1.5f, 3, "#5D4037", crush: 40, n: "巧克力").Label("#FFFFFF", 0.4f);
            Add("popcorn", "微波爆米花 (3包)", Snacks, 3.48f, 0.25f, Crumple)
                .Body(18, 12, 8, "#FFF59D", crush: 25, n: "纸盒").Logo("#E53935", 0.6f, 0.4f, 0.5f);
            Add("peanut_butter", "花生酱", Pantry, 2.78f, 1.13f, Dent)
                .CylBody(10, 13, "#C48A3F", crush: 500, n: "塑料罐").Label("#FFFFFF", 0.45f)
                .Cap(10.2f, 2.5f, "#D32F2F", 400);
            Add("gummies", "小熊软糖", Snacks, 2.28f, 0.2f, Squish)
                .Body(15, 20, 4, "#AB47BC", crush: 40, n: "软糖袋").Gloss(0.7f).Logo("#FFEB3B", 0.6f, 0.35f, 0.5f);

            // ============ 饮料 ============
            Add("soda_12", "可乐 (12罐装)", Drinks, 6.98f, 4.6f, Dent).Liquid("#3E2723")
                .Body(40, 12, 14, "#C62828", crush: 900, sharp: 0.2f, n: "罐装箱").Logo("#FFFFFF", 0.6f, 0.4f, 0.5f).Bulky();
            Add("water_case", "矿泉水 (24瓶)", Drinks, 4.48f, 12.3f, Dent).Liquid("#E3F2FD")
                .Body(40, 22, 27, "#90CAF9", crush: 3000, n: "水箱").Gloss(0.8f).Label("#1565C0", 0.2f, 0.8f).Bulky().Freq(0.6f);
            Add("orange_juice", "鲜榨橙汁 (玻璃瓶)", Drinks, 3.98f, 1.6f, Shatter).Liquid("#FFA726")
                .CylBody(9, 22, "#FFB74D", crush: 600, impact: 4.5f, n: "玻璃瓶").Gloss(0.9f).Label("#FFFFFF", 0.35f)
                .Cyl("瓶颈", 0, 25, 0, 4.5f, 6, 4.5f, "#FFB74D", 500, 4.2f).Gloss(0.9f)
                .Cap(4.6f, 1.5f, "#2E7D32", 400).Com(0, -2, 0);
            Add("wine", "红酒", Drinks, 9.98f, 1.3f, Shatter).Liquid("#6A1B2A")
                .CylBody(7.5f, 20, "#2B0A12", crush: 900, impact: 3.6f, n: "瓶身").Gloss(0.9f).Label("#F5F0E1", 0.35f, 0.45f)
                .Cyl("瓶颈", 0, 25, 0, 3, 10, 3, "#2B0A12", 700, 3.3f).Gloss(0.9f)
                .Deco(Shape.Cylinder, 0, 28.5f, 0, 3.2f, 3, 3.2f, "#8E0000").Com(0, -3, 0).Freq(0.6f);
            Add("soda_2l", "汽水 (2L)", Drinks, 1.98f, 2.1f, Leak).Liquid("#C5E1A5")
                .CylBody(11, 26, "#A5D6A7", crush: 400, n: "塑料瓶").Gloss(0.8f).Label("#1B5E20", 0.3f)
                .Cone("瓶肩", 0, 29, 0, 11, 6, 11, "#A5D6A7", 400).Gloss(0.8f)
                .Cap(3, 2, "#1B5E20", 300).Com(0, -2, 0);
            Add("coffee", "咖啡豆", Drinks, 8.98f, 0.9f, Crumple)
                .Body(12, 24, 8, "#4E342E", crush: 200, n: "豆袋").Logo("#FFCC80", 0.6f, 0.3f, 0.6f);
            Add("energy_drink", "能量饮料", Drinks, 2.48f, 0.5f, Dent).Liquid("#C6FF00")
                .CylBody(6.6f, 16, "#212121", crush: 300, sharp: 0.15f, n: "罐身").Gloss(0.8f).Label("#76FF03", 0.5f);
            Add("milk_tea", "瓶装奶茶", Drinks, 2.98f, 0.55f, Leak).Liquid("#D7B899")
                .CylBody(7, 18, "#D7B899", crush: 200, n: "瓶身").Gloss(0.8f).Label("#5D4037", 0.4f)
                .Cap(3.5f, 2, "#FFFFFF", 150);

            // ============ 粮油干货 ============
            Add("canned_soup", "番茄浓汤罐头", Pantry, 1.24f, 0.35f, Dent)
                .CylBody(7, 10, "#B0BEC5", crush: 600, sharp: 0.15f, n: "罐身").Gloss(0.8f).Label("#D32F2F", 0.65f).Freq(1.3f);
            Add("canned_tuna", "金枪鱼罐头", Pantry, 1.12f, 0.17f, Dent)
                .CylBody(8.5f, 4, "#B0BEC5", crush: 700, sharp: 0.15f, n: "罐身").Gloss(0.8f).Label("#0277BD", 0.6f);
            Add("canned_beans", "茄汁黄豆罐头", Pantry, 0.98f, 0.45f, Dent)
                .CylBody(7.5f, 11, "#B0BEC5", crush: 600, sharp: 0.15f, n: "罐身").Gloss(0.8f).Label("#FF7043", 0.65f);
            Add("pasta", "意大利面", Pantry, 1.28f, 0.45f, Crumple)
                .Body(27, 7, 4, "#1976D2", crush: 40, sharp: 0.2f, n: "面盒").Logo("#FFFFFF", 0.4f, 0.5f, 0.5f);
            Add("rice", "长粒米 (5磅)", Pantry, 3.48f, 2.27f, Crumple)
                .Body(25, 8, 18, "#FAFAFA", crush: 1500, n: "米袋").Label("#C62828", 0.35f);
            Add("flour", "面粉 (5磅)", Pantry, 2.82f, 2.27f, Leak).Liquid("#FFFFFF")
                .Body(18, 25, 10, "#F5F5F5", crush: 250, n: "纸袋").Logo("#1565C0", 0.7f, 0.3f, 0.55f);
            Add("sugar", "砂糖 (4磅)", Pantry, 3.22f, 1.8f, Leak).Liquid("#FFFFFF")
                .Body(16, 22, 9, "#FFFFFF", crush: 250, n: "纸袋").Logo("#E91E63", 0.7f, 0.3f, 0.55f);
            Add("pasta_sauce", "意面酱 (玻璃罐)", Pantry, 2.98f, 0.68f, Shatter).Liquid("#C62828")
                .CylBody(8, 14, "#C62828", crush: 700, impact: 1.9f, n: "玻璃罐").Gloss(0.9f).Label("#FFF8E1", 0.45f)
                .Cap(7, 1.5f, "#FFD54F", 500);
            Add("olive_oil", "橄榄油 (玻璃瓶)", Pantry, 7.98f, 1.0f, Shatter).Liquid("#C0CA33")
                .CylBody(7, 24, "#9E9D24", crush: 700, impact: 2.8f, n: "瓶身").Gloss(0.9f).Label("#FFFFFF", 0.3f)
                .Cyl("瓶颈", 0, 26.5f, 0, 2.8f, 5, 2.8f, "#9E9D24", 600, 2.6f)
                .Cap(3, 1.5f, "#212121", 400).Com(0, -3, 0);
            Add("honey", "小熊蜂蜜", Pantry, 5.98f, 0.68f, Dent)
                .CylBody(7, 11, "#FFB300", crush: 400, n: "熊身").Gloss(0.8f)
                .Ball("熊头", 0, 13, 0, 6, 5, 6, "#FFB300", 400).Gloss(0.8f)
                .Cap(3, 1.5f, "#D32F2F", 300).Com(0, -1, 0);
            Add("ketchup", "番茄酱", Pantry, 2.12f, 0.6f, Leak).Liquid("#C62828")
                .Body(9, 18, 5.5f, "#D32F2F", crush: 150, n: "挤瓶").Gloss(0.8f).Label("#FFFFFF", 0.3f)
                .Cap(4, 3, "#FFFFFF", 120);

            // ============ 日用 ============
            Add("toilet_paper", "卫生纸 (12卷)", Household, 8.97f, 1.3f, Crumple)
                .Body(30, 25, 22, "#FFFFFF", crush: 120, n: "卷纸包").Logo("#29B6F6", 0.6f, 0.3f, 0.6f).Bulky();
            Add("paper_towels", "厨房纸 (6卷)", Household, 7.98f, 1.1f, Crumple)
                .Body(25, 28, 17, "#FFFFFF", crush: 100, n: "纸巾包").Logo("#66BB6A", 0.6f, 0.3f, 0.6f);
            Add("detergent", "洗衣液", Household, 11.97f, 4.5f, Leak).Liquid("#29B6F6")
                .Body(18, 30, 11, "#FF6F00", crush: 900, n: "洗衣液桶").Gloss(0.6f).Logo("#0D47A1", 0.6f, 0.35f, 0.5f)
                .Cap(5, 3, "#0D47A1", 400).Com(0, -4, 0);
            Add("bleach", "漂白剂", Household, 3.48f, 3.8f, Leak).Liquid("#FFFFFF")
                .Body(15, 28, 10, "#FAFAFA", crush: 700, n: "漂白剂桶").Label("#1E88E5", 0.35f)
                .Cap(4, 2.5f, "#1E88E5", 300).Com(0, -4, 0);
            Add("light_bulbs", "灯泡 (4只装)", Household, 4.98f, 0.2f, Shatter).Liquid("#E0E0E0")
                .Body(12, 12, 6, "#FFF59D", crush: 20, impact: 0.45f, n: "灯泡盒").Logo("#F57F17", 0.7f, 0.4f, 0.5f).Freq(0.6f);
            Add("forks", "一次性叉子 (50支)", Household, 2.48f, 0.2f, Crumple)
                .Body(20, 5, 8, "#FFFFFF", crush: 60, sharp: 0.9f, n: "叉子袋").Gloss(0.7f)
                .Deco(Shape.Box, 10.5f, 2.5f, 0, 1, 1, 2, "#E0E0E0").Freq(0.7f);
            Add("skewers", "烧烤竹签", Household, 1.98f, 0.15f, Crumple)
                .Cyl("竹签束", 0, 2, 0, 4, 30, 4, "#D7B98E", 300, sharp: 1.0f).Rot(0, 0, 90).Freq(0.6f);
            Add("knife", "水果刀 (带护套)", Household, 9.97f, 0.15f, Crumple)
                .Body(30, 2, 8, "#ECEFF1", crush: 200, sharp: 0.85f, n: "刀具吸塑").Gloss(0.9f)
                .Deco(Shape.Box, -6, 2.1f, 0, 10, 0.3f, 3, "#212121").Freq(0.4f);
            Add("sponges", "清洁海绵 (3块)", Household, 2.98f, 0.06f, Crumple)
                .Body(14, 4, 10, "#FFEB3B", crush: 0, n: "海绵")
                .Deco(Shape.Box, 0, 4.2f, 0, 14, 0.8f, 10, "#2E7D32");
            Add("foil", "铝箔纸", Household, 4.47f, 0.5f, Dent)
                .Body(32, 5, 5, "#E0E0E0", crush: 100, sharp: 0.2f, n: "长盒").Label("#1565C0", 0.4f);
            Add("flower_vase", "插花 (玻璃花瓶)", Household, 12.98f, 1.25f, Shatter).Liquid("#D6ECFF")
                .CylBody(10, 18, "#CFE8F0", crush: 500, impact: 2.6f, n: "花瓶").Gloss(0.9f)
                .Ball("花束", 0, 25, 0, 17, 14, 17, "#F48FB1", 12).Style(Squish).Com(0, -4, 0).Upright().Freq(0.5f);
            Add("tissues", "抽纸", Household, 1.98f, 0.25f, Crumple)
                .Body(22, 9, 12, "#B3E5FC", crush: 40, n: "纸盒").Deco(Shape.Box, 0, 9.05f, 0, 8, 0.2f, 3, "#FFFFFF");
            Add("batteries", "AA电池 (8节)", Household, 8.98f, 0.2f, Dent)
                .Body(10, 15, 2.5f, "#212121", crush: 2000, n: "吸塑卡").Logo("#FFC107", 0.7f, 0.3f, 0.5f);

            // ============ 宠物 (猫猫区!) ============
            Add("cat_can", "金枪鱼猫罐头", Pet, 0.98f, 0.16f, Dent)
                .CylBody(8.5f, 4, "#B0BEC5", crush: 700, sharp: 0.15f, n: "罐身").Gloss(0.8f).Label("#7E57C2", 0.6f).Freq(1.6f);
            Add("cat_litter", "猫砂 (20磅)", Pet, 12.98f, 9.1f, Dent)
                .Body(30, 30, 20, "#455A64", crush: 4000, n: "猫砂桶").Logo("#FFEB3B", 0.6f, 0.3f, 0.6f)
                .Deco(Shape.Box, 0, 31, 0, 10, 2, 4, "#263238").Bulky().Freq(0.7f);
            Add("catnip", "有机猫薄荷", Pet, 4.98f, 0.03f, Crumple)
                .Body(10, 15, 3, "#66BB6A", crush: 6, n: "薄荷袋").Gloss(0.6f).Logo("#FFFFFF", 0.6f, 0.4f, 0.55f);
            Add("cat_kibble", "全价猫粮 (7磅)", Pet, 14.98f, 3.2f, Crumple)
                .Body(25, 38, 10, "#AD1457", crush: 800, n: "猫粮袋").Logo("#FFCDD2", 0.7f, 0.3f, 0.6f).Com(0, -4, 0);
            Add("cat_wand", "羽毛逗猫棒", Pet, 3.98f, 0.08f, Crumple)
                .Box("逗猫棒", 0, 1.5f, 0, 45, 3, 3, "#FFCA28", 100, sharp: 0.5f)
                .Deco(Shape.Sphere, 23, 1.5f, 0, 7, 4, 4, "#EC407A");
            Add("fish_treats", "冻干小鱼干", Pet, 5.48f, 0.1f, Crumple)
                .Body(12, 17, 4, "#4DD0E1", crush: 15, n: "零食袋").Gloss(0.7f).Logo("#FF7043", 0.6f, 0.35f, 0.55f);
        }
    }
}
