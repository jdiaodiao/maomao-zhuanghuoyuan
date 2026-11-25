using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 森友会风后期：柔光（亮部微微发光）+ 暖色调色 + 轻微暗角。
    /// 内置渲染管线的 OnRenderImage 实现，无需额外 package。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CozyPost : MonoBehaviour
    {
        public float Threshold = 1.1f;
        public float Knee = 0.35f;
        public float Intensity = 0.22f;
        public float Saturation = 1.18f;
        public float Vignette = 0.45f;
        public Color Tint = new Color(1.0f, 0.985f, 0.96f);
        public Color ShadowLift = new Color(0.55f, 0.45f, 0.75f);

        Material _m;
        readonly RenderTexture[] _levels = new RenderTexture[6];

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nopost") >= 0) { Graphics.Blit(src, dst); return; }
            if (_m == null)
            {
                var tpl = Resources.Load<Material>("CatMart/CozyPostMat");
                if (tpl == null) { Graphics.Blit(src, dst); return; }
                _m = new Material(tpl);
            }
            _m.SetFloat("_Threshold", Threshold);
            _m.SetFloat("_Knee", Knee);
            _m.SetFloat("_Intensity", Intensity);
            _m.SetFloat("_Saturation", Saturation);
            _m.SetFloat("_Vignette", Vignette);
            _m.SetColor("_Tint", Tint);
            _m.SetColor("_ShadowLift", ShadowLift);

            int w = src.width / 2, h = src.height / 2;
            var fmt = src.format;
            int count = 0;
            _levels[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, _levels[0], _m, 0);
            count = 1;
            for (int i = 1; i < _levels.Length; i++)
            {
                w /= 2; h /= 2;
                if (w < 4 || h < 4) break;
                _levels[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(_levels[i - 1], _levels[i], _m, 1);
                count++;
            }
            for (int i = count - 2; i >= 0; i--)
                Graphics.Blit(_levels[i + 1], _levels[i], _m, 2);   // 逐级向上叠加

            _m.SetTexture("_BloomTex", _levels[0]);
            Graphics.Blit(src, dst, _m, 3);
            for (int i = 0; i < count; i++) { RenderTexture.ReleaseTemporary(_levels[i]); _levels[i] = null; }
        }
    }
}
