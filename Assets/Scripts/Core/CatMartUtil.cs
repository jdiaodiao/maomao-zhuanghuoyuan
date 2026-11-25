using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>小工具：颜色、单位换算。</summary>
    public static class U
    {
        public static Color Hex(string hex)
        {
            if (!hex.StartsWith("#")) hex = "#" + hex;
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Vector3 Cm(float x, float y, float z) => new Vector3(x, y, z) * 0.01f;

        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        public static void DestroySafe(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        public static string Money(float v) => (v < 0 ? "-$" : "$") + Mathf.Abs(v).ToString("0.00");
    }

    /// <summary>运行时材质缓存。以默认材质为模板，所以内置管线/URP 都能用。</summary>
    public static class Mats
    {
        static Material _template;
        static readonly Dictionary<long, Material> Cache = new Dictionary<long, Material>();

        static Material Template
        {
            get
            {
                if (_template == null)
                {
                    // 优先用 Resources 里的材质（保证打包时着色器不被剔除）
                    var res = Resources.Load<Material>("CatMart/BaseMat");
                    if (res != null) _template = new Material(res);
                    else
                    {
                        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        _template = new Material(go.GetComponent<Renderer>().sharedMaterial);
                        U.DestroySafe(go);
                    }
                }
                return _template;
            }
        }

        public static Material Get(Color c, float smooth = 0.25f)
        {
            long key = ((long)(c.r * 255) << 32) | ((long)(c.g * 255) << 24) | ((long)(c.b * 255) << 16)
                       | ((long)(c.a * 255) << 8) | (long)(Mathf.Clamp01(smooth) * 255);
            if (Cache.TryGetValue(key, out var m) && m != null) return m;
            m = New(c, smooth);
            Cache[key] = m;
            return m;
        }

        public static Material New(Color c, float smooth = 0.25f)
        {
            if (ArtStyle.Cozy) return CozyNew("CozyMat", ArtStyle.Pastel(c), null, 0, smooth * 0.4f, Vector2.one);
            var m = new Material(Template) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        public static Material Textured(Texture2D tex, Color tint, float smooth = 0.2f)
        {
            var m = New(tint, smooth);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            return m;
        }

        public static Material Emissive(Color c, Color emission)
        {
            if (ArtStyle.Cozy)
            {
                var cm = CozyNew("CozyMat", ArtStyle.Pastel(c), null, 0, 0.3f, Vector2.one);
                cm.SetColor("_EmissionColor", emission * 0.85f);
                return cm;
            }
            var res = Resources.Load<Material>("CatMart/EmissiveMat");
            var m = res != null ? new Material(res) { color = c } : New(c, 0.5f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        // ---------------------------------------------------------------- 生产级材质
        static readonly Dictionary<string, Material> Named = new Dictionary<string, Material>();
        static readonly Dictionary<string, Material> Templates = new Dictionary<string, Material>();

        static Material Tpl(string name)
        {
            if (Templates.TryGetValue(name, out var t) && t != null) return t;
            var res = Resources.Load<Material>("CatMart/" + name);
            t = res != null ? res : Template;
            Templates[name] = t;
            return t;
        }

        static string K(params object[] parts) => string.Join("|", parts);

        /// <summary>PBR 不透明材质。albedo 与颜色相乘；normal 为法线贴图。</summary>
        public static Material Lit(Color c, float smooth = 0.3f, float metallic = 0f, Texture albedo = null, Texture normal = null,
            float bump = 1f, Vector2? tiling = null)
        {
            Vector2 tile = tiling ?? Vector2.one;
            string key = K("lit", Col(c), smooth.ToString("0.00"), metallic.ToString("0.00"), albedo ? albedo.GetInstanceIDSafe() : 0,
                normal ? normal.GetInstanceIDSafe() : 0, bump.ToString("0.0"), tile.x.ToString("0.00"), tile.y.ToString("0.00"));
            if (Named.TryGetValue(key, out var m) && m != null) return m;
            if (ArtStyle.Cozy)
            {
                // 卡通风：不用法线贴图；灰度细节贴图只保留一点点，彩色贴图（果皮、面包等）保留颜色
                bool colorTex = albedo != null && c.r > 0.95f && c.g > 0.95f && c.b > 0.95f;
                float detail = albedo == null ? 0 : colorTex ? 0.85f : 0.3f;
                m = CozyNew("CozyMat", colorTex ? c : ArtStyle.Pastel(c), albedo, detail, smooth * 0.45f + metallic * 0.25f, tile);
                Named[key] = m;
                return m;
            }
            m = new Material(Tpl(normal != null ? "NormalMat" : "BaseMat")) { color = c };
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metallic);
            if (albedo != null) m.mainTexture = albedo;
            if (normal != null)
            {
                m.SetTexture("_BumpMap", normal);
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }
            m.mainTextureScale = tile;
            Named[key] = m;
            return m;
        }

        /// <summary>玻璃/透明塑料（预乘透明，保留高光）。</summary>
        public static Material Glass(Color tint, float alpha = 0.25f, float smooth = 0.95f, Texture normal = null, float bump = 0.35f)
        {
            string key = K("glass", Col(tint), alpha.ToString("0.00"), smooth.ToString("0.00"), normal ? normal.GetInstanceIDSafe() : 0, bump.ToString("0.00"));
            if (Named.TryGetValue(key, out var m) && m != null) return m;
            if (ArtStyle.Cozy)
            {
                var pc = ArtStyle.Pastel(tint);
                pc.a = Mathf.Clamp01(alpha * 1.15f + 0.05f);
                m = CozyNew("CozyTransparentMat", pc, null, 0, 0.7f, Vector2.one);
                Named[key] = m;
                return m;
            }
            tint.a = alpha;
            m = new Material(Tpl(normal != null ? "GlassNormalMat" : "GlassMat")) { color = tint };
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", 0.05f);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", bump); m.EnableKeyword("_NORMALMAP"); }
            Named[key] = m;
            return m;
        }

        /// <summary>镂空材质（网袋、羽毛）。</summary>
        public static Material Cutout(Color c, Texture alphaTex, float cutoff = 0.5f, Vector2? tiling = null)
        {
            string key = K("cut", Col(c), alphaTex.GetInstanceIDSafe(), tiling ?? Vector2.one);
            if (Named.TryGetValue(key, out var m) && m != null) return m;
            if (ArtStyle.Cozy)
            {
                m = CozyNew("CozyCutoutMat", ArtStyle.Pastel(c), alphaTex, 0, 0.1f, tiling ?? Vector2.one);
                m.SetFloat("_Cutoff", cutoff);
                Named[key] = m;
                return m;
            }
            m = new Material(Tpl("CutoutMat")) { color = c, mainTexture = alphaTex };
            m.SetFloat("_Cutoff", cutoff);
            m.mainTextureScale = tiling ?? Vector2.one;
            Named[key] = m;
            return m;
        }

        /// <summary>贴图标签材质（每件商品独有）。</summary>
        public static Material Label(Texture tex, float smooth = 0.35f, float metallic = 0f)
        {
            if (ArtStyle.Cozy) return CozyNew("CozyMat", Color.white, tex, 1f, smooth * 0.4f, Vector2.one);
            var m = new Material(Tpl("BaseMat")) { color = Color.white, mainTexture = tex };
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metallic);
            return m;
        }

        public static Material Unlit(Color c)
        {
            string key = K("unlit", Col(c));
            if (Named.TryGetValue(key, out var m) && m != null) return m;
            var tpl = Resources.Load<Material>("CatMart/UnlitColorMat");
            m = tpl != null ? new Material(tpl) : new Material(Shader.Find("Unlit/Color"));
            m.color = c;
            Named[key] = m;
            return m;
        }

        public static Material UnlitTex(Texture tex)
        {
            string key = K("unlitT", tex.GetInstanceIDSafe());
            if (Named.TryGetValue(key, out var m) && m != null) return m;
            var tpl = Resources.Load<Material>("CatMart/UnlitTexMat");
            m = tpl != null ? new Material(tpl) : new Material(Shader.Find("Unlit/Texture"));
            m.mainTexture = tex;
            Named[key] = m;
            return m;
        }

        static string Col(Color c) => ColorUtility.ToHtmlStringRGBA(c);

        // ---------------------------------------------------------------- 森友会风
        static Material CozyNew(string tpl, Color c, Texture tex, float detail, float gloss, Vector2 tile)
        {
            var m = new Material(Tpl(tpl)) { color = c };
            if (tex != null) m.mainTexture = tex;
            if (m.HasProperty("_Detail")) m.SetFloat("_Detail", tex != null ? detail : 0f);
            if (m.HasProperty("_Gloss")) m.SetFloat("_Gloss", Mathf.Clamp01(gloss));
            m.mainTextureScale = tile;
            return m;
        }

        /// <summary>切换画风时清空所有材质缓存。</summary>
        public static void ClearCache()
        {
            Cache.Clear();
            Named.Clear();
            Templates.Clear();
            _template = null;
        }
    }

    public static class ObjIdExt
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Object, object> Ids = new System.Runtime.CompilerServices.ConditionalWeakTable<Object, object>();
        static int _next = 1;

        /// <summary>给对象分配稳定的整数 id（GetInstanceID 在新版 Unity 已弃用）。</summary>
        public static int GetInstanceIDSafe(this Object o)
        {
            if (o == null) return 0;
            return (int)Ids.GetValue(o, _ => _next++);
        }
    }

    /// <summary>中文动态字体（系统字体）。</summary>
    public static class Fonts
    {
        static Font _main;
        static Material _world;
        static bool _hooked;
        public static void ClearCache() { _main = null; _world = null; }

        /// <summary>场景里的 3D 文字材质：开启深度测试，不会透过物体显示。</summary>
        public static Material WorldMaterial
        {
            get
            {
                if (_world == null)
                {
                    var tpl = Resources.Load<Material>("CatMart/WorldTextMat");
                    _world = tpl != null ? new Material(tpl) : new Material(Main.material);
                    _world.color = Color.white;
                    if (!_hooked)
                    {
                        // 动态字体图集重建时，同步贴图（所有 3D 文字共用这个材质）
                        Font.textureRebuilt += f => { if (_world != null && f == _main) _world.mainTexture = f.material.mainTexture; };
                        _hooked = true;
                    }
                }
                _world.mainTexture = Main.material.mainTexture;
                return _world;
            }
        }
        public static Font Main
        {
            get
            {
                if (_main == null)
                {
                    // 森友会风：用打包进游戏的开源圆润字体「站酷快乐体」（OFL），缺字时由系统字体补
                    if (ArtStyle.Cozy)
                    {
                        var bundled = Resources.Load<Font>("Fonts/ZCOOLKuaiLe/ZCOOLKuaiLe-Regular");
                        if (bundled != null) { _main = bundled; return _main; }
                    }
                    var names = ArtStyle.Cozy
                        ? new[] { "YouYuan", "幼圆", "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Arial Rounded MT Bold", "Arial" }
                        : new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "PingFang SC", "Hiragino Sans GB", "Noto Sans CJK SC", "Arial" };
                    _main = Font.CreateDynamicFontFromOSFont(names, 64);
                }
                return _main;
            }
        }
    }

    /// <summary>3D 文字（TextMesh）。</summary>
    public static class Text3D
    {
        public static TextMesh Create(Transform parent, string text, Vector3 localPos, float height, Color color,
            TextAnchor anchor = TextAnchor.MiddleCenter, Quaternion? localRot = null)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Fonts.Main;
            tm.fontSize = 64;
            tm.characterSize = 0.01f;
            go.GetComponent<MeshRenderer>().sharedMaterial = Fonts.Main.material;
            // 用一个汉字实测字高，再换算成要求的高度（米）
            tm.text = "国";
            var q = go.transform.rotation;
            go.transform.rotation = Quaternion.identity;
            float measured = go.GetComponent<MeshRenderer>().bounds.size.y;
            go.transform.rotation = q;
            tm.characterSize = measured > 1e-6f ? 0.01f * height / measured : height * 0.15f;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft
                ? TextAlignment.Left : TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = Fonts.WorldMaterial;
            return tm;
        }
    }

    /// <summary>朝向摄像机。</summary>
    public class Billboard : MonoBehaviour
    {
        public bool YawOnly = true;
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 d = transform.position - cam.transform.position;
            if (YawOnly) d.y = 0;
            if (d.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(d);
        }
    }

    /// <summary>飘字提示。</summary>
    public class FloatText : MonoBehaviour
    {
        float _t;
        TextMesh _tm;
        Color _c;

        public static void Spawn(Vector3 pos, string text, Color color, float height = 0.05f)
        {
            var go = new GameObject("FloatText");
            go.transform.position = pos;
            var ft = go.AddComponent<FloatText>();
            ft._tm = Text3D.Create(go.transform, text, Vector3.zero, height, color);
            ft._c = color;
            go.AddComponent<Billboard>().YawOnly = false;
        }

        void Update()
        {
            _t += Time.deltaTime;
            transform.position += Vector3.up * Time.deltaTime * 0.15f;
            var c = _c;
            c.a = Mathf.Clamp01(1.8f - _t);
            _tm.color = c;
            if (_t > 1.8f) Destroy(gameObject);
        }
    }

    /// <summary>程序化音效。</summary>
    public static class Sfx
    {
        static AudioSource _src;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        public static void Init(GameObject host)
        {
            _src = host.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            var rng = new System.Random(7);
            float N() => (float)(rng.NextDouble() * 2 - 1);
            const float TAU = Mathf.PI * 2f;

            Clips["beep"] = Make("beep", 0.09f, t => Mathf.Sin(TAU * 1850 * t) * Env(t, 0.09f) * 0.3f);
            Clips["grab"] = Make("grab", 0.05f, t => N() * Mathf.Exp(-t * 90) * 0.25f + Mathf.Sin(TAU * 600 * t) * Mathf.Exp(-t * 60) * 0.2f);
            Clips["thud"] = Make("thud", 0.18f, t => Mathf.Sin(TAU * 85 * t) * Mathf.Exp(-t * 28) * 0.6f);
            float lp = 0;
            Clips["crunch"] = Make("crunch", 0.35f, t =>
            {
                lp = Mathf.Lerp(lp, N(), 0.35f);
                float crackle = rng.NextDouble() < 0.02 ? N() * 2f : 0f;
                return (lp + crackle) * Mathf.Exp(-t * 9) * 0.7f;
            });
            Clips["glass"] = Make("glass", 0.6f, t =>
                (Mathf.Sin(TAU * 3100 * t) + Mathf.Sin(TAU * 4570 * t) * 0.7f + Mathf.Sin(TAU * 6230 * t) * 0.5f) * Mathf.Exp(-t * 10) * 0.18f
                + N() * Mathf.Exp(-t * 30) * 0.4f);
            Clips["squish"] = Make("squish", 0.3f, t => (N() * 0.3f + Mathf.Sin(TAU * (220 - t * 300) * t)) * Mathf.Exp(-t * 10) * 0.4f);
            Clips["rip"] = Make("rip", 0.45f, t => N() * (0.6f + 0.4f * Mathf.Sin(TAU * 35 * t)) * Mathf.Exp(-t * 5) * 0.45f);
            Clips["ding"] = Make("ding", 1.0f, t =>
                (Mathf.Sin(TAU * 1320 * t) + 0.6f * Mathf.Sin(TAU * 1980 * t) + 0.3f * Mathf.Sin(TAU * 2640 * t)) * Mathf.Exp(-t * 4) * 0.22f);
            Clips["cash"] = Make("cash", 0.9f, t =>
            {
                float a = Mathf.Sin(TAU * 1568 * t) * Mathf.Exp(-t * 6);
                float b = t > 0.12f ? Mathf.Sin(TAU * 2093 * (t - 0.12f)) * Mathf.Exp(-(t - 0.12f) * 5) : 0;
                return (a + b) * 0.25f + N() * Mathf.Exp(-t * 40) * 0.2f;
            });
            Clips["bag"] = Make("bag", 0.3f, t => N() * Mathf.Exp(-t * 14) * 0.3f * (0.5f + 0.5f * Mathf.Sin(TAU * 18 * t)));
            Clips["meow"] = Make("meow", 0.5f, t =>
            {
                float f = 600 + 350 * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.5f));
                return Mathf.Sin(TAU * f * t) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.5f)) * 0.2f;
            });
        }

        static float Env(float t, float dur) => Mathf.Min(1f, t * 300f) * Mathf.Min(1f, (dur - t) * 300f);

        static AudioClip Make(string name, float dur, System.Func<float, float> f)
        {
            const int sr = 44100;
            int len = Mathf.Max(1, (int)(sr * dur));
            var d = new float[len];
            for (int i = 0; i < len; i++) d[i] = Mathf.Clamp(f(i / (float)sr), -1f, 1f);
            var c = AudioClip.Create(name, len, 1, sr, false);
            c.SetData(d, 0);
            return c;
        }

        public static void Play(string name, float volume = 1f)
        {
            if (_src != null && Clips.TryGetValue(name, out var c)) _src.PlayOneShot(c, volume);
        }
    }
}
