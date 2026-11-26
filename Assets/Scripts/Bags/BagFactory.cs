using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    public static class BagFactory
    {
        public static readonly Color Kraft = U.Hex("#C49A6C");
        public static readonly Color KraftDark = U.Hex("#9C7447");
        public static readonly Color PlasticWhite = U.Hex("#F4F6F7");
        public static readonly Color PlasticBlue = U.Hex("#2F6FB8");
        public static readonly Color Brand = U.Hex("#F28C28");

        static Material KraftMat => Mats.Lit(Kraft, 0.12f, 0, TexGen.Kraft, TexGen.KraftN, 0.35f, new Vector2(4, 4));
        static Material KraftInner => Mats.Lit(Color.Lerp(Kraft, Color.black, 0.12f), 0.1f, 0, TexGen.Kraft, TexGen.KraftN, 0.35f, new Vector2(4, 4));
        static Material FilmMat => Mats.Glass(new Color(0.97f, 0.98f, 1f), 0.62f, 0.55f, TexGen.PlasticWrinkleN);

        static readonly Dictionary<string, (Mesh, Material[])> Cache = new Dictionary<string, (Mesh, Material[])>();
        public static void ClearCache() => Cache.Clear();

        /// <summary>生成袋子，position 为袋底中心。</summary>
        public static Bag Create(BagSpec spec, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(spec.Name);
            root.transform.SetPositionAndRotation(position, rotation);
            var t = root.transform;
            float w = spec.Inner.x, h = spec.Inner.y, d = spec.Inner.z;
            const float col = 0.05f;   // 碰撞体向外加厚（画面不变），更难被推穿

            // 外观（按规格缓存）
            string key = spec.Material + "_" + spec.Size;
            if (!Cache.TryGetValue(key, out var look))
            {
                var v = spec.Material == BagMaterial.Paper ? PaperBag(spec) : PlasticBag(spec);
                v.Build(key, out var mesh, out var mats);
                look = (mesh, mats);
                Cache[key] = look;
            }
            var vis = new GameObject("袋子外观");
            vis.transform.SetParent(t, false);
            vis.AddComponent<MeshFilter>().sharedMesh = look.Item1;
            vis.AddComponent<MeshRenderer>().sharedMaterials = look.Item2;

            // 袋壁：每个袋子一份，可被内容物撑开
            if (spec.Material == BagMaterial.Paper) BagWallDeformer.Create(t, spec, KraftMat, KraftInner);
            else BagWallDeformer.Create(t, spec, FilmMat, FilmMat);

            // 碰撞（向外加厚，防穿透）
            // 塑料袋有弹性：碰撞墙往外让出 Stretch，稍大的东西能撑进去（画面上袋壁会鼓起来）
            float cw = w + spec.Stretch * 2, cd = d + spec.Stretch * 2;
            Col(t, new Vector3(0, -col / 2, 0), new Vector3(cw + col * 2, col, cd + col * 2));
            Col(t, new Vector3(-(cw + col) / 2, h / 2, 0), new Vector3(col, h, cd + col * 2));
            Col(t, new Vector3((cw + col) / 2, h / 2, 0), new Vector3(col, h, cd + col * 2));
            Col(t, new Vector3(0, h / 2, (cd + col) / 2), new Vector3(cw + col * 2, h, col));
            Col(t, new Vector3(0, h / 2, -(cd + col) / 2), new Vector3(cw + col * 2, h, col));

            // 尺寸标
            Text3D.Create(t, spec.SizeLetter, new Vector3(w * 0.36f, h * 0.88f, -(d / 2 + 0.004f)), 0.04f,
                spec.Material == BagMaterial.Paper ? U.Hex("#5A3A18") : PlasticBlue);

            // 重心标记（底部）
            var marker = ProcMesh.Visual(t, Shape.Cylinder, new Vector3(0, 0.004f, 0), new Vector3(0.025f, 0.003f, 0.025f), Color.green, null, 0.6f, "重心标记");
            ProcMesh.Visual(t, Shape.Box, new Vector3(0, 0.0015f, 0), new Vector3(w * 0.3f, 0.001f, 0.003f), new Color(0.25f, 0.25f, 0.25f));
            ProcMesh.Visual(t, Shape.Box, new Vector3(0, 0.0015f, 0), new Vector3(0.003f, 0.001f, d * 0.3f), new Color(0.25f, 0.25f, 0.25f));

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var bag = root.AddComponent<Bag>();
            bag.Spec = spec;
            bag.ComMarker = marker.transform;
            return bag;
        }

        static void Col(Transform parent, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("袋壁碰撞");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<BoxCollider>().size = size;
        }

        // ---------------------------------------------------------------- 纸袋
        static VisualSet PaperBag(BagSpec spec)
        {
            float w = spec.Inner.x, h = spec.Inner.y, d = spec.Inner.z;
            float crease = d * 0.22f;
            var v = new VisualSet();
            // 袋壁在 BagWallDeformer 里生成（会随内容物形变）
            // 底
            v.Add(Shapes.RoundedBox(new Vector3(w, 0.002f, d), 0.0008f, 1, 1, (f, l) => l * 2), new Vector3(0, 0.001f, 0), Quaternion.identity, KraftInner);
            // 印刷 logo（前后）
            var st = new LabelStyle { Brand = "CatMart", Title = "喵喵超市", Bg = Kraft, Bg2 = Kraft, Accent = Brand, Ink = U.Hex("#4A2C10"), Icon = LabelIcon.Cat, IconColor = Brand, Seed = 21 };
            var tex = LabelBaker.Sticker(new Vector2(w * 0.7f, h * 0.45f), st);
            var logo = Mats.Lit(Color.white, 0.12f, 0, tex, TexGen.KraftN, 0.5f);
            v.Add(Shapes.Quad(w * 0.7f, h * 0.45f), new Vector3(0, h * 0.45f, -d / 2 - 0.0035f), Quaternion.identity, logo);
            v.Add(Shapes.Quad(w * 0.7f, h * 0.45f), new Vector3(0, h * 0.45f, d / 2 + 0.0035f), Quaternion.Euler(0, 180, 0), logo);
            // 纸绳提手（中/大号）
            // 纸绳提手
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var path = new List<Vector3>(); var rr = new List<float>();
                    for (int k = 0; k <= 14; k++)
                    {
                        float a = k / 14f * Mathf.PI;
                        path.Add(new Vector3(Mathf.Cos(a) * w * 0.22f, h - 0.03f + Mathf.Sin(a) * h * 0.22f, side * (d / 2 + 0.003f)));
                        rr.Add(0.003f);
                    }
                    var rope = Shapes.Tube(path, rr, 6, true);
                    rope.Displace((p, n) => p + n * 0.0006f * Mathf.Sin(p.x * 900f + p.y * 900f));
                    v.Add(rope, KraftMat);
                }
            }
            return v;
        }

        // ---------------------------------------------------------------- 塑料袋（背心袋）
        static VisualSet PlasticBag(BagSpec spec)
        {
            float w = spec.Inner.x, h = spec.Inner.y, d = spec.Inner.z;
            var v = new VisualSet();
            v.Add(Shapes.Quad(w, d), new Vector3(0, 0.001f, 0), Quaternion.Euler(90, 0, 0), FilmMat);

            // 提手：两侧各一个扁环
            for (int side = -1; side <= 1; side += 2)
            {
                var path = new List<Vector3>(); var rr = new List<float>();
                for (int k = 0; k <= 16; k++)
                {
                    float a = k / 16f * Mathf.PI;
                    path.Add(new Vector3(side * (w / 2 - d * 0.12f), h + Mathf.Sin(a) * h * 0.28f, Mathf.Cos(a) * d * 0.42f));
                    rr.Add(0.0045f);
                }
                var handle = Shapes.Tube(path, rr, 6, true);
                handle.Transform(Matrix4x4.Scale(new Vector3(1, 1, 1)));
                v.Add(handle, FilmMat);
            }
            // 印刷：蓝色 CatMart
            var st = new LabelStyle { Brand = "CatMart", Title = "谢谢惠顾", Bg = Color.white, Bg2 = Color.white, Accent = PlasticBlue, Ink = PlasticBlue, BrandInk = Color.white, Icon = LabelIcon.Cat, IconColor = PlasticBlue, Seed = 22 };
            var tex = LabelBaker.Sticker(new Vector2(w * 0.55f, h * 0.35f), st, true);
            var print = Mats.Glass(Color.white, 0.85f, 0.5f);
            print.mainTexture = tex;
            v.Add(Shapes.Quad(w * 0.55f, h * 0.35f), new Vector3(0, h * 0.5f, -d / 2 - 0.012f), Quaternion.identity, print);
            v.Add(Shapes.Quad(w * 0.55f, h * 0.35f), new Vector3(0, h * 0.5f, d / 2 + 0.012f), Quaternion.Euler(0, 180, 0), print);
            return v;
        }

        // ---------------------------------------------------------------- 袋架
        /// <summary>袋架上的一叠折好的袋子。</summary>
        public static GameObject CreateRackStack(Transform parent, BagSpec spec, Vector3 localPos)
        {
            var root = new GameObject("袋架_" + spec.Name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            bool paper = spec.Material == BagMaterial.Paper;
            float scale = spec.Size == BagSize.Medium ? 0.85f : 1f;
            Vector3 size = spec.Size == BagSize.Square ? new Vector3(0.14f, 0.05f, 0.14f) : new Vector3(0.12f * scale, 0.05f, 0.2f * scale);
            var v = new VisualSet();
            var mat = paper ? KraftMat : Mats.Lit(PlasticWhite, 0.55f, 0, null, TexGen.PlasticWrinkleN, 0.5f);
            for (int i = 0; i < 10; i++)
            {
                var sheet = Shapes.RoundedBox(new Vector3(size.x, 0.0045f, size.z), 0.0015f, 1, 1, (f, l) => l);
                v.Add(sheet, new Vector3(((i * 7) % 3 - 1) * 0.0015f, i * 0.005f + 0.0025f, ((i * 5) % 3 - 1) * 0.0015f), Quaternion.Euler(0, (i % 3 - 1) * 1.5f, 0), mat);
            }
            var st = new LabelStyle { Brand = spec.SizeName, Title = spec.MaterialName, Bg = paper ? Kraft : Color.white, Bg2 = paper ? Kraft : Color.white, Accent = paper ? Brand : PlasticBlue, Ink = paper ? U.Hex("#4A2C10") : PlasticBlue, Seed = 5 };
            var tex = LabelBaker.Sticker(new Vector2(size.x * 0.9f, size.z * 0.6f), st, true);
            v.Add(Shapes.Quad(size.x * 0.9f, size.z * 0.6f), new Vector3(0, 0.0505f, 0), Quaternion.Euler(90, 0, 0), Mats.Label(tex, 0.2f));
            v.Spawn(root.transform, "袋堆外观");
            var colGo = new GameObject("袋堆");
            colGo.transform.SetParent(root.transform, false);
            colGo.transform.localPosition = new Vector3(0, size.y / 2, 0);
            colGo.AddComponent<BoxCollider>().size = size;
            colGo.AddComponent<BagRackButton>().Spec = spec;
            return root;
        }
    }
}
