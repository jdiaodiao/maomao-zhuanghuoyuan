using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatMart
{
    /// <summary>
    /// 画风切换：森友会风（卡通、粉彩、圆润）/ 写实风。
    /// 启动时从 PlayerPrefs 读取；切换后清空所有缓存并重新加载场景。
    /// </summary>
    public static class ArtStyle
    {
        public enum Kind { Cozy, Realistic }
        const string Key = "CatMart.ArtStyle";

        static Kind? _current;
        public static Kind Current
        {
            get
            {
                if (!_current.HasValue) _current = (Kind)PlayerPrefs.GetInt(Key, (int)Kind.Cozy);
                return _current.Value;
            }
        }

        public static bool Cozy => Current == Kind.Cozy;
        public static string Name => Cozy ? "森友会风" : "写实风";

        public static void Toggle()
        {
            _current = Cozy ? Kind.Realistic : Kind.Cozy;
            PlayerPrefs.SetInt(Key, (int)_current.Value);
            PlayerPrefs.Save();
            ClearCaches();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        static void ClearCaches()
        {
            Mats.ClearCache();
            ItemLooks.ClearCache();
            BagFactory.ClearCache();
            CatBuilder.ClearCache();
            PawRig.ClearCache();
            Fonts.ClearCache();
            Shapes.ApplyStyle();
        }

        /// <summary>把颜色调成粉彩：稍降饱和、提亮、偏暖。</summary>
        public static Color Pastel(Color c)
        {
            if (!Cozy) return c;
            Color.RGBToHSV(c, out float h, out float s, out float v);
            s *= v < 0.25f ? 0.7f : 0.9f;
            v = Mathf.Lerp(v, 1f, v < 0.25f ? 0.2f : 0.1f);
            var o = Color.HSVToRGB(h, s, v);
            o = Color.Lerp(o, new Color(1f, 0.9f, 0.8f), 0.05f);
            o.a = c.a;
            return o;
        }
    }
}
