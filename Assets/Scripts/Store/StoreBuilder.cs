using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>场景尺寸常量（米）。</summary>
    public static class Layout
    {
        public const float CounterTop = 0.9f;
        public const float LaneZ = 0.2f;
        public static readonly float[] BagSlotX = { -0.62f, -0.2f, 0.22f, 0.64f };
        public const float BagSlotZ = 0.22f;

        public const float BeltStartX = -4.3f, BeltEndX = -0.9f, BeltWidth = 0.48f;
        public const float EyeX = -1.02f;

        public static readonly Vector3 CartCenter = new Vector3(-0.3f, 0f, 0.9f);
        public static readonly Vector3 CartInner = new Vector3(0.8f, 0.42f, 0.44f); // 宽 高 深
        public const float CartFloorY = 0.55f;

        public static readonly Vector3 CustomerPos = new Vector3(0.5f, 0, 1.2f);
        public static readonly Vector3 PlayerStart = new Vector3(0f, 1.5f, -0.6f);
        public static readonly Vector2 PlayerMinXZ = new Vector2(-1.4f, -1.2f);
        public static readonly Vector2 PlayerMaxXZ = new Vector2(1.5f, -0.38f);
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

    /// <summary>程序化搭建 CatMart 收银区。</summary>
    public static class StoreBuilder
    {
        static readonly Color Steel = U.Hex("#B8BEC4");
        static readonly Color SteelDark = U.Hex("#7D858C");
        static readonly Color CounterBody = U.Hex("#E9E4DA");
        static readonly Color Orange = U.Hex("#F28C28");
        static readonly Color Navy = U.Hex("#24364B");

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

        // ---------------------------------------------------------------- 房间
        static void BuildRoom(Transform root)
        {
            // 地板（格子砖）
            var floorTex = new Texture2D(64, 64) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
            for (int x = 0; x < 64; x++)
            for (int y = 0; y < 64; y++)
            {
                bool a = (x < 32) ^ (y < 32);
                bool grout = x % 32 == 0 || y % 32 == 0;
                float n = Mathf.PerlinNoise(x * 0.2f, y * 0.2f) * 0.04f;
                Color c = grout ? new Color(0.62f, 0.62f, 0.6f) : a ? new Color(0.93f + n, 0.92f + n, 0.89f + n) : new Color(0.83f + n, 0.84f + n, 0.82f + n);
                floorTex.SetPixel(x, y, c);
            }
            floorTex.Apply();
            var floor = new GameObject("地板");
            floor.transform.SetParent(root, false);
            floor.transform.localPosition = new Vector3(0, -0.05f, 1f);
            floor.transform.localScale = new Vector3(18, 0.1f, 14);
            floor.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Get(Shape.Box);
            var fm = Mats.Textured(floorTex, Color.white, 0.55f);
            fm.mainTextureScale = new Vector2(18, 14);
            floor.AddComponent<MeshRenderer>().sharedMaterial = fm;
            floor.AddComponent<BoxCollider>();

            Color wall = U.Hex("#F3EEE3");
            ProcMesh.Visual(root, Shape.Box, new Vector3(0, 2, 8), new Vector3(18, 4, 0.2f), wall);
            ProcMesh.Visual(root, Shape.Box, new Vector3(-9, 2, 1), new Vector3(0.2f, 4, 14), wall);
            ProcMesh.Visual(root, Shape.Box, new Vector3(9, 2, 1), new Vector3(0.2f, 4, 14), wall);
            ProcMesh.Visual(root, Shape.Box, new Vector3(0, 2, -6), new Vector3(18, 4, 0.2f), wall);
            ProcMesh.Visual(root, Shape.Box, new Vector3(0, 0.15f, 7.85f), new Vector3(18, 0.3f, 0.1f), Orange);
            // 天花板 + 灯带
            ProcMesh.Visual(root, Shape.Box, new Vector3(0, 4.05f, 1), new Vector3(18, 0.1f, 14), U.Hex("#E6E6E6"));
            var lightMat = Mats.Emissive(Color.white, new Color(1.2f, 1.15f, 1.0f));
            for (int i = -3; i <= 3; i++)
            for (int j = -2; j <= 3; j++)
            {
                var p = ProcMesh.Visual(root, Shape.Box, new Vector3(i * 2.4f, 3.98f, j * 2f), new Vector3(1.6f, 0.04f, 0.3f), Color.white);
                p.GetComponent<MeshRenderer>().sharedMaterial = lightMat;
            }

            // 后墙大招牌
            var sign = new GameObject("招牌").transform;
            sign.SetParent(root, false);
            sign.localPosition = new Vector3(0, 3.0f, 7.85f);
            ProcMesh.Visual(sign, Shape.Box, Vector3.zero, new Vector3(5.2f, 1.0f, 0.1f), Orange, null, 0.4f);
            var signText = Text3D.Create(sign, "CatMart 喵喵超市", new Vector3(0.25f, 0, -0.06f), 0.5f, Color.white);
            signText.fontStyle = FontStyle.Bold;
            CatFaceLogo(sign, new Vector3(-2.1f, 0, -0.06f), 0.7f);
            var sub = Text3D.Create(root, "省钱 · 省心 · 省猫粮", new Vector3(0, 2.25f, 7.85f - 0.06f), 0.18f, Navy);
        }

        static void CatFaceLogo(Transform parent, Vector3 pos, float size)
        {
            var t = new GameObject("logo").transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            ProcMesh.Visual(t, Shape.Cylinder, Vector3.zero, new Vector3(size, 0.02f, size), Color.white, Quaternion.Euler(90, 0, 0));
            ProcMesh.Visual(t, Shape.Cone, new Vector3(-size * 0.28f, size * 0.42f, 0), new Vector3(size * 0.3f, size * 0.3f, 0.02f), Color.white, Quaternion.Euler(0, 0, 15));
            ProcMesh.Visual(t, Shape.Cone, new Vector3(size * 0.28f, size * 0.42f, 0), new Vector3(size * 0.3f, size * 0.3f, 0.02f), Color.white, Quaternion.Euler(0, 0, -15));
            ProcMesh.Visual(t, Shape.Sphere, new Vector3(-size * 0.16f, size * 0.05f, -0.015f), new Vector3(size * 0.1f, size * 0.14f, 0.01f), Orange);
            ProcMesh.Visual(t, Shape.Sphere, new Vector3(size * 0.16f, size * 0.05f, -0.015f), new Vector3(size * 0.1f, size * 0.14f, 0.01f), Orange);
            ProcMesh.Visual(t, Shape.Sphere, new Vector3(0, -size * 0.12f, -0.015f), new Vector3(size * 0.08f, size * 0.06f, 0.01f), Orange);
        }

        // ---------------------------------------------------------------- 打包台
        static void BuildBaggingStation(Transform root, StoreRefs refs)
        {
            float top = Layout.CounterTop;
            // 主台
            ProcMesh.Visual(root, Shape.Box, new Vector3(0, top / 2 - 0.015f, Layout.LaneZ), new Vector3(1.75f, top - 0.03f, 0.6f), CounterBody);
            ProcMesh.Solid(root, Shape.Box, new Vector3(0, top - 0.015f, Layout.LaneZ), new Vector3(1.75f, 0.03f, 0.62f), Steel, null, 0.7f, "打包台面");
            ProcMesh.Visual(root, Shape.Box, new Vector3(0, top * 0.5f, Layout.LaneZ - 0.305f), new Vector3(1.75f, 0.12f, 0.01f), Orange);
            // 挡板（后侧，防止东西掉到顾客那边）
            ProcMesh.Solid(root, Shape.Box, new Vector3(0, top + 0.03f, Layout.LaneZ + 0.32f), new Vector3(1.75f, 0.06f, 0.02f), SteelDark, null, 0.6f, "后挡板");

            // 袋位标记
            refs.BagSlots = new Vector3[Layout.BagSlotX.Length];
            for (int i = 0; i < Layout.BagSlotX.Length; i++)
            {
                refs.BagSlots[i] = new Vector3(Layout.BagSlotX[i], top, Layout.BagSlotZ);
                ProcMesh.Visual(root, Shape.Box, new Vector3(Layout.BagSlotX[i], top + 0.0005f, Layout.BagSlotZ), new Vector3(0.38f, 0.001f, 0.24f), U.Hex("#9AA3AA"));
                ProcMesh.Visual(root, Shape.Box, new Vector3(Layout.BagSlotX[i], top + 0.001f, Layout.BagSlotZ), new Vector3(0.36f, 0.001f, 0.22f), Steel, null, 0.7f);
                Text3D.Create(root, $"{i + 1}", new Vector3(Layout.BagSlotX[i], top + 0.002f, Layout.BagSlotZ - 0.14f), 0.03f, SteelDark,
                    TextAnchor.MiddleCenter, Quaternion.Euler(90, 0, 0));
            }

            // 袋架（右侧延伸台）
            ProcMesh.Visual(root, Shape.Box, new Vector3(1.12f, top / 2 - 0.015f, Layout.LaneZ), new Vector3(0.5f, top - 0.03f, 0.6f), CounterBody);
            ProcMesh.Solid(root, Shape.Box, new Vector3(1.12f, top - 0.015f, Layout.LaneZ), new Vector3(0.5f, 0.03f, 0.62f), Steel, null, 0.7f, "袋架台面");
            BagSize[] sizes = { BagSize.Small, BagSize.Medium, BagSize.Large };
            for (int i = 0; i < 3; i++)
            {
                BagFactory.CreateRackStack(root, BagSpec.Get(BagMaterial.Paper, sizes[i]), new Vector3(0.96f + i * 0.155f, top, 0.37f));
                BagFactory.CreateRackStack(root, BagSpec.Get(BagMaterial.Plastic, sizes[i]), new Vector3(0.96f + i * 0.155f, top, 0.07f));
            }
            Text3D.Create(root, "袋架", new Vector3(1.12f, top + 0.002f, -0.06f), 0.035f, Navy, TextAnchor.MiddleCenter, Quaternion.Euler(90, 0, 0));

            // 猫铃（完成订单）
            var bell = new GameObject("猫铃").transform;
            bell.SetParent(root, false);
            bell.localPosition = new Vector3(0.78f, top, -0.02f);
            ProcMesh.Visual(bell, Shape.Cylinder, new Vector3(0, 0.008f, 0), new Vector3(0.1f, 0.016f, 0.1f), Navy, null, 0.5f);
            var dome = ProcMesh.Solid(bell, Shape.Sphere, new Vector3(0, 0.035f, 0), new Vector3(0.08f, 0.07f, 0.08f), U.Hex("#F2C14E"), null, 0.95f, "铃");
            dome.AddComponent<FinishBell>();
            ProcMesh.Visual(bell, Shape.Sphere, new Vector3(0, 0.072f, 0), new Vector3(0.018f, 0.018f, 0.018f), U.Hex("#F2C14E"), null, 0.95f);
            Text3D.Create(bell, "完成", new Vector3(0, 0.0175f, -0.065f), 0.022f, Navy, TextAnchor.MiddleCenter, Quaternion.Euler(30, 0, 0));
        }

        // ---------------------------------------------------------------- 传送带
        static void BuildConveyor(Transform root, StoreRefs refs)
        {
            float top = Layout.CounterTop;
            float len = Layout.BeltEndX - Layout.BeltStartX;
            float cx = (Layout.BeltStartX + Layout.BeltEndX) / 2f;
            float z = Layout.LaneZ;

            ProcMesh.Visual(root, Shape.Box, new Vector3(cx, (top - 0.04f) / 2, z), new Vector3(len, top - 0.04f, 0.62f), CounterBody);
            ProcMesh.Visual(root, Shape.Box, new Vector3(cx, top * 0.5f, z - 0.305f), new Vector3(len, 0.12f, 0.01f), Orange);

            // 皮带（运动学刚体）
            var belt = new GameObject("传送带");
            belt.transform.SetParent(root, false);
            belt.transform.localPosition = new Vector3(cx, top - 0.02f, z);
            var bc = belt.AddComponent<BoxCollider>();
            bc.size = new Vector3(len, 0.04f, Layout.BeltWidth);
            bc.sharedMaterial = new PhysicsMaterial("Belt") { dynamicFriction = 0.9f, staticFriction = 1.0f, frictionCombine = PhysicsMaterialCombine.Maximum };
            var rb = belt.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.None;

            var vis = ProcMesh.Visual(root, Shape.Box, new Vector3(cx, top - 0.02f, z), new Vector3(len, 0.04f, Layout.BeltWidth), Color.white, null, 0.15f, "皮带表面");
            var beltMat = Mats.Textured(ConveyorBelt.StripeTexture(), Color.white, 0.15f);
            beltMat.mainTextureScale = new Vector2(len / 0.25f, 1);
            vis.GetComponent<MeshRenderer>().sharedMaterial = beltMat;

            // 侧栏
            float railZ = Layout.BeltWidth / 2 + 0.015f;
            ProcMesh.Solid(root, Shape.Box, new Vector3(cx, top + 0.03f, z - railZ), new Vector3(len, 0.06f, 0.03f), Steel, null, 0.8f, "侧栏");
            ProcMesh.Solid(root, Shape.Box, new Vector3(cx, top + 0.03f, z + railZ), new Vector3(len, 0.06f, 0.03f), Steel, null, 0.8f, "侧栏");
            // 末端挡条
            ProcMesh.Solid(root, Shape.Box, new Vector3(Layout.BeltEndX + 0.01f, top + 0.012f, z), new Vector3(0.025f, 0.024f, Layout.BeltWidth + 0.06f), SteelDark, null, 0.6f, "末端挡条");

            // 光电传感器
            float eyeY = top + 0.014f;
            ProcMesh.Visual(root, Shape.Box, new Vector3(Layout.EyeX, top + 0.05f, z - railZ - 0.005f), new Vector3(0.03f, 0.05f, 0.03f), Navy);
            var eyeLight = ProcMesh.Visual(root, Shape.Sphere, new Vector3(Layout.EyeX, top + 0.085f, z - railZ - 0.005f), new Vector3(0.015f, 0.015f, 0.015f), Color.green, null, 0.8f);
            ProcMesh.Visual(root, Shape.Box, new Vector3(Layout.EyeX, top + 0.05f, z + railZ + 0.005f), new Vector3(0.03f, 0.05f, 0.03f), Navy);

            var cb = belt.AddComponent<ConveyorBelt>();
            cb.Direction = Vector3.right;
            cb.BeltRenderer = vis.GetComponent<MeshRenderer>();
            cb.EyeLight = eyeLight.GetComponent<MeshRenderer>();
            cb.EyeFrom = new Vector3(Layout.EyeX, eyeY, z - Layout.BeltWidth / 2 + 0.005f);
            cb.EyeTo = new Vector3(Layout.EyeX, eyeY, z + Layout.BeltWidth / 2 - 0.005f);
            refs.Belt = cb;
        }

        // ---------------------------------------------------------------- 上游扫描台
        static void BuildUpstream(Transform root, StoreRefs refs)
        {
            float top = Layout.CounterTop;
            float x = Layout.BeltStartX + 0.2f;
            float z = Layout.LaneZ;
            // 扫描拱门
            ProcMesh.Visual(root, Shape.Box, new Vector3(x, top + 0.2f, z - 0.3f), new Vector3(0.06f, 0.4f, 0.05f), Navy);
            ProcMesh.Visual(root, Shape.Box, new Vector3(x, top + 0.2f, z + 0.3f), new Vector3(0.06f, 0.4f, 0.05f), Navy);
            ProcMesh.Visual(root, Shape.Box, new Vector3(x, top + 0.42f, z), new Vector3(0.1f, 0.06f, 0.65f), Navy);
            Text3D.Create(root, "SCAN", new Vector3(x - 0.051f, top + 0.42f, z), 0.04f, Orange, TextAnchor.MiddleCenter, Quaternion.Euler(0, 90, 0));
            var laser = ProcMesh.Visual(root, Shape.Box, new Vector3(x, top + 0.385f, z), new Vector3(0.01f, 0.004f, 0.58f), Color.red);
            laser.GetComponent<MeshRenderer>().sharedMaterial = Mats.Emissive(Color.red, new Color(2f, 0.1f, 0.1f));

            // 收银机 + 屏幕
            ProcMesh.Visual(root, Shape.Box, new Vector3(x + 0.35f, top / 2, z - 0.55f), new Vector3(0.5f, top, 0.4f), CounterBody);
            ProcMesh.Visual(root, Shape.Box, new Vector3(x + 0.35f, top + 0.05f, z - 0.55f), new Vector3(0.35f, 0.1f, 0.3f), U.Hex("#333333"));
            ProcMesh.Visual(root, Shape.Cylinder, new Vector3(x + 0.35f, top + 0.25f, z - 0.55f), new Vector3(0.04f, 0.3f, 0.04f), SteelDark);
            var screen = ProcMesh.Visual(root, Shape.Box, new Vector3(x + 0.35f, top + 0.45f, z - 0.55f), new Vector3(0.36f, 0.24f, 0.03f), U.Hex("#111111"), Quaternion.Euler(0, 200, 0));
            ProcMesh.Visual(screen.transform, Shape.Box, new Vector3(0, 0, -0.55f), new Vector3(0.9f, 0.85f, 0.1f), U.Hex("#1E88E5"))
                .GetComponent<MeshRenderer>().sharedMaterial = Mats.Emissive(U.Hex("#1E88E5"), new Color(0.2f, 0.5f, 1.2f));

            refs.Cashier = CatBuilder.Build(root, new Vector3(x - 0.05f, 0, z - 0.6f), 10, U.Hex("#E8923A"), Orange, false);
            refs.Cashier.gameObject.name = "上游收银猫";

            var feeder = new GameObject("上游扫描").AddComponent<ItemFeeder>();
            feeder.transform.SetParent(root, false);
            feeder.SpawnCenter = new Vector3(x + 0.08f, top, z);
            feeder.BeltDirection = Vector3.right;
            feeder.ScannerLight = laser.transform;
            refs.Feeder = feeder;
        }

        // ---------------------------------------------------------------- 小票打印机
        static void BuildReceiptPrinter(Transform root, StoreRefs refs)
        {
            float top = Layout.CounterTop;
            var stand = new GameObject("小票打印机").transform;
            stand.SetParent(root, false);
            stand.localPosition = new Vector3(-1.12f, 0, -0.3f);
            ProcMesh.Visual(stand, Shape.Box, new Vector3(0, (top + 0.05f) / 2, 0), new Vector3(0.22f, top + 0.05f, 0.22f), CounterBody);
            ProcMesh.Visual(stand, Shape.Box, new Vector3(0, top + 0.09f, 0), new Vector3(0.16f, 0.08f, 0.18f), U.Hex("#2B2B2B"), null, 0.5f);

            var paperRoot = new GameObject("小票").transform;
            paperRoot.SetParent(stand, false);
            paperRoot.localPosition = new Vector3(0, top + 0.13f, 0);
            paperRoot.localRotation = Quaternion.Euler(18, -77, 0);
            var paper = ProcMesh.Visual(paperRoot, Shape.Box, new Vector3(0, 0.02f, 0), new Vector3(0.1f, 0.04f, 0.001f), U.Hex("#FBFAF5"), null, 0.1f);
            var text = Text3D.Create(paperRoot, "", new Vector3(-0.047f, 0.03f, -0.002f), 0.0065f, new Color(0.15f, 0.15f, 0.2f), TextAnchor.UpperLeft);

            var rp = stand.gameObject.AddComponent<ReceiptPrinter>();
            rp.Paper = paper.transform;
            rp.Text = text;
            refs.Receipt = rp;
            refs.Feeder.Receipt = rp;
        }

        // ---------------------------------------------------------------- 购物车
        static void BuildCart(Transform root, StoreRefs refs)
        {
            var cart = new GameObject("购物车").transform;
            cart.SetParent(root, false);
            cart.localPosition = Layout.CartCenter;
            Vector3 inner = Layout.CartInner;
            float fy = Layout.CartFloorY;
            Color wire = U.Hex("#C7CCD1");
            Color red = U.Hex("#D93A3A");

            ProcMesh.Solid(cart, Shape.Box, new Vector3(0, fy - 0.01f, 0), new Vector3(inner.x, 0.02f, inner.z), wire, null, 0.8f, "车底");
            float h = inner.y;
            ProcMesh.Solid(cart, Shape.Box, new Vector3(-inner.x / 2, fy + h / 2, 0), new Vector3(0.015f, h, inner.z), wire, null, 0.8f);
            ProcMesh.Solid(cart, Shape.Box, new Vector3(inner.x / 2, fy + h / 2, 0), new Vector3(0.015f, h, inner.z), wire, null, 0.8f);
            ProcMesh.Solid(cart, Shape.Box, new Vector3(0, fy + h / 2, -inner.z / 2), new Vector3(inner.x, h, 0.015f), wire, null, 0.8f);
            ProcMesh.Solid(cart, Shape.Box, new Vector3(0, fy + h / 2, inner.z / 2), new Vector3(inner.x, h, 0.015f), wire, null, 0.8f);
            // 网格感：几条横杠
            for (int i = 1; i < 5; i++)
                ProcMesh.Visual(cart, Shape.Box, new Vector3(0, fy + h * i / 5f, -inner.z / 2 - 0.01f), new Vector3(inner.x, 0.006f, 0.006f), SteelDark);
            ProcMesh.Visual(cart, Shape.Box, new Vector3(0, fy + h + 0.01f, -inner.z / 2), new Vector3(inner.x + 0.02f, 0.02f, 0.02f), red);
            // 把手（朝顾客一侧）
            ProcMesh.Visual(cart, Shape.Cylinder, new Vector3(0, fy + h + 0.08f, inner.z / 2 + 0.1f), new Vector3(0.03f, inner.x, 0.03f), red, Quaternion.Euler(0, 0, 90));
            // 支架和轮子
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                ProcMesh.Visual(cart, Shape.Cylinder, new Vector3(sx * inner.x * 0.45f, fy / 2, sz * inner.z * 0.45f), new Vector3(0.02f, fy, 0.02f), wire);
                ProcMesh.Visual(cart, Shape.Cylinder, new Vector3(sx * inner.x * 0.45f, 0.05f, sz * inner.z * 0.45f), new Vector3(0.1f, 0.03f, 0.1f), U.Hex("#222222"), Quaternion.Euler(0, 0, 90));
            }
            Text3D.Create(cart, "大件放这里", new Vector3(0, fy + h + 0.06f, -inner.z / 2 - 0.02f), 0.035f, red);
            refs.Cart = cart;
        }

        public static bool InCart(Transform cart, GroceryItem item)
        {
            if (cart == null || item == null || item.IsDestroyed) return false;
            Vector3 p = cart.InverseTransformPoint(item.WorldCom);
            Vector3 h = Layout.CartInner * 0.5f;
            return Mathf.Abs(p.x) <= h.x + 0.02f && Mathf.Abs(p.z) <= h.z + 0.02f && p.y >= Layout.CartFloorY - 0.03f && p.y <= Layout.CartFloorY + Layout.CartInner.y + 0.25f;
        }

        // ---------------------------------------------------------------- 装饰
        static void BuildDecor(Transform root)
        {
            // 收银台号码灯
            var pole = new GameObject("3号台灯柱").transform;
            pole.SetParent(root, false);
            pole.localPosition = new Vector3(-0.95f, 0, 0.6f);
            ProcMesh.Visual(pole, Shape.Cylinder, new Vector3(0, 1.1f, 0), new Vector3(0.05f, 2.2f, 0.05f), SteelDark);
            ProcMesh.Visual(pole, Shape.Cylinder, new Vector3(0, 2.3f, 0), new Vector3(0.3f, 0.22f, 0.3f), Orange, null, 0.4f);
            Text3D.Create(pole, "3", new Vector3(0, 2.3f, -0.155f), 0.18f, Color.white);
            Text3D.Create(pole, "3", new Vector3(0, 2.3f, 0.155f), 0.18f, Color.white, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
            var bulb = ProcMesh.Visual(pole, Shape.Sphere, new Vector3(0, 2.45f, 0), new Vector3(0.1f, 0.1f, 0.1f), Color.white);
            bulb.GetComponent<MeshRenderer>().sharedMaterial = Mats.Emissive(U.Hex("#9CFF9C"), new Color(0.4f, 1.5f, 0.4f));

            // 货架区
            var rng = new System.Random(42);
            string[] aisles = { "零食饮料", "乳制品·冷冻", "宠物用品", "日用百货" };
            for (int a = 0; a < 4; a++)
            {
                float x = -5.4f + a * 3.6f;
                var shelf = new GameObject("货架" + aisles[a]).transform;
                shelf.SetParent(root, false);
                shelf.localPosition = new Vector3(x, 0, 4.5f);
                ProcMesh.Visual(shelf, Shape.Box, new Vector3(0, 0.9f, 0), new Vector3(2.4f, 1.8f, 0.08f), U.Hex("#D5D9DC"));
                for (int side = -1; side <= 1; side += 2)
                for (int lvl = 0; lvl < 4; lvl++)
                {
                    float y = 0.15f + lvl * 0.45f;
                    ProcMesh.Visual(shelf, Shape.Box, new Vector3(0, y, side * 0.25f), new Vector3(2.4f, 0.03f, 0.45f), U.Hex("#E7EAEC"));
                    float cursor = -1.15f;
                    while (cursor < 1.1f)
                    {
                        float w = 0.08f + (float)rng.NextDouble() * 0.14f;
                        float hh = 0.12f + (float)rng.NextDouble() * 0.25f;
                        Color c = Color.HSVToRGB((float)rng.NextDouble(), 0.45f + (float)rng.NextDouble() * 0.4f, 0.6f + (float)rng.NextDouble() * 0.35f);
                        ProcMesh.Visual(shelf, Shape.Box, new Vector3(cursor + w / 2, y + 0.015f + hh / 2, side * 0.25f), new Vector3(w * 0.92f, hh, 0.3f), c);
                        cursor += w;
                    }
                }
                ProcMesh.Visual(shelf, Shape.Box, new Vector3(0, 3.0f, 0), new Vector3(1.6f, 0.35f, 0.04f), Navy);
                Text3D.Create(shelf, aisles[a], new Vector3(0, 3.0f, -0.03f), 0.18f, Color.white);
                ProcMesh.Visual(shelf, Shape.Cylinder, new Vector3(0, 3.6f, 0), new Vector3(0.01f, 0.9f, 0.01f), SteelDark);
            }

            // 收银台旁的小零食架
            var impulse = new GameObject("零食架").transform;
            impulse.SetParent(root, false);
            impulse.localPosition = new Vector3(-2.3f, 0, -0.42f);
            ProcMesh.Visual(impulse, Shape.Box, new Vector3(0, 0.6f, 0), new Vector3(0.6f, 1.2f, 0.2f), U.Hex("#D5D9DC"));
            for (int lvl = 0; lvl < 4; lvl++)
            for (int i = 0; i < 5; i++)
            {
                Color c = Color.HSVToRGB((float)rng.NextDouble(), 0.7f, 0.9f);
                ProcMesh.Visual(impulse, Shape.Box, new Vector3(-0.24f + i * 0.12f, 0.3f + lvl * 0.25f, -0.12f), new Vector3(0.1f, 0.14f, 0.03f), c);
            }

            // 另一条收银通道（背景）
            ProcMesh.Visual(root, Shape.Box, new Vector3(-1.8f, 0.45f, -2.6f), new Vector3(3.6f, 0.9f, 0.6f), CounterBody);
            ProcMesh.Visual(root, Shape.Box, new Vector3(-1.8f, 0.91f, -2.6f), new Vector3(3.6f, 0.02f, 0.45f), U.Hex("#2A2A2A"));
            var pole2 = ProcMesh.Visual(root, Shape.Cylinder, new Vector3(0.1f, 1.1f, -2.3f), new Vector3(0.05f, 2.2f, 0.05f), SteelDark);
            ProcMesh.Visual(root, Shape.Cylinder, new Vector3(0.1f, 2.3f, -2.3f), new Vector3(0.3f, 0.22f, 0.3f), Orange);
            Text3D.Create(root, "4", new Vector3(0.1f, 2.3f, -2.455f + 0.31f), 0.18f, Color.white, TextAnchor.MiddleCenter, Quaternion.Euler(0, 180, 0));
        }

        /// <summary>灯光与环境。</summary>
        public static void SetupLighting(Transform root)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.78f, 0.78f, 0.8f);
            RenderSettings.ambientEquatorColor = new Color(0.6f, 0.58f, 0.55f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.33f, 0.3f);
            RenderSettings.fog = false;

            var sun = new GameObject("主光源").AddComponent<Light>();
            sun.transform.SetParent(root, false);
            sun.type = LightType.Directional;
            sun.intensity = 0.75f;
            sun.color = new Color(1f, 0.97f, 0.92f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(62, 20, 0);

            Vector3[] pts = { new Vector3(0, 2.6f, 0.2f), new Vector3(-2.4f, 2.6f, 0.2f), new Vector3(2.4f, 2.6f, 1.5f), new Vector3(0, 2.8f, 4f) };
            foreach (var p in pts)
            {
                var l = new GameObject("顶灯").AddComponent<Light>();
                l.transform.SetParent(root, false);
                l.transform.position = p;
                l.type = LightType.Point;
                l.range = 6f;
                l.intensity = 0.9f;
                l.color = new Color(1f, 0.98f, 0.94f);
            }
        }
    }
}
