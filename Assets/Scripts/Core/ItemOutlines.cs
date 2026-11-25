using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatMart
{
    /// <summary>
    /// 商品属性光边：每件商品外侧一圈细细的光边，颜色 = 它的属性（白 = 普通；
    /// 怕压/易碎/尖锐/大件/不能倒 各一种颜色，和悬停说明里的字一个颜色）。
    /// 同时有好几种属性的商品，光边在这几种颜色之间慢慢轮换。
    /// 关卡可以通过 GameManager.TraitHints 关掉（靠记忆的关卡）。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ItemOutlines : MonoBehaviour
    {
        public float Width = 1.5f;        // 1080p 下内圈的像素宽度
        public float Strength = 1f;

        static readonly int ColorId = Shader.PropertyToID("_OutlineColor");

        Camera _cam;
        CommandBuffer _cmd;
        Material _m;
        bool _drew;
        static readonly bool Debug = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-outlineDebug") >= 0;
        RenderTexture _mask;

        void OnEnable()
        {
            _cam = GetComponent<Camera>();
            _cam.depthTextureMode |= DepthTextureMode.Depth;
            _cmd = new CommandBuffer { name = "CatMart 商品光边" };
            _cam.AddCommandBuffer(CameraEvent.BeforeImageEffects, _cmd);
        }

        void OnDisable()
        {
            if (_cam != null && _cmd != null) _cam.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, _cmd);
            _cmd?.Release();
            _cmd = null;
            if (_mask != null) { _mask.Release(); Destroy(_mask); _mask = null; }
        }

        bool Enabled => GameManager.I == null || GameManager.I.TraitHints;

        void OnPreRender()
        {
            _cmd.Clear();
            _drew = false;
            if (!Enabled || Mat == null || GroceryItem.Active.Count == 0) return;

            int w = _cam.pixelWidth, h = _cam.pixelHeight;
            if (_mask == null || _mask.width != w || _mask.height != h)
            {
                if (_mask != null) { _mask.Release(); Destroy(_mask); }
                _mask = new RenderTexture(w, h, 24, RenderTextureFormat.ARGBHalf) { filterMode = FilterMode.Point, name = "OutlineMask" };
            }
            _cmd.SetRenderTarget(_mask);
            _cmd.ClearRenderTarget(true, true, Color.clear);
            foreach (var it in GroceryItem.Active)
            {
                if (it == null || it.IsDestroyed) continue;
                _cmd.SetGlobalColor(ColorId, OutlineColor(it));
                foreach (var p in it.Parts)
                {
                    var mr = p.Mr;
                    if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                    int subs = p.Mf != null && p.Mf.sharedMesh != null ? p.Mf.sharedMesh.subMeshCount : 1;
                    for (int s = 0; s < subs; s++) _cmd.DrawRenderer(mr, Mat, s, 0);
                }
            }
            _drew = true;
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!_drew || Mat == null) { Graphics.Blit(src, dst); return; }
            Mat.SetFloat("_Width", Width * Mathf.Max(1f, src.height / 1080f));
            Mat.SetFloat("_Strength", Strength);
            Mat.SetTexture("_MaskTex", _mask);
            Mat.SetFloat("_Debug", Debug ? 1 : 0);
            Mat.SetFloat("_Boost", ArtStyle.Cozy ? 1.35f : 1.1f);
            Graphics.Blit(src, dst, Mat, 1);
        }

        Material Mat
        {
            get
            {
                if (_m == null)
                {
                    var tpl = Resources.Load<Material>("CatMart/ItemOutlineMat");
                    if (tpl != null) _m = new Material(tpl);
                }
                return _m;
            }
        }

        // ------------------------------------------------------------ 颜色
        static readonly List<Color> _tmp = new List<Color>();

        public static Color OutlineColor(GroceryItem it)
        {
            if (it.IsDamaged) return new Color(0.45f, 0.42f, 0.42f);
            // 不能倒的东西正在倾斜 → 红色闪烁提醒
            if (it.Def.Upright && !it.Tipped && it.Tilt > GroceryItem.TipWarnDeg)
                return Color.Lerp(ItemTraits.Color(ItemTrait.Upright), new Color(1f, 0.25f, 0.2f), 0.5f + 0.5f * Mathf.Sin(Time.time * 18f));
            _tmp.Clear();
            foreach (var t in ItemTraits.Of(it.Def)) _tmp.Add(ItemTraits.Color(t));
            if (_tmp.Count == 0) return ItemTraits.NormalColor;
            if (_tmp.Count == 1) return _tmp[0];
            // 每种颜色停 1.1 秒，最后 0.35 秒渐变到下一种
            const float hold = 1.1f, fade = 0.35f;
            float t0 = Time.time / hold;
            int i = Mathf.FloorToInt(t0) % _tmp.Count;
            float f = Mathf.InverseLerp(1f - fade / hold, 1f, t0 - Mathf.Floor(t0));
            return Color.Lerp(_tmp[i], _tmp[(i + 1) % _tmp.Count], Mathf.SmoothStep(0, 1, f));
        }
    }
}
