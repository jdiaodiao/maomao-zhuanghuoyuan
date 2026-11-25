using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CatMart.LookKit;

namespace CatMart
{
    /// <summary>场景尺寸常量（米）。</summary>
    public static class Layout
    {
        public const float CounterTop = 0.82f;
        public const float LaneZ = 0.2f;
        public static readonly float[] BagSlotX = { -0.62f, -0.2f, 0.22f, 0.64f };
        public const float BagSlotZ = 0.17f;

        public const float BeltStartX = -4.3f, BeltEndX = -0.9f, BeltWidth = 0.48f;
        public const float EyeX = -1.02f;

        public static readonly Vector3 CartCenter = new Vector3(1.83f, 0f, 0.12f);  // 打包台最右端
        public static readonly Vector3 CartInner = new Vector3(0.8f, 0.42f, 0.44f); // 宽 高 深
        public const float CartFloorY = 0.55f;

        public static readonly Vector3 CustomerPos = new Vector3(0.5f, 0, 1.2f);
        public static readonly Vector3 PlayerStart = new Vector3(0f, 1.5f, -0.6f);
        public static readonly Vector2 PlayerMinXZ = new Vector2(-1.4f, -1.2f);
        public static readonly Vector2 PlayerMaxXZ = new Vector2(2.0f, -0.38f);
    }

    public class StoreRefs
    {
        public Transform Root;
        public ConveyorBelt Belt;
        public ItemFeeder Feeder;
        public ReceiptPrinter Receipt;
        public CatNpc Customer;
        public CatNpc Cashier;
        public Transform Cart;
        public Vector3[] BagSlots;
    }

    /// <summary>程序化搭建 CatMart 收银区（生产级外观，碰撞体与外观分离）。</summary>
    public static class StoreBuilder
    {
        static readonly Color Orange = U.Hex("#F28C28");
        static readonly Color Navy = U.Hex("#24364B");

        // ---------------------------------------------------------------- 材质
        static Material SteelTop => Mats.Lit(U.Hex("#C9CED3"), 0.74f, 0.9f, TexGen.BrushedSteel, TexGen.BrushedSteelN, 0.1f, new Vector2(6, 2));
        static Material SteelSatin => Mats.Lit(U.Hex("#B7BDC3"), 0.6f, 0.85f, TexGen.BrushedSteel, TexGen.BrushedSteelN, 0.1f, new Vector2(2, 4));
        static Material Chrome => Mats.Lit(U.Hex("#D9DDE2"), 0.9f, 1f);
        static Material Laminate => ArtStyle.Cozy ? Mats.Lit(U.Hex("#FFF6E8"), 0.3f) : Mats.Lit(U.Hex("#EEE8DC"), 0.38f, 0, TexGen.Speckle, null, 1, new Vector2(4, 4));
        static Material WoodPanel => ArtStyle.Cozy ? Mats.Lit(U.Hex("#A9DCC9"), 0.3f) : Mats.Lit(U.Hex("#E9D6B4"), 0.42f, 0, TexGen.Wood, null, 1, new Vector2(12, 2));
        static Material Kick => ArtStyle.Cozy ? Mats.Lit(U.Hex("#8A6A55"), 0.3f) : Mats.Lit(U.Hex("#2B2F36"), 0.35f, 0.2f);
        static Material DarkPlastic => Mats.Lit(U.Hex("#2A2D33"), 0.55f, 0);
        static Material OrangeGloss => Mats.Lit(Orange, 0.55f, 0);
        static Material NavyGloss => Mats.Lit(Navy, 0.5f, 0);
        static Material BeltRubber => Mats.Lit(U.Hex("#3A3C40"), 0.28f, 0, TexGen.Rubber, TexGen.RubberN, 1.2f);

        static GameObject Col(Transform parent, Vector3 center, Vector3 size, string name, PhysicsMaterial pm = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            bc.sharedMaterial = pm;
            return go;
        }

        static MB RB(Vector3 size, float r) => Shapes.RoundedBox(size, r, 2, 2);

        public static StoreRefs Build()
        {
            var refs = new StoreRefs();
            var root = new GameObject("CatMart 商店").transform;
            refs.Root = root;

            BuildRoom(root);
            BuildBaggingStation(root, refs);
            BuildConveyor(root, refs);
            BuildUpstream(root, refs);
            BuildReceiptPrinter(root, refs);
            BuildCart(root, refs);
            BuildDecor(root);

            refs.Customer = CatBuilder.Build(root, Layout.CustomerPos, 180, U.Hex("#8A8A8A"), U.Hex("#5C8DD6"), true);
            refs.Customer.gameObject.name = "顾客";
            return refs;
        }

        // ================================================================ 房间
        static void BuildRoom(Transform root)
        {
            var v = new VisualSet();
            // 抛光地砖
            var floorMat = ArtStyle.Cozy
                ? Mats.Lit(Color.white, 0.35f, 0, TexGen.WoodPlanks, null, 1, new Vector2(18 / 2.4f, 14 / 2.4f))
                : Mats.Lit(Color.white, 0.62f, 0, TexGen.FloorTile, TexGen.FloorTileN, 0.5f, new Vector2(18 / 1.2f, 14 / 1.2f));
            var floor = Shapes.Quad(18, 14, 18, 14);
            v.Add(floor, new Vector3(0, 0, 1), Quaternion.Euler(90, 0, 0), floorMat);
            Col(root, new Vector3(0, -0.05f, 1), new Vector3(18, 0.1f, 14), "地板");

            // 墙：上部乳胶漆 + 下部护墙板 + 橙色腰线
            var wall = ArtStyle.Cozy ? Mats.Lit(U.Hex("#FFF2DC"), 0.1f) : Mats.Lit(U.Hex("#F3EEE4"), 0.15f, 0, TexGen.Speckle, null, 1, new Vector2(8, 2));
            var wains = ArtStyle.Cozy ? Mats.Lit(U.Hex("#9ED6C2"), 0.3f) : Mats.Lit(U.Hex("#2F3B4C"), 0.45f, 0);
            void Wall(Vector3 c, float w, float rotY)
            {
                var q = Quaternion.Euler(0, rotY, 0);
                v.Add(Shapes.Quad(w, 4, 4, 2), c + Vector3.up * 2, q, wall);
                v.Add(Shapes.Quad(w, 1.0f), c + Vector3.up * 0.5f + q * new Vector3(0, 0, -0.005f), q, wains);
                v.Add(Shapes.Quad(w, 0.08f), c + Vector3.up * 1.04f + q * new Vector3(0, 0, -0.006f), q, OrangeGloss);
            }
            Wall(new Vector3(0, 0, 8), 18, 0);
            Wall(new Vector3(-9, 0, 1), 14, -90);
            Wall(new Vector3(9, 0, 1), 14, 90);
            Wall(new Vector3(0, 0, -6), 18, 180);

            // 吊顶 + 灯盘
            var ceil = Mats.Lit(Color.white, 0.1f, 0, TexGen.CeilingTile, null, 1, new Vector2(18 / 1.2f, 14 / 1.2f));
            v.Add(Shapes.Quad(18, 14), new Vector3(0, 4.0f, 1), Quaternion.Euler(-90, 0, 0), ceil);
            var lightMat = Mats.Emissive(Color.white, new Color(1.4f, 1.35f, 1.25f));
            var frame = Mats.Lit(U.Hex("#D8D8D8"), 0.5f, 0.6f);
            for (int i = -3; i <= 3; i++)
            for (int j = -2; j <= 3; j++)
            {
                Vector3 p = new Vector3(i * 2.4f, 3.99f, j * 2f);
                v.Add(RB(new Vector3(1.24f, 0.03f, 0.64f), 0.005f), p, Quaternion.identity, frame);
                v.Add(Shapes.Quad(1.18f, 0.58f), p + Vector3.down * 0.016f, Quaternion.Euler(-90, 0, 0), lightMat);
            }
            v.Spawn(root, "房间");

            // 后墙招牌（自发光灯箱）
            var sign = new GameObject("招牌").transform;
            sign.SetParent(root, false);
            sign.localPosition = new Vector3(0, 3.0f, 7.9f);
            var sv = new VisualSet();
            sv.Add(RB(new Vector3(5.4f, 1.05f, 0.16f), 0.04f), Vector3.zero, Quaternion.identity, Mats.Emissive(Orange, Orange * 0.6f));
            sv.Add(RB(new Vector3(5.5f, 1.15f, 0.1f), 0.04f), new Vector3(0, 0, 0.04f), Quaternion.identity, NavyGloss);
            sv.Spawn(sign, "灯箱");
            var signText = Text3D.Create(sign, "CatMart 喵喵超市", new Vector3(0.25f, 0, -0.085f), 0.5f, Color.white);
            signText.fontStyle = FontStyle.Bold;
            CatFaceLogo(sign, new Vector3(-2.15f, 0, -0.085f), 0.72f);
            Text3D.Create(root, "省钱 · 省心 · 省猫粮", new Vector3(0, 2.2f, 7.92f), 0.18f, Navy);

            BuildFridges(root);
        }

        static void CatFaceLogo(Transform parent, Vector3 pos, float size)
        {
            var v = new VisualSet();
            var white = Mats.Emissive(Color.white, new Color(0.8f, 0.8f, 0.8f));
            v.Add(Shapes.Lathe(Shapes.Fillet(LookKit.P(0, 0, size / 2, 0, size / 2, 0.02f, 0, 0.02f), 0.008f), 40), Vector3.zero, Quaternion.Euler(-90, 0, 0), white);
            var ear = Shapes.Prism(Shapes.EnsureCCW(new[] { new Vector2(-size * 0.16f, 0), new Vector2(size * 0.16f, 0), new Vector2(0, size * 0.3f) }), 0.02f);
            v.Add(ear, new Vector3(-size * 0.28f, size * 0.32f, 0), Quaternion.Euler(-90, 0, 15), white);
            v.Add(ear, new Vector3(size * 0.28f, size * 0.32f, 0), Quaternion.Euler(-90, 0, -15), white);
            var eye = Mats.Lit(Orange, 0.6f);
            v.Add(Shapes.Ellipsoid(new Vector3(size * 0.1f, size * 0.15f, 0.02f)), new Vector3(-size * 0.16f, size * 0.05f, -0.015f), Quaternion.identity, eye);
            v.Add(Shapes.Ellipsoid(new Vector3(size * 0.1f, size * 0.15f, 0.02f)), new Vector3(size * 0.16f, size * 0.05f, -0.015f), Quaternion.identity, eye);
            v.Add(Shapes.Ellipsoid(new Vector3(size * 0.08f, size * 0.06f, 0.02f)), new Vector3(0, -size * 0.12f, -0.015f), Quaternion.identity, eye);
            v.Spawn(parent, "logo", pos);
        }

        // ================================================================ 冷柜（后墙）
        static void BuildFridges(Transform root)
        {
            var t = new GameObject("冷柜").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(-6.2f, 0, 7.45f);
            var v = new VisualSet();
            var frame = Mats.Lit(U.Hex("#1E2228"), 0.5f, 0.6f);
            var inside = Mats.Emissive(U.Hex("#EEF3F8"), new Color(0.35f, 0.37f, 0.4f));
            int doors = 5;
            float dw = 0.75f;
            v.Add(RB(new Vector3(dw * doors + 0.1f, 2.3f, 0.8f), 0.02f), new Vector3(dw * doors / 2, 1.15f, 0.05f), Quaternion.identity, frame);
            v.Add(Shapes.Quad(dw * doors, 2.0f), new Vector3(dw * doors / 2, 1.15f, -0.3f), Quaternion.identity, inside);
            var dairy = ItemCatalog.All.Where(d => (d.Category == ItemCategory.Dairy || d.Category == ItemCategory.Frozen || d.Category == ItemCategory.Drinks) && !d.Bulky && d.Size.y < 0.32f).ToList();
            int pick = 0;
            for (int s = 0; s < 4; s++)
            {
                float y = 0.28f + s * 0.48f;
                v.Add(RB(new Vector3(dw * doors, 0.015f, 0.5f), 0.004f), new Vector3(dw * doors / 2, y, -0.12f), Quaternion.identity, Chrome);
                float x = 0.06f;
                while (x < dw * doors - 0.1f)
                {
                    var d = dairy[(pick / 3) % dairy.Count];
                    float w = Mathf.Max(d.Size.x, 0.06f);
                    if (x + w > dw * doors - 0.05f) break;
                    ItemFactory.CreateDisplay(d, t, new Vector3(x + w / 2, y + 0.008f + d.Size.y / 2, -0.25f), Quaternion.identity);
                    x += w + 0.012f;
                    pick++;
                }
            }
            for (int k = 0; k <= doors; k++)
                v.Add(RB(new Vector3(0.05f, 2.1f, 0.06f), 0.01f), new Vector3(k * dw, 1.15f, -0.42f), Quaternion.identity, frame);
            for (int k = 0; k < doors; k++)
            {
                v.Add(Shapes.Quad(dw - 0.05f, 2.0f), new Vector3(k * dw + dw / 2, 1.15f, -0.43f), Quaternion.identity, Mats.Glass(new Color(0.85f, 0.92f, 1f), 0.12f, 0.98f));
                v.Add(Shapes.Tube(new[] { new Vector3(0, -0.35f, 0), new Vector3(0, 0.35f, 0) }, new[] { 0.012f, 0.012f }, 10), new Vector3(k * dw + dw - 0.08f, 1.15f, -0.47f), Quaternion.identity, Chrome);
            }
            v.Add(RB(new Vector3(dw * doors + 0.1f, 0.25f, 0.82f), 0.02f), new Vector3(dw * doors / 2, 2.42f, 0.04f), Quaternion.identity, NavyGloss);
            v.Spawn(t, "冷柜外观");
            Text3D.Create(t, "冷藏 · 冷冻", new Vector3(dw * doors / 2, 2.42f, -0.375f), 0.13f, Color.white);
            StaticBatchingUtility.Combine(t.gameObject);
        }

        // ================================================================ 打包台
        static void BuildCounterBody(VisualSet v, Vector3 center, float len, float depth, bool topSteel = true)
        {
            float top = Layout.CounterTop;
            v.Add(RB(new Vector3(len, top - 0.12f, depth - 0.02f), 0.012f), new Vector3(center.x, (top - 0.12f) / 2 + 0.1f, center.z), Quaternion.identity, Laminate);
            v.Add(RB(new Vector3(len - 0.04f, 0.1f, depth - 0.08f), 0.006f), new Vector3(center.x, 0.05f, center.z + 0.02f), Quaternion.identity, Kick);
            // 前面板（木纹 + 橙色腰带）
            v.Add(RB(new Vector3(len - 0.04f, 0.5f, 0.012f), 0.004f), new Vector3(center.x, 0.48f, center.z - depth / 2 + 0.006f), Quaternion.identity, WoodPanel);
            v.Add(RB(new Vector3(len - 0.02f, 0.05f, 0.016f), 0.004f), new Vector3(center.x, 0.79f, center.z - depth / 2 + 0.004f), Quaternion.identity, OrangeGloss);
            if (topSteel)
                v.Add(Shapes.RoundedBox(new Vector3(len + 0.02f, 0.035f, depth + 0.03f), 0.01f, 3, 3), new Vector3(center.x, top - 0.0175f, center.z), Quaternion.identity, SteelTop);
        }

        static void BuildBaggingStation(Transform root, StoreRefs refs)
        {
            float top = Layout.CounterTop;
            var v = new VisualSet();
            BuildCounterBody(v, new Vector3(0, 0, Layout.LaneZ), 1.75f, 0.62f);
            Col(root, new Vector3(0, top - 0.015f, Layout.LaneZ), new Vector3(1.75f, 0.03f, 0.62f), "打包台面");
            Col(root, new Vector3(0, top / 2 - 0.02f, Layout.LaneZ), new Vector3(1.75f, top - 0.04f, 0.6f), "打包台体");
            // 后挡板：不锈钢底座 + 透明亚克力 + 顶部扶手，防止商品掉到顾客那边
            {
                float x0 = -0.875f, x1 = 1.37f, gz = Layout.LaneZ + 0.315f;
                float len = x1 - x0, cx = (x0 + x1) / 2, gh = 0.18f;
                v.Add(Shapes.RoundedBox(new Vector3(len, 0.035f, 0.03f), 0.008f, 2, 2), new Vector3(cx, top + 0.0175f, gz), Quaternion.identity, SteelSatin);
                v.Add(Shapes.RoundedBox(new Vector3(len - 0.02f, gh - 0.04f, 0.008f), 0.003f, 2, 2), new Vector3(cx, top + 0.035f + (gh - 0.04f) / 2, gz), Quaternion.identity,
                    Mats.Glass(new Color(0.9f, 0.96f, 1f), 0.16f, 0.97f));
                v.Add(Shapes.Tube(new[] { new Vector3(x0, top + gh, gz), new Vector3(x1, top + gh, gz) }, new[] { 0.009f, 0.009f }, 12), Chrome);
                for (float px = x0 + 0.02f; px <= x1; px += (len - 0.04f) / 4f)
                    v.Add(Shapes.Tube(new[] { new Vector3(px, top, gz), new Vector3(px, top + gh, gz) }, new[] { 0.007f, 0.007f }, 10), Chrome);
                Col(root, new Vector3(cx, top + gh / 2, gz), new Vector3(len, gh, 0.03f), "后挡板");
            }
            // 前面板 logo 贴
            var st = new LabelStyle { Brand = "CatMart", Title = "谢谢惠顾 · 欢迎再来", Bg = Orange, Bg2 = Color.Lerp(Orange, Color.black, 0.15f), Accent = Navy, Ink = Color.white, Icon = LabelIcon.Cat, IconColor = Color.white, Seed = 3 };
            var tex = LabelBaker.Sticker(new Vector2(0.9f, 0.3f), st, true);
            v.Add(Shapes.Quad(0.9f, 0.3f), new Vector3(0, 0.48f, Layout.LaneZ - 0.31f - 0.004f), Quaternion.identity, Mats.Label(tex, 0.5f));

            // 袋位：浅凹槽 + 编号
            refs.BagSlots = new Vector3[Layout.BagSlotX.Length];
            var slotMat = Mats.Lit(U.Hex("#A8AFB6"), 0.55f, 0.8f, TexGen.BrushedSteel, null, 1, new Vector2(1, 1));
            for (int i = 0; i < Layout.BagSlotX.Length; i++)
            {
                refs.BagSlots[i] = new Vector3(Layout.BagSlotX[i], top, Layout.BagSlotZ);
                v.Add(Shapes.RoundedBox(new Vector3(0.38f, 0.002f, 0.24f), 0.001f, 1, 1), new Vector3(Layout.BagSlotX[i], top + 0.0005f, Layout.BagSlotZ), Quaternion.identity, slotMat);
                Text3D.Create(root, $"{i + 1}", new Vector3(Layout.BagSlotX[i], top + 0.002f, Layout.BagSlotZ - 0.145f), 0.03f, U.Hex("#5A626A"),
                    TextAnchor.MiddleCenter, Quaternion.Euler(90, 0, 0));
            }

            // 袋架延伸台
            BuildCounterBody(v, new Vector3(1.12f, 0, Layout.LaneZ), 0.5f, 0.62f);
            Col(root, new Vector3(1.12f, top - 0.015f, Layout.LaneZ), new Vector3(0.5f, 0.03f, 0.62f), "袋架台面");
            // 袋架小立牌
            v.Add(RB(new Vector3(0.46f, 0.1f, 0.012f), 0.004f), new Vector3(1.12f, top + 0.12f, Layout.LaneZ + 0.3f), Quaternion.Euler(-8, 0, 0), NavyGloss);
            v.Add(Shapes.Tube(new[] { new Vector3(1.0f, top, Layout.LaneZ + 0.3f), new Vector3(1.0f, top + 0.12f, Layout.LaneZ + 0.3f) }, new[] { 0.006f, 0.006f }, 8), Chrome);
            v.Add(Shapes.Tube(new[] { new Vector3(1.24f, top, Layout.LaneZ + 0.3f), new Vector3(1.24f, top + 0.12f, Layout.LaneZ + 0.3f) }, new[] { 0.006f, 0.006f }, 8), Chrome);
            Text3D.Create(root, "袋子 · 点一下取用", new Vector3(1.12f, top + 0.12f, Layout.LaneZ + 0.292f), 0.035f, Color.white, TextAnchor.MiddleCenter, Quaternion.Euler(-8, 0, 0));
            BagSize[] sizes = { BagSize.Small, BagSize.Medium, BagSize.Large };
            for (int i = 0; i < 3; i++)
            {
                BagFactory.CreateRackStack(root, BagSpec.Get(BagMaterial.Paper, sizes[i]), new Vector3(0.96f + i * 0.155f, top, 0.2f));
                BagFactory.CreateRackStack(root, BagSpec.Get(BagMaterial.Plastic, sizes[i]), new Vector3(0.96f + i * 0.155f, top, -0.01f));
            }

            // 猫铃
            var bell = new GameObject("猫铃").transform;
            bell.SetParent(root, false);
            bell.localPosition = new Vector3(0.78f, top, -0.02f);
            var bv = new VisualSet();
            bv.Add(Shapes.Lathe(Shapes.Fillet(LookKit.P(0, 0, 0.05f, 0, 0.052f, 0.006f, 0.046f, 0.016f, 0, 0.016f), 0.003f), 32), Mats.Lit(U.Hex("#3E2A1E"), 0.55f, 0, TexGen.Wood));
            var gold = Mats.Lit(U.Hex("#E8B64C"), 0.88f, 1f);
            bv.Add(Shapes.Lathe(Shapes.Fillet(LookKit.P(0, 0.016f, 0.04f, 0.016f, 0.041f, 0.02f, 0.034f, 0.045f, 0.012f, 0.058f, 0, 0.06f), 0.006f, 4), 40), gold);
            bv.Add(Shapes.Lathe(LookKit.P(0, 0.058f, 0.004f, 0.058f, 0.004f, 0.068f, 0, 0.068f), 12), gold);
            bv.Add(Shapes.Ellipsoid(Vector3.one * 0.016f), new Vector3(0, 0.072f, 0), gold);
            bv.Spawn(bell, "铃外观");
            var dome = new GameObject("铃");
            dome.transform.SetParent(bell, false);
            dome.transform.localPosition = new Vector3(0, 0.04f, 0);
            dome.AddComponent<SphereCollider>().radius = 0.045f;
            dome.AddComponent<FinishBell>();
            Text3D.Create(bell, "完成", new Vector3(0, 0.009f, -0.054f), 0.018f, Color.white, TextAnchor.MiddleCenter, Quaternion.Euler(30, 0, 0));

            v.Spawn(root, "打包台外观");
        }

        // ================================================================ 传送带
        static void BuildConveyor(Transform root, StoreRefs refs)
        {
            float top = Layout.CounterTop;
            float len = Layout.BeltEndX - Layout.BeltStartX;
            float cx = (Layout.BeltStartX + Layout.BeltEndX) / 2f;
            float z = Layout.LaneZ;
            var v = new VisualSet();

            BuildCounterBody(v, new Vector3(cx, 0, z), len, 0.62f, false);
            Col(root, new Vector3(cx, (top - 0.04f) / 2, z), new Vector3(len, top - 0.04f, 0.62f), "传送带机箱");
            // 不锈钢边框
            float railZ = Layout.BeltWidth / 2 + 0.015f;
            foreach (int s in new[] { -1, 1 })
            {
                v.Add(Shapes.RoundedBox(new Vector3(len, 0.075f, 0.05f), 0.012f, 3, 2), new Vector3(cx, top + 0.0f, z + s * (railZ + 0.012f)), Quaternion.identity, SteelTop);
                Col(root, new Vector3(cx, top + 0.03f, z + s * railZ), new Vector3(len, 0.06f, 0.03f), "侧栏");
            }
            // 端部滚筒
            foreach (float ex in new[] { Layout.BeltStartX + 0.03f, Layout.BeltEndX - 0.03f })
                v.Add(Shapes.Lathe(Shapes.Fillet(LookKit.P(0, -Layout.BeltWidth / 2, 0.03f, -Layout.BeltWidth / 2, 0.03f, Layout.BeltWidth / 2, 0, Layout.BeltWidth / 2), 0.004f), 24),
                    new Vector3(ex, top - 0.03f, z), Quaternion.Euler(90, 0, 0), SteelSatin);
            // 末端挡条
            v.Add(Shapes.RoundedBox(new Vector3(0.03f, 0.03f, Layout.BeltWidth + 0.06f), 0.008f, 2, 2), new Vector3(Layout.BeltEndX + 0.01f, top + 0.012f, z), Quaternion.identity, SteelSatin);
            Col(root, new Vector3(Layout.BeltEndX + 0.01f, top + 0.012f, z), new Vector3(0.025f, 0.024f, Layout.BeltWidth + 0.06f), "末端挡条");

            // 皮带（运动学刚体）
            var belt = new GameObject("传送带");
            belt.transform.SetParent(root, false);
            belt.transform.localPosition = new Vector3(cx, top - 0.02f, z);
            var bc = belt.AddComponent<BoxCollider>();
            bc.size = new Vector3(len, 0.04f, Layout.BeltWidth);
            bc.sharedMaterial = new PhysicsMaterial("Belt") { dynamicFriction = 0.9f, staticFriction = 1.0f, frictionCombine = PhysicsMaterialCombine.Maximum };
            var rb = belt.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var beltVis = new VisualSet();
            var beltMat = new Material(BeltRubber);
            beltMat.mainTextureScale = new Vector2(len / 0.25f, 1);
            beltVis.Add(Shapes.RoundedBox(new Vector3(len - 0.02f, 0.03f, Layout.BeltWidth), 0.012f, 3, 2, (f, l) => l), beltMat);
            var beltGo = beltVis.Spawn(root, "皮带表面", new Vector3(cx, top - 0.015f, z));

            // 光电传感器
            float eyeY = top + 0.014f;
            var housing = DarkPlastic;
            foreach (int s in new[] { -1, 1 })
            {
                v.Add(Shapes.RoundedBox(new Vector3(0.035f, 0.06f, 0.03f), 0.008f, 2, 2), new Vector3(Layout.EyeX, top + 0.06f, z + s * (railZ + 0.012f)), Quaternion.identity, housing);
                v.Add(Shapes.Ellipsoid(new Vector3(0.012f, 0.012f, 0.006f)), new Vector3(Layout.EyeX, eyeY + 0.02f, z + s * (railZ - 0.004f)), Quaternion.identity, Mats.Glass(new Color(0.2f, 0.1f, 0.1f), 0.8f));
            }
            var eyeLight = ProcMesh.Visual(root, Shape.Sphere, new Vector3(Layout.EyeX, top + 0.094f, z - railZ - 0.012f), new Vector3(0.012f, 0.012f, 0.012f), Color.green, null, 0.8f);

            // "下一位顾客"分隔棒（摆在侧栏上）
            var tri = Shapes.Prism(Shapes.EnsureCCW(new[] { new Vector2(-0.02f, 0), new Vector2(0.02f, 0), new Vector2(0, 0.03f) }), 0.4f);
            v.Add(tri, new Vector3(Layout.BeltStartX + 0.9f, top + 0.04f, z - railZ - 0.012f), Quaternion.Euler(0, 0, 90) * Quaternion.Euler(0, 90, 0), OrangeGloss);
            v.Spawn(root, "传送带外观");

            var cb = belt.AddComponent<ConveyorBelt>();
            cb.Direction = Vector3.right;
            cb.BeltRenderer = beltGo.GetComponent<MeshRenderer>();
            cb.EyeLight = eyeLight.GetComponent<MeshRenderer>();
            cb.EyeFrom = new Vector3(Layout.EyeX, eyeY, z - Layout.BeltWidth / 2 + 0.005f);
            cb.EyeTo = new Vector3(Layout.EyeX, eyeY, z + Layout.BeltWidth / 2 - 0.005f);
            refs.Belt = cb;
        }

        // ================================================================ 上游扫描台
        static void BuildUpstream(Transform root, StoreRefs refs)
        {
            float top = Layout.CounterTop;
            float x = Layout.BeltStartX + 0.2f;
            float z = Layout.LaneZ;
            var v = new VisualSet();
            // 扫描塔（竖式扫描窗）
            v.Add(Shapes.RoundedBox(new Vector3(0.08f, 0.32f, 0.36f), 0.02f, 3, 2), new Vector3(x - 0.02f, top + 0.16f, z - 0.33f), Quaternion.identity, DarkPlastic);
            v.Add(Shapes.Quad(0.26f, 0.2f), new Vector3(x - 0.02f + 0.041f, top + 0.17f, z - 0.33f), Quaternion.Euler(0, -90, 0), Mats.Glass(new Color(0.3f, 0.05f, 0.05f), 0.85f));
            var laserMat = Mats.Emissive(Color.red, new Color(3f, 0.1f, 0.1f));
            for (int k = 0; k < 3; k++)
                v.Add(Shapes.Quad(0.22f, 0.003f), new Vector3(x + 0.022f, top + 0.12f + k * 0.04f, z - 0.33f), Quaternion.Euler(0, -90, (k - 1) * 20), laserMat);
            v.Add(Shapes.Quad(0.003f, 0.22f), new Vector3(x + 0.025f, top + 0.003f, z), Quaternion.Euler(90, 0, 0), laserMat);

            // 收银机
            BuildCounterBody(v, new Vector3(x + 0.35f, 0, z - 0.6f), 0.55f, 0.45f);
            v.Add(Shapes.RoundedBox(new Vector3(0.36f, 0.1f, 0.3f), 0.012f, 2, 2), new Vector3(x + 0.35f, top + 0.05f, z - 0.6f), Quaternion.identity, DarkPlastic);
            v.Add(Shapes.Tube(new[] { new Vector3(x + 0.35f, top + 0.1f, z - 0.55f), new Vector3(x + 0.35f, top + 0.33f, z - 0.55f) }, new[] { 0.018f, 0.015f }, 10), SteelSatin);
            var screenRot = Quaternion.Euler(-10, 200, 0);
            Vector3 screenPos = new Vector3(x + 0.35f, top + 0.45f, z - 0.55f);
            v.Add(Shapes.RoundedBox(new Vector3(0.38f, 0.26f, 0.035f), 0.012f, 2, 2), screenPos, screenRot, DarkPlastic);
            var pos = new LabelStyle { Brand = "CatMart POS", Title = "收银中 · 3号台", Sub = "扫码成功 喵~", Bg = U.Hex("#1565C0"), Bg2 = U.Hex("#0D47A1"), Accent = Orange, Ink = Color.white, Icon = LabelIcon.Cat, IconColor = Color.white, Pattern = LabelPattern.Dots, Seed = 9 };
            var screenTex = LabelBaker.Sticker(new Vector2(0.34f, 0.22f), pos);
            var screenMat = Mats.Emissive(Color.white, new Color(0.9f, 0.9f, 0.9f));
            screenMat.mainTexture = screenTex;
            screenMat.SetTexture("_EmissionMap", screenTex);
            v.Add(Shapes.Quad(0.34f, 0.22f), screenPos + screenRot * new Vector3(0, 0, -0.0185f), screenRot, screenMat);
            // 刷卡机
            v.Add(Shapes.RoundedBox(new Vector3(0.09f, 0.16f, 0.04f), 0.012f, 2, 2), new Vector3(x + 0.62f, top + 0.08f, z - 0.42f), Quaternion.Euler(-25, 180, 0), DarkPlastic);
            v.Spawn(root, "收银机外观");

            refs.Cashier = CatBuilder.Build(root, new Vector3(x - 0.05f, 0, z - 0.75f), 10, U.Hex("#E8923A"), Orange, false);
            refs.Cashier.gameObject.name = "上游收银猫";

            var feeder = new GameObject("上游扫描").AddComponent<ItemFeeder>();
            feeder.transform.SetParent(root, false);
            feeder.SpawnCenter = new Vector3(x + 0.08f, top, z);
            feeder.BeltDirection = Vector3.right;
            refs.Feeder = feeder;
        }

        // ================================================================ 小票打印机
        static void BuildReceiptPrinter(Transform root, StoreRefs refs)
        {
            float top = Layout.CounterTop;
            var stand = new GameObject("小票打印机").transform;
            stand.SetParent(root, false);
            stand.localPosition = new Vector3(-1.12f, 0, -0.3f);
            var v = new VisualSet();
            v.Add(Shapes.Tube(new[] { new Vector3(0, 0, 0), new Vector3(0, top + 0.04f, 0) }, new[] { 0.025f, 0.025f }, 12), SteelSatin);
            v.Add(Shapes.Lathe(Shapes.Fillet(LookKit.P(0, 0, 0.14f, 0, 0.14f, 0.012f, 0, 0.012f), 0.005f), 32), Kick);
            v.Add(Shapes.RoundedBox(new Vector3(0.18f, 0.02f, 0.18f), 0.005f, 2, 2), new Vector3(0, top + 0.05f, 0), Quaternion.identity, SteelSatin);
            v.Add(Shapes.RoundedBox(new Vector3(0.15f, 0.09f, 0.17f), 0.025f, 3, 2), new Vector3(0, top + 0.1f, 0), Quaternion.identity, Mats.Lit(U.Hex("#2F3237"), 0.62f));
            v.Add(Shapes.RoundedBox(new Vector3(0.11f, 0.004f, 0.02f), 0.001f, 1, 1), new Vector3(0, top + 0.146f, 0.0f), Quaternion.identity, Mats.Lit(Color.black, 0.2f));
            v.Add(Shapes.Ellipsoid(Vector3.one * 0.008f), new Vector3(0.05f, top + 0.1f, -0.086f), Quaternion.identity, Mats.Emissive(Color.green, new Color(0.2f, 1.2f, 0.2f)));
            v.Spawn(stand, "打印机外观");

            var paperRoot = new GameObject("小票").transform;
            paperRoot.SetParent(stand, false);
            paperRoot.localPosition = new Vector3(0, top + 0.145f, 0);
            paperRoot.localRotation = Quaternion.Euler(18, -77, 0);
            var paper = ProcMesh.Visual(paperRoot, Shape.Box, new Vector3(0, 0.02f, 0), new Vector3(0.1f, 0.04f, 0.0006f), U.Hex("#FBFAF5"), null, 0.1f);
            var text = Text3D.Create(paperRoot, "", new Vector3(-0.047f, 0.03f, -0.0012f), 0.0065f, new Color(0.15f, 0.15f, 0.2f), TextAnchor.UpperLeft);
            var rp = stand.gameObject.AddComponent<ReceiptPrinter>();
            rp.Paper = paper.transform;
            rp.Text = text;
            refs.Receipt = rp;
            refs.Feeder.Receipt = rp;
        }

        // ================================================================ 购物车（镀铬钢丝筐）
        static void BuildCart(Transform root, StoreRefs refs)
        {
            var cart = new GameObject("购物车").transform;
            cart.SetParent(root, false);
            cart.localPosition = Layout.CartCenter;
            Vector3 inner = Layout.CartInner;
            float fy = Layout.CartFloorY;
            float h = inner.y;
            var v = new VisualSet();
            var wire = Chrome;
            var red = Mats.Lit(U.Hex("#D93A3A"), 0.6f);
            const float wr = 0.0028f;

            void Wire(Vector3 a, Vector3 b, float r = wr) => v.Add(Shapes.Tube(new[] { a, b }, new[] { r, r }, 6, false), wire);

            float hx = inner.x / 2, hz = inner.z / 2;
            // 竖丝
            for (float x = -hx; x <= hx + 1e-4f; x += 0.04f)
            {
                Wire(new Vector3(x, fy, -hz), new Vector3(x, fy + h, -hz));
                Wire(new Vector3(x, fy, hz), new Vector3(x, fy + h, hz));
                Wire(new Vector3(x, fy, -hz), new Vector3(x, fy, hz));
            }
            for (float z2 = -hz; z2 <= hz + 1e-4f; z2 += 0.04f)
            {
                Wire(new Vector3(-hx, fy, z2), new Vector3(-hx, fy + h, z2));
                Wire(new Vector3(hx, fy, z2), new Vector3(hx, fy + h, z2));
            }
            // 横丝
            for (float y = fy; y <= fy + h + 1e-4f; y += 0.07f)
            {
                Wire(new Vector3(-hx, y, -hz), new Vector3(hx, y, -hz));
                Wire(new Vector3(-hx, y, hz), new Vector3(hx, y, hz));
                Wire(new Vector3(-hx, y, -hz), new Vector3(-hx, y, hz));
                Wire(new Vector3(hx, y, -hz), new Vector3(hx, y, hz));
            }
            for (float z2 = -hz; z2 <= hz + 1e-4f; z2 += 0.05f) Wire(new Vector3(-hx, fy, z2), new Vector3(hx, fy, z2));
            // 上沿：红色塑料包边
            v.Add(Shapes.RoundedBox(new Vector3(inner.x + 0.03f, 0.02f, 0.02f), 0.008f, 2, 2), new Vector3(0, fy + h + 0.01f, -hz), Quaternion.identity, red);
            v.Add(Shapes.RoundedBox(new Vector3(inner.x + 0.03f, 0.02f, 0.02f), 0.008f, 2, 2), new Vector3(0, fy + h + 0.01f, hz), Quaternion.identity, red);
            v.Add(Shapes.RoundedBox(new Vector3(0.02f, 0.02f, inner.z), 0.008f, 2, 2), new Vector3(-hx, fy + h + 0.01f, 0), Quaternion.identity, red);
            v.Add(Shapes.RoundedBox(new Vector3(0.02f, 0.02f, inner.z), 0.008f, 2, 2), new Vector3(hx, fy + h + 0.01f, 0), Quaternion.identity, red);
            // 把手（朝顾客）
            v.Add(Shapes.Tube(new[] { new Vector3(-hx, fy + h + 0.08f, hz + 0.1f), new Vector3(hx, fy + h + 0.08f, hz + 0.1f) }, new[] { 0.016f, 0.016f }, 12), red);
            Wire(new Vector3(-hx, fy + h, hz), new Vector3(-hx, fy + h + 0.08f, hz + 0.1f), 0.006f);
            Wire(new Vector3(hx, fy + h, hz), new Vector3(hx, fy + h + 0.08f, hz + 0.1f), 0.006f);
            // 底架 + 轮子
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Wire(new Vector3(sx * hx * 0.9f, 0.1f, -hz * 1.1f), new Vector3(sx * hx * 0.9f, 0.1f, hz * 1.2f), 0.008f);
                Wire(new Vector3(sx * hx * 0.9f, 0.1f, -hz), new Vector3(sx * hx * 0.9f, fy, -hz * 0.8f), 0.01f);
                Wire(new Vector3(sx * hx * 0.9f, 0.1f, hz), new Vector3(sx * hx * 0.9f, fy, hz * 0.8f), 0.01f);
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var wheel = Shapes.Lathe(Shapes.Fillet(LookKit.P(0, -0.012f, 0.05f, -0.012f, 0.05f, 0.012f, 0, 0.012f), 0.008f), 20);
                    v.Add(wheel, new Vector3(sx * hx * 0.9f, 0.05f, sz * hz * 1.05f), Quaternion.Euler(0, 0, 90), Mats.Lit(U.Hex("#222222"), 0.3f));
                    v.Add(Shapes.RoundedBox(new Vector3(0.03f, 0.05f, 0.03f), 0.005f, 1, 1), new Vector3(sx * hx * 0.9f, 0.085f, sz * hz * 1.05f), Quaternion.identity, wire);
                }
            }
            v.Spawn(cart, "购物车外观");

            // 碰撞
            Col(cart, new Vector3(0, fy - 0.01f, 0), new Vector3(inner.x, 0.02f, inner.z), "车底");
            Col(cart, new Vector3(-hx, fy + h / 2, 0), new Vector3(0.015f, h, inner.z), "车壁");
            Col(cart, new Vector3(hx, fy + h / 2, 0), new Vector3(0.015f, h, inner.z), "车壁");
            Col(cart, new Vector3(0, fy + h / 2, -hz), new Vector3(inner.x, h, 0.015f), "车壁");
            Col(cart, new Vector3(0, fy + h / 2, hz), new Vector3(inner.x, h, 0.015f), "车壁");
            Text3D.Create(cart, "大件放这里", new Vector3(0, fy + h + 0.06f, -hz - 0.02f), 0.035f, U.Hex("#D93A3A"));
            refs.Cart = cart;
        }

        public static bool InCart(Transform cart, GroceryItem item)
        {
            if (cart == null || item == null || item.IsDestroyed) return false;
            Vector3 p = cart.InverseTransformPoint(item.WorldCom);
            Vector3 h = Layout.CartInner * 0.5f;
            return Mathf.Abs(p.x) <= h.x + 0.02f && Mathf.Abs(p.z) <= h.z + 0.02f && p.y >= Layout.CartFloorY - 0.03f && p.y <= Layout.CartFloorY + Layout.CartInner.y + 0.25f;
        }

        // ================================================================ 装饰：灯柱、货架
        static void BuildDecor(Transform root)
        {
            // 3 号台灯柱
            var pole = new GameObject("3号台灯柱").transform;
            pole.SetParent(root, false);
            pole.localPosition = new Vector3(-0.95f, 0, 0.6f);
            var pv = new VisualSet();
            pv.Add(Shapes.Tube(new[] { Vector3.zero, new Vector3(0, 2.2f, 0) }, new[] { 0.028f, 0.024f }, 14), Chrome);
            pv.Add(Shapes.Lathe(Shapes.Fillet(LookKit.P(0, 2.18f, 0.16f, 2.18f, 0.16f, 2.42f, 0, 2.42f), 0.02f, 3), 40), Mats.Emissive(Orange, Orange * 0.5f));
            pv.Add(Shapes.Lathe(LookKit.P(0, 2.42f, 0.17f, 2.42f, 0.17f, 2.44f, 0, 2.44f), 40), NavyGloss);
            pv.Spawn(pole, "灯柱外观");
            Text3D.Create(pole, "3", new Vector3(0, 2.3f, -0.165f), 0.17f, Color.white);
            Text3D.Create(pole, "3", new Vector3(0, 2.3f, 0.165f), 0.17f, Color.white, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            var bulb = ProcMesh.Visual(pole, Shape.Sphere, new Vector3(0, 2.5f, 0), new Vector3(0.09f, 0.09f, 0.09f), Color.white);
            bulb.GetComponent<MeshRenderer>().sharedMaterial = Mats.Emissive(U.Hex("#9CFF9C"), new Color(0.5f, 1.8f, 0.5f));

            BuildShelves(root);
            BuildBackgroundLane(root);
        }

        static void BuildShelves(Transform root)
        {
            var shelfRoot = new GameObject("货架区").transform;
            shelfRoot.SetParent(root, false);
            string[] aisles = { "零食饮料", "乳制品 · 烘焙", "宠物用品", "日用百货" };
            ItemCategory[][] cats =
            {
                new[] { ItemCategory.Snacks, ItemCategory.Drinks },
                new[] { ItemCategory.Bakery, ItemCategory.Pantry },
                new[] { ItemCategory.Pet, ItemCategory.Produce },
                new[] { ItemCategory.Household, ItemCategory.Pantry },
            };
            var metal = Mats.Lit(U.Hex("#E4E7EA"), 0.45f, 0.35f);
            var peg = Mats.Lit(U.Hex("#D3D7DB"), 0.3f, 0.2f, TexGen.Speckle, TexGen.PitsN, 0.5f, new Vector2(6, 3));
            var tag = Mats.Lit(U.Hex("#FFE34D"), 0.4f);
            for (int a = 0; a < 4; a++)
            {
                float x = -5.4f + a * 3.6f;
                var shelf = new GameObject("货架 " + aisles[a]).transform;
                shelf.SetParent(shelfRoot, false);
                shelf.localPosition = new Vector3(x, 0, 4.5f);
                var v = new VisualSet();
                v.Add(RB(new Vector3(2.4f, 1.9f, 0.06f), 0.01f), new Vector3(0, 1.0f, 0), Quaternion.identity, peg);
                v.Add(RB(new Vector3(2.44f, 0.14f, 0.96f), 0.01f), new Vector3(0, 0.07f, 0), Quaternion.identity, Kick);
                foreach (int sx in new[] { -1, 1 })
                    v.Add(RB(new Vector3(0.05f, 2.0f, 0.96f), 0.01f), new Vector3(sx * 1.22f, 1.0f, 0), Quaternion.identity, metal);
                var defs = ItemCatalog.All.Where(d => cats[a].Contains(d.Category) && !d.Bulky && d.Size.y < 0.36f && d.Size.z < 0.4f).ToList();
                int pick = a * 3;
                for (int side = -1; side <= 1; side += 2)
                for (int lvl = 0; lvl < 4; lvl++)
                {
                    float y = 0.16f + lvl * 0.45f;
                    v.Add(RB(new Vector3(2.4f, 0.025f, 0.44f), 0.004f), new Vector3(0, y, side * 0.25f), Quaternion.identity, metal);
                    v.Add(RB(new Vector3(2.4f, 0.035f, 0.006f), 0.002f), new Vector3(0, y - 0.005f, side * 0.473f), Quaternion.identity, tag);
                    float cursor = -1.16f;
                    while (true)
                    {
                        var d = defs[(pick / 3) % defs.Count];
                        float w = Mathf.Max(d.Size.x, 0.05f);
                        if (cursor + w > 1.16f) break;
                        Quaternion rot = side < 0 ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
                        ItemFactory.CreateDisplay(d, shelf, new Vector3(cursor + w / 2, y + 0.013f + d.Size.y / 2, side * (0.47f - Mathf.Min(d.Size.z, 0.35f) / 2 - 0.01f)), rot);
                        cursor += w + 0.008f;
                        pick++;
                    }
                }
                // 吊牌
                v.Add(RB(new Vector3(1.6f, 0.36f, 0.05f), 0.02f), new Vector3(0, 3.0f, 0), Quaternion.identity, NavyGloss);
                v.Add(Shapes.Tube(new[] { new Vector3(-0.6f, 3.18f, 0), new Vector3(-0.6f, 4f, 0) }, new[] { 0.004f, 0.004f }, 6), Chrome);
                v.Add(Shapes.Tube(new[] { new Vector3(0.6f, 3.18f, 0), new Vector3(0.6f, 4f, 0) }, new[] { 0.004f, 0.004f }, 6), Chrome);
                v.Spawn(shelf, "货架外观");
                Text3D.Create(shelf, aisles[a], new Vector3(0, 3.0f, -0.03f), 0.17f, Color.white);
                Text3D.Create(shelf, $"{a + 1}", new Vector3(-0.68f, 3.0f, -0.03f), 0.2f, Orange);
            }
            StaticBatchingUtility.Combine(shelfRoot.gameObject);
        }

        static void BuildBackgroundLane(Transform root)
        {
            var v = new VisualSet();
            BuildCounterBody(v, new Vector3(-1.8f, 0, -2.6f), 3.6f, 0.6f, false);
            var beltMat = new Material(BeltRubber);
            beltMat.mainTextureScale = new Vector2(3.4f / 0.25f, 1);
            v.Add(Shapes.RoundedBox(new Vector3(3.4f, 0.03f, 0.46f), 0.012f, 3, 2, (f, l) => l), new Vector3(-1.8f, Layout.CounterTop - 0.005f, -2.6f), Quaternion.identity, beltMat);
            foreach (int s in new[] { -1, 1 })
                v.Add(Shapes.RoundedBox(new Vector3(3.6f, 0.075f, 0.05f), 0.012f, 3, 2), new Vector3(-1.8f, Layout.CounterTop, -2.6f + s * 0.27f), Quaternion.identity, SteelTop);
            v.Add(Shapes.Tube(new[] { new Vector3(0.1f, 0, -2.3f), new Vector3(0.1f, 2.2f, -2.3f) }, new[] { 0.028f, 0.024f }, 14), Chrome);
            v.Add(Shapes.Lathe(Shapes.Fillet(LookKit.P(0, 2.18f, 0.16f, 2.18f, 0.16f, 2.42f, 0, 2.42f), 0.02f, 3), 40), new Vector3(0.1f, 0, -2.3f), Quaternion.identity, Mats.Emissive(Orange, Orange * 0.5f));
            v.Spawn(root, "4号收银台");
            Col(root, new Vector3(-1.8f, Layout.CounterTop / 2, -2.6f), new Vector3(3.6f, Layout.CounterTop, 0.6f), "4号台碰撞");
            Text3D.Create(root, "4", new Vector3(0.1f, 2.3f, -2.3f + 0.165f), 0.17f, Color.white, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
        }

        // ================================================================ 灯光
        public static void SetupLighting(Transform root)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.63f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.48f, 0.46f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.28f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.86f, 0.85f, 0.82f);
            RenderSettings.fogStartDistance = 9f;
            RenderSettings.fogEndDistance = 30f;

            var sun = new GameObject("主光源").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.intensity = 0.7f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.02f;
            sun.shadowNormalBias = 0.15f;
            sun.transform.rotation = Quaternion.Euler(58, 28, 0);

            // 反射探针：让金属、玻璃、塑料反射出店内环境
            var probeGo = new GameObject("反射探针");
            probeGo.transform.SetParent(root, false);
            probeGo.transform.position = new Vector3(0, 1.4f, 0.4f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(18, 8, 14);
            probe.center = new Vector3(0, 0.6f, 0.6f);
            probe.boxProjection = true;
            probe.resolution = 256;
            probe.hdr = true;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(0.85f, 0.85f, 0.83f);
            probe.intensity = 1f;
            probeGo.AddComponent<ProbeRefresher>();

            QualitySettings.shadowDistance = 14f;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.pixelLightCount = 8;

            // 顶灯（点光源，打出商品的高光）
            Vector3[] pts =
            {
                new Vector3(0, 2.7f, 0.0f), new Vector3(-1.8f, 2.7f, 0.2f), new Vector3(-3.6f, 2.7f, 0.2f), new Vector3(1.6f, 2.7f, 0.4f),
                new Vector3(-3.6f, 3.2f, 4.0f), new Vector3(0, 3.2f, 4.0f), new Vector3(3.6f, 3.2f, 4.0f), new Vector3(-5f, 3.0f, 6.5f)
            };
            foreach (var p in pts)
            {
                var l = new GameObject("顶灯").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = p;
                l.type = LightType.Point;
                l.range = 5.5f;
                l.intensity = 0.55f;
                l.color = new Color(1f, 0.97f, 0.92f);
            }
            // 打包台正上方的软阴影灯
            var spot = new GameObject("打包台灯").AddComponent<Light>();
            spot.transform.SetParent(root, false);
            spot.transform.position = new Vector3(0.1f, 2.6f, -0.2f);
            spot.transform.rotation = Quaternion.Euler(75, 0, 0);
            spot.type = LightType.Spot;
            spot.spotAngle = 85;
            spot.range = 4f;
            spot.intensity = 0.75f;
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.55f;
            spot.color = new Color(1f, 0.97f, 0.93f);

            if (ArtStyle.Cozy)
            {
                // 森友会风：明亮、温暖、阴影柔和偏淡
                // 卡通光影的受光面是"整面全亮"，所以总亮度要比写实风低不少
                RenderSettings.ambientSkyColor = new Color(0.40f, 0.40f, 0.44f);
                RenderSettings.ambientEquatorColor = new Color(0.36f, 0.33f, 0.32f);
                RenderSettings.ambientGroundColor = new Color(0.26f, 0.23f, 0.23f);
                RenderSettings.fogColor = new Color(0.95f, 0.91f, 0.86f);
                RenderSettings.fogStartDistance = 12f;
                RenderSettings.fogEndDistance = 40f;
                sun.intensity = 0.62f;
                sun.color = new Color(1f, 0.93f, 0.83f);
                sun.shadowStrength = 0.5f;
                sun.transform.rotation = Quaternion.Euler(52, 35, 0);
                spot.intensity = 0.12f;
                spot.shadowStrength = 0.3f;
                spot.color = new Color(1f, 0.92f, 0.8f);
                probe.backgroundColor = new Color(0.8f, 0.75f, 0.68f);
                probe.intensity = 0.35f;
                foreach (var l in root.GetComponentsInChildren<Light>())
                    if (l.type == LightType.Point) { l.intensity = 0.1f; l.color = new Color(1f, 0.9f, 0.78f); }
            }
        }
    }
}
