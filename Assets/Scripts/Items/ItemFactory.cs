using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    public static class ItemFactory
    {
        static PhysicsMaterial _itemMat;

        static PhysicsMaterial ItemMat
        {
            get
            {
                if (_itemMat == null)
                    _itemMat = new PhysicsMaterial("Item")
                    {
                        dynamicFriction = 0.55f, staticFriction = 0.7f, bounciness = 0.02f,
                        frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum
                    };
                return _itemMat;
            }
        }

        /// <summary>按定义生成商品。position 为包围盒中心。</summary>
        public static GroceryItem Create(ItemDefinition def, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(def.Name);
            root.transform.SetPositionAndRotation(position, rotation);

            var item = root.AddComponent<GroceryItem>();
            item.Def = def;

            Vector3 center = def.RawBounds.center;
            ItemPart host = null;
            Vector3 comSum = Vector3.zero;
            float volSum = 0;

            foreach (var p in def.Parts)
            {
                var go = new GameObject(p.Deco ? "装饰" : p.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = p.Pos - center;
                go.transform.localRotation = Quaternion.Euler(p.Rot);
                go.transform.localScale = p.Size;
                go.AddComponent<MeshFilter>().sharedMesh = ProcMesh.Get(p.Shape);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.Get(p.Color, p.Smooth);

                if (p.Deco)
                {
                    if (host != null) host.Decos.Add(go.GetComponent<MeshFilter>());
                    continue;
                }

                var col = ProcMesh.AddCollider(go, p.Shape, p.Size);
                col.sharedMaterial = ItemMat;
                var part = go.AddComponent<ItemPart>();
                part.Def = p;
                part.Owner = item;
                part.Col = col;
                part.Mf = go.GetComponent<MeshFilter>();
                part.Mr = mr;
                item.Parts.Add(part);
                host = part;

                float vol = p.Size.x * p.Size.y * p.Size.z;
                comSum += (p.Pos - center) * vol;
                volSum += vol;
            }

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = def.Mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.2f;
            rb.maxDepenetrationVelocity = 0.6f;
            rb.centerOfMass = (volSum > 0 ? comSum / volSum : Vector3.zero) + def.ComOffset;
            item.Rb = rb;
            item.Init();
            return item;
        }

        /// <summary>按权重随机抽取商品。</summary>
        public static ItemDefinition RandomDef(System.Random rng, System.Func<ItemDefinition, bool> filter = null)
        {
            float total = 0;
            var list = new List<ItemDefinition>();
            foreach (var d in ItemCatalog.All)
                if (filter == null || filter(d)) { list.Add(d); total += d.Weight; }
            float r = (float)rng.NextDouble() * total;
            foreach (var d in list)
            {
                r -= d.Weight;
                if (r <= 0) return d;
            }
            return list[list.Count - 1];
        }
    }
}
