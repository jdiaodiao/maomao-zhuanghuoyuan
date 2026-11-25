using System;
using System.Collections.Generic;
using UnityEngine;
using static CatMart.LookKit;

namespace CatMart
{
    /// <summary>
    /// 每件商品的生产级外观。物理碰撞仍用 ItemCatalog 里的简化部件，
    /// 这里只负责"看起来像真的"。结果按商品缓存（同种商品共享网格和材质）。
    /// </summary>
    public static class ItemLooks
    {
        public class Result { public Mesh[] Meshes; public Material[][] Mats; }

        static readonly Dictionary<string, Result> Cache = new Dictionary<string, Result>();
        static readonly Dictionary<string, Action<LookCtx>> B = new Dictionary<string, Action<LookCtx>>();

        public static void ClearCache() => Cache.Clear();

        public static Result Get(ItemDefinition def)
        {
            if (Cache.TryGetValue(def.Id, out var r)) return r;
            if (B.Count == 0) Register();
            var c = new LookCtx(def);
            try
            {
                if (B.TryGetValue(def.Id, out var build)) build(c);
            }
            catch (Exception e) { Debug.LogError($"外观构建失败 {def.Id}: {e}"); }
            for (int i = 0; i < c.Sets.Length; i++) if (c.Sets[i].Empty) Generic(c, i);

            r = new Result { Meshes = new Mesh[c.Sets.Length], Mats = new Material[c.Sets.Length][] };
            for (int i = 0; i < c.Sets.Length; i++)
            {
                c.Sets[i].Build(def.Id + "_" + i, out var m, out var mats);
                r.Meshes[i] = m;
                r.Mats[i] = mats;
            }
            Cache[def.Id] = r;
            return r;
        }

        /// <summary>预先生成全部外观（开场时调用）。</summary>
        public static void WarmAll()
        {
            foreach (var d in ItemCatalog.All) Get(d);
        }

        static void Generic(LookCtx c, int i)
        {
            var p = c.Solids[i];
            switch (p.Shape)
            {
                case Shape.Box: Carton(c, i); break;
                case Shape.Cylinder:
                {
                    var s = c.S(i);
                    c.V(i).Add(Shapes.Lathe(Shapes.Fillet(P(0, -s.y / 2, s.x / 2, -s.y / 2, s.x / 2, s.y / 2, 0, s.y / 2), 0.003f), 32), Plastic(p.Color));
                    break;
                }
                case Shape.Cone:
                {
                    var s = c.S(i);
                    c.V(i).Add(Shapes.Lathe(P(0, -s.y / 2, s.x / 2, -s.y / 2, 0.001f, s.y / 2, 0, s.y / 2), 24), Plastic(p.Color));
                    break;
                }
                default:
                    c.V(i).Add(Shapes.Ellipsoid(c.S(i)), Mats.Lit(p.Color, 0.4f, 0, TexGen.Speckle));
                    break;
            }
        }

        static Color H(string hex) => U.Hex(hex);

        static void Register()
        {
            // ======================== 乳制品 ========================
            B["eggs"] = c =>
            {
                var s = c.S(0);
                var sdf = new Sdf { DefaultBlend = 0.004f };
                float hx = s.x / 2, hy = s.y / 2, hz = s.z / 2;
                float cell = s.x / 6f;
                // 底托：12 个蛋窝从侧面鼓出
                sdf.Box(new Vector3(0, -hy * 0.45f, 0), new Vector3(hx * 0.96f, hy * 0.5f, hz * 0.9f), 0.005f);
                for (int k = 0; k < 6; k++)
                for (int j = 0; j < 2; j++)
                    sdf.Ellipsoid(new Vector3(-hx + cell * (k + 0.5f), -hy * 0.45f, (j - 0.5f) * hz), new Vector3(cell * 0.46f, hy * 0.55f, hz * 0.5f), 0, 0.006f);
                // 盖子：边沿 + 12 个蛋形拱顶 + 中间加强筋
                sdf.Box(new Vector3(0, hy * 0.12f, 0), new Vector3(hx, hy * 0.12f, hz), 0.004f, 0, 0.003f);
                for (int k = 0; k < 6; k++)
                for (int j = 0; j < 2; j++)
                    sdf.Ellipsoid(new Vector3(-hx + cell * (k + 0.5f), hy * 0.35f, (j - 0.5f) * hz * 0.98f), new Vector3(cell * 0.42f, hy * 0.62f, hz * 0.44f), 0, 0.008f);
                sdf.Box(new Vector3(0, hy * 0.55f, 0), new Vector3(hx * 0.9f, hy * 0.38f, hz * 0.06f), 0.004f, 0, 0.006f);
                var mb = sdf.Mesh(new Bounds(Vector3.zero, s * 1.05f), 0.0025f, 1, 25)[0];
                c.V(0).Add(mb, Pulp(H("#CBC2B2")));
                var st = c.Style; st.Bg = H("#FFF6E0"); st.Bg2 = H("#FBE3B0"); st.Ink = H("#5D3A1A");
                Sticker(c, 0, new Vector3(0, -hy * 0.45f, -hz * 0.92f - 0.0015f), new Vector2(s.x * 0.45f, s.y * 0.42f), null, st);
            };

            Action<LookCtx, Color> jug = (c, capCol) =>
            {
                var s = c.S(0);
                float hx = s.x / 2, hy = s.y / 2, hz = s.z / 2;
                var sdf = new Sdf { DefaultBlend = 0.012f };
                sdf.Box(new Vector3(0, -hy * 0.18f, 0), new Vector3(hx, hy * 0.8f, hz), hx * 0.28f);
                sdf.Ellipsoid(new Vector3(-hx * 0.15f, hy * 0.55f, -hz * 0.1f), new Vector3(hx * 0.8f, hy * 0.42f, hz * 0.8f));
                sdf.Capsule(new Vector3(-hx * 0.2f, hy * 0.6f, -hz * 0.2f), new Vector3(-hx * 0.2f, hy, -hz * 0.2f), hx * 0.28f, hx * 0.26f, 0, 0.01f);
                // 把手
                sdf.Capsule(new Vector3(hx * 0.65f, hy * 0.85f, hz * 0.05f), new Vector3(hx * 0.75f, hy * 0.05f, hz * 0.05f), hx * 0.2f, hx * 0.24f, 0, 0.02f);
                sdf.Capsule(new Vector3(hx * 0.35f, hy * 0.62f, hz * 0.05f), new Vector3(hx * 0.4f, hy * 0.18f, hz * 0.05f), hx * 0.11f, hx * 0.11f, 0, 0.008f).Cut();
                var mb = sdf.Mesh(new Bounds(Vector3.zero, s * 1.05f), 0.004f, 1, 20)[0];
                c.V(0).Add(mb, Mats.Lit(new Color(0.97f, 0.97f, 0.95f), 0.55f, 0, null, TexGen.PlasticWrinkleN, 0.15f));
                var st = c.Style; st.Bg = Color.white; st.Bg2 = new Color(0.94f, 0.95f, 0.98f); st.Ink = capCol * 0.8f; st.Accent = capCol;
                Sticker(c, 0, new Vector3(-hx * 0.05f, -hy * 0.2f, -hz - 0.0012f), new Vector2(s.x * 0.8f, s.y * 0.48f), null, st);
                var back = c.Style;
                Sticker(c, 0, new Vector3(0, -hy * 0.2f, hz + 0.0012f), new Vector2(s.x * 0.8f, s.y * 0.48f), Quaternion.Euler(0, 180, 0), st);
                if (c.Sets.Length > 1)
                {
                    Cap(c, 1, capCol);
                }
            };
            B["milk_gallon"] = c => jug(c, H("#1E5AA8"));
            B["milk_half"] = c => jug(c, H("#D33F49"));

            B["yogurt_cup"] = c => Cup(c, 0);
            B["yogurt_4pack"] = c =>
            {
                var s = c.S(0);
                var cupSize = new Vector3(s.x * 0.48f, s.y * 0.98f, s.z * 0.48f);
                var cc = new LookCtx(c.Def);
                for (int k = 0; k < 4; k++)
                {
                    Vector3 off = new Vector3((k % 2 - 0.5f) * s.x * 0.5f, 0, (k / 2 - 0.5f) * s.z * 0.5f);
                    float r = cupSize.x / 2, h = cupSize.y / 2, rb = r * 0.8f;
                    var prof = Shapes.Fillet(P(0, -h, rb, -h, r, h - 0.002f, r * 1.04f, h, 0, h), 0.0015f, 2);
                    c.V(0).Add(Shapes.Lathe(prof, 28), off, Quaternion.identity, Plastic(Color.white, 0.5f));
                    c.V(0).Add(Shapes.Disc(r * 1.04f, 28), off + new Vector3(0, h + 0.0004f, 0), Quaternion.identity, Metal(new Color(0.85f, 0.88f, 0.95f), 0.5f));
                }
                // 纸套
                var sleeve = new Vector3(s.x * 1.02f, s.y * 0.55f, s.z * 1.02f);
                var tex = LabelBaker.Box(sleeve, c.Style);
                var mb = Shapes.RoundedBox(sleeve, 0.004f, 2, 2, LabelBaker.BoxUv);
                var open = new MB();
                for (int k = 0; k < mb.V.Count; k++) open.Vert(mb.V[k], mb.N[k], mb.UV[k]);
                for (int k = 0; k < mb.T.Count; k += 3)
                {
                    Vector3 n = Vector3.Cross(mb.V[mb.T[k + 1]] - mb.V[mb.T[k]], mb.V[mb.T[k + 2]] - mb.V[mb.T[k]]).normalized;
                    if (Mathf.Abs(n.y) > 0.9f) continue;
                    open.Tri(mb.T[k], mb.T[k + 1], mb.T[k + 2]);
                }
                c.V(0).Add(open, new Vector3(0, -s.y * 0.15f, 0), Quaternion.identity, Mats.Label(tex, 0.25f));
            };

            B["cheese"] = c =>
            {
                var s = c.S(0);
                c.V(0).Add(Shapes.RoundedBox(s, 0.004f, 3, 3), Mats.Lit(H("#F2A900"), 0.72f, 0, null, TexGen.PlasticWrinkleN, 0.25f));
                Sticker(c, 0, new Vector3(0, s.y / 2 + 0.0007f, 0), new Vector2(s.x * 0.6f, s.z * 0.7f), FaceUp);
            };
            B["butter"] = c => Carton(c, 0, 0.002f, 0.35f);
            B["ice_cream"] = c =>
            {
                var s = c.S(0);
                float r = s.x / 2, h = s.y / 2;
                var prof = Shapes.Fillet(P(0, -h, r * 0.86f, -h, r * 0.97f, h - 0.012f, 0, h - 0.012f), 0.003f, 2);
                c.V(0).Add(Shapes.Lathe(prof, 40), Plastic(H("#FFF8E1"), 0.4f));
                var lid = Shapes.Fillet(P(0, h - 0.014f, r * 1.01f, h - 0.014f, r * 1.01f, h - 0.002f, r * 0.96f, h, 0, h), 0.002f, 2);
                c.V(0).Add(Shapes.Lathe(lid, 40), Plastic(H("#6D4C41"), 0.45f));
                WrapLabel(c, 0, r * 0.88f, -h + 0.004f, h - 0.018f, null, 0.4f, r * 0.965f);
                Sticker(c, 0, new Vector3(0, h + 0.0006f, 0), new Vector2(s.x * 0.62f, s.x * 0.5f), FaceUp);
            };

            // ======================== 烘焙 ========================
            B["bread"] = c =>
            {
                var s = c.S(0);
                var sdf = new Sdf { DefaultBlend = 0.02f };
                sdf.Box(new Vector3(0, -s.y * 0.12f, 0), new Vector3(s.x * 0.47f, s.y * 0.36f, s.z * 0.44f), 0.012f);
                sdf.Ellipsoid(new Vector3(0, s.y * 0.12f, 0), new Vector3(s.x * 0.47f, s.y * 0.36f, s.z * 0.46f));
                sdf.Displace = p => (Noise.Value3(p * 90, 3) - 0.5f) * 0.0015f;
                c.V(0).Add(sdf.Mesh(new Bounds(Vector3.zero, s), 0.005f, 1, 12)[0], Mats.Lit(H("#E2B06A"), 0.25f, 0, TexGen.Bread, TexGen.BreadN, 0.8f));
                ClearBag(c, 0, 0.42f, 0.5f);
                // 扎口
                c.V(0).Add(Shapes.Torus(0.012f, 0.0025f, 16, 6), new Vector3(s.x * 0.5f + 0.004f, 0, 0), Quaternion.Euler(0, 0, 90), Plastic(H("#D32F2F")));
            };
            B["bagels"] = c =>
            {
                var s = c.S(0);
                for (int k = 0; k < 3; k++) Donut(c.V(0), new Vector3((k - 1) * s.x * 0.31f, -s.y * 0.05f, 0), s.x * 0.32f, H("#C8935A"), null);
                ClearBag(c, 0, 0.3f, 0.82f);
            };
            B["donuts"] = c => Clamshell(c, 0, (v, inner) =>
            {
                Color[] fr = { H("#F48FB1"), H("#5D4037"), H("#FFF59D"), H("#F8BBD0"), H("#4E342E"), H("#FFFFFF") };
                for (int k = 0; k < 6; k++)
                    Donut(v, new Vector3((k % 3 - 1) * inner.x * 0.32f, -inner.y * 0.15f, (k / 3 - 0.5f) * inner.z * 0.48f), inner.x * 0.3f, H("#D99A52"), fr[k]);
            }, Cardboard(Color.white));
            B["croissants"] = c => Clamshell(c, 0, (v, inner) =>
            {
                for (int k = 0; k < 4; k++)
                {
                    var sdf = new Sdf { DefaultBlend = 0.012f };
                    for (int j = -3; j <= 3; j++)
                    {
                        float a = j / 3f * 1.1f;
                        float rr = 0.022f * (1 - Mathf.Abs(j) / 4.2f);
                        sdf.Sphere(new Vector3(Mathf.Sin(a) * 0.05f, 0, -Mathf.Cos(a) * 0.03f + 0.03f), rr);
                    }
                    sdf.Displace = p => Mathf.Sin(Mathf.Atan2(p.z, p.x) * 18) * 0.001f;
                    var mb = sdf.Mesh(new Bounds(Vector3.zero, new Vector3(0.14f, 0.05f, 0.08f)), 0.003f, 1, 15)[0];
                    v.Add(mb, new Vector3((k % 2 - 0.5f) * inner.x * 0.48f, -inner.y * 0.15f, (k / 2 - 0.5f) * inner.z * 0.48f), Quaternion.Euler(0, k * 40, 0),
                        Mats.Lit(H("#D18A3A"), 0.45f, 0, TexGen.Bread, TexGen.BreadN, 0.7f));
                }
            });
            B["buns"] = c =>
            {
                var s = c.S(0);
                for (int k = 0; k < 8; k++)
                {
                    var bun = Shapes.Blob(d => 0.5f / Mathf.Sqrt(d.x * d.x + d.y * d.y * 2.6f + d.z * d.z) * 0.95f, 24, 14);
                    float sz = Mathf.Min(s.x / 4, s.z / 2) * 0.98f;
                    bun.Transform(Matrix4x4.Scale(Vector3.one * sz));
                    c.V(0).Add(bun, new Vector3((k % 4 - 1.5f) * s.x / 4, -s.y * 0.05f, (k / 4 - 0.5f) * s.z / 2), Quaternion.identity,
                        Mats.Lit(H("#D9A35F"), 0.4f, 0, TexGen.Bread, TexGen.BreadN, 0.5f));
                }
                ClearBag(c, 0, 0.35f, 0.82f);
            };
            B["cake"] = c => Clamshell(c, 0, (v, inner) =>
            {
                float r = Mathf.Min(inner.x, inner.z) * 0.44f, h = inner.y * 0.42f;
                v.Add(Shapes.Lathe(Shapes.Fillet(P(0, -inner.y * 0.32f, r, -inner.y * 0.32f, r, -inner.y * 0.32f + h, 0, -inner.y * 0.32f + h), 0.006f, 3), 40), Mats.Lit(H("#F8BBD0"), 0.55f));
                // 奶油裱花：一圈波浪形的环 + 顶面奶油层
                var cream = Mats.Lit(H("#FFFDF7"), 0.45f);
                var ring = Shapes.Torus(r * 0.86f, 0.0065f, 48, 10);
                ring.Displace((p, n) => p + n * 0.0025f * Mathf.Sin(Mathf.Atan2(p.z, p.x) * 18));
                v.Add(ring, new Vector3(0, -inner.y * 0.32f + h + 0.003f, 0), Quaternion.identity, cream);
                v.Add(Shapes.Torus(r * 0.99f, 0.005f, 48, 8), new Vector3(0, -inner.y * 0.32f + 0.004f, 0), Quaternion.identity, cream);
                v.Add(Shapes.Lathe(P(0, -inner.y * 0.32f + h, r * 0.97f, -inner.y * 0.32f + h, r * 0.9f, -inner.y * 0.32f + h + 0.003f, 0, -inner.y * 0.32f + h + 0.003f), 40), cream);
                for (int k = 0; k < 5; k++)
                {
                    float a = k / 5f * Mathf.PI * 2;
                    Vector3 p = new Vector3(Mathf.Cos(a) * r * 0.45f, -inner.y * 0.32f + h + 0.015f, Mathf.Sin(a) * r * 0.45f);
                    v.Add(Shapes.Lathe(P(0, -0.012f, 0.0025f, -0.012f, 0.0025f, 0.012f, 0, 0.012f), 8), p, Quaternion.identity, Plastic(Color.HSVToRGB(k / 5f, 0.5f, 1f)));
                    v.Add(Shapes.Ellipsoid(new Vector3(0.004f, 0.008f, 0.004f)), p + new Vector3(0, 0.017f, 0), Quaternion.identity, Mats.Emissive(H("#FFC107"), new Color(1f, 0.6f, 0.1f)));
                }
            }, Cardboard(H("#F5F5F5")));

            // ======================== 果蔬 ========================
            B["bananas"] = c =>
            {
                for (int i = 0; i < c.Sets.Length; i++)
                {
                    var s = c.S(i);
                    var mb = Banana(s.y, s.x * 1.05f, s.y * 0.12f);
                    c.V(i).Add(mb, Mats.Lit(Color.white, 0.35f, 0, TexGen.BananaSkin));
                }
                // 果柄连在中间那根的一端
                var s1 = c.S(1);
                c.V(1).Add(Shapes.Tube(new[] { new Vector3(0, s1.y / 2 - 0.002f, 0), new Vector3(0.004f, s1.y / 2 + 0.018f, 0) }, new[] { 0.007f, 0.005f }, 6),
                    Mats.Lit(H("#4E3B1F"), 0.2f));
            };
            B["apples"] = c =>
            {
                for (int i = 0; i < c.Sets.Length; i++)
                {
                    var s = c.S(i);
                    c.V(i).Add(Apple(s.x), Quaternion.Euler(0, i * 77, 0) * Vector3.zero, Quaternion.Euler(0, i * 77, 0), Mats.Lit(Color.white, 0.62f, 0, TexGen.AppleSkin));
                    c.V(i).Add(Shapes.Tube(new[] { new Vector3(0, s.y * 0.36f, 0), new Vector3(0.002f, s.y * 0.56f, 0.001f) }, new[] { 0.0018f, 0.0014f }, 5), Mats.Lit(H("#5D4037"), 0.2f));
                }
                Sticker(c, 0, new Vector3(0, c.S(0).y * 0.47f, -c.S(0).z * 0.2f), new Vector2(0.024f, 0.016f), Quaternion.Euler(60, 0, 0), null, true);
            };
            B["tomatoes"] = c =>
            {
                for (int i = 0; i < c.Sets.Length; i++)
                {
                    var s = c.S(i);
                    c.V(i).Add(Tomato(s), Mats.Lit(H("#E0301E"), 0.8f, 0, TexGen.Speckle));
                    var star = new List<Vector2>();
                    for (int k = 0; k < 10; k++) { float a = k * Mathf.PI / 5; float rr = k % 2 == 0 ? s.x * 0.22f : s.x * 0.06f; star.Add(new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr)); }
                    c.V(i).Add(Shapes.Prism(Shapes.EnsureCCW(star), 0.002f), new Vector3(0, s.y * 0.43f, 0), Quaternion.identity, Mats.Lit(H("#3C7A2A"), 0.3f));
                }
            };
            B["strawberries"] = c => Clamshell(c, 0, (v, inner) =>
            {
                var rng = new System.Random(4);
                for (int k = 0; k < 10; k++)
                {
                    float d = 0.03f + (float)rng.NextDouble() * 0.01f;
                    var berry = Shapes.Blob(dir => d / 2 * (1 - 0.35f * Mathf.Max(0, -dir.y)) * (1 + 0.1f * dir.y), 20, 14);
                    Vector3 pos = new Vector3((k % 4 - 1.5f) * inner.x * 0.23f, -inner.y * 0.2f + (k / 8) * 0.02f, ((k / 4) % 2 - 0.5f) * inner.z * 0.45f);
                    v.Add(berry, pos, Quaternion.Euler((float)rng.NextDouble() * 60 - 30, 0, (float)rng.NextDouble() * 60 + 60), Mats.Lit(Color.white, 0.75f, 0, TexGen.StrawberrySkin));
                }
            });
            B["grapes"] = c =>
            {
                var s = c.S(0);
                var rng = new System.Random(8);
                var grape = Shapes.Ellipsoid(new Vector3(0.018f, 0.021f, 0.018f), 14, 10);
                for (int k = 0; k < 70; k++)
                {
                    Vector3 p = new Vector3((float)(rng.NextDouble() - 0.5) * s.x * 0.85f, (float)(rng.NextDouble() - 0.5) * s.y * 0.75f, (float)(rng.NextDouble() - 0.5) * s.z * 0.75f);
                    c.V(0).Add(grape, p, Quaternion.Euler(0, 0, (float)rng.NextDouble() * 180), Mats.Lit(H("#5B2A6E"), 0.7f));
                }
                ClearBag(c, 0, 0.22f, 0.86f);
            };
            B["watermelon"] = c =>
            {
                var s = c.S(0);
                // 两极沿长轴（x），条纹从一头到另一头
                c.V(0).Add(Shapes.Blob(d => 1 / Mathf.Sqrt(d.x * d.x / (s.y * s.y / 4) + d.y * d.y / (s.x * s.x / 4) + d.z * d.z / (s.z * s.z / 4)) * (1 + Noise.Fbm3(d * 2, 2, 5) * 0.03f), 40, 26),
                    Vector3.zero, Quaternion.Euler(0, 0, 90), Mats.Lit(Color.white, 0.38f, 0, TexGen.Watermelon, TexGen.PitsN, 0.25f));
                Sticker(c, 0, new Vector3(0, s.y * 0.49f, 0), new Vector2(0.04f, 0.03f), FaceUp, null, true);
            };
            B["lettuce"] = c =>
            {
                var s = c.S(0);
                for (int layer = 0; layer < 3; layer++)
                {
                    float k = 1 - layer * 0.12f;
                    int seed = layer * 11;
                    var mb = Shapes.Blob(d =>
                    {
                        float e = 1 / Mathf.Sqrt(d.x * d.x / (s.x * s.x / 4) + d.y * d.y / (s.y * s.y / 4) + d.z * d.z / (s.z * s.z / 4));
                        float ruffle = Noise.Fbm3(d * 4f, 3, seed) * 0.22f + Mathf.Sin(Mathf.Atan2(d.z, d.x) * 9 + layer) * 0.03f;
                        return e * k * (0.88f + ruffle);
                    }, 40, 26);
                    c.V(0).Add(mb, Quaternion.Euler(0, layer * 40, 0) * Vector3.up * layer * 0.004f, Quaternion.Euler(0, layer * 40, 0),
                        Mats.Lit(Color.Lerp(H("#6FAE3B"), H("#C5E49B"), layer * 0.4f), 0.45f, 0, TexGen.Leaf, TexGen.PitsN, 0.4f));
                }
            };
            B["avocados"] = c =>
            {
                for (int i = 0; i < c.Sets.Length; i++)
                {
                    var s = c.S(i);
                    var sdf = new Sdf { DefaultBlend = 0.02f };
                    sdf.Sphere(new Vector3(0, -s.y * 0.15f, 0), s.x * 0.48f);
                    sdf.Sphere(new Vector3(0, s.y * 0.22f, 0), s.x * 0.33f);
                    c.V(i).Add(sdf.Mesh(new Bounds(Vector3.zero, s), 0.003f, 1, 30)[0], Mats.Lit(H("#2E3B1C"), 0.45f, 0, TexGen.Speckle, TexGen.BumpyN, 1.2f));
                }
            };
            Action<LookCtx, Color, Func<int, MB>> netProduce = (c, net, make) => NetBag(c, 0, (v, inner) =>
            {
                var rng = new System.Random(c.Seed);
                int n = 0;
                for (int y = 0; y < 2; y++)
                for (int x = 0; x < 3; x++)
                for (int z = 0; z < 2; z++)
                {
                    Vector3 p = new Vector3((x - 1) * inner.x * 0.32f, (y - 0.5f) * inner.y * 0.45f, (z - 0.5f) * inner.z * 0.5f);
                    p += new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.01f;
                    v.Add(make(n++), p, Quaternion.Euler((float)rng.NextDouble() * 360, (float)rng.NextDouble() * 360, 0), MatFor(c.Def.Id));
                }
            }, net);
            B["potatoes"] = c => netProduce(c, H("#C62828"), n => Potato(new Vector3(0.095f, 0.06f, 0.065f), n));
            B["onions"] = c => netProduce(c, H("#E65100"), n => Onion(0.072f));
            B["pineapple"] = c =>
            {
                var s = c.S(0);
                c.V(0).Add(Shapes.Blob(d => 1 / Mathf.Sqrt(d.x * d.x / (s.x * s.x / 4) + d.y * d.y / (s.y * s.y / 4) + d.z * d.z / (s.z * s.z / 4)) * (1 - 0.06f * d.y * d.y), 36, 26),
                    Mats.Lit(Color.white, 0.25f, 0, TexGen.Pineapple, TexGen.PineappleN, 1.2f));
                var cs = c.S(1);
                var rng = new System.Random(3);
                for (int k = 0; k < 22; k++)
                {
                    float a = k * 137.5f;
                    float tilt = 8 + (k / 22f) * 35;
                    float len = cs.y * (1.1f - k / 22f * 0.5f);
                    var leaf = Leaf(len, 0.016f, 0.15f, k);
                    c.V(1).Add(leaf, new Vector3(0, -cs.y * 0.5f, 0), Quaternion.Euler(0, a, 0) * Quaternion.Euler(tilt, 0, 0) * Quaternion.Euler(-90 + 90, 0, 0),
                        Mats.Lit(Color.Lerp(H("#4C7A2E"), H("#7FA84A"), (float)rng.NextDouble()), 0.35f, 0, TexGen.Leaf));
                }
            };
            B["lemons"] = c =>
            {
                for (int i = 0; i < c.Sets.Length; i++)
                    c.V(i).Add(Lemon(c.S(i)), Mats.Lit(H("#F7D531"), 0.55f, 0, TexGen.Speckle, TexGen.PitsN, 0.8f));
            };
            B["carrots"] = c =>
            {
                var s = c.S(0);
                for (int k = 0; k < 7; k++)
                {
                    float len = s.x * (0.8f + (k % 3) * 0.06f), r = s.y * 0.2f;
                    var prof = new List<Vector2> { new Vector2(0, -len / 2) };
                    for (int j = 1; j <= 10; j++) { float t = j / 10f; prof.Add(new Vector2(r * Mathf.Pow(t, 0.55f) * (1 + 0.04f * Mathf.Sin(t * 40)), -len / 2 + t * len)); }
                    prof.Add(new Vector2(0, len / 2));
                    c.V(0).Add(Shapes.Lathe(prof, 14), new Vector3(0, ((k % 2) - 0.5f) * s.y * 0.35f, (k - 3) * s.z * 0.13f), Quaternion.Euler(0, 0, -90),
                        Mats.Lit(H("#F57C00"), 0.45f, 0, TexGen.Speckle, TexGen.PitsN, 0.5f));
                }
                ClearBag(c, 0, 0.3f, 0.5f, true);
            };

            // ======================== 肉类 & 冷冻 ========================
            B["chicken"] = c => MeatTray(c, 0, (v, inner) =>
            {
                for (int k = 0; k < 2; k++)
                {
                    var sdf = new Sdf { DefaultBlend = 0.02f };
                    sdf.Ellipsoid(Vector3.zero, new Vector3(inner.x * 0.24f, inner.y * 0.35f, inner.z * 0.4f));
                    sdf.Ellipsoid(new Vector3(inner.x * 0.12f, 0, 0), new Vector3(inner.x * 0.16f, inner.y * 0.28f, inner.z * 0.3f));
                    sdf.Displace = p => Noise.Value3(p * 60, k) * 0.002f;
                    v.Add(sdf.Mesh(new Bounds(Vector3.zero, inner), 0.003f, 1, 20)[0], new Vector3((k - 0.5f) * inner.x * 0.48f, -inner.y * 0.05f, 0),
                        Quaternion.Euler(0, k * 180 + 10, 0), Mats.Lit(H("#F3B9A5"), 0.75f, 0, TexGen.Speckle, TexGen.PitsN, 0.3f));
                }
            });
            B["ground_beef"] = c => MeatTray(c, 0, (v, inner) =>
            {
                var sdf = new Sdf { DefaultBlend = 0.01f };
                sdf.Box(Vector3.zero, new Vector3(inner.x * 0.42f, inner.y * 0.3f, inner.z * 0.42f), 0.01f);
                sdf.Displace = p => (Noise.Value3(p * 260, 2) - 0.5f) * 0.0035f;
                v.Add(sdf.Mesh(new Bounds(Vector3.zero, inner), 0.0022f, 1, 20)[0], new Vector3(0, -inner.y * 0.05f, 0), Quaternion.identity,
                    Mats.Lit(Color.white, 0.6f, 0, TexGen.Meat, TexGen.BumpyN, 0.8f));
            });
            B["steak"] = c => MeatTray(c, 0, (v, inner) =>
            {
                var sdf = new Sdf { DefaultBlend = 0.02f };
                sdf.Ellipsoid(Vector3.zero, new Vector3(inner.x * 0.45f, inner.y * 0.32f, inner.z * 0.38f));
                sdf.Ellipsoid(new Vector3(-inner.x * 0.2f, 0, inner.z * 0.1f), new Vector3(inner.x * 0.25f, inner.y * 0.3f, inner.z * 0.33f));
                sdf.Displace = p => Noise.Value3(p * 40, 6) * 0.002f;
                v.Add(sdf.Mesh(new Bounds(Vector3.zero, inner), 0.0025f, 1, 15)[0], new Vector3(0, -inner.y * 0.08f, 0), Quaternion.identity,
                    Mats.Lit(Color.white, 0.7f, 0, TexGen.Meat, TexGen.PitsN, 0.3f));
            }, H("#1E1E1E"));
            B["fish_fillet"] = c => MeatTray(c, 0, (v, inner) =>
            {
                var sdf = new Sdf { DefaultBlend = 0.02f };
                sdf.Capsule(new Vector3(-inner.x * 0.32f, 0, 0), new Vector3(inner.x * 0.32f, 0, 0), inner.z * 0.33f, inner.z * 0.18f);
                sdf.Box(new Vector3(0, -inner.y * 0.6f, 0), new Vector3(inner.x, inner.y * 0.4f, inner.z), 0.001f).Cut();
                v.Add(sdf.Mesh(new Bounds(Vector3.zero, inner), 0.0025f, 1, 18)[0], new Vector3(0, -inner.y * 0.1f, 0), Quaternion.identity,
                    Mats.Lit(Color.white, 0.7f, 0, TexGen.Salmon));
            }, H("#1E1E1E"));
            B["frozen_pizza"] = c => Carton(c, 0, 0.002f, 0.45f);
            B["frozen_shrimp"] = c => Pouch(c, 0, false, 0.7f, 0.25f, 0.8f);
            B["frozen_peas"] = c => Pouch(c, 0, false, 0.6f, 0.1f, 0.7f);
            B["frozen_dinner"] = c => Carton(c, 0, 0.002f, 0.4f);

            // ======================== 零食 ========================
            B["chips"] = c => Pouch(c, 0, false, 0.75f, 0.4f);
            B["tortilla_chips"] = c => Pouch(c, 0, false, 0.7f, 0.3f);
            B["crackers"] = c => Carton(c, 0);
            B["cookies"] = c => Pouch(c, 0, true, 0.55f, 0.1f, 0.9f);
            B["cereal"] = c => Carton(c, 0, 0.0025f, 0.3f);
            B["candy_bar"] = c => Pouch(c, 0, true, 0.6f, 0.3f, 1f, new Vector3(c.S(0).x, c.S(0).z, c.S(0).y), Quaternion.Euler(90, 0, 0));
            B["popcorn"] = c => Carton(c, 0);
            B["peanut_butter"] = c =>
            {
                BottleBody(c, 0, Mats.Glass(H("#C48A3F"), 0.85f), null, 0.9f, c.S(0).x / 2 * 0.95f, 0.9f, 0.1f, 0.88f);
                if (c.Sets.Length > 1) Cap(c, 1, H("#D32F2F"));
            };
            B["gummies"] = c => Pouch(c, 0, false, 0.75f, 0.2f, 1f);

            // ======================== 饮料 ========================
            B["soda_12"] = c => Carton(c, 0, 0.003f, 0.45f);
            B["water_case"] = c =>
            {
                var s = c.S(0);
                int nx = 6, nz = 4;
                float d = Mathf.Min(s.x / nx, s.z / nz) * 0.96f, h = s.y * 0.94f;
                float r = d / 2;
                var prof = Shapes.Fillet(P(0, -h / 2, r * 0.85f, -h / 2, r, -h / 2 + 0.006f, r, h * 0.2f, r * 0.96f, h * 0.24f, r, h * 0.28f,
                    r * 0.5f, h * 0.43f, r * 0.4f, h * 0.46f, r * 0.4f, h / 2, 0, h / 2), 0.002f, 2);
                var bottle = Shapes.Lathe(prof, 18);
                var cap = Shapes.Lathe(P(0, h / 2, r * 0.42f, h / 2, r * 0.42f, h / 2 + 0.01f, 0, h / 2 + 0.01f), 12);
                var water = Shapes.Lathe(P(0, -h / 2 + 0.002f, r * 0.9f, -h / 2 + 0.002f, r * 0.9f, h * 0.3f, 0, h * 0.3f), 12);
                for (int x = 0; x < nx; x++)
                for (int z = 0; z < nz; z++)
                {
                    Vector3 p = new Vector3((x - (nx - 1) / 2f) * s.x / nx, -0.005f, (z - (nz - 1) / 2f) * s.z / nz);
                    c.V(0).Add(water, p, Mats.Lit(H("#CFE8FF"), 0.9f));
                    c.V(0).Add(bottle, p, Mats.Glass(H("#D6ECFF"), 0.28f));
                    c.V(0).Add(cap, p, Plastic(H("#1565C0")));
                }
                c.V(0).Add(Shapes.OpenBox(new Vector3(s.x, 0.04f, s.z), 0.003f, 0.003f), new Vector3(0, -s.y / 2 + 0.02f, 0), Quaternion.identity, Cardboard(H("#C49A6C")));
                c.V(0).Add(Shapes.RoundedBox(s * 1.01f, 0.02f, 3, 3), Film);
                var tex = LabelBaker.Wrap((s.x + s.z) * 2, s.y * 0.2f, c.Style);
                var band = Shapes.RoundedBox(new Vector3(s.x * 1.015f, s.y * 0.2f, s.z * 1.015f), 0.004f, 2, 2, (f, l) => f == Shapes.Face.NegZ ? new Vector2(0.25f + l.x * 0.3f, l.y) : f == Shapes.Face.PosZ ? new Vector2(0.75f + l.x * 0.25f, l.y) : new Vector2(l.x * 0.2f, l.y));
                c.V(0).Add(band, new Vector3(0, s.y * 0.28f, 0), Quaternion.identity, Mats.Label(tex, 0.5f));
            };
            B["orange_juice"] = c =>
            {
                var glass = Glass(H("#FFF3E0"), 0.22f);
                BottleBody(c, 0, glass, H("#FF9800"), 0.97f, c.S(1).x / 2, 0.72f, 0.12f, 0.6f);
                Neck(c, 1, glass, H("#FF9800"));
                Cap(c, 2, H("#2E7D32"));
            };
            B["wine"] = c =>
            {
                var glass = Glass(H("#1B3A1B"), 0.85f);
                var st = c.Style; st.Bg = H("#F5F0E1"); st.Bg2 = H("#E8DFC8"); st.Ink = H("#4A1020"); st.Accent = H("#8E0000"); st.Pattern = LabelPattern.None; st.Brand = "猫爪庄园"; st.Badge = "2019";
                BottleBody(c, 0, glass, null, 0, c.S(1).x / 2, 0.86f, 0.2f, 0.62f);
                Neck(c, 1, glass);
                var s1 = c.S(1);
                // 锡箔帽
                c.V(1).Add(Shapes.Lathe(Shapes.Fillet(P(0, s1.y * 0.12f, s1.x / 2 + 0.0012f, s1.y * 0.12f, s1.x / 2 + 0.0012f, s1.y / 2, 0, s1.y / 2 + 0.0005f), 0.001f), 24),
                    Mats.Lit(H("#8E0000"), 0.7f, 0.6f));
            };
            B["soda_2l"] = c =>
            {
                var pet = Mats.Glass(H("#C8F0C8"), 0.25f, 0.97f);
                var s = c.S(0);
                float r = s.x / 2, h = s.y / 2;
                var prof = Shapes.Fillet(P(0, -h + 0.008f, r * 0.55f, -h, r * 0.85f, -h + 0.004f, r, -h + 0.02f, r, -h + s.y * 0.38f, r * 0.94f, -h + s.y * 0.42f,
                    r, -h + s.y * 0.46f, r, h, 0, h), 0.003f, 2);
                c.V(0).Add(Shapes.Lathe(prof, 36), pet);
                c.V(0).Add(Shapes.Lathe(P(0, -h + 0.01f, r * 0.92f, -h + 0.012f, r * 0.93f, h, 0, h), 24), Mats.Lit(H("#9CCC65"), 0.6f));
                WrapLabel(c, 0, r, -h + s.y * 0.55f, -h + s.y * 0.85f);
                var cs = c.S(1);
                var shoulder = Shapes.Fillet(P(0, -cs.y / 2, cs.x / 2, -cs.y / 2, cs.x * 0.15f, cs.y * 0.3f, cs.x * 0.13f, cs.y / 2, 0, cs.y / 2), 0.006f, 3);
                c.V(1).Add(Shapes.Lathe(shoulder, 36), pet);
                c.V(1).Add(Shapes.Lathe(P(0, -cs.y / 2, cs.x / 2 * 0.93f, -cs.y / 2, cs.x * 0.2f, cs.y * 0.0f, 0, 0), 24), Mats.Lit(H("#9CCC65"), 0.6f));
                Cap(c, 2, H("#1B5E20"));
            };
            B["coffee"] = c =>
            {
                var s = c.S(0);
                var tex = LabelBaker.Box(s, c.Style);
                var mb = Shapes.RoundedBox(s, 0.006f, 3, 4, LabelBaker.BoxUv);
                mb.Displace((p, n) => p + n * (Noise.Value3(p * 70, 4) - 0.5f) * 0.0015f);
                c.V(0).Add(mb, Mats.Label(tex, 0.35f, 0.3f));
                c.V(0).Add(Shapes.RoundedBox(new Vector3(s.x * 1.02f, 0.012f, s.z * 0.3f), 0.002f, 2, 1), new Vector3(0, s.y / 2 - 0.004f, 0), Quaternion.identity, Metal(H("#B0B0B0")));
                c.V(0).Add(Shapes.Lathe(P(0, 0, 0.007f, 0, 0.007f, 0.002f, 0, 0.002f), 16), new Vector3(0, s.y * 0.2f, -s.z / 2 - 0.0005f), Quaternion.Euler(-90, 0, 0), Plastic(H("#222222")));
            };
            B["energy_drink"] = c => Can(c, 0);
            B["milk_tea"] = c =>
            {
                BottleBody(c, 0, Mats.Glass(H("#F5EDE0"), 0.4f), H("#C9A27A"), 0.92f, c.S(1).x / 2 * 0.9f, 0.82f, 0.08f, 0.8f);
                Cap(c, 1, Color.white);
            };

            // ======================== 粮油干货 ========================
            B["canned_soup"] = c => Can(c, 0);
            B["canned_tuna"] = c => Can(c, 0, true, 0.004f);
            B["canned_beans"] = c => Can(c, 0);
            B["pasta"] = c => Carton(c, 0, 0.0015f, 0.3f);
            B["rice"] = c => Pouch(c, 0, false, 0.45f, 0f, 0.6f, new Vector3(c.S(0).x, c.S(0).z, c.S(0).y), Quaternion.Euler(90, 0, 0));
            Action<LookCtx> sack = c =>
            {
                var s = c.S(0);
                var body = new Vector3(s.x, s.y * 0.88f, s.z);
                var tex = LabelBaker.Box(body, c.Style);
                var mb = Shapes.RoundedBox(body, 0.004f, 2, 4, LabelBaker.BoxUv);
                mb.Displace((p, n) => p + n * (Noise.Value3(p * 50, 7) - 0.5f) * 0.0018f);
                c.V(0).Add(mb, new Vector3(0, -s.y * 0.06f, 0), Quaternion.identity, Mats.Label(tex, 0.15f));
                var fold = Shapes.RoundedBox(new Vector3(s.x * 0.98f, s.y * 0.12f, s.z * 0.35f), 0.003f, 2, 2);
                c.V(0).Add(fold, new Vector3(0, s.y * 0.44f, 0), Quaternion.Euler(-8, 0, 0), Mats.Lit(Color.Lerp(c.C(0), Color.white, 0.1f), 0.15f, 0, TexGen.Kraft));
            };
            B["flour"] = sack;
            B["sugar"] = sack;
            B["pasta_sauce"] = c =>
            {
                var s = c.S(0);
                BottleBody(c, 0, Glass(H("#FFEBEE"), 0.2f), H("#B71C1C"), 0.96f, s.x / 2 * 0.85f, 0.88f, 0.14f, 0.72f);
                if (c.Sets.Length > 1) { Cap(c, 1, H("#D4A017"), false, 0.75f); }
            };
            B["olive_oil"] = c =>
            {
                var glass = Glass(H("#556B2F"), 0.35f);
                BottleBody(c, 0, glass, H("#B5A823"), 0.92f, c.S(1).x / 2, 0.8f, 0.2f, 0.65f);
                Neck(c, 1, glass, H("#B5A823"));
                Cap(c, 2, H("#212121"));
            };
            B["honey"] = c =>
            {
                var amber = Mats.Glass(H("#FFA000"), 0.82f, 0.95f);
                var s = c.S(0);
                var sdf = new Sdf { DefaultBlend = 0.012f };
                sdf.Ellipsoid(new Vector3(0, -s.y * 0.05f, 0), new Vector3(s.x * 0.5f, s.y * 0.48f, s.z * 0.48f));
                sdf.Sphere(new Vector3(-s.x * 0.22f, -s.y * 0.38f, -s.z * 0.2f), s.x * 0.2f);
                sdf.Sphere(new Vector3(s.x * 0.22f, -s.y * 0.38f, -s.z * 0.2f), s.x * 0.2f);
                sdf.Capsule(new Vector3(-s.x * 0.42f, s.y * 0.15f, -s.z * 0.1f), new Vector3(-s.x * 0.2f, -s.y * 0.05f, -s.z * 0.38f), s.x * 0.12f, s.x * 0.1f);
                sdf.Capsule(new Vector3(s.x * 0.42f, s.y * 0.15f, -s.z * 0.1f), new Vector3(s.x * 0.2f, -s.y * 0.05f, -s.z * 0.38f), s.x * 0.12f, s.x * 0.1f);
                c.V(0).Add(sdf.Mesh(new Bounds(Vector3.zero, s * 1.1f), 0.0025f, 1, 20)[0], amber);
                Sticker(c, 0, new Vector3(0, -s.y * 0.02f, -s.z * 0.5f - 0.002f), new Vector2(s.x * 0.6f, s.y * 0.42f));
                var hs = c.S(1);
                var head = new Sdf { DefaultBlend = 0.008f };
                head.Ellipsoid(Vector3.zero, hs * 0.5f);
                head.Sphere(new Vector3(-hs.x * 0.36f, hs.y * 0.3f, 0), hs.x * 0.16f);
                head.Sphere(new Vector3(hs.x * 0.36f, hs.y * 0.3f, 0), hs.x * 0.16f);
                head.Ellipsoid(new Vector3(0, -hs.y * 0.1f, -hs.z * 0.38f), new Vector3(hs.x * 0.2f, hs.y * 0.15f, hs.z * 0.15f));
                c.V(1).Add(head.Mesh(new Bounds(Vector3.zero, hs * 1.2f), 0.0022f, 1, 20)[0], amber);
                c.V(1).Add(Shapes.Ellipsoid(Vector3.one * 0.006f), new Vector3(-hs.x * 0.15f, hs.y * 0.08f, -hs.z * 0.45f), Quaternion.identity, Plastic(H("#3E2723"), 0.8f));
                c.V(1).Add(Shapes.Ellipsoid(Vector3.one * 0.006f), new Vector3(hs.x * 0.15f, hs.y * 0.08f, -hs.z * 0.45f), Quaternion.identity, Plastic(H("#3E2723"), 0.8f));
                if (c.Sets.Length > 2) Cap(c, 2, H("#D32F2F"));
            };
            B["ketchup"] = c =>
            {
                var s = c.S(0);
                var sdf = new Sdf { DefaultBlend = 0.02f };
                sdf.Box(new Vector3(0, -s.y * 0.05f, 0), new Vector3(s.x * 0.47f, s.y * 0.43f, s.z * 0.45f), s.z * 0.4f);
                sdf.Capsule(new Vector3(0, s.y * 0.3f, 0), new Vector3(0, s.y * 0.48f, 0), s.z * 0.42f, s.z * 0.35f);
                c.V(0).Add(sdf.Mesh(new Bounds(Vector3.zero, s * 1.05f), 0.0025f, 1, 20)[0], Mats.Lit(H("#C62828"), 0.8f));
                var st = c.Style; st.Bg = Color.white; st.Bg2 = H("#F2F2F2"); st.Ink = H("#B71C1C"); st.Accent = H("#2E7D32");
                Sticker(c, 0, new Vector3(0, -s.y * 0.05f, -s.z * 0.5f - 0.0012f), new Vector2(s.x * 0.85f, s.y * 0.45f), null, st);
                if (c.Sets.Length > 1)
                {
                    var cs = c.S(1);
                    c.V(1).Add(Shapes.Lathe(Shapes.Fillet(P(0, -cs.y / 2, cs.x / 2, -cs.y / 2, cs.x / 2 * 0.9f, cs.y / 2, 0, cs.y / 2), 0.004f, 3), 28), Plastic(Color.white, 0.5f));
                }
            };

            // ======================== 日用 ========================
            Action<LookCtx, int, int, int, bool> rolls = (c, nx, ny, nz, tall) =>
            {
                var s = c.S(0);
                float d = Mathf.Min(s.x / nx, tall ? s.z / nz : s.z / nz) * 0.97f;
                float h = s.y / ny * 0.97f;
                float r = d / 2, core = r * 0.3f;
                var prof = Shapes.Fillet(P(core, h / 2, core, -h / 2, r * 0.97f, -h / 2, r, -h / 2 + 0.006f, r, h / 2 - 0.006f, r * 0.97f, h / 2, core * 1.15f, h / 2), 0.004f, 2);
                prof.Insert(0, new Vector2(core * 1.15f, h / 2));
                var roll = Shapes.Lathe(prof, 32);
                var inner = Shapes.Lathe(P(core * 0.98f, -h / 2, core * 0.98f, h / 2), 16).Flip();
                for (int x = 0; x < nx; x++)
                for (int y = 0; y < ny; y++)
                for (int z = 0; z < nz; z++)
                {
                    Vector3 p = new Vector3((x - (nx - 1) / 2f) * s.x / nx, (y - (ny - 1) / 2f) * s.y / ny, (z - (nz - 1) / 2f) * s.z / nz);
                    c.V(0).Add(roll, p, Mats.Lit(Color.white, 0.1f, 0, TexGen.Cardboard));
                    c.V(0).Add(inner, p, Cardboard(H("#B98D5A")));
                }
                c.V(0).Add(Shapes.RoundedBox(s * 1.012f, 0.015f, 3, 3), Film);
                var tex = LabelBaker.Box(new Vector3(s.x, s.y * 0.35f, s.z), c.Style);
                c.V(0).Add(Shapes.Quad(s.x * 0.8f, s.y * 0.35f), new Vector3(0, 0, -s.z * 0.506f - 0.001f), Quaternion.identity,
                    Mats.Label(LabelBaker.Sticker(new Vector2(s.x * 0.8f, s.y * 0.35f), c.Style), 0.5f));
            };
            B["toilet_paper"] = c => rolls(c, 3, 2, 2, false);
            B["paper_towels"] = c => rolls(c, 3, 1, 2, true);
            Action<LookCtx, Color> jugHandle = (c, capCol) =>
            {
                var s = c.S(0);
                float hx = s.x / 2, hy = s.y / 2, hz = s.z / 2;
                var sdf = new Sdf { DefaultBlend = 0.015f };
                sdf.Box(new Vector3(0, -hy * 0.1f, 0), new Vector3(hx, hy * 0.88f, hz), hz * 0.5f);
                sdf.Capsule(new Vector3(-hx * 0.45f, hy * 0.7f, 0), new Vector3(-hx * 0.45f, hy, 0), hz * 0.42f, hz * 0.4f, 0, 0.02f);
                sdf.Capsule(new Vector3(hx * 0.15f, hy * 0.55f, 0), new Vector3(hx * 0.6f, hy * 0.25f, 0), hz * 0.28f, hz * 0.28f, 0, 0.008f).Cut();
                var mb = sdf.Mesh(new Bounds(Vector3.zero, s * 1.05f), 0.0045f, 1, 15)[0];
                c.V(0).Add(mb, Mats.Lit(c.C(0), 0.55f));
                var st = c.Style;
                Sticker(c, 0, new Vector3(0, -hy * 0.25f, -hz - 0.0012f), new Vector2(s.x * 0.86f, s.y * 0.5f), null, st);
                if (c.Sets.Length > 1) Cap(c, 1, capCol);
            };
            B["detergent"] = c => jugHandle(c, H("#0D47A1"));
            B["bleach"] = c => jugHandle(c, H("#1E88E5"));
            B["light_bulbs"] = c => Carton(c, 0, 0.002f, 0.35f);
            B["forks"] = c =>
            {
                var s = c.S(0);
                for (int k = 0; k < 9; k++)
                {
                    var fork = new MB();
                    var handle = Shapes.RoundedBox(new Vector3(s.x * 0.62f, 0.003f, 0.009f), 0.0012f, 1, 1);
                    var head = Shapes.RoundedBox(new Vector3(s.x * 0.3f, 0.002f, 0.02f), 0.001f, 1, 1);
                    c.V(0).Add(handle, new Vector3(-s.x * 0.15f, (k - 4) * 0.004f, (k % 3 - 1) * 0.015f), Quaternion.Euler(0, k * 3 - 12, 0), Plastic(Color.white, 0.7f));
                    c.V(0).Add(head, new Vector3(s.x * 0.28f, (k - 4) * 0.004f, (k % 3 - 1) * 0.015f), Quaternion.Euler(0, k * 3 - 12, 0), Plastic(Color.white, 0.7f));
                }
                ClearBag(c, 0, 0.0001f, 0.5f, true);
                c.V(0).Add(Shapes.RoundedBox(new Vector3(0.03f, s.y * 1.1f, s.z * 1.05f), 0.001f, 1, 1, (f, l) => l), new Vector3(-s.x * 0.5f + 0.015f, 0, 0), Quaternion.identity,
                    Mats.Label(LabelBaker.Sticker(new Vector2(s.z, s.y * 1.1f), c.Style, true), 0.3f));
            };
            B["skewers"] = c =>
            {
                var s = c.S(0);
                var rng = new System.Random(2);
                var stick = Shapes.Lathe(P(0, -s.y / 2, 0.0015f, -s.y / 2 + 0.002f, 0.0016f, s.y / 2 - 0.012f, 0.0002f, s.y / 2, 0, s.y / 2), 6);
                for (int k = 0; k < 40; k++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2, rr = Mathf.Sqrt((float)rng.NextDouble()) * s.x * 0.42f;
                    c.V(0).Add(stick, new Vector3(Mathf.Cos(a) * rr, (float)(rng.NextDouble() - 0.5) * 0.004f, Mathf.Sin(a) * rr), Mats.Lit(Color.white, 0.2f, 0, TexGen.Wood));
                }
                var tex = LabelBaker.Wrap(Mathf.PI * s.x, s.y * 0.25f, c.Style);
                c.V(0).Add(Shapes.Band(s.x / 2 + 0.0005f, s.x / 2 + 0.0005f, -s.y * 0.15f, s.y * 0.1f, 24, 2), Mats.Label(tex, 0.3f));
            };
            B["knife"] = c => Blister(c, 0, true, (v, pos, rot) =>
            {
                var s = c.S(0);
                v.Add(Shapes.RoundedBox(new Vector3(s.x * 0.36f, 0.006f, 0.02f), 0.004f, 2, 2), pos + rot * new Vector3(-s.x * 0.2f, 0, 0), rot, Plastic(H("#212121"), 0.4f));
                var blade = Shapes.RoundedBox(new Vector3(s.x * 0.42f, 0.002f, 0.022f), 0.0008f, 1, 2);
                blade.Displace((p, n) => p.x > 0 ? new Vector3(p.x, p.y, p.z - Mathf.Pow(p.x / (s.x * 0.21f), 3) * 0.008f * Mathf.Sign(p.z + 0.02f)) : p);
                v.Add(blade, pos + rot * new Vector3(s.x * 0.18f, 0, 0), rot, Metal(new Color(0.9f, 0.9f, 0.92f), 0.85f));
            });
            B["sponges"] = c =>
            {
                var s = c.S(0);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = new Vector3((k - 1) * s.x / 3, 0, 0);
                    c.V(0).Add(Shapes.RoundedBox(new Vector3(s.x / 3 * 0.95f, s.y * 0.62f, s.z * 0.95f), 0.003f, 2, 2), p + new Vector3(0, -s.y * 0.19f, 0), Quaternion.identity,
                        Mats.Lit(H("#FFD54F"), 0.05f, 0, TexGen.Sponge, TexGen.SpongeN, 1.2f, new Vector2(2, 2)));
                    c.V(0).Add(Shapes.RoundedBox(new Vector3(s.x / 3 * 0.95f, s.y * 0.33f, s.z * 0.95f), 0.002f, 2, 2), p + new Vector3(0, s.y * 0.3f, 0), Quaternion.identity,
                        Mats.Lit(H("#2E7D32"), 0.05f, 0, TexGen.Speckle, TexGen.BumpyN, 1.5f, new Vector2(3, 3)));
                }
                c.V(0).Add(Shapes.RoundedBox(s * 1.02f, 0.006f, 2, 2), Film);
                Sticker(c, 0, new Vector3(0, 0, -s.z * 0.51f - 0.001f), new Vector2(s.x * 0.5f, s.y * 0.7f));
            };
            B["foil"] = c =>
            {
                Carton(c, 0, 0.0015f, 0.4f);
                var s = c.S(0);
                c.V(0).Add(Shapes.RoundedBox(new Vector3(s.x * 0.98f, 0.003f, 0.002f), 0.0005f, 1, 1), new Vector3(0, s.y * 0.35f, -s.z / 2 - 0.0008f), Quaternion.identity, Metal(H("#D0D0D0")));
            };
            B["tissues"] = c =>
            {
                Carton(c, 0, 0.002f, 0.25f);
                var s = c.S(0);
                c.V(0).Add(Shapes.Disc(0.5f, 24).Transform(Matrix4x4.Scale(new Vector3(s.x * 0.45f, 1, s.z * 0.3f))), new Vector3(0, s.y / 2 + 0.0006f, 0), Quaternion.identity, Matte(H("#3A3A3A")));
                var tissue = Leaf(0.06f, 0.08f, 0.2f, 1);
                c.V(0).Add(tissue, new Vector3(0, s.y / 2 - 0.005f, 0), Quaternion.Euler(-10, 0, 0), Matte(Color.white));
            };
            B["batteries"] = c => Blister(c, 0, false, (v, pos, rot) =>
            {
                var s = c.S(0);
                var cell = Shapes.Lathe(Shapes.Fillet(P(0, -0.025f, 0.007f, -0.025f, 0.007f, 0.024f, 0.003f, 0.024f, 0.003f, 0.026f, 0, 0.026f), 0.0008f, 2), 18);
                for (int k = 0; k < 8; k++)
                {
                    Vector3 p = pos + new Vector3((k % 4 - 1.5f) * 0.016f, (k / 4 - 0.5f) * 0.055f, 0);
                    v.Add(cell, p, Quaternion.identity, Mats.Lit(k % 2 == 0 ? H("#212121") : H("#FFB300"), 0.6f, 0.4f));
                }
            });

            // ======================== 宠物 ========================
            B["cat_can"] = c => Can(c, 0, true, 0.004f);
            B["cat_litter"] = c =>
            {
                var s = c.S(0);
                var sdf = new Sdf { DefaultBlend = 0.01f };
                sdf.Box(new Vector3(0, -s.y * 0.04f, 0), new Vector3(s.x * 0.48f, s.y * 0.46f, s.z * 0.48f), 0.03f);
                c.V(0).Add(sdf.Mesh(new Bounds(Vector3.zero, s), 0.006f, 1, 10)[0], Mats.Lit(H("#37474F"), 0.4f));
                c.V(0).Add(Shapes.RoundedBox(new Vector3(s.x * 1.01f, s.y * 0.08f, s.z * 1.01f), 0.02f, 3, 2), new Vector3(0, s.y * 0.44f, 0), Quaternion.identity, Plastic(H("#FFC107"), 0.45f));
                var arc = new List<Vector3>(); var rr = new List<float>();
                for (int k = 0; k <= 12; k++) { float a = k / 12f * Mathf.PI; arc.Add(new Vector3(Mathf.Cos(a) * s.x * 0.3f, s.y * 0.48f + Mathf.Sin(a) * 0.04f, 0)); rr.Add(0.005f); }
                c.V(0).Add(Shapes.Tube(arc, rr, 8), Plastic(H("#263238")));
                Sticker(c, 0, new Vector3(0, -s.y * 0.05f, -s.z * 0.5f - 0.0015f), new Vector2(s.x * 0.82f, s.y * 0.62f));
            };
            B["catnip"] = c => Pouch(c, 0, false, 0.4f, 0f, 1f);
            B["cat_kibble"] = c => Pouch(c, 0, false, 0.5f, 0.05f, 0.9f);
            B["cat_wand"] = c =>
            {
                var s = c.S(0);
                c.V(0).Add(Shapes.Tube(new[] { new Vector3(-s.x / 2, 0, 0), new Vector3(s.x * 0.35f, 0, 0) }, new[] { 0.004f, 0.003f }, 8), Plastic(H("#FFCA28"), 0.6f));
                var path = new List<Vector3>(); var rr = new List<float>();
                for (int k = 0; k <= 8; k++) { float t = k / 8f; path.Add(new Vector3(s.x * 0.35f + t * s.x * 0.12f, -Mathf.Sin(t * Mathf.PI) * 0.01f, 0)); rr.Add(0.0006f); }
                c.V(0).Add(Shapes.Tube(path, rr, 4), Matte(Color.white));
                Color[] cols = { H("#EC407A"), H("#AB47BC"), H("#29B6F6"), H("#FFEE58") };
                for (int k = 0; k < 7; k++)
                    c.V(0).Add(Leaf(0.06f, 0.016f, 0.1f, k), new Vector3(s.x * 0.47f, 0, 0), Quaternion.Euler(k * 50, 0, -90 + (k - 3) * 12), Mats.Lit(cols[k % 4], 0.2f, 0, TexGen.Fur));
                c.V(0).Add(Shapes.Ellipsoid(Vector3.one * 0.012f), new Vector3(s.x * 0.47f, -0.006f, 0), Quaternion.identity, Mats.Lit(H("#FFD54F"), 0.9f, 1f));
            };
            B["fish_treats"] = c => Pouch(c, 0, false, 0.7f, 0.2f, 1f);
        }

        static Vector3 Rot(LookCtx c, int i)
        {
            // 柠檬的部件已经旋转了 90°，这里把尺寸转换回长轴在 x
            var s = c.S(i);
            return new Vector3(s.y, s.x, s.z);
        }

        static Material MatFor(string id)
        {
            switch (id)
            {
                case "potatoes": return Mats.Lit(Color.white, 0.25f, 0, TexGen.Potato, TexGen.PitsN, 0.6f);
                case "onions": return Mats.Lit(Color.white, 0.45f, 0, TexGen.Onion, null);
                default: return Mats.Lit(Color.white, 0.3f, 0, TexGen.Speckle);
            }
        }
    }
}
