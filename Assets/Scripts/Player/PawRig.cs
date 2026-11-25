using UnityEngine;

namespace CatMart
{
    /// <summary>屏幕下方的两只猫爪：拿东西时右爪伸向商品，空手点击会挥一爪。</summary>
    public class PawRig : MonoBehaviour
    {
        public Transform Left, Right;
        public GroceryItem HoldTarget;
        public bool Walking;

        Vector3 _restL = new Vector3(-0.2f, -0.24f, 0.38f);
        Vector3 _restR = new Vector3(0.2f, -0.24f, 0.38f);
        float _swipe, _t;

        public static readonly Color Fur = U.Hex("#E8923A");
        public static readonly Color FurLight = U.Hex("#F7D9B5");
        public static readonly Color Pad = U.Hex("#F4A6B8");

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
            var root = new GameObject(side < 0 ? "左爪" : "右爪").transform;
            root.SetParent(parent, false);
            // 前臂向后延伸到屏幕外
            ProcMesh.Visual(root, Shape.Cylinder, new Vector3(0, -0.02f, -0.16f), new Vector3(0.075f, 0.3f, 0.075f), Fur, Quaternion.Euler(80, 0, 0), 0.05f);
            // 条纹
            for (int i = 0; i < 3; i++)
                ProcMesh.Visual(root, Shape.Cylinder, new Vector3(0, -0.01f - i * 0.0f, -0.07f - i * 0.07f), new Vector3(0.078f, 0.018f, 0.078f), U.Hex("#C06A22"), Quaternion.Euler(80, 0, 0), 0.05f);
            // 爪掌
            ProcMesh.Visual(root, Shape.Sphere, Vector3.zero, new Vector3(0.095f, 0.07f, 0.11f), FurLight, null, 0.05f);
            // 肉垫（下方）
            ProcMesh.Visual(root, Shape.Sphere, new Vector3(0, -0.03f, 0.0f), new Vector3(0.05f, 0.02f, 0.045f), Pad, null, 0.4f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * 0.022f;
                float z = 0.035f + (i == 1 || i == 2 ? 0.008f : 0f);
                ProcMesh.Visual(root, Shape.Sphere, new Vector3(x, -0.026f, z), new Vector3(0.018f, 0.012f, 0.018f), Pad, null, 0.4f);
                // 指头
                ProcMesh.Visual(root, Shape.Sphere, new Vector3(x, 0.005f, z + 0.012f), new Vector3(0.026f, 0.03f, 0.03f), FurLight, null, 0.05f);
            }
            root.localRotation = Quaternion.Euler(15, side * -8f, 0);
            return root;
        }

        public void Swipe() { if (_swipe <= 0) { _swipe = 0.35f; Sfx.Play("grab", 0.3f); } }

        void LateUpdate()
        {
            _t += Time.deltaTime * (Walking ? 9f : 2f);
            float bob = Mathf.Sin(_t) * (Walking ? 0.012f : 0.004f);
            var cam = transform.parent;

            // 左爪：待机
            Left.localPosition = Vector3.Lerp(Left.localPosition, _restL + new Vector3(0, bob, 0), Time.deltaTime * 10f);

            // 右爪：拿东西 / 挥爪 / 待机
            Vector3 targetR = _restR + new Vector3(0, -bob, 0);
            Quaternion rotR = Quaternion.Euler(15, 8, 0);
            if (HoldTarget != null && HoldTarget.gameObject.activeInHierarchy)
            {
                Vector3 top = HoldTarget.WorldCom + Vector3.up * HoldTarget.Def.Size.y * 0.5f;
                Vector3 local = cam.InverseTransformPoint(top);
                local.z = Mathf.Clamp(local.z, 0.3f, 1.3f);
                targetR = local + new Vector3(0.03f, 0.03f, -0.04f);
                rotR = Quaternion.Euler(35, 0, 0);
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
