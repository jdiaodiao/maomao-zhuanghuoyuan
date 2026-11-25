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
            var res = Resources.Load<Material>("CatMart/EmissiveMat");
            var m = res != null ? new Material(res) { color = c } : New(c, 0.5f);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }
    }

    /// <summary>中文动态字体（系统字体）。</summary>
    public static class Fonts
    {
        static Font _main;
        public static Font Main
        {
            get
            {
                if (_main == null)
                {
                    _main = Font.CreateDynamicFontFromOSFont(new[]
                    {
                        "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "PingFang SC",
                        "Hiragino Sans GB", "Noto Sans CJK SC", "Arial"
                    }, 64);
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
            tm.characterSize = height / 64f * 1.4f;
            tm.anchor = anchor;
            tm.alignment = anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.UpperLeft || anchor == TextAnchor.LowerLeft
                ? TextAlignment.Left : TextAlignment.Center;
            tm.color = color;
            tm.text = text;
            go.GetComponent<MeshRenderer>().sharedMaterial = Fonts.Main.material;
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
