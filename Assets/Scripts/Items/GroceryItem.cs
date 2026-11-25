using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 一件商品。负责把碰撞力分配到各个部件：
    /// - 来自其他商品、且接触点不在本部件下方的力 → 持续承压
    /// - 任何碰撞的冲量 → 摔/撞
    /// - 对方部件尖锐度会放大压力 (1 + 3×Sharp)
    /// </summary>
    public class GroceryItem : MonoBehaviour
    {
        public const float SharpMultiplier = 3f;

        public ItemDefinition Def;
        public Rigidbody Rb;
        public readonly List<ItemPart> Parts = new List<ItemPart>();

        public bool IsDamaged { get; private set; }
        public bool IsDestroyed { get; private set; }
        public DamageCause LastCause { get; private set; }
        public bool Held;
        public bool OnBelt;
        public int Seed;

        float _grace = 0.35f;      // 刚生成时忽略伤害（防止穿插冲量）
        readonly Dictionary<Collider, ItemPart> _lookup = new Dictionary<Collider, ItemPart>();

        public static readonly List<GroceryItem> Active = new List<GroceryItem>();

        public void Init()
        {
            foreach (var p in Parts) _lookup[p.Col] = p;
            Seed = Random.Range(0, 1 << 20);
        }

        void OnEnable() { Active.Add(this); }
        void OnDisable() { Active.Remove(this); }

        public ItemPart PartFor(Collider c) => c != null && _lookup.TryGetValue(c, out var p) ? p : null;

        public Vector3 WorldCom => Rb != null ? Rb.worldCenterOfMass : transform.position;

        /// <summary>手里拿着时给部件的近似最大尺寸。</summary>
        public float Radius => Def.Size.magnitude * 0.5f;

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (_grace > 0) { _grace -= dt; foreach (var p in Parts) p.ResetLoad(); return; }
            foreach (var p in Parts) p.Tick(dt);
        }

        void OnCollisionEnter(Collision c) => HandleCollision(c, true);
        void OnCollisionStay(Collision c) => HandleCollision(c, false);

        /// <summary>最近一次"下方有东西托住"的时间（轻放用来判断已经落到实处）。</summary>
        public float LastSupportTime = -1f;

        void HandleCollision(Collision c, bool enter)
        {
            int n = c.contactCount;
            if (n == 0) return;
            float comY = WorldCom.y;
            for (int i = 0; i < n; i++)
                if (c.GetContact(i).point.y < comY - 0.005f) { LastSupportTime = Time.time; break; }
            if (_grace > 0 || IsDestroyed) return;
            float dt = Time.fixedDeltaTime;
            float perContact = c.impulse.magnitude / n;
            if (perContact <= 0) return;

            GroceryItem other = c.rigidbody != null ? c.rigidbody.GetComponent<GroceryItem>() : null;

            // 撞击冲量：取物理引擎冲量与 "等效质量×相对速度" 的较大值（首帧接触时引擎冲量常被分摊）
            float impactTotal = 0;
            if (enter && !Held)
            {
                float m = Rb.mass;
                if (c.rigidbody != null && !c.rigidbody.isKinematic) m = m * c.rigidbody.mass / (m + c.rigidbody.mass);
                impactTotal = Mathf.Max(c.impulse.magnitude, m * c.relativeVelocity.magnitude);
            }
            ItemPart impactPart = null;
            float impactSharp = 0;

            for (int i = 0; i < n; i++)
            {
                var cp = c.GetContact(i);
                var part = PartFor(cp.thisCollider);
                if (part == null || part.Broken) continue;

                float otherSharp = 0;
                if (other != null)
                {
                    var op = other.PartFor(cp.otherCollider);
                    if (op != null && !op.Broken) otherSharp = op.Def.Sharp;
                }
                float sharpMul = 1f + SharpMultiplier * otherSharp;

                part.LastPoint = cp.point;
                part.LastNormal = cp.normal;
                part.LastSharp = otherSharp;

                // 撞击：整次碰撞的冲量算在一个部件上（手里拿着的不算）
                if (impactPart == null || otherSharp > impactSharp) { impactPart = part; impactSharp = otherSharp; }

                // 承压：只算其他商品，且接触点不在本部件下方（下方是支撑力）
                if (other != null)
                {
                    float below = part.transform.position.y - part.HalfHeightWorld * 0.35f;
                    if (cp.point.y > below) part.AddLoad(perContact / dt * sharpMul);
                }
            }

            if (impactTotal > 0 && impactPart != null)
                impactPart.AddImpact(impactTotal * (1f + SharpMultiplier * impactSharp));
        }

        /// <summary>部件损坏。整件商品只计一次赔偿。</summary>
        public void Damage(ItemPart part, DamageCause cause)
        {
            if (part.Broken || IsDestroyed) return;
            part.Broken = true;
            LastCause = cause;

            Vector3 dir = -part.LastNormal;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.down;
            DamageVisuals.Apply(this, part, part.LastPoint, dir);

            if (!IsDamaged)
            {
                IsDamaged = true;
                string what = cause == DamageCause.Impact ? "摔坏了!" : cause == DamageCause.Pierce ? "被扎破了!" : "压坏了!";
                FloatText.Spawn(WorldCom + Vector3.up * 0.12f, $"{Def.Name} {what}\n-{U.Money(Def.Price)}", new Color(1f, 0.25f, 0.2f), 0.035f);
                if (GameManager.I != null) GameManager.I.OnItemDamaged(this);
            }

            // 所有实体部件都碎了 → 商品消失（只剩碎片和水渍）
            bool anyLeft = false;
            foreach (var p in Parts) if (p.Col != null && p.Col.enabled) { anyLeft = true; break; }
            if (!anyLeft)
            {
                IsDestroyed = true;
                gameObject.SetActive(false);
            }
        }
    }
}
