using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatMart
{
    /// <summary>网格构建器：顶点/UV/三角形，支持焊接平滑法线。</summary>
    public class MB
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<int> T = new List<int>();
        bool _hasNormals;

        public int Vert(Vector3 p, Vector2 uv) { V.Add(p); UV.Add(uv); N.Add(Vector3.zero); return V.Count - 1; }
        public int Vert(Vector3 p, Vector3 n, Vector2 uv) { V.Add(p); UV.Add(uv); N.Add(n); _hasNormals = true; return V.Count - 1; }
        public void Tri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }
        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        /// <summary>计算法线。weld=true 时同位置顶点共享法线（平滑）。</summary>
        public MB Normals(bool weld = true)
        {
            if (_hasNormals) return this;
            var acc = new Vector3[V.Count];
            for (int i = 0; i < T.Count; i += 3)
            {
                Vector3 a = V[T[i]], b = V[T[i + 1]], c = V[T[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                acc[T[i]] += n; acc[T[i + 1]] += n; acc[T[i + 2]] += n;
            }
            if (weld)
            {
                var map = new Dictionary<Vector3Int, Vector3>();
                for (int i = 0; i < V.Count; i++)
                {
                    var k = Key(V[i]);
                    map.TryGetValue(k, out var s);
                    map[k] = s + acc[i];
                }
                for (int i = 0; i < V.Count; i++) N[i] = map[Key(V[i])].normalized;
            }
            else for (int i = 0; i < V.Count; i++) N[i] = acc[i].normalized;
            _hasNormals = true;
            return this;
        }

        static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x * 20000f), Mathf.RoundToInt(p.y * 20000f), Mathf.RoundToInt(p.z * 20000f));

        public MB Flip()
        {
            for (int i = 0; i < T.Count; i += 3) { int t = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = t; }
            for (int i = 0; i < N.Count; i++) N[i] = -N[i];
            return this;
        }

        /// <summary>加一份反面（薄片双面显示）。</summary>
        public MB DoubleSided()
        {
            Normals();
            int n = V.Count, t = T.Count;
            for (int i = 0; i < n; i++) { V.Add(V[i]); N.Add(-N[i]); UV.Add(UV[i]); }
            for (int i = 0; i < t; i += 3) { T.Add(T[i] + n); T.Add(T[i + 2] + n); T.Add(T[i + 1] + n); }
            return this;
        }

        public MB Transform(Matrix4x4 m)
        {
            Normals();
            for (int i = 0; i < V.Count; i++) { V[i] = m.MultiplyPoint3x4(V[i]); N[i] = m.MultiplyVector(N[i]).normalized; }
            return this;
        }

        public MB Displace(Func<Vector3, Vector3, Vector3> f)
        {
            Normals();
            for (int i = 0; i < V.Count; i++) V[i] = f(V[i], N[i]);
            _hasNormals = false;
            for (int i = 0; i < N.Count; i++) N[i] = Vector3.zero;
            return Normals();
        }

        public Mesh ToMesh(string name = "mesh")
        {
            Normals();
            var m = new Mesh { name = name };
            if (V.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetTriangles(T, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    /// <summary>把许多零件按材质合并成一个带子网格的 Mesh（一件商品一个部件只有一个 Renderer）。</summary>
    public class VisualSet
    {
        class Sub { public Material Mat; public readonly MB Mb = new MB(); }
        readonly List<Sub> _subs = new List<Sub>();

        public bool Empty => _subs.Count == 0;

        public VisualSet Add(MB piece, Material mat) => Add(piece, Matrix4x4.identity, mat);

        public VisualSet Add(MB piece, Matrix4x4 m, Material mat)
        {
            piece.Normals();
            Sub s = _subs.Find(x => x.Mat == mat);
            if (s == null) { s = new Sub { Mat = mat }; _subs.Add(s); }
            int off = s.Mb.V.Count;
            for (int i = 0; i < piece.V.Count; i++)
                s.Mb.Vert(m.MultiplyPoint3x4(piece.V[i]), m.MultiplyVector(piece.N[i]).normalized, piece.UV[i]);
            foreach (int t in piece.T) s.Mb.T.Add(t + off);
            return this;
        }

        public VisualSet Add(MB piece, Vector3 pos, Quaternion rot, Material mat) => Add(piece, Matrix4x4.TRS(pos, rot, Vector3.one), mat);
        public VisualSet Add(MB piece, Vector3 pos, Material mat) => Add(piece, Matrix4x4.Translate(pos), mat);

        public void Build(string name, out Mesh mesh, out Material[] mats)
        {
            mesh = new Mesh { name = name };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            foreach (var s in _subs) { v.AddRange(s.Mb.V); n.AddRange(s.Mb.N); uv.AddRange(s.Mb.UV); }
            if (v.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = _subs.Count;
            int off = 0;
            mats = new Material[_subs.Count];
            for (int i = 0; i < _subs.Count; i++)
            {
                var tris = new int[_subs[i].Mb.T.Count];
                for (int k = 0; k < tris.Length; k++) tris[k] = _subs[i].Mb.T[k] + off;
                mesh.SetTriangles(tris, i, false);
                off += _subs[i].Mb.V.Count;
                mats[i] = _subs[i].Mat;
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }

        /// <summary>直接生成一个带 Renderer 的物体。</summary>
        public GameObject Spawn(Transform parent, string name, Vector3 localPos = default, Quaternion? rot = null, bool shadows = true)
        {
            Build(name, out var mesh, out var mats);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }
    }

    /// <summary>参数化几何体（真实尺寸，米）。</summary>
    public static class Shapes
    {
        public enum Face { PosX, NegX, PosY, NegY, PosZ, NegZ }

        /// <summary>
        /// 圆角盒。uvMap(face, local01) → 贴图 UV；默认每面铺满 0~1。
        /// 侧面文字方向保证从外面看是正的。
        /// </summary>
        public static MB RoundedBox(Vector3 size, float r, int bevelSeg = 3, int inner = 3, Func<Face, Vector2, Vector2> uvMap = null)
        {
            var mb = new MB();
            Vector3 h = size * 0.5f;
            r = Mathf.Clamp(r, 0.0005f, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.98f);
            float[] cx = Coords(h.x, r, bevelSeg, inner), cy = Coords(h.y, r, bevelSeg, inner), cz = Coords(h.z, r, bevelSeg, inner);
            Vector3 innerH = h - Vector3.one * r;

            for (int f = 0; f < 6; f++)
            {
                Vector3 N, U, Vv;
                FaceAxes((Face)f, out N, out U, out Vv);
                float[] cu = Pick(U, cx, cy, cz), cv = Pick(Vv, cx, cy, cz);
                float hn = Mathf.Abs(Vector3.Dot(N, h));
                float hu = Mathf.Abs(Vector3.Dot(U, h)), hv = Mathf.Abs(Vector3.Dot(Vv, h));
                int start = mb.V.Count;
                int nu = cu.Length, nv = cv.Length;
                for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    Vector3 p = N * hn + U * cu[i] + Vv * cv[j];
                    Vector3 c = new Vector3(Mathf.Clamp(p.x, -innerH.x, innerH.x), Mathf.Clamp(p.y, -innerH.y, innerH.y), Mathf.Clamp(p.z, -innerH.z, innerH.z));
                    Vector3 d = p - c;
                    Vector3 q = d.sqrMagnitude > 1e-12f ? c + d.normalized * r : p;
                    Vector2 l = new Vector2((cu[i] + hu) / (2 * hu), (cv[j] + hv) / (2 * hv));
                    mb.Vert(q, uvMap != null ? uvMap((Face)f, l) : l);
                }
                bool flip = Vector3.Dot(Vector3.Cross(U, Vv), N) < 0;
                for (int j = 0; j < nv - 1; j++)
                for (int i = 0; i < nu - 1; i++)
                {
                    int a = start + j * nu + i, b = a + 1, c2 = a + nu + 1, d2 = a + nu;
                    if (!flip) mb.Quad(a, b, c2, d2); else mb.Quad(a, d2, c2, b);
                }
            }
            return mb.Normals(true);
        }

        static float[] Pick(Vector3 axis, float[] cx, float[] cy, float[] cz)
        {
            float[] src = Mathf.Abs(axis.x) > 0.5f ? cx : Mathf.Abs(axis.y) > 0.5f ? cy : cz;
            float s = axis.x + axis.y + axis.z;
            if (s > 0) return src;
            var r = new float[src.Length];
            for (int i = 0; i < src.Length; i++) r[i] = -src[src.Length - 1 - i];
            return r;
        }

        static float[] Coords(float h, float r, int bevel, int inner)
        {
            var l = new List<float>();
            for (int k = 0; k <= bevel; k++) l.Add(-h + r * k / bevel);
            for (int k = 1; k < inner; k++) l.Add(Mathf.Lerp(-h + r, h - r, k / (float)inner));
            for (int k = 0; k <= bevel; k++) l.Add(h - r + r * k / bevel);
            // 去重
            var o = new List<float>();
            foreach (var x in l) if (o.Count == 0 || x - o[o.Count - 1] > 1e-6f) o.Add(x);
            return o.ToArray();
        }

        /// <summary>每个面的法线、水平(U)、竖直(V)。侧面 V=上，U=从外看向右。</summary>
        public static void FaceAxes(Face f, out Vector3 N, out Vector3 U, out Vector3 V)
        {
            switch (f)
            {
                case Face.PosX: N = Vector3.right; V = Vector3.up; U = Vector3.forward; break;
                case Face.NegX: N = Vector3.left; V = Vector3.up; U = Vector3.back; break;
                case Face.PosZ: N = Vector3.forward; V = Vector3.up; U = Vector3.left; break;
                case Face.NegZ: N = Vector3.back; V = Vector3.up; U = Vector3.right; break;
                case Face.PosY: N = Vector3.up; V = Vector3.forward; U = Vector3.right; break;
                default: N = Vector3.down; V = Vector3.back; U = Vector3.right; break;
            }
        }

        /// <summary>
        /// 旋转体。profile: (半径, 高度) 从底部中心 → 外沿 → 向上 → 顶部中心。
        /// UV: u 绕一圈 0~1，v 沿轮廓长度 0~1。
        /// </summary>
        public static MB Lathe(IList<Vector2> profile, int radial = 32)
        {
            var mb = new MB();
            float total = 0;
            var len = new float[profile.Count];
            for (int i = 1; i < profile.Count; i++) { total += Vector2.Distance(profile[i], profile[i - 1]); len[i] = total; }
            for (int j = 0; j < profile.Count; j++)
            for (int i = 0; i <= radial; i++)
            {
                float th = i / (float)radial * Mathf.PI * 2f;
                var p = profile[j];
                mb.Vert(new Vector3(Mathf.Cos(th) * p.x, p.y, Mathf.Sin(th) * p.x), new Vector2(i / (float)radial, total > 0 ? len[j] / total : 0));
            }
            for (int j = 0; j < profile.Count - 1; j++)
            for (int i = 0; i < radial; i++)
            {
                int a = j * (radial + 1) + i, b = a + 1, d = a + radial + 1, c = d + 1;
                mb.Tri(a, d, b); mb.Tri(b, d, c);
            }
            return mb.Normals(true);
        }

        /// <summary>圆柱侧面带（标签用），u 0~1 绕一圈，v 0~1 自下而上。</summary>
        public static MB Band(float radiusBottom, float radiusTop, float y0, float y1, int radial = 40, int rows = 4, float arc = 1f)
        {
            var mb = new MB();
            for (int j = 0; j <= rows; j++)
            {
                float t = j / (float)rows;
                float y = Mathf.Lerp(y0, y1, t), r = Mathf.Lerp(radiusBottom, radiusTop, t);
                for (int i = 0; i <= radial; i++)
                {
                    float th = ((i / (float)radial - 0.5f) * arc - 0.25f) * Mathf.PI * 2f; // u=0.5 正对 -z
                    mb.Vert(new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r), new Vector2(i / (float)radial, t));
                }
            }
            for (int j = 0; j < rows; j++)
            for (int i = 0; i < radial; i++)
            {
                int a = j * (radial + 1) + i, b = a + 1, d = a + radial + 1, c = d + 1;
                mb.Tri(a, d, b); mb.Tri(b, d, c);
            }
            return mb.Normals(true);
        }

        /// <summary>带圆角的轮廓点（给 Lathe 用）：在拐角 p 处插入圆弧。</summary>
        public static List<Vector2> Fillet(IList<Vector2> pts, float radius, int seg = 3)
        {
            var o = new List<Vector2> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i - 1], p = pts[i], b = pts[i + 1];
                Vector2 da = (a - p), db = (b - p);
                float la = da.magnitude, lb = db.magnitude;
                float r = Mathf.Min(radius, la * 0.45f, lb * 0.45f);
                if (r < 1e-5f) { o.Add(p); continue; }
                Vector2 s = p + da / la * r, e = p + db / lb * r;
                for (int k = 0; k <= seg; k++)
                {
                    float t = k / (float)seg;
                    // 二次贝塞尔近似圆角
                    Vector2 q = (1 - t) * (1 - t) * s + 2 * (1 - t) * t * p + t * t * e;
                    o.Add(q);
                }
            }
            o.Add(pts[pts.Count - 1]);
            return o;
        }

        /// <summary>
        /// 枕形包装袋（薯片袋）。size: 宽(x) 高(y) 厚(z)。上下有压边封口。
        /// UV: 正面 u∈[0,0.5]，背面 u∈[0.5,1]。sideSeals=true 时左右也有封口（巧克力棒）。
        /// </summary>
        public static MB Pillow(Vector3 size, float seal = 0.015f, bool sideSeals = false, int nx = 18, int ny = 24, int seed = 0)
        {
            var mb = new MB();
            float hx = size.x / 2, hy = size.y / 2, hz = size.z / 2;
            for (int side = 0; side < 2; side++)
            {
                float sgn = side == 0 ? -1 : 1; // 正面在 -z
                int start = mb.V.Count;
                for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float u = i / (float)nx, v = j / (float)ny;
                    float x = Mathf.Lerp(-hx, hx, u), y = Mathf.Lerp(-hy, hy, v);
                    float ey = Mathf.Min(hy - Mathf.Abs(y), 9) / Mathf.Max(seal, 1e-4f);
                    float ex = sideSeals ? (hx - Mathf.Abs(x)) / Mathf.Max(seal, 1e-4f) : 99;
                    float puffY = Mathf.Clamp01((hy - Mathf.Abs(y) - seal) / (hy * 0.35f));
                    float puffX = Mathf.Sqrt(Mathf.Clamp01(1 - Mathf.Pow(Mathf.Abs(x) / hx, 4)));
                    if (sideSeals) puffX *= Mathf.Clamp01((hx - Mathf.Abs(x) - seal) / (hx * 0.3f));
                    float t = Mathf.Sqrt(puffY) * puffX;
                    float z = hz * t;
                    // 封口压纹
                    if (ey < 1 || ex < 1) z = 0.0006f + 0.0006f * Mathf.Sin(x * 900f + y * 900f);
                    // 褶皱
                    z += hz * 0.05f * (Noise.Value2(x * 40 + seed, y * 40 + side * 13) - 0.5f) * t;
                    float uu = side == 0 ? u * 0.5f : 1f - u * 0.5f;
                    mb.Vert(new Vector3(x, y, z * sgn), new Vector2(uu, v));
                }
                for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = start + j * (nx + 1) + i, b = a + 1, c = a + nx + 2, d = a + nx + 1;
                    if (side == 0) mb.Quad(a, d, c, b); else mb.Quad(a, b, c, d);
                }
            }
            return mb.Normals(true);
        }

        /// <summary>沿路径的管子。</summary>
        public static MB Tube(IList<Vector3> path, IList<float> radius, int sides = 10, bool caps = true)
        {
            var mb = new MB();
            int n = path.Count;
            Vector3 prevN = Vector3.zero;
            for (int j = 0; j < n; j++)
            {
                Vector3 t = (path[Mathf.Min(j + 1, n - 1)] - path[Mathf.Max(j - 1, 0)]).normalized;
                Vector3 nrm = j == 0 ? Vector3.Cross(t, Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right).normalized
                    : Vector3.ProjectOnPlane(prevN, t).normalized;
                prevN = nrm;
                Vector3 bin = Vector3.Cross(t, nrm);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2;
                    Vector3 off = (nrm * Mathf.Cos(a) + bin * Mathf.Sin(a)) * radius[j];
                    mb.Vert(path[j] + off, new Vector2(i / (float)sides, j / (float)(n - 1)));
                }
            }
            for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < sides; i++)
            {
                int a = j * (sides + 1) + i, b = a + 1, d = a + sides + 1, c = d + 1;
                mb.Tri(a, b, d); mb.Tri(b, c, d);
            }
            if (caps)
            {
                for (int end = 0; end < 2; end++)
                {
                    int ring = end == 0 ? 0 : (n - 1) * (sides + 1);
                    int center = mb.Vert(path[end == 0 ? 0 : n - 1], new Vector2(0.5f, end));
                    for (int i = 0; i < sides; i++)
                        if (end == 0) mb.Tri(center, ring + i + 1, ring + i); else mb.Tri(center, ring + i, ring + i + 1);
                }
            }
            return mb.Normals(true);
        }

        public static MB Torus(float R, float r, int segU = 28, int segV = 12, float arc = 1f)
        {
            var mb = new MB();
            for (int j = 0; j <= segU; j++)
            {
                float a = j / (float)segU * Mathf.PI * 2 * arc;
                Vector3 c = new Vector3(Mathf.Cos(a) * R, 0, Mathf.Sin(a) * R);
                Vector3 outDir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int i = 0; i <= segV; i++)
                {
                    float b = i / (float)segV * Mathf.PI * 2;
                    mb.Vert(c + outDir * Mathf.Cos(b) * r + Vector3.up * Mathf.Sin(b) * r, new Vector2(j / (float)segU, i / (float)segV));
                }
            }
            for (int j = 0; j < segU; j++)
            for (int i = 0; i < segV; i++)
            {
                int a = j * (segV + 1) + i, b = a + 1, d = a + segV + 1, c = d + 1;
                mb.Tri(a, b, d); mb.Tri(b, c, d);
            }
            return mb.Normals(true);
        }

        /// <summary>变形球：radiusFn(方向) → 该方向的半径。UV 为经纬度。</summary>
        public static MB Blob(Func<Vector3, float> radiusFn, int lon = 32, int lat = 20)
        {
            var mb = new MB();
            for (int j = 0; j <= lat; j++)
            {
                float phi = j / (float)lat * Mathf.PI;
                for (int i = 0; i <= lon; i++)
                {
                    float th = i / (float)lon * Mathf.PI * 2;
                    Vector3 d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                    mb.Vert(d * radiusFn(d), new Vector2(i / (float)lon, 1 - j / (float)lat));
                }
            }
            for (int j = 0; j < lat; j++)
            for (int i = 0; i < lon; i++)
            {
                int a = j * (lon + 1) + i, b = a + 1, d = a + lon + 1, c = d + 1;
                mb.Tri(a, b, d); mb.Tri(b, c, d);
            }
            return mb.Normals(true);
        }

        public static MB Ellipsoid(Vector3 size, int lon = 28, int lat = 18) =>
            Blob(d => 1f / Mathf.Sqrt(Sq(d.x / (size.x / 2)) + Sq(d.y / (size.y / 2)) + Sq(d.z / (size.z / 2))), lon, lat);

        static float Sq(float x) => x * x;

        /// <summary>平面矩形（朝 -z，UV 0~1）。</summary>
        public static MB Quad(float w, float h, int nx = 1, int ny = 1)
        {
            var mb = new MB();
            for (int j = 0; j <= ny; j++)
            for (int i = 0; i <= nx; i++)
                mb.Vert(new Vector3((i / (float)nx - 0.5f) * w, (j / (float)ny - 0.5f) * h, 0), new Vector2(i / (float)nx, j / (float)ny));
            for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 2, d = a + nx + 1;
                mb.Quad(a, d, c, b);
            }
            return mb.Normals(false);
        }

        /// <summary>圆盘（朝上）。</summary>
        public static MB Disc(float radius, int seg = 32, float noise = 0, int seed = 0)
        {
            var mb = new MB();
            int c = mb.Vert(Vector3.zero, new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= seg; i++)
            {
                float th = i / (float)seg * Mathf.PI * 2;
                float r = radius * (1 + noise * (Noise.Value2(Mathf.Cos(th) * 2 + seed, Mathf.Sin(th) * 2) - 0.5f) * 2);
                if (i == seg) r = mb.V[1].magnitude;
                mb.Vert(new Vector3(Mathf.Cos(th) * r, 0, Mathf.Sin(th) * r), new Vector2(0.5f + Mathf.Cos(th) * 0.5f, 0.5f + Mathf.Sin(th) * 0.5f));
            }
            for (int i = 1; i <= seg; i++) mb.Tri(c, i + 1, i);
            return mb.Normals(false);
        }

        /// <summary>两端开口的薄壁盒（托盘/袋身），厚度向内。</summary>
        public static MB OpenBox(Vector3 size, float wall, float r = 0.004f)
        {
            var outer = RoundedBox(size, r, 2, 2);
            // 把顶面挖掉：简单做法——删除法线朝上且位于顶部的三角形
            var mb = new MB();
            float top = size.y / 2 - r * 0.5f;
            var keep = new List<int>();
            for (int i = 0; i < outer.T.Count; i += 3)
            {
                Vector3 a = outer.V[outer.T[i]], b = outer.V[outer.T[i + 1]], c = outer.V[outer.T[i + 2]];
                if (a.y > top && b.y > top && c.y > top) continue;
                keep.Add(outer.T[i]); keep.Add(outer.T[i + 1]); keep.Add(outer.T[i + 2]);
            }
            for (int i = 0; i < outer.V.Count; i++) mb.Vert(outer.V[i], outer.N[i], outer.UV[i]);
            mb.T.AddRange(keep);
            // 内壁
            var inner = RoundedBox(size - new Vector3(wall * 2, wall, wall * 2), r * 0.6f, 2, 2);
            float itop = (size.y - wall) / 2 - r * 0.3f;
            int off = mb.V.Count;
            for (int i = 0; i < inner.V.Count; i++) mb.Vert(inner.V[i] + Vector3.up * wall / 2, -inner.N[i], inner.UV[i]);
            for (int i = 0; i < inner.T.Count; i += 3)
            {
                Vector3 a = inner.V[inner.T[i]], b = inner.V[inner.T[i + 1]], c = inner.V[inner.T[i + 2]];
                if (a.y > itop && b.y > itop && c.y > itop) continue;
                mb.Tri(inner.T[i] + off, inner.T[i + 2] + off, inner.T[i + 1] + off);
            }
            return mb;
        }

        /// <summary>多边形挤出（锥形/星形等），朝上。</summary>
        public static MB Prism(IList<Vector2> poly, float h)
        {
            var mb = new MB();
            int n = poly.Count;
            // 侧面
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                int v0 = mb.Vert(new Vector3(a.x, 0, a.y), new Vector2(i / (float)n, 0));
                int v1 = mb.Vert(new Vector3(b.x, 0, b.y), new Vector2((i + 1) / (float)n, 0));
                int v2 = mb.Vert(new Vector3(b.x, h, b.y), new Vector2((i + 1) / (float)n, 1));
                int v3 = mb.Vert(new Vector3(a.x, h, a.y), new Vector2(i / (float)n, 1));
                mb.Quad(v0, v3, v2, v1);
            }
            // 顶底（扇形）
            int ct = mb.Vert(new Vector3(0, h, 0), new Vector2(0.5f, 0.5f));
            int cb = mb.Vert(Vector3.zero, new Vector2(0.5f, 0.5f));
            int st = mb.V.Count;
            for (int i = 0; i < n; i++) mb.Vert(new Vector3(poly[i].x, h, poly[i].y), poly[i]);
            int sb = mb.V.Count;
            for (int i = 0; i < n; i++) mb.Vert(new Vector3(poly[i].x, 0, poly[i].y), poly[i]);
            for (int i = 0; i < n; i++)
            {
                mb.Tri(ct, st + (i + 1) % n, st + i);
                mb.Tri(cb, sb + i, sb + (i + 1) % n);
            }
            return mb.Normals(false);
        }

        /// <summary>保证多边形在 (x,z) 平面逆时针，这样 Prism 的侧面朝外。</summary>
        public static List<Vector2> EnsureCCW(IList<Vector2> poly)
        {
            float area = 0;
            for (int i = 0; i < poly.Count; i++) { var a = poly[i]; var b = poly[(i + 1) % poly.Count]; area += a.x * b.y - b.x * a.y; }
            var l = new List<Vector2>(poly);
            if (area < 0) l.Reverse();
            return l;
        }
    }
}
