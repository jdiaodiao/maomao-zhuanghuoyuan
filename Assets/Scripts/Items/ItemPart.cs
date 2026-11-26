using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 商品部件：累计压力、判定压坏 / 扎坏 / 摔坏。
    /// - 压坏：上方/侧面其他商品的力（不管尖不尖）持续超过 Crush 0.2 秒
    /// - 扎坏：尖锐商品（尖锐度 ≥ 0.3）顶着软包装，力 ×(1 + 3×尖锐度) 持续超过 Crush × 0.3 达 0.2 秒；
    ///         罐头（Dent）、玻璃（Shatter）扎不坏
    /// - 摔坏：单次碰撞冲量超过 Impact
    /// </summary>
    public class ItemPart : MonoBehaviour
    {
        public PartDef Def;
        public GroceryItem Owner;
        public Collider Col;
        public MeshFilter Mf;
        public MeshRenderer Mr;
        public readonly List<MeshFilter> Decos = new List<MeshFilter>();
        public bool Broken;

        public Vector3 LastPoint;
        public Vector3 LastNormal = Vector3.up;
        public float LastSharp;

        float _acc, _smoothed, _over;
        float _pAcc, _pSmoothed, _pOver;

        /// <summary>当前持续承压（平滑后，N）。</summary>
        public float Load => _smoothed;
        /// <summary>当前被尖锐物顶着的等效压力（已乘尖锐放大，N）。</summary>
        public float PierceLoad => _pSmoothed;

        /// <summary>软包装（袋、盒、果蔬、蛋…）会被扎破；罐头、玻璃扎不坏。</summary>
        public bool Pierceable
        {
            get
            {
                var s = Def.Style ?? Owner.Def.Style;
                return s != DamageStyle.Dent && s != DamageStyle.Shatter;
            }
        }

        /// <summary>承压 / 被扎 占上限的比例（取较大的），用于 UI。</summary>
        public float LoadRatio => Mathf.Max(CrushRatio, PierceRatio);
        public float CrushRatio => Def.Crush > 0 ? _smoothed / Def.Crush : 0;
        public float PierceRatio => Def.Crush > 0 ? _pSmoothed / (Def.Crush * PierceFactor) : 0;
        /// <summary>软包装被尖东西顶着时，超过 承压上限 × 这个比例 就会被扎破（比压坏更容易）。</summary>
        public const float PierceFactor = 0.3f;

        public float HalfHeightWorld => Col != null ? Col.bounds.extents.y : 0.05f;

        public void AddLoad(float f) { _acc += f; }
        public void AddPierce(float f) { _pAcc += f; }

        public void ResetLoad() { _acc = 0; _smoothed = 0; _over = 0; _pAcc = 0; _pSmoothed = 0; _pOver = 0; }

        public void AddImpact(float impulse, bool sharp)
        {
            if (Broken || Def.Impact <= 0) return;
            if (impulse > Def.Impact)
                Owner.Damage(this, sharp && Pierceable ? DamageCause.Pierce : DamageCause.Impact);
        }

        public void Tick(float dt)
        {
            if (Broken) return;
            // 刚体睡着后物理引擎不再报告接触力：保持睡着前的压力（东西还压在上面），不要让它衰减成 0
            bool asleep = Owner.Rb != null && Owner.Rb.IsSleeping();
            if (!asleep) _smoothed = Mathf.Lerp(_smoothed, _acc, 0.15f);
            if (!asleep) _pSmoothed = Mathf.Lerp(_pSmoothed, _pAcc, 0.15f);
            _acc = 0;
            _pAcc = 0;
            if (Def.Crush <= 0) return;

            if (_smoothed > Def.Crush) { _over += dt; if (_over > 0.2f) { Owner.Damage(this, DamageCause.Crush); return; } }
            else _over = Mathf.Max(0, _over - dt);

            if (_pSmoothed > Def.Crush * PierceFactor) { _pOver += dt; if (_pOver > 0.2f) Owner.Damage(this, DamageCause.Pierce); }
            else _pOver = Mathf.Max(0, _pOver - dt);
        }
    }
}
