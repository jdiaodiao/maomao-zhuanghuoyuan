using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    public enum BagMaterial { Plastic, Paper }
    /// <summary>Square = 方底袋（只有塑料的）：底面是正方形，蛋糕、甜甜圈盒这种宽而扁、又不能倒的东西用。</summary>
    public enum BagSize { Medium, Large, Square }

    public struct BagSpec
    {
        public BagMaterial Material;
        public BagSize Size;
        public Vector3 Inner;       // 宽(x) 高(y) 深(z)，米
        public float CapacityKg;
        public float MinSharp;      // 尖锐度达到此值才可能刺破
        public float PunctureForce; // 力×尖锐度 超过此值刺破
        /// <summary>袋壁弹性：碰撞墙比名义尺寸每侧多让出的距离（米）。塑料袋 1.5cm，纸袋 0。</summary>
        public float Stretch => Material == BagMaterial.Plastic ? 0.015f : 0f;

        public string MaterialName => Material == BagMaterial.Paper ? "纸袋" : "塑料袋";
        public string SizeName => Size == BagSize.Square ? "方底" : Size == BagSize.Medium ? "中号" : "大号";
        public string SizeLetter => Size == BagSize.Square ? "方" : Size == BagSize.Medium ? "M" : "L";
        public string Name => SizeName + MaterialName;
        public float Volume => Inner.x * Inner.y * Inner.z;

        /// <summary>每种材质有哪些尺寸：塑料 中/大/方底，纸袋 中/大。</summary>
        public static BagSize[] SizesFor(BagMaterial m) =>
            m == BagMaterial.Plastic ? new[] { BagSize.Medium, BagSize.Large, BagSize.Square } : new[] { BagSize.Medium, BagSize.Large };

        public static readonly BagSize[] AllSizes = { BagSize.Large, BagSize.Medium, BagSize.Square };

        public static BagSpec Get(BagMaterial m, BagSize s)
        {
            var spec = new BagSpec { Material = m, Size = s };
            if (m == BagMaterial.Paper)
            {
                spec.MinSharp = 0.8f; spec.PunctureForce = 40f;
                switch (s)
                {
                    case BagSize.Medium: spec.Inner = U.Cm(28, 30, 17); spec.CapacityKg = 9; break;
                    default: spec.Inner = U.Cm(34, 40, 22); spec.CapacityKg = 12; break;
                }
            }
            else
            {
                spec.MinSharp = 0.4f; spec.PunctureForce = 6f;
                switch (s)
                {
                    case BagSize.Medium: spec.Inner = U.Cm(30, 34, 18); spec.CapacityKg = 7; break;
                    case BagSize.Square: spec.Inner = U.Cm(30, 28, 30); spec.CapacityKg = 8; break;
                    default: spec.Inner = U.Cm(36, 40, 22); spec.CapacityKg = 10; break;
                }
            }
            return spec;
        }

        /// <summary>
        /// 这件商品能不能好好放进这种袋子：
        /// 不能倒的东西必须立着放得下（底面放得进袋底，高度不超过袋子太多）；
        /// 其他东西只要有一种摆法：两条边放得进袋底，第三条边（竖着）不超过袋高的 1.5 倍（重心不高出袋口）。
        /// </summary>
        public bool Fits(ItemDefinition d)
        {
            Vector3 s = d.Size;
            float bx = Inner.x + Stretch * 2, bz = Inner.z + Stretch * 2;
            bool Foot(float a, float b) => (a <= bx && b <= bz) || (a <= bz && b <= bx);
            if (d.Upright) return Foot(s.x, s.z) && s.y <= Inner.y * 1.15f;
            float hMax = Inner.y * 1.5f;
            return (Foot(s.x, s.z) && s.y <= hMax) || (Foot(s.x, s.y) && s.z <= hMax) || (Foot(s.y, s.z) && s.x <= hMax);
        }
    }

    /// <summary>
    /// 放在打包台上的袋子（运动学刚体，不会被撞走）。
    /// 根节点在袋底中心。负责：统计袋内商品、重心、刺破判定。
    /// </summary>
    public class Bag : MonoBehaviour
    {
        public BagSpec Spec;
        public int Slot;
        public bool Torn { get; private set; }
        public Transform ComMarker;
        public static bool ShowMarkers = true;

        float _pierceAcc, _pierceSmoothed, _pierceOver;

        public static readonly List<Bag> Active = new List<Bag>();
        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        /// <summary>商品重心是否在袋子内部空间。</summary>
        public bool Contains(GroceryItem item)
        {
            if (item == null || item.IsDestroyed || !item.gameObject.activeInHierarchy) return false;
            Vector3 p = transform.InverseTransformPoint(item.WorldCom);
            Vector3 h = Spec.Inner * 0.5f;
            float m = 0.01f + Spec.Stretch;
            return Mathf.Abs(p.x) <= h.x + m && Mathf.Abs(p.z) <= h.z + m && p.y >= -0.02f && p.y <= Spec.Inner.y + 0.18f;
        }

        public List<GroceryItem> Contents()
        {
            var list = new List<GroceryItem>();
            foreach (var it in GroceryItem.Active) if (!it.Held && Contains(it)) list.Add(it);
            return list;
        }

        public struct Stats
        {
            public int Count;
            public float Mass;
            public Vector3 LocalCom;
            public float Imbalance; // 0 = 完美居中，1 = 重心在袋边
            public bool Overweight;
            public int Unstable;    // 质心高出袋口的件数
        }

        /// <summary>商品质心是否高出袋口（放得不稳）。</summary>
        public bool AboveRim(GroceryItem it) => transform.InverseTransformPoint(it.WorldCom).y > Spec.Inner.y;

        public Stats Compute()
        {
            var s = new Stats();
            Vector3 sum = Vector3.zero;
            foreach (var it in Contents())
            {
                s.Count++;
                s.Mass += it.Def.Mass;
                Vector3 local = transform.InverseTransformPoint(it.WorldCom);
                sum += local * it.Def.Mass;
                if (local.y > Spec.Inner.y) s.Unstable++;
            }
            if (s.Mass > 0)
            {
                s.LocalCom = sum / s.Mass;
                Vector3 h = Spec.Inner * 0.5f;
                s.Imbalance = Mathf.Max(Mathf.Abs(s.LocalCom.x) / h.x, Mathf.Abs(s.LocalCom.z) / h.z);
            }
            s.Overweight = s.Mass > Spec.CapacityKg;
            return s;
        }

        void Update()
        {
            if (ComMarker == null) return;
            var s = Compute();
            bool show = ShowMarkers && s.Count > 0;
            ComMarker.gameObject.SetActive(show);
            if (!show) return;
            ComMarker.localPosition = new Vector3(s.LocalCom.x, 0.004f, s.LocalCom.z);
            Color c = s.Imbalance <= 0.15f ? new Color(0.2f, 0.85f, 0.3f) : s.Imbalance <= 0.35f ? new Color(1f, 0.8f, 0.1f) : new Color(1f, 0.25f, 0.2f);
            ComMarker.GetComponent<MeshRenderer>().sharedMaterial = Mats.Get(c, 0.6f);
        }

        void FixedUpdate()
        {
            _pierceSmoothed = Mathf.Lerp(_pierceSmoothed, _pierceAcc, 0.3f);
            _pierceAcc = 0;
            if (!Torn && _pierceSmoothed > Spec.PunctureForce)
            {
                _pierceOver += Time.fixedDeltaTime;
                if (_pierceOver > 0.1f) Tear(_lastPierce);
            }
            else _pierceOver = 0;
        }

        Vector3 _lastPierce;

        void OnCollisionStay(Collision c) => CheckPierce(c);
        void OnCollisionEnter(Collision c) => CheckPierce(c);

        void CheckPierce(Collision c)
        {
            if (Torn || c.rigidbody == null) return;
            var item = c.rigidbody.GetComponent<GroceryItem>();
            if (item == null) return;
            int n = c.contactCount;
            if (n == 0) return;
            float perContact = c.impulse.magnitude / n / Time.fixedDeltaTime;
            for (int i = 0; i < n; i++)
            {
                var cp = c.GetContact(i);
                var part = item.PartFor(cp.otherCollider);
                if (part == null || part.Def.Sharp < Spec.MinSharp) continue;
                float v = perContact * part.Def.Sharp;
                if (v > _pierceAcc) { _pierceAcc = v; _lastPierce = cp.point; }
            }
        }

        public void Tear(Vector3 at)
        {
            if (Torn) return;
            Torn = true;
            Sfx.Play("rip");
            FloatText.Spawn(at + Vector3.up * 0.1f, $"{Spec.Name}被刺破了!", new Color(1f, 0.5f, 0.1f), 0.035f);
            // 破洞：在最近的袋壁上贴一块深色
            Vector3 local = transform.InverseTransformPoint(at);
            var hole = ProcMesh.Visual(transform, Shape.Sphere, local, new Vector3(0.05f, 0.04f, 0.05f), new Color(0.1f, 0.1f, 0.1f), null, 0f, "破洞");
            hole.transform.localScale = new Vector3(0.06f, 0.045f, 0.06f);
        }
    }

    /// <summary>袋架上的一叠袋子，点一下取一个。</summary>
    public class BagRackButton : MonoBehaviour
    {
        public BagSpec Spec;
    }

    /// <summary>完成订单的猫铃。</summary>
    public class FinishBell : MonoBehaviour { }
}
