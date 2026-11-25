using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>商品部件：累计压力、判定压坏/摔坏。</summary>
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

        /// <summary>当前持续承压（平滑后，N）。</summary>
        public float Load => _smoothed;

        /// <summary>承压占上限的比例，用于 UI。</summary>
        public float LoadRatio => Def.Crush > 0 ? _smoothed / Def.Crush : 0;

        public float HalfHeightWorld => Col != null ? Col.bounds.extents.y : 0.05f;

        public void AddLoad(float f) { _acc += f; }

        public void ResetLoad() { _acc = 0; _smoothed = 0; _over = 0; }

        public void AddImpact(float impulse)
        {
            if (Broken || Def.Impact <= 0) return;
            if (impulse > Def.Impact)
                Owner.Damage(this, LastSharp > 0.4f ? DamageCause.Pierce : DamageCause.Impact);
        }

        public void Tick(float dt)
        {
            if (Broken) return;
            _smoothed = Mathf.Lerp(_smoothed, _acc, 0.15f);
            _acc = 0;
            if (Def.Crush > 0 && _smoothed > Def.Crush)
            {
                _over += dt;
                if (_over > 0.2f) Owner.Damage(this, LastSharp > 0.4f ? DamageCause.Pierce : DamageCause.Crush);
            }
            else _over = Mathf.Max(0, _over - dt);
        }
    }
}
