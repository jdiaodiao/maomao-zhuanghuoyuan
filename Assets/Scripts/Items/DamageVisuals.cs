using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 损坏后的模型：在商品局部空间对部件（及其装饰）网格做形变，变色，
    /// 碎裂时生成碎片，漏液时生成水渍。
    /// </summary>
    public static class DamageVisuals
    {
        /// <summary>碎片、水渍等，换顾客时统一清理。</summary>
        public static readonly List<GameObject> Debris = new List<GameObject>();

        public static void ClearDebris()
        {
            foreach (var d in Debris) U.DestroySafe(d);
            Debris.Clear();
        }

        public static void Apply(GroceryItem item, ItemPart part, Vector3 worldPoint, Vector3 worldDir)
        {
            var style = part.Def.Style ?? item.Def.Style;
            var root = item.transform;
            int seed = item.Seed & 1023;

            var filters = new List<MeshFilter> { part.Mf };
            filters.AddRange(part.Decos);

            switch (style)
            {
                case DamageStyle.Shatter:
                    Shatter(item, part, filters);
                    Sfx.Play("glass", 0.9f);
                    return;
                case DamageStyle.None:
                    return;
            }

            // 计算宿主部件在商品局部空间中的包围盒
            Bounds hb = RootSpaceBounds(root, part.Mf);
            Vector3 hitN = Normalize(root.InverseTransformPoint(worldPoint), hb);
            Vector3 dirRoot = root.InverseTransformDirection(worldDir);
            Vector3 dirN = new Vector3(dirRoot.x / Mathf.Max(hb.extents.x, 1e-4f), dirRoot.y / Mathf.Max(hb.extents.y, 1e-4f),
                dirRoot.z / Mathf.Max(hb.extents.z, 1e-4f)).normalized;
            int axis = DominantAxis(dirRoot);

            foreach (var mf in filters)
            {
                if (mf == null) continue;
                var mesh = Object.Instantiate(mf.sharedMesh);
                var verts = mesh.vertices;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 pr = root.InverseTransformPoint(mf.transform.TransformPoint(verts[i]));
                    Vector3 q = Normalize(pr, hb);
                    Vector3 q0 = q;
                    q = DeformNormalized(q, style, hitN, dirN, axis, seed);
                    // 损坏只会让商品往里皱/凹/压扁，绝不鼓出原来的包围盒（否则会穿出袋子等容器）
                    for (int a = 0; a < 3; a++)
                    {
                        float lim = Mathf.Max(1f, Mathf.Abs(q0[a]));
                        q[a] = Mathf.Clamp(q[a], -lim, lim);
                    }
                    pr = Denormalize(q, hb);
                    verts[i] = mf.transform.InverseTransformPoint(root.TransformPoint(pr));
                }
                mesh.vertices = verts;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mf.sharedMesh = mesh;

                var mr = mf.GetComponent<MeshRenderer>();
                var mats = mr.sharedMaterials;
                for (int k = 0; k < mats.Length; k++) mats[k] = DamagedMat(mats[k], style);
                mr.sharedMaterials = mats;
            }

            switch (style)
            {
                case DamageStyle.Crumple: Sfx.Play("crunch"); break;
                case DamageStyle.Dent: Sfx.Play("thud"); break;
                case DamageStyle.Leak:
                    Sfx.Play("squish");
                    Puddle(worldPoint, item.Def.LiquidColor, 0.12f + item.Def.Mass * 0.03f);
                    break;
                default: Sfx.Play("squish"); break;
            }
        }

        static Vector3 DeformNormalized(Vector3 q, DamageStyle style, Vector3 hit, Vector3 dir, int axis, int seed)
        {
            switch (style)
            {
                case DamageStyle.Crumple:
                {
                    q[axis] *= 0.62f;
                    q += new Vector3(Noise(q, seed) - 0.5f, Noise(q, seed + 31) - 0.5f, Noise(q, seed + 77) - 0.5f) * 0.32f;
                    return q;
                }
                case DamageStyle.Squish:
                {
                    q[axis] *= 0.5f;
                    for (int a = 0; a < 3; a++) if (a != axis) q[a] *= 1.18f;
                    q += new Vector3(Noise(q, seed) - 0.5f, 0, Noise(q, seed + 9) - 0.5f) * 0.12f;
                    return q;
                }
                case DamageStyle.Dent:
                case DamageStyle.Leak:
                {
                    float w = U.Smooth(1.0f, 0f, Vector3.Distance(q, hit));
                    q += dir * 0.5f * w;
                    q += (new Vector3(Noise(q, seed), Noise(q, seed + 3), Noise(q, seed + 5)) - Vector3.one * 0.5f) * 0.06f;
                    return q;
                }
                case DamageStyle.Bruise:
                {
                    float w = U.Smooth(0.8f, 0f, Vector3.Distance(q, hit));
                    q += dir * 0.25f * w;
                    return q;
                }
            }
            return q;
        }

        static float Noise(Vector3 q, int seed) =>
            Mathf.PerlinNoise(q.x * 2.3f + q.z * 1.7f + seed * 0.13f, q.y * 2.9f - q.z * 1.1f + seed * 0.07f);

        static readonly Dictionary<(Material, DamageStyle), Material> DamagedCache = new Dictionary<(Material, DamageStyle), Material>();

        /// <summary>损坏后的材质：保留贴图，颜色变暗/变褐，光泽降低。</summary>
        static Material DamagedMat(Material m, DamageStyle s)
        {
            if (m == null) return null;
            if (DamagedCache.TryGetValue((m, s), out var d) && d != null) return d;
            d = new Material(m);
            Color c = DamagedColor(m.color, s);
            c.a = m.color.a;
            d.color = c;
            if (d.HasProperty("_Glossiness")) d.SetFloat("_Glossiness", d.GetFloat("_Glossiness") * 0.6f);
            DamagedCache[(m, s)] = d;
            return d;
        }

        static Color DamagedColor(Color c, DamageStyle s)
        {
            switch (s)
            {
                case DamageStyle.Squish: return Color.Lerp(c, new Color(0.42f, 0.28f, 0.18f), 0.4f);
                case DamageStyle.Bruise: return Color.Lerp(c, new Color(0.35f, 0.22f, 0.12f), 0.35f);
                case DamageStyle.Leak: return c * 0.82f;
                default: return c * 0.78f;
            }
        }

        static void Shatter(GroceryItem item, ItemPart part, List<MeshFilter> filters)
        {
            Color c = part.Mr.sharedMaterial.color;
            Vector3 center = part.Col.bounds.center;
            Vector3 ext = part.Col.bounds.extents;
            foreach (var mf in filters) if (mf != null) mf.GetComponent<MeshRenderer>().enabled = false;
            part.Col.enabled = false;

            var rng = new System.Random(item.Seed);
            int count = 10;
            for (int i = 0; i < count; i++)
            {
                Vector3 off = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
                float s = Mathf.Max(0.008f, ext.magnitude * (0.12f + (float)rng.NextDouble() * 0.15f));
                var shard = ProcMesh.Visual(null, Shape.Box, center + Vector3.Scale(off, ext), new Vector3(s, s * 0.35f, s * 0.8f),
                    c, Random.rotation, 0.9f, "碎片");
                var col = shard.AddComponent<BoxCollider>();
                col.sharedMaterial = null;
                var rb = shard.AddComponent<Rigidbody>();
                rb.mass = 0.01f;
                rb.linearVelocity = off * 0.8f + Vector3.up * 0.3f;
                Debris.Add(shard);
            }
            Puddle(center, item.Def.LiquidColor, 0.15f + item.Def.Mass * 0.04f);
        }

        public static void Puddle(Vector3 near, Color color, float size)
        {
            Vector3 pos = near;
            if (Physics.Raycast(near + Vector3.up * 0.05f, Vector3.down, out var hit, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                pos = hit.point + Vector3.up * 0.002f;
            var go = ProcMesh.Visual(null, Shape.Box, pos, new Vector3(size, 0.002f, size * 0.8f), color, Quaternion.Euler(0, Random.Range(0, 360), 0), 0.85f, "水渍");
            go.GetComponent<MeshFilter>().sharedMesh = ProcMesh.Disc;
            go.transform.localScale = new Vector3(size, 1f, size * 0.8f);
            Debris.Add(go);
        }

        static Bounds RootSpaceBounds(Transform root, MeshFilter mf)
        {
            var verts = mf.sharedMesh.vertices;
            var b = new Bounds(root.InverseTransformPoint(mf.transform.TransformPoint(verts[0])), Vector3.zero);
            for (int i = 1; i < verts.Length; i++) b.Encapsulate(root.InverseTransformPoint(mf.transform.TransformPoint(verts[i])));
            return b;
        }

        static Vector3 Normalize(Vector3 p, Bounds b)
        {
            Vector3 e = b.extents;
            return new Vector3((p.x - b.center.x) / Mathf.Max(e.x, 1e-4f), (p.y - b.center.y) / Mathf.Max(e.y, 1e-4f), (p.z - b.center.z) / Mathf.Max(e.z, 1e-4f));
        }

        static Vector3 Denormalize(Vector3 q, Bounds b) => b.center + Vector3.Scale(q, b.extents);

        static int DominantAxis(Vector3 d)
        {
            d = new Vector3(Mathf.Abs(d.x), Mathf.Abs(d.y), Mathf.Abs(d.z));
            if (d.y >= d.x && d.y >= d.z) return 1;
            return d.x >= d.z ? 0 : 2;
        }
    }
}
