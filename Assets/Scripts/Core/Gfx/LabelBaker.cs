using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    public enum LabelPattern { None, Stripes, Waves, Dots, Paws, Checker, Rays, Diagonal }
    public enum LabelIcon { None, Cat, Fish, Paw, Leaf, Drop, Star, Wheat, Snow, Flame, Heart, Milk, Bubble, Sun, Fruit, Bone, Egg, Cookie, Bean, Pizza, Bolt }

    /// <summary>包装设计参数。</summary>
    public class LabelStyle
    {
        public string Brand = "CatMart";
        public string Title = "";
        public string Sub = "";
        public string Badge = "";
        public Color Bg = Color.white, Bg2 = Color.white, Accent = new Color(0.95f, 0.55f, 0.15f), Ink = new Color(0.1f, 0.1f, 0.15f), BrandInk = Color.white;
        public LabelPattern Pattern = LabelPattern.None;
        public LabelIcon Icon = LabelIcon.None;
        public Color IconColor = Color.white;
        public int Seed;
    }

    /// <summary>
    /// 用离屏相机把"矢量图形 + 文字"渲染成包装贴图。
    /// 所有坐标都是真实物理尺寸（米），所以文字在不同长宽比的面上都不会被拉伸。
    /// </summary>
    public static class LabelBaker
    {
        const int Layer = 31;
        static Camera _cam;
        static Transform _root;
        static int _slot;

        static Camera Cam
        {
            get
            {
                if (_cam == null)
                {
                    var go = new GameObject("LabelBakerCam");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    _cam = go.AddComponent<Camera>();
                    _cam.enabled = false;
                    _cam.orthographic = true;
                    _cam.cullingMask = 1 << Layer;
                    _cam.clearFlags = CameraClearFlags.SolidColor;
                    _cam.nearClipPlane = 0.01f;
                    _cam.farClipPlane = 4f;
                    _cam.allowMSAA = true;
                    _cam.allowHDR = false;
                    _root = new GameObject("LabelBakerRoot") { hideFlags = HideFlags.HideAndDontSave }.transform;
                    _root.position = new Vector3(0, -500, 0);
                }
                return _cam;
            }
        }

        // ================================================================ 画布
        public class Canvas
        {
            public readonly Transform Root;
            public readonly float W, H;
            int _layer;
            readonly List<TextMesh> _texts = new List<TextMesh>();

            public Canvas(Transform root, float w, float h) { Root = root; W = w; H = h; }

            float Z() => -0.0005f * (++_layer);

            GameObject Mesh(MB mb, Material m, Vector3 pos, float rotZ = 0)
            {
                var go = new GameObject("s") { layer = Layer };
                go.transform.SetParent(Root, false);
                go.transform.localPosition = new Vector3(pos.x, pos.y, Z());
                go.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
                go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh();
                go.AddComponent<MeshRenderer>().sharedMaterial = m;
                return go;
            }

            public void Rect(float x, float y, float w, float h, Color c, float rotZ = 0) => Mesh(Shapes.Quad(w, h), Mats.Unlit(c), new Vector3(x, y), rotZ);

            public void Gradient(Color top, Color bottom)
            {
                Mesh(Shapes.Quad(W * 1.02f, H * 1.02f), Mats.UnlitTex(TexGen.Gradient(top, bottom)), Vector3.zero);
            }

            public void Circle(float x, float y, float r, Color c, float sx = 1, float sy = 1)
            {
                var mb = Shapes.Disc(r, 40);
                mb.Transform(Matrix4x4.Scale(new Vector3(sx, 1, sy)));
                mb.Transform(Matrix4x4.Rotate(Quaternion.Euler(-90, 0, 0)));
                Mesh(mb, Mats.Unlit(c), new Vector3(x, y));
            }

            public void RoundRect(float x, float y, float w, float h, float r, Color c)
            {
                r = Mathf.Min(r, Mathf.Min(w, h) / 2);
                Rect(x, y, w - 2 * r, h, c);
                _layer--; Rect(x, y, w, h - 2 * r, c);
                for (int i = 0; i < 4; i++)
                {
                    _layer--;
                    float cx = x + (i % 2 == 0 ? -1 : 1) * (w / 2 - r), cy = y + (i < 2 ? -1 : 1) * (h / 2 - r);
                    Circle(cx, cy, r, c);
                }
            }

            /// <summary>以中心为扇心的多边形（适用于星形等）。点为 (x,y)。</summary>
            public void Poly(IList<Vector2> pts, Color c, float x = 0, float y = 0)
            {
                var mb = new MB();
                Vector2 cen = Vector2.zero;
                foreach (var p in pts) cen += p;
                cen /= pts.Count;
                float area = 0;
                for (int i = 0; i < pts.Count; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Count]; area += a.x * b.y - b.x * a.y; }
                int ci = mb.Vert(new Vector3(cen.x, cen.y), Vector2.zero);
                foreach (var p in pts) mb.Vert(new Vector3(p.x, p.y), Vector2.zero);
                for (int i = 0; i < pts.Count; i++)
                {
                    int a = 1 + i, b = 1 + (i + 1) % pts.Count;
                    if (area > 0) mb.Tri(ci, b, a); else mb.Tri(ci, a, b);
                }
                Mesh(mb.Normals(false), Mats.Unlit(c), new Vector3(x, y));
            }

            /// <summary>文字：自动缩放到 height（行高）且不超过 maxW。</summary>
            public TextMesh Text(string s, float x, float y, float height, Color c, float maxW = 0, TextAnchor anchor = TextAnchor.MiddleCenter,
                FontStyle style = FontStyle.Normal, float rotZ = 0)
            {
                if (string.IsNullOrEmpty(s)) return null;
                var go = new GameObject("t") { layer = Layer };
                go.transform.SetParent(Root, false);
                go.transform.localPosition = new Vector3(x, y, Z() - 0.002f);
                go.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
                var tm = go.AddComponent<TextMesh>();
                tm.font = Fonts.Main;
                tm.fontSize = 96;
                tm.fontStyle = style;
                tm.characterSize = 0.01f;
                tm.anchor = anchor;
                tm.alignment = TextAlignment.Center;
                tm.color = c;
                tm.text = s;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = Fonts.Main.material;
                var b = mr.bounds.size;
                float bh = rotZ != 0 ? b.x : b.y, bw = rotZ != 0 ? b.y : b.x;
                if (bh > 1e-6f)
                {
                    float k = height / bh;
                    if (maxW > 0 && bw * k > maxW) k = maxW / bw;
                    go.transform.localScale = Vector3.one * k;
                }
                _texts.Add(tm);
                return tm;
            }

            public void RefreshTexts()
            {
                foreach (var t in _texts) { var s = t.text; t.text = ""; t.text = s; }
            }
        }

        // ================================================================ 渲染
        struct Region { public Rect Px; public float W, H; public System.Action<Canvas> Draw; public Color Clear; }

        static Texture2D Render(int w, int h, List<Region> regions, string name)
        {
            var cam = Cam;
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = true });
            var prevActive = RenderTexture.active;
            var canvases = new List<Canvas>();
            // 先把所有文字的字符请求进字体贴图，避免渲染途中字体图集重建
            foreach (var r in regions)
            {
                var holder = new GameObject("canvas") { layer = Layer, hideFlags = HideFlags.HideAndDontSave };
                holder.transform.SetParent(_root, false);
                holder.transform.localPosition = new Vector3((_slot++ % 50) * 3f, 0, 0);
                var cv = new Canvas(holder.transform, r.W, r.H);
                r.Draw(cv);
                canvases.Add(cv);
            }
            foreach (var cv in canvases) cv.RefreshTexts();

            cam.targetTexture = rt;
            for (int i = 0; i < regions.Count; i++)
            {
                var r = regions[i];
                var cv = canvases[i];
                cam.transform.position = cv.Root.position + new Vector3(0, 0, -2f);
                cam.transform.rotation = Quaternion.identity;
                cam.rect = new Rect(r.Px.x / w, r.Px.y / h, r.Px.width / w, r.Px.height / h);
                cam.orthographicSize = r.H / 2f;
                cam.aspect = r.W / r.H;
                cam.backgroundColor = r.Clear;
                cam.Render();
            }
            cam.targetTexture = null;

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 8 };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            tex.Apply(true, false);
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);
            tex.Compress(true);
            tex.Apply(false, true);

            foreach (var cv in canvases) Object.Destroy(cv.Root.gameObject);
            return tex;
        }

        // ================================================================ 公共接口

        /// <summary>
        /// 盒子图集 1024×512：正面 [0,0.375] 背面 [0.375,0.75] 侧面 [0.75,0.875] 顶面 [0.875,1]。
        /// 配合 BoxUv 使用。size = 盒子尺寸 (x 宽, y 高, z 深)。
        /// </summary>
        /// <summary>森友会风：包装颜色调成粉彩、背景纯色、文字用深棕色而不是纯黑。</summary>
        static LabelStyle Cz(LabelStyle s)
        {
            if (!ArtStyle.Cozy || s == null) return s;
            return new LabelStyle
            {
                Brand = s.Brand, Title = s.Title, Sub = s.Sub, Badge = s.Badge,
                Bg = ArtStyle.Pastel(s.Bg),
                Bg2 = Color.Lerp(ArtStyle.Pastel(s.Bg), ArtStyle.Pastel(s.Bg2), 0.35f),
                Accent = ArtStyle.Pastel(s.Accent),
                Ink = s.Ink.grayscale < 0.35f ? new Color(0.30f, 0.20f, 0.15f) : s.Ink,
                BrandInk = s.BrandInk,
                Pattern = s.Pattern == LabelPattern.None ? LabelPattern.Paws : s.Pattern,
                Icon = s.Icon == LabelIcon.None ? LabelIcon.Cat : s.Icon,
                IconColor = ArtStyle.Pastel(s.IconColor),
                Seed = s.Seed,
            };
        }

        public static Texture2D Box(Vector3 size, LabelStyle s)
        {
            s = Cz(s);
            var regions = new List<Region>
            {
                new Region { Px = new Rect(0, 0, 384, 512), W = size.x, H = size.y, Draw = c => Front(c, s), Clear = s.Bg },
                new Region { Px = new Rect(384, 0, 384, 512), W = size.x, H = size.y, Draw = c => Back(c, s), Clear = s.Bg },
                new Region { Px = new Rect(768, 0, 128, 512), W = size.z, H = size.y, Draw = c => Side(c, s), Clear = s.Bg },
                new Region { Px = new Rect(896, 0, 128, 512), W = size.x, H = size.z, Draw = c => Top(c, s), Clear = s.Bg },
            };
            return Render(1024, 512, regions, s.Title + "_box");
        }

        /// <summary>RoundedBox 的 uvMap：配合 Box() 图集。</summary>
        public static Vector2 BoxUv(Shapes.Face f, Vector2 l)
        {
            switch (f)
            {
                case Shapes.Face.NegZ: return new Vector2(l.x * 0.375f, l.y);
                case Shapes.Face.PosZ: return new Vector2(0.375f + l.x * 0.375f, l.y);
                case Shapes.Face.PosX:
                case Shapes.Face.NegX: return new Vector2(0.75f + l.x * 0.125f, l.y);
                default: return new Vector2(0.875f + l.x * 0.125f, l.y);
            }
        }

        /// <summary>环绕标签（罐头/瓶子）：W=周长, H=标签高度。u=0.5 是正面。</summary>
        public static Texture2D Wrap(float circumference, float height, LabelStyle s)
        {
            s = Cz(s);
            int w = 1024;
            int h = Mathf.Clamp(Mathf.NextPowerOfTwo((int)(w * height / circumference)), 128, 512);
            var regions = new List<Region>
            {
                new Region { Px = new Rect(0, 0, w, h), W = circumference, H = height, Clear = s.Bg, Draw = c =>
                {
                    c.Gradient(s.Bg, s.Bg2);
                    DrawPattern(c, s, 0.9f);
                    // 正面居中
                    float fw = Mathf.Min(circumference * 0.42f, height * 1.4f);
                    var sub = new Canvas(c.Root, fw, height);
                    FrontContent(c, s, 0, fw, height);
                    if (ArtStyle.Cozy)
                    {
                        // 背面：两个大图标（不要写实的营养表和条码）
                        float ir = Mathf.Min(circumference * 0.08f, height * 0.3f);
                        DrawIcon(c, s.Icon, -circumference / 2 + circumference * 0.2f, 0, ir, s.IconColor, s.Accent, s.Bg);
                        DrawIcon(c, LabelIcon.Paw, circumference / 2 - circumference * 0.2f, 0, ir * 0.8f, s.Accent, s.Accent, s.Bg);
                        return;
                    }
                    // 背面：营养表 + 条码，放在接缝两侧
                    Nutrition(c, s, -circumference / 2 + circumference * 0.17f, 0, Mathf.Min(circumference * 0.22f, height * 0.9f), height * 0.8f);
                    Barcode(c, circumference / 2 - circumference * 0.14f, -height * 0.1f, Mathf.Min(circumference * 0.14f, height * 0.6f), height * 0.45f, s.Seed);
                }},
            };
            return Render(w, h, regions, s.Title + "_wrap");
        }

        /// <summary>包装袋：1024×512，正面 [0,0.5]，背面 [0.5,1]（配合 Shapes.Pillow 的 UV）。</summary>
        public static Texture2D Pouch(Vector2 size, LabelStyle s)
        {
            s = Cz(s);
            var regions = new List<Region>
            {
                new Region { Px = new Rect(0, 0, 512, 512), W = size.x, H = size.y, Draw = c => Front(c, s), Clear = s.Bg },
                new Region { Px = new Rect(512, 0, 512, 512), W = size.x, H = size.y, Draw = c => Back(c, s), Clear = s.Bg },
            };
            return Render(1024, 512, regions, s.Title + "_pouch");
        }

        /// <summary>小贴纸/价签/腰封。</summary>
        public static Texture2D Sticker(Vector2 size, LabelStyle s, bool simple = false)
        {
            s = Cz(s);
            int w = 512, h = Mathf.Clamp(Mathf.NextPowerOfTwo((int)(512 * size.y / size.x)), 64, 512);
            var regions = new List<Region>
            {
                new Region { Px = new Rect(0, 0, w, h), W = size.x, H = size.y, Clear = s.Bg, Draw = c =>
                {
                    c.Gradient(s.Bg, s.Bg2);
                    if (simple)
                    {
                        c.Text(s.Brand, 0, size.y * 0.15f, size.y * 0.32f, s.Accent, size.x * 0.9f, TextAnchor.MiddleCenter, FontStyle.Bold);
                        c.Text(s.Title, 0, -size.y * 0.22f, size.y * 0.24f, s.Ink, size.x * 0.9f);
                    }
                    else FrontContent(c, s, 0, size.x, size.y);
                }},
            };
            return Render(w, h, regions, s.Title + "_sticker");
        }

        // ================================================================ 版式
        static void Front(Canvas c, LabelStyle s)
        {
            c.Gradient(s.Bg, s.Bg2);
            DrawPattern(c, s, 1f);
            FrontContent(c, s, 0, c.W, c.H);
        }

        static void FrontContent(Canvas c, LabelStyle s, float x0, float W, float H)
        {
            bool wide = W > H * 1.5f;
            float u = Mathf.Min(W, H);
            if (!wide)
            {
                // 品牌横幅
                float bh = H * 0.13f;
                c.RoundRect(x0, H * 0.36f, W * 0.86f, bh, bh * 0.35f, s.Accent);
                c.Text(s.Brand, x0, H * 0.36f, bh * 0.62f, s.BrandInk, W * 0.8f, TextAnchor.MiddleCenter, FontStyle.Bold);
                // 主图
                DrawIcon(c, s.Icon, x0, H * 0.05f, Mathf.Min(W * 0.32f, H * 0.2f), s.IconColor, s.Accent, s.Bg);
                // 标题
                c.Text(s.Title, x0, -H * 0.24f, H * 0.11f, s.Ink, W * 0.9f, TextAnchor.MiddleCenter, FontStyle.Bold);
                c.Text(s.Sub, x0, -H * 0.37f, H * 0.055f, s.Ink, W * 0.85f);
                if (!string.IsNullOrEmpty(s.Badge))
                {
                    float br = u * 0.13f;
                    c.Circle(x0 + W * 0.33f, H * 0.17f, br, Color.Lerp(s.Accent, Color.yellow, 0.5f));
                    c.Text(s.Badge, x0 + W * 0.33f, H * 0.17f, br * 0.7f, s.Ink, br * 1.6f, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
            }
            else
            {
                float iconR = H * 0.3f;
                DrawIcon(c, s.Icon, x0 - W * 0.3f, 0, iconR, s.IconColor, s.Accent, s.Bg);
                float tx = x0 + W * 0.12f, tw = W * 0.62f;
                c.RoundRect(tx, H * 0.27f, tw * 0.9f, H * 0.22f, H * 0.08f, s.Accent);
                c.Text(s.Brand, tx, H * 0.27f, H * 0.15f, s.BrandInk, tw * 0.8f, TextAnchor.MiddleCenter, FontStyle.Bold);
                c.Text(s.Title, tx, -H * 0.05f, H * 0.2f, s.Ink, tw, TextAnchor.MiddleCenter, FontStyle.Bold);
                c.Text(s.Sub, tx, -H * 0.3f, H * 0.11f, s.Ink, tw);
            }
        }

        static void Back(Canvas c, LabelStyle s)
        {
            if (ArtStyle.Cozy) { CozyBack(c, s); return; }
            c.Gradient(Color.Lerp(s.Bg, Color.white, 0.25f), Color.Lerp(s.Bg2, Color.white, 0.25f));
            float W = c.W, H = c.H;
            c.Text(s.Brand + " · " + s.Title, 0, H * 0.4f, H * 0.06f, s.Ink, W * 0.9f, TextAnchor.MiddleCenter, FontStyle.Bold);
            Nutrition(c, s, -W * 0.18f, H * 0.02f, W * 0.5f, H * 0.6f);
            Barcode(c, W * 0.26f, -H * 0.3f, W * 0.3f, H * 0.18f, s.Seed);
            DrawIcon(c, LabelIcon.Cat, W * 0.26f, H * 0.12f, Mathf.Min(W, H) * 0.12f, s.Accent, s.Accent, Color.white);
            c.Text("CatMart 喵喵超市监制", W * 0.26f, -H * 0.05f, H * 0.035f, s.Ink, W * 0.4f);
        }

        /// <summary>森友会风背面：花纹 + 大图标 + 品牌横幅 + 一句卖萌的话。</summary>
        static void CozyBack(Canvas c, LabelStyle s)
        {
            float W = c.W, H = c.H, u = Mathf.Min(W, H);
            c.Gradient(s.Bg, s.Bg2);
            DrawPattern(c, s, 1f);
            c.Circle(0, H * 0.08f, u * 0.3f, Color.Lerp(s.Bg, Color.white, 0.55f));
            DrawIcon(c, s.Icon, 0, H * 0.08f, u * 0.22f, s.IconColor, s.Accent, s.Bg);
            float bh = H * 0.12f;
            c.RoundRect(0, -H * 0.3f, W * 0.8f, bh, bh * 0.5f, s.Accent);
            c.Text(s.Brand, 0, -H * 0.3f, bh * 0.6f, s.BrandInk, W * 0.75f, TextAnchor.MiddleCenter, FontStyle.Bold);
            string[] lines = { "喵~ 好好吃", "猫猫推荐", "今天也要开心喵", "CatMart 精选" };
            c.Text(lines[Mathf.Abs(s.Seed) % lines.Length], 0, H * 0.38f, H * 0.065f, s.Ink, W * 0.85f, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        static void Side(Canvas c, LabelStyle s)
        {
            c.Gradient(s.Bg, s.Bg2);
            c.Rect(0, 0, c.W * 0.22f, c.H, s.Accent);
            c.Text(s.Brand, 0, c.H * 0.1f, c.W * 0.55f, s.Ink, c.H * 0.7f, TextAnchor.MiddleCenter, FontStyle.Bold, 90);
            DrawIcon(c, s.Icon, 0, -c.H * 0.36f, c.W * 0.28f, s.IconColor, s.Accent, s.Bg);
        }

        static void Top(Canvas c, LabelStyle s)
        {
            c.Gradient(s.Bg, s.Bg2);
            DrawPattern(c, s, 0.8f);
            c.Text(s.Brand, 0, 0, Mathf.Min(c.H, c.W) * 0.3f, s.Accent, c.W * 0.85f, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        static void Nutrition(Canvas c, LabelStyle s, float x, float y, float w, float h)
        {
            c.Rect(x, y, w, h, Color.white);
            float lh = h / 9f;
            c.Text("营养成分表", x, y + h / 2 - lh * 0.7f, lh * 0.75f, Color.black, w * 0.9f, TextAnchor.MiddleCenter, FontStyle.Bold);
            c.Rect(x, y + h / 2 - lh * 1.35f, w * 0.92f, lh * 0.08f, Color.black);
            string[] rows = { "能量", "蛋白质", "脂肪", "碳水化合物", "钠", "喵力值" };
            var rng = new System.Random(s.Seed);
            for (int i = 0; i < rows.Length; i++)
            {
                float ry = y + h / 2 - lh * (2.1f + i * 1.05f);
                c.Text(rows[i], x - w * 0.42f, ry, lh * 0.55f, Color.black, w * 0.5f, TextAnchor.MiddleLeft);
                c.Text($"{rng.Next(1, 900)}{(i == 0 ? "kJ" : i == 4 ? "mg" : "g")}", x + w * 0.42f, ry, lh * 0.55f, Color.black, w * 0.4f, TextAnchor.MiddleRight);
                c.Rect(x, ry - lh * 0.5f, w * 0.92f, lh * 0.03f, new Color(0.6f, 0.6f, 0.6f));
            }
        }

        static void Barcode(Canvas c, float x, float y, float w, float h, int seed)
        {
            c.Rect(x, y, w * 1.12f, h * 1.25f, Color.white);
            var rng = new System.Random(seed * 7 + 3);
            float cur = x - w / 2;
            while (cur < x + w / 2)
            {
                float bw = w * (0.012f + (float)rng.NextDouble() * 0.03f);
                if (rng.NextDouble() < 0.55) c.Rect(cur + bw / 2, y + h * 0.08f, bw, h * 0.8f, Color.black);
                cur += bw;
            }
            c.Text($"6 9{seed % 10}{(seed * 37) % 100000:00000} {(seed * 53) % 1000000:000000}", x, y - h * 0.48f, h * 0.18f, Color.black, w);
        }

        static void DrawPattern(Canvas c, LabelStyle s, float strength)
        {
            Color pc = Color.Lerp(s.Bg, s.Accent, 0.18f * strength);
            float W = c.W, H = c.H;
            var rng = new System.Random(s.Seed);
            switch (s.Pattern)
            {
                case LabelPattern.Stripes:
                    for (float x = -W / 2; x < W / 2; x += H * 0.12f) c.Rect(x, 0, H * 0.04f, H, pc);
                    break;
                case LabelPattern.Diagonal:
                    for (float x = -W; x < W; x += H * 0.15f) c.Rect(x, 0, H * 0.05f, H * 2.2f, pc, 30);
                    break;
                case LabelPattern.Dots:
                    for (float y = -H / 2; y < H / 2; y += H * 0.1f)
                    for (float x = -W / 2; x < W / 2; x += H * 0.1f)
                        c.Circle(x + ((int)(y / (H * 0.1f)) % 2) * H * 0.05f, y, H * 0.018f, pc);
                    break;
                case LabelPattern.Waves:
                    for (int k = 0; k < 3; k++)
                    {
                        float yy = -H * 0.45f + k * H * 0.06f;
                        for (float x = -W / 2; x < W / 2; x += H * 0.03f)
                            c.Circle(x, yy + Mathf.Sin(x / H * 20f) * H * 0.02f, H * 0.03f, Color.Lerp(s.Bg2, s.Accent, 0.3f + k * 0.15f));
                    }
                    break;
                case LabelPattern.Paws:
                    for (int i = 0; i < 10; i++)
                        DrawIcon(c, LabelIcon.Paw, (float)(rng.NextDouble() - 0.5) * W, (float)(rng.NextDouble() - 0.5) * H, H * 0.05f, pc, pc, s.Bg);
                    break;
                case LabelPattern.Checker:
                    float cs = H * 0.08f;
                    for (float y = -H / 2; y < -H * 0.32f; y += cs)
                    for (float x = -W / 2; x < W / 2; x += cs)
                        if (((int)((x + W) / cs) + (int)((y + H) / cs)) % 2 == 0) c.Rect(x + cs / 2, y + cs / 2, cs, cs, pc);
                    break;
                case LabelPattern.Rays:
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2, b = a + Mathf.PI / 12;
                        float R = Mathf.Max(W, H);
                        c.Poly(new[] { Vector2.zero, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R, new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * R }, pc, 0, H * 0.05f);
                    }
                    break;
            }
        }

        static List<Vector2> Star(float r, float inner, int n = 5)
        {
            var l = new List<Vector2>();
            for (int i = 0; i < n * 2; i++)
            {
                float a = Mathf.PI / 2 + i * Mathf.PI / n;
                float rr = i % 2 == 0 ? r : r * inner;
                l.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
            }
            return l;
        }

        /// <summary>矢量图标。</summary>
        public static void DrawIcon(Canvas c, LabelIcon icon, float x, float y, float r, Color main, Color accent, Color bg)
        {
            Color dark = new Color(0.15f, 0.12f, 0.12f);
            switch (icon)
            {
                case LabelIcon.Cat:
                    c.Poly(new[] { new Vector2(-r * 0.85f, r * 0.2f), new Vector2(-r * 0.75f, r * 1.05f), new Vector2(-r * 0.2f, r * 0.6f) }, main, x, y);
                    c.Poly(new[] { new Vector2(r * 0.85f, r * 0.2f), new Vector2(r * 0.2f, r * 0.6f), new Vector2(r * 0.75f, r * 1.05f) }, main, x, y);
                    c.Circle(x, y, r * 0.85f, main, 1.1f, 0.95f);
                    c.Circle(x - r * 0.32f, y + r * 0.1f, r * 0.12f, dark, 1, 1.3f);
                    c.Circle(x + r * 0.32f, y + r * 0.1f, r * 0.12f, dark, 1, 1.3f);
                    c.Poly(new[] { new Vector2(-r * 0.09f, -r * 0.12f), new Vector2(r * 0.09f, -r * 0.12f), new Vector2(0, -r * 0.24f) }, new Color(0.95f, 0.5f, 0.6f), x, y);
                    c.Rect(x - r * 0.55f, y - r * 0.22f, r * 0.4f, r * 0.02f, dark, 8);
                    c.Rect(x + r * 0.55f, y - r * 0.22f, r * 0.4f, r * 0.02f, dark, -8);
                    break;
                case LabelIcon.Fish:
                    c.Circle(x, y, r * 0.7f, main, 1.3f, 0.75f);
                    c.Poly(new[] { new Vector2(r * 0.75f, 0), new Vector2(r * 1.2f, r * 0.45f), new Vector2(r * 1.2f, -r * 0.45f) }, main, x, y);
                    c.Circle(x - r * 0.45f, y + r * 0.1f, r * 0.09f, dark);
                    c.Rect(x - r * 0.15f, y, r * 0.04f, r * 0.6f, Color.Lerp(main, dark, 0.3f));
                    break;
                case LabelIcon.Paw:
                    c.Circle(x, y - r * 0.2f, r * 0.45f, main, 1.15f, 0.9f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = Mathf.Lerp(150, 30, i / 3f) * Mathf.Deg2Rad;
                        c.Circle(x + Mathf.Cos(a) * r * 0.62f, y + Mathf.Sin(a) * r * 0.55f + r * 0.05f, r * 0.19f, main);
                    }
                    break;
                case LabelIcon.Leaf:
                    c.Circle(x, y, r * 0.8f, main, 0.55f, 1.2f);
                    c.Rect(x, y - r * 0.1f, r * 0.05f, r * 1.6f, Color.Lerp(main, Color.white, 0.4f));
                    break;
                case LabelIcon.Drop:
                    c.Circle(x, y - r * 0.2f, r * 0.6f, main);
                    c.Poly(new[] { new Vector2(-r * 0.55f, -r * 0.05f), new Vector2(0, r * 0.95f), new Vector2(r * 0.55f, -r * 0.05f) }, main, x, y);
                    c.Circle(x - r * 0.2f, y - r * 0.1f, r * 0.12f, Color.Lerp(main, Color.white, 0.6f));
                    break;
                case LabelIcon.Star:
                    c.Poly(Star(r, 0.45f), main, x, y);
                    break;
                case LabelIcon.Wheat:
                    c.Rect(x, y - r * 0.3f, r * 0.06f, r * 1.4f, main);
                    for (int i = 0; i < 4; i++)
                    {
                        c.Circle(x - r * 0.15f, y - r * 0.1f + i * r * 0.25f, r * 0.13f, main, 0.7f, 1.3f);
                        c.Circle(x + r * 0.15f, y - r * 0.1f + i * r * 0.25f, r * 0.13f, main, 0.7f, 1.3f);
                    }
                    break;
                case LabelIcon.Snow:
                    for (int i = 0; i < 3; i++) c.Rect(x, y, r * 0.12f, r * 1.8f, main, i * 60);
                    c.Circle(x, y, r * 0.2f, main);
                    break;
                case LabelIcon.Flame:
                    c.Circle(x, y - r * 0.25f, r * 0.6f, accent);
                    c.Poly(new[] { new Vector2(-r * 0.58f, -r * 0.1f), new Vector2(0, r), new Vector2(r * 0.58f, -r * 0.1f) }, accent, x, y);
                    c.Circle(x, y - r * 0.35f, r * 0.32f, main);
                    break;
                case LabelIcon.Heart:
                    c.Circle(x - r * 0.3f, y + r * 0.15f, r * 0.42f, main);
                    c.Circle(x + r * 0.3f, y + r * 0.15f, r * 0.42f, main);
                    c.Poly(new[] { new Vector2(-r * 0.7f, r * 0.05f), new Vector2(r * 0.7f, r * 0.05f), new Vector2(0, -r * 0.8f) }, main, x, y);
                    break;
                case LabelIcon.Milk:
                    c.RoundRect(x, y - r * 0.15f, r * 0.9f, r * 1.3f, r * 0.15f, main);
                    c.Poly(new[] { new Vector2(-r * 0.45f, r * 0.5f), new Vector2(0, r * 0.9f), new Vector2(r * 0.45f, r * 0.5f) }, main, x, y);
                    c.Circle(x - r * 0.15f, y - r * 0.2f, r * 0.15f, dark);
                    c.Circle(x + r * 0.2f, y + r * 0.1f, r * 0.12f, dark);
                    break;
                case LabelIcon.Bubble:
                    c.Circle(x - r * 0.3f, y - r * 0.2f, r * 0.45f, main);
                    c.Circle(x + r * 0.35f, y + r * 0.25f, r * 0.32f, main);
                    c.Circle(x + r * 0.1f, y - r * 0.55f, r * 0.18f, main);
                    break;
                case LabelIcon.Sun:
                    for (int i = 0; i < 8; i++) c.Rect(x, y, r * 0.1f, r * 1.9f, accent, i * 22.5f);
                    c.Circle(x, y, r * 0.6f, main);
                    break;
                case LabelIcon.Fruit:
                    c.Circle(x, y - r * 0.1f, r * 0.75f, main);
                    c.Circle(x + r * 0.3f, y + r * 0.75f, r * 0.25f, new Color(0.3f, 0.65f, 0.25f), 1.5f, 0.6f);
                    c.Circle(x - r * 0.25f, y + r * 0.15f, r * 0.15f, Color.Lerp(main, Color.white, 0.5f));
                    break;
                case LabelIcon.Bone:
                    c.Rect(x, y, r * 1.4f, r * 0.35f, main);
                    foreach (var sx in new[] { -1, 1 })
                    foreach (var sy in new[] { -1, 1 })
                        c.Circle(x + sx * r * 0.72f, y + sy * r * 0.18f, r * 0.24f, main);
                    break;
                case LabelIcon.Egg:
                    c.Circle(x - r * 0.35f, y, r * 0.42f, Color.white, 0.85f, 1.1f);
                    c.Circle(x + r * 0.35f, y, r * 0.42f, new Color(0.95f, 0.85f, 0.7f), 0.85f, 1.1f);
                    c.Circle(x + r * 0.35f, y - r * 0.05f, r * 0.2f, new Color(1f, 0.75f, 0.1f));
                    break;
                case LabelIcon.Cookie:
                    c.Circle(x, y, r * 0.8f, main);
                    var rng = new System.Random(3);
                    for (int i = 0; i < 6; i++) c.Circle(x + (float)(rng.NextDouble() - 0.5) * r, y + (float)(rng.NextDouble() - 0.5) * r, r * 0.1f, dark);
                    break;
                case LabelIcon.Bean:
                    c.Circle(x, y, r * 0.75f, main, 0.75f, 1.05f);
                    c.Rect(x, y, r * 0.06f, r * 1.1f, Color.Lerp(main, dark, 0.5f), 12);
                    break;
                case LabelIcon.Pizza:
                    c.Circle(x, y, r * 0.85f, new Color(0.9f, 0.65f, 0.3f));
                    c.Circle(x, y, r * 0.72f, new Color(0.85f, 0.2f, 0.15f));
                    var rp = new System.Random(5);
                    for (int i = 0; i < 6; i++) c.Circle(x + (float)(rp.NextDouble() - 0.5) * r, y + (float)(rp.NextDouble() - 0.5) * r, r * 0.12f, new Color(1f, 0.92f, 0.6f));
                    break;
                case LabelIcon.Bolt:
                    c.Poly(new[] { new Vector2(r * 0.2f, r), new Vector2(-r * 0.5f, -r * 0.1f), new Vector2(0, -r * 0.1f), new Vector2(-r * 0.2f, -r), new Vector2(r * 0.5f, r * 0.1f), new Vector2(0, r * 0.1f) }, main, x, y);
                    break;
            }
        }
    }
}
