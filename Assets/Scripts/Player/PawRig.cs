using UnityEngine;

namespace CatMart
{
    /// <summary>屏幕下方的两只猫爪：拿东西时右爪伸向商品，空手点击会挥一爪。</summary>
    public class PawRig : MonoBehaviour
    {
        public Transform Left, Right;
        public GroceryItem HoldTarget;
        public bool Walking;

        Vector3 _restL = new Vector3(-0.2f, -0.23f, 0.36f);
        Vector3 _restR = new Vector3(0.2f, -0.23f, 0.36f);
        float _swipe, _t;

        public static readonly Color Fur = U.Hex("#E8923A");
        public static readonly Color Sock = U.Hex("#FBF6EE");
        public static readonly Color Pad = U.Hex("#F4A0B4");

        static MB[] _mesh;

        static Texture2D Tabby => TexGen.Get("tabby", () => TexGen.Make(256, 256, (u, v) =>
        {
            float n = Noise.Fbm2(u * 4, v * 4, 3, 301, 4);
            float s = Mathf.Abs(Mathf.Sin((u * 7 + v * 2 + n * 1.6f) * Mathf.PI));
            float stripe = Mathf.SmoothStep(0.72f, 0.95f, s);
            float fine = Noise.Fbm2(u * 64, v * 10, 3, 302, 64) * 0.18f;
            float g = 1f - stripe * 0.38f + fine - 0.09f;
            return new Color(g, g, g, 1);
        }));

        static MB[] Mesh()
        {
            if (_mesh != null) return _mesh;
            var s = new Sdf { DefaultBlend = 0.022f };
            // 前臂（向后、向下延伸出屏幕）
            s.Capsule(new Vector3(0, -0.01f, -0.04f), new Vector3(0, -0.07f, -0.42f), 0.043f, 0.052f);
            // 手掌 + 脚趾（白袜子）
            s.Ellipsoid(new Vector3(0, 0, 0.01f), new Vector3(0.052f, 0.034f, 0.06f), 1, 0.03f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.024f;
                float z = 0.052f + (i == 1 || i == 2 ? 0.01f : 0f);
                s.Sphere(new Vector3(x, 0.006f, z), 0.019f, 1, 0.012f);
            }
            // 肉垫（粉）
            s.Ellipsoid(new Vector3(0, -0.03f, 0.0f), new Vector3(0.026f, 0.012f, 0.022f), 2, 0.006f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.024f;
                float z = 0.054f + (i == 1 || i == 2 ? 0.01f : 0f);
                s.Ellipsoid(new Vector3(x, -0.012f, z), new Vector3(0.009f, 0.007f, 0.009f), 2, 0.004f);
            }
            _mesh = s.Mesh(new Bounds(new Vector3(0, -0.03f, -0.17f), new Vector3(0.16f, 0.16f, 0.56f)), 0.0045f, 3, 18);
            return _mesh;
        }

        public static PawRig Build(Camera cam)
        {
            var rig = new GameObject("猫爪").AddComponent<PawRig>();
            rig.transform.SetParent(cam.transform, false);
            rig.Left = BuildPaw(rig.transform, -1);
            rig.Right = BuildPaw(rig.transform, 1);
            rig.Left.localPosition = rig._restL;
            rig.Right.localPosition = rig._restR;
            return rig;
        }

        static Transform BuildPaw(Transform parent, int side)
        {
            var regions = Mesh();
            var v = new VisualSet();
            v.Add(regions[0], Mats.Lit(Fur, 0.22f, 0, Tabby, TexGen.FurN, 0.8f, new Vector2(1, 1)));
            v.Add(regions[1], Mats.Lit(Sock, 0.22f, 0, TexGen.Fur, TexGen.FurN, 0.7f));
            v.Add(regions[2], Mats.Lit(Pad, 0.55f, 0));
            var go = v.Spawn(parent, side < 0 ? "左爪" : "右爪");
            go.transform.localRotation = Quaternion.Euler(15, side * -8f, 0);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        public void Swipe() { if (_swipe <= 0) { _swipe = 0.35f; Sfx.Play("grab", 0.3f); } }

        void LateUpdate()
        {
            _t += Time.deltaTime * (Walking ? 9f : 2f);
            float bob = Mathf.Sin(_t) * (Walking ? 0.012f : 0.004f);
            var cam = transform.parent;

            Left.localPosition = Vector3.Lerp(Left.localPosition, _restL + new Vector3(0, bob, 0), Time.deltaTime * 10f);

            Vector3 targetR = _restR + new Vector3(0, -bob, 0);
            Quaternion rotR = Quaternion.Euler(15, 8, 0);
            if (HoldTarget != null && HoldTarget.gameObject.activeInHierarchy)
            {
                Vector3 top = HoldTarget.WorldCom + Vector3.up * HoldTarget.Def.Size.y * 0.5f;
                Vector3 local = cam.InverseTransformPoint(top);
                local.z = Mathf.Clamp(local.z, 0.3f, 1.3f);
                targetR = local + new Vector3(0.02f, 0.035f, -0.03f);
                rotR = Quaternion.Euler(32, 0, 0);
            }
            else if (_swipe > 0)
            {
                _swipe -= Time.deltaTime;
                float k = Mathf.Sin((1f - _swipe / 0.35f) * Mathf.PI);
                targetR = _restR + new Vector3(-0.15f * k, 0.08f * k, 0.25f * k);
                rotR = Quaternion.Euler(15 - 30 * k, 8 - 40 * k, 0);
            }
            Right.localPosition = Vector3.Lerp(Right.localPosition, targetR, Time.deltaTime * 14f);
            Right.localRotation = Quaternion.Slerp(Right.localRotation, rotR, Time.deltaTime * 10f);
        }
    }
}
