using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatMart
{
    public static class ItemFactory
    {
        static PhysicsMaterial _itemMat;
        static readonly Dictionary<string, Mesh> ColliderCache = new Dictionary<string, Mesh>();

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

        static Mesh ScaledCollider(Shape s, Vector3 size)
        {
            string key = s + "_" + size.ToString("F4");
            if (ColliderCache.TryGetValue(key, out var m) && m != null) return m;
            var src = ProcMesh.GetCollider(s);
            m = Object.Instantiate(src);
            var v = m.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = Vector3.Scale(v[i], size);
            m.vertices = v;
            m.RecalculateBounds();
            ColliderCache[key] = m;
            return m;
        }

        static Collider AddCollider(GameObject go, PartDef p)
        {
            switch (p.Shape)
            {
                case Shape.Box:
                    var bc = go.AddComponent<BoxCollider>();
                    bc.size = p.Size;
                    return bc;
                case Shape.Sphere when Mathf.Approximately(p.Size.x, p.Size.y) && Mathf.Approximately(p.Size.y, p.Size.z):
                    var sc = go.AddComponent<SphereCollider>();
                    sc.radius = p.Size.x / 2;
                    return sc;
                default:
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = ScaledCollider(p.Shape, p.Size);
                    mc.convex = true;
                    return mc;
            }
        }

        /// <summary>按定义生成商品。position 为包围盒中心。</summary>
        public static GroceryItem Create(ItemDefinition def, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(def.Name);
            root.transform.SetPositionAndRotation(position, rotation);

            var item = root.AddComponent<GroceryItem>();
            item.Def = def;
            var look = ItemLooks.Get(def);

            Vector3 center = def.RawBounds.center;
            Vector3 comSum = Vector3.zero;
            float volSum = 0;
            int solid = 0;

            foreach (var p in def.Parts)
            {
                if (p.Deco) continue;
                var go = new GameObject(p.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = p.Pos - center;
                go.transform.localRotation = Quaternion.Euler(p.Rot);

                var col = AddCollider(go, p);
                col.sharedMaterial = ItemMat;

                // 视觉
                var vis = new GameObject("外观");
                vis.transform.SetParent(go.transform, false);
                var mf = vis.AddComponent<MeshFilter>();
                mf.sharedMesh = look.Meshes[solid];
                var mr = vis.AddComponent<MeshRenderer>();
                mr.sharedMaterials = look.Mats[solid];
                mr.shadowCastingMode = ShadowCastingMode.On;

                var part = go.AddComponent<ItemPart>();
                part.Def = p;
                part.Owner = item;
                part.Col = col;
                part.Mf = mf;
                part.Mr = mr;
                item.Parts.Add(part);

                float vol = p.Size.x * p.Size.y * p.Size.z;
                comSum += (p.Pos - center) * vol;
                volSum += vol;
                solid++;
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

        /// <summary>只有外观、没有物理的商品（货架陈列用）。</summary>
        public static GameObject CreateDisplay(ItemDefinition def, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            var root = new GameObject(def.Name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = localRot;
            var look = ItemLooks.Get(def);
            Vector3 center = def.RawBounds.center;
            int solid = 0;
            foreach (var p in def.Parts)
            {
                if (p.Deco) continue;
                var go = new GameObject(p.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = p.Pos - center;
                go.transform.localRotation = Quaternion.Euler(p.Rot);
                go.AddComponent<MeshFilter>().sharedMesh = look.Meshes[solid];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = look.Mats[solid];
                mr.shadowCastingMode = ShadowCastingMode.Off;
                solid++;
            }
            return root;
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
