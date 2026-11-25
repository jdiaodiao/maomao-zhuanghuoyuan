using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 有向距离场建模：平滑并集/差集 + Surface Nets 网格化。
    /// 用于猫、猫爪、面包、水果、带把手的塑料桶等有机形体。
    /// 每个图元带一个 region 编号，网格按 region 拆成子网格（不同材质）。
    /// </summary>
    public class Sdf
    {
        enum Kind { Sphere, Ellipsoid, RoundCone, RoundBox, Torus }

        class Prim
        {
            public Kind K;
            public Vector3 A, B, Size;
            public float Ra, Rb, Blend;
            public bool Subtract;
            public int Region;
            public Quaternion InvRot = Quaternion.identity;
        }

        readonly List<Prim> _prims = new List<Prim>();
        public Func<Vector3, float> Displace;   // 额外表面起伏（米）
        public float DefaultBlend = 0.01f;

        public Sdf Sphere(Vector3 c, float r, int region = 0, float blend = -1) => Add(new Prim { K = Kind.Sphere, A = c, Ra = r, Region = region, Blend = blend });
        public Sdf Ellipsoid(Vector3 c, Vector3 radii, int region = 0, float blend = -1, Quaternion? rot = null) =>
            Add(new Prim { K = Kind.Ellipsoid, A = c, Size = radii, Region = region, Blend = blend, InvRot = Quaternion.Inverse(rot ?? Quaternion.identity) });
        /// <summary>两端半径不同的胶囊。</summary>
        public Sdf Capsule(Vector3 a, Vector3 b, float ra, float rb, int region = 0, float blend = -1) =>
            Add(new Prim { K = Kind.RoundCone, A = a, B = b, Ra = ra, Rb = rb, Region = region, Blend = blend });
        public Sdf Box(Vector3 c, Vector3 half, float round, int region = 0, float blend = -1, Quaternion? rot = null) =>
            Add(new Prim { K = Kind.RoundBox, A = c, Size = half, Ra = round, Region = region, Blend = blend, InvRot = Quaternion.Inverse(rot ?? Quaternion.identity) });
        public Sdf Torus(Vector3 c, float R, float r, Quaternion rot, int region = 0, float blend = -1) =>
            Add(new Prim { K = Kind.Torus, A = c, Ra = R, Rb = r, Region = region, Blend = blend, InvRot = Quaternion.Inverse(rot) });

        /// <summary>把上一个图元改成"挖掉"。</summary>
        public Sdf Cut() { _prims[_prims.Count - 1].Subtract = true; return this; }

        Sdf Add(Prim p) { _prims.Add(p); return this; }

        static float PrimDist(Prim p, Vector3 q)
        {
            switch (p.K)
            {
                case Kind.Sphere: return (q - p.A).magnitude - p.Ra;
                case Kind.Ellipsoid:
                {
                    Vector3 l = p.InvRot * (q - p.A);
                    Vector3 r = p.Size;
                    float k0 = new Vector3(l.x / r.x, l.y / r.y, l.z / r.z).magnitude;
                    float k1 = new Vector3(l.x / (r.x * r.x), l.y / (r.y * r.y), l.z / (r.z * r.z)).magnitude;
                    return k1 > 1e-6f ? k0 * (k0 - 1f) / k1 : -Mathf.Min(r.x, Mathf.Min(r.y, r.z));
                }
                case Kind.RoundCone:
                {
                    Vector3 ba = p.B - p.A, pa = q - p.A;
                    float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Mathf.Max(Vector3.Dot(ba, ba), 1e-9f));
                    return (pa - ba * h).magnitude - Mathf.Lerp(p.Ra, p.Rb, h);
                }
                case Kind.RoundBox:
                {
                    Vector3 l = p.InvRot * (q - p.A);
                    Vector3 d = new Vector3(Mathf.Abs(l.x), Mathf.Abs(l.y), Mathf.Abs(l.z)) - p.Size + Vector3.one * p.Ra;
                    Vector3 mx = Vector3.Max(d, Vector3.zero);
                    return mx.magnitude + Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0) - p.Ra;
                }
                case Kind.Torus:
                {
                    Vector3 l = p.InvRot * (q - p.A);
                    float xz = new Vector2(l.x, l.z).magnitude - p.Ra;
                    return new Vector2(xz, l.y).magnitude - p.Rb;
                }
            }
            return 1;
        }

        static float SMin(float a, float b, float k)
        {
            if (k <= 0) return Mathf.Min(a, b);
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1 - h);
        }

        static float SSub(float d, float cut, float k)
        {
            if (k <= 0) return Mathf.Max(d, -cut);
            float h = Mathf.Clamp01(0.5f - 0.5f * (d + cut) / k);
            return Mathf.Lerp(d, -cut, h) + k * h * (1 - h);
        }

        public float Eval(Vector3 q)
        {
            float d = 1e9f;
            bool first = true;
            foreach (var p in _prims)
            {
                float pd = PrimDist(p, q);
                float k = p.Blend >= 0 ? p.Blend : DefaultBlend;
                if (p.Subtract) d = SSub(d, pd, k);
                else if (first) { d = pd; first = false; }
                else d = SMin(d, pd, k);
            }
            if (Displace != null) d -= Displace(q);
            return d;
        }

        public int RegionAt(Vector3 q)
        {
            float best = 1e9f; int r = 0;
            foreach (var p in _prims)
            {
                if (p.Subtract) continue;
                float pd = PrimDist(p, q);
                if (pd < best) { best = pd; r = p.Region; }
            }
            return r;
        }

        /// <summary>
        /// Surface Nets 网格化。cell = 体素大小（米）。返回每个 region 一个 MB（region 编号即下标）。
        /// uvScale：盒式投影 UV 的缩放（每米多少次重复）。
        /// </summary>
        public MB[] Mesh(Bounds bounds, float cell, int regionCount = 1, float uvScale = 10f)
        {
            bounds.Expand(cell * 4);
            Vector3 o = bounds.min;
            int nx = Mathf.CeilToInt(bounds.size.x / cell) + 1, ny = Mathf.CeilToInt(bounds.size.y / cell) + 1, nz = Mathf.CeilToInt(bounds.size.z / cell) + 1;
            var f = new float[nx * ny * nz];
            int Idx(int x, int y, int z) => (z * ny + y) * nx + x;
            for (int z = 0; z < nz; z++)
            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
                f[Idx(x, y, z)] = Eval(o + new Vector3(x, y, z) * cell);

            int cx = nx - 1, cy = ny - 1, cz = nz - 1;
            var cellVert = new int[cx * cy * cz];
            for (int i = 0; i < cellVert.Length; i++) cellVert[i] = -1;
            var verts = new List<Vector3>();
            int CIdx(int x, int y, int z) => (z * cy + y) * cx + x;

            int[,] edges = { { 0, 1 }, { 2, 3 }, { 4, 5 }, { 6, 7 }, { 0, 2 }, { 1, 3 }, { 4, 6 }, { 5, 7 }, { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 } };
            var cv = new float[8];
            var cp = new Vector3[8];
            for (int z = 0; z < cz; z++)
            for (int y = 0; y < cy; y++)
            for (int x = 0; x < cx; x++)
            {
                int mask = 0;
                for (int c = 0; c < 8; c++)
                {
                    int dx = c & 1, dy = (c >> 1) & 1, dz = (c >> 2) & 1;
                    cv[c] = f[Idx(x + dx, y + dy, z + dz)];
                    cp[c] = new Vector3(x + dx, y + dy, z + dz);
                    if (cv[c] < 0) mask |= 1 << c;
                }
                if (mask == 0 || mask == 255) continue;
                Vector3 sum = Vector3.zero; int cnt = 0;
                for (int e = 0; e < 12; e++)
                {
                    float a = cv[edges[e, 0]], b = cv[edges[e, 1]];
                    if ((a < 0) == (b < 0)) continue;
                    float t = a / (a - b);
                    sum += Vector3.Lerp(cp[edges[e, 0]], cp[edges[e, 1]], t);
                    cnt++;
                }
                cellVert[CIdx(x, y, z)] = verts.Count;
                verts.Add(o + sum / cnt * cell);
            }

            // 面
            var tris = new List<int>();
            for (int z = 0; z < nz; z++)
            for (int y = 0; y < ny; y++)
            for (int x = 0; x < nx; x++)
            {
                float v0 = f[Idx(x, y, z)];
                // x 方向边
                if (x < nx - 1 && y > 0 && z > 0 && y < cy + 1 && z < cz + 1)
                {
                    float v1 = f[Idx(x + 1, y, z)];
                    if ((v0 < 0) != (v1 < 0) && y - 1 < cy && z - 1 < cz && y < cy && z < cz)
                        EmitQuad(tris, cellVert, CIdx(x, y - 1, z - 1), CIdx(x, y, z - 1), CIdx(x, y, z), CIdx(x, y - 1, z), v0 < 0);
                }
                if (y < ny - 1 && x > 0 && z > 0 && x < cx && z < cz)
                {
                    float v1 = f[Idx(x, y + 1, z)];
                    if ((v0 < 0) != (v1 < 0))
                        EmitQuad(tris, cellVert, CIdx(x - 1, y, z - 1), CIdx(x - 1, y, z), CIdx(x, y, z), CIdx(x, y, z - 1), v0 < 0);
                }
                if (z < nz - 1 && x > 0 && y > 0 && x < cx && y < cy)
                {
                    float v1 = f[Idx(x, y, z + 1)];
                    if ((v0 < 0) != (v1 < 0))
                        EmitQuad(tris, cellVert, CIdx(x - 1, y - 1, z), CIdx(x, y - 1, z), CIdx(x, y, z), CIdx(x - 1, y, z), v0 < 0);
                }
            }

            // 法线（梯度）
            var normals = new Vector3[verts.Count];
            float e2 = cell * 0.5f;
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 p = verts[i];
                normals[i] = new Vector3(
                    Eval(p + new Vector3(e2, 0, 0)) - Eval(p - new Vector3(e2, 0, 0)),
                    Eval(p + new Vector3(0, e2, 0)) - Eval(p - new Vector3(0, e2, 0)),
                    Eval(p + new Vector3(0, 0, e2)) - Eval(p - new Vector3(0, 0, e2))).normalized;
            }

            // 按 region 拆分（三角形重心决定）
            var outMb = new MB[Mathf.Max(1, regionCount)];
            var remap = new Dictionary<int, int>[outMb.Length];
            for (int r = 0; r < outMb.Length; r++) { outMb[r] = new MB(); remap[r] = new Dictionary<int, int>(); }
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector3 c = (verts[tris[t]] + verts[tris[t + 1]] + verts[tris[t + 2]]) / 3f;
                int r = regionCount > 1 ? Mathf.Clamp(RegionAt(c), 0, regionCount - 1) : 0;
                var mb = outMb[r];
                int[] ids = new int[3];
                for (int k = 0; k < 3; k++)
                {
                    int vi = tris[t + k];
                    if (!remap[r].TryGetValue(vi, out int ni))
                    {
                        Vector3 p = verts[vi], n = normals[vi];
                        ni = mb.Vert(p, n, BoxUv(p, n, uvScale));
                        remap[r][vi] = ni;
                    }
                    ids[k] = ni;
                }
                mb.Tri(ids[0], ids[1], ids[2]);
            }
            return outMb;
        }

        static void EmitQuad(List<int> tris, int[] cellVert, int c0, int c1, int c2, int c3, bool insideFirst)
        {
            int a = cellVert[c0], b = cellVert[c1], c = cellVert[c2], d = cellVert[c3];
            if (a < 0 || b < 0 || c < 0 || d < 0) return;
            if (insideFirst) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
        }

        static Vector2 BoxUv(Vector3 p, Vector3 n, float s)
        {
            Vector3 a = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
            if (a.y >= a.x && a.y >= a.z) return new Vector2(p.x, p.z) * s;
            if (a.x >= a.z) return new Vector2(p.z, p.y) * s;
            return new Vector2(p.x, p.y) * s;
        }
    }
}
