using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 袋壁随内容物形变（接触驱动的顶点位移）：
    /// 每个袋壁顶点从外向内发射射线，碰到袋内商品就把该处袋壁推到商品外沿。
    /// - 两侧向内折的风琴褶会被撑平；
    /// - 塑料袋有弹性，可以再鼓出 1.5cm；纸袋硬，最多被撑平。
    /// 碰撞体仍是刚性的盒子（物理稳定），这里只负责"看起来被撑开"。
    /// </summary>
    public class BagWallDeformer : MonoBehaviour
    {
        class Grid { public int Start, Nu, Nv, Axis, Sign; }

        BagSpec _spec;
        Mesh _mesh;
        Vector3[] _rest, _verts;
        float[] _push, _target;
        readonly List<Grid> _grids = new List<Grid>();
        int _n;
        float _maxBulge, _thickness, _timer;
        bool _dirty = true;
        bool _paper;

        /// <summary>当前最大外推量（米），调试用。</summary>
        public float MaxPush { get { float m = 0; for (int i = 0; i < _n; i++) m = Mathf.Max(m, _push[i]); return m; } }

        /// <summary>诊断：袋壁视觉顶点落在商品碰撞体内部的数量（= 看起来穿模的点）。</summary>
        public int DebugVertsInsideItems(out string worst)
        {
            int count = 0;
            var tally = new Dictionary<string, int>();
            for (int i = 0; i < _n; i++)
            {
                Vector3 p = transform.TransformPoint(_verts[i]);
                foreach (var c in Physics.OverlapSphere(p, 0.001f, Physics.AllLayers, QueryTriggerInteraction.Ignore))
                {
                    var it = c.GetComponentInParent<GroceryItem>();
                    if (it == null) continue;
                    count++;
                    tally.TryGetValue(it.Def.Name, out int k);
                    tally[it.Def.Name] = k + 1;
                    break;
                }
            }
            worst = string.Join(", ", System.Linq.Enumerable.Select(tally, kv => $"{kv.Key}:{kv.Value}"));
            return count;
        }

        /// <summary>每隔多久重新探测内容物（秒）。</summary>
        const float Interval = 0.12f;

        public static BagWallDeformer Create(Transform bag, BagSpec spec, Material outer, Material inner)
        {
            var go = new GameObject("袋壁（可形变）");
            go.transform.SetParent(bag, false);
            var d = go.AddComponent<BagWallDeformer>();
            d.Build(spec);
            go.AddComponent<MeshFilter>().sharedMesh = d._mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { outer, inner };
            return d;
        }

        // ------------------------------------------------------------ 生成静止形状（与原袋型一致）
        void Build(BagSpec spec)
        {
            _spec = spec;
            _paper = spec.Material == BagMaterial.Paper;
            _maxBulge = _paper ? 0.004f : spec.Stretch + 0.006f;   // 能鼓出的上限要覆盖弹性空间
            _thickness = _paper ? 0.0006f : 0.0004f;
            float w = spec.Inner.x, h = spec.Inner.y, d = spec.Inner.z;
            var rest = new List<Vector3>();
            var uv = new List<Vector2>();

            float Zig(float x) => _paper ? Mathf.Abs((x * 250f) % 2f - 1f) * 0.004f : 0f;
            float Wr(Vector3 p) => _paper ? 0 : (Noise.Value3(p * 40f, 5) - 0.5f) * 0.006f + Mathf.Sin(p.y * 90f + p.x * 30f) * 0.0012f;
            float creaseFrac = _paper ? 0.22f : 0.18f;
            float creaseStart = _paper ? 0.25f : 0.4f;

            // 前后（法线沿 z）
            for (int side = -1; side <= 1; side += 2)
            {
                int nx = _paper ? Mathf.Max(8, (int)(w / 0.004f)) : 24, ny = _paper ? 8 : 12;
                var g = new Grid { Start = rest.Count, Nu = nx + 1, Nv = ny + 1, Axis = 2, Sign = side };
                for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = (i / (float)nx - 0.5f) * w, y = j / (float)ny * h;
                    if (j == ny) y -= Zig(x);
                    float bulge = Mathf.Sin(Mathf.Clamp01(y / h) * Mathf.PI * 0.9f) * (_paper ? 0.003f : 0.008f) * (1 - Mathf.Pow(2 * x / w, 2));
                    var p = new Vector3(x, y, side * (d / 2 + bulge));
                    p.z += side * Wr(p);
                    rest.Add(p);
                    uv.Add(new Vector2(x * (_paper ? 3 : 2), y * (_paper ? 3 : 2)));
                }
                _grids.Add(g);
            }
            // 两侧风琴褶（法线沿 x）
            for (int side = -1; side <= 1; side += 2)
            {
                int nz = _paper ? 12 : 10, ny = _paper ? 8 : 12;
                var g = new Grid { Start = rest.Count, Nu = nz + 1, Nv = ny + 1, Axis = 0, Sign = side };
                for (int j = 0; j <= ny; j++)
                for (int k = 0; k <= nz; k++)
                {
                    float z = (k / (float)nz - 0.5f) * d, y = j / (float)ny * h;
                    if (j == ny) y -= Zig(z + 1);
                    float fold = d * creaseFrac * (1 - Mathf.Abs(z) / (d / 2)) * Mathf.SmoothStep(0, 1, y / (h * creaseStart));
                    var p = new Vector3(side * (w / 2 - fold), y, z);
                    p.x += side * Wr(p);
                    rest.Add(p);
                    uv.Add(new Vector2(z * (_paper ? 3 : 2), y * (_paper ? 3 : 2)));
                }
                _grids.Add(g);
            }

            _n = rest.Count;
            _rest = rest.ToArray();
            _verts = new Vector3[_n * 2];
            _push = new float[_n];
            _target = new float[_n];
            var uvs = new Vector2[_n * 2];
            for (int i = 0; i < _n; i++) { uvs[i] = uv[i]; uvs[i + _n] = uv[i]; }

            // 三角形：外层（子网格0）+ 内层反向（子网格1）
            var outerT = new List<int>();
            foreach (var g in _grids)
            {
                // 外法线方向：z 墙 side<0 朝 -z；x 墙 side>0 朝 +x（与原袋型一致）
                bool flip = g.Axis == 2 ? g.Sign > 0 : g.Sign < 0;
                for (int j = 0; j < g.Nv - 1; j++)
                for (int i = 0; i < g.Nu - 1; i++)
                {
                    int a = g.Start + j * g.Nu + i, b = a + 1, c = a + g.Nu + 1, dd = a + g.Nu;
                    if (!flip) { outerT.Add(a); outerT.Add(dd); outerT.Add(c); outerT.Add(a); outerT.Add(c); outerT.Add(b); }
                    else { outerT.Add(a); outerT.Add(b); outerT.Add(c); outerT.Add(a); outerT.Add(c); outerT.Add(dd); }
                }
            }
            var innerT = new int[outerT.Count];
            for (int t = 0; t < outerT.Count; t += 3) { innerT[t] = outerT[t] + _n; innerT[t + 1] = outerT[t + 2] + _n; innerT[t + 2] = outerT[t + 1] + _n; }

            _mesh = new Mesh { name = "BagWalls" };
            _mesh.MarkDynamic();
            WriteVertices();
            _mesh.vertices = _verts;
            _mesh.uv = uvs;
            _mesh.subMeshCount = 2;
            _mesh.SetTriangles(outerT, 0);
            _mesh.SetTriangles(innerT, 1);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        static Vector3 AxisVec(int axis) => axis == 0 ? Vector3.right : Vector3.forward;

        void WriteVertices()
        {
            foreach (var g in _grids)
            {
                Vector3 outward = AxisVec(g.Axis) * g.Sign;
                for (int k = 0; k < g.Nu * g.Nv; k++)
                {
                    int i = g.Start + k;
                    Vector3 p = _rest[i] + outward * _push[i];
                    _verts[i] = p;
                    _verts[i + _n] = p - outward * _thickness;
                }
            }
        }

        // ------------------------------------------------------------ 每帧
        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0)
            {
                _timer = Interval;
                Probe();
            }

            // 平滑过渡到目标形状
            bool changed = false;
            float k = 1f - Mathf.Exp(-Time.deltaTime * 14f);
            for (int i = 0; i < _n; i++)
            {
                float nv = Mathf.Lerp(_push[i], _target[i], k);
                if (Mathf.Abs(nv - _push[i]) > 1e-5f) { _push[i] = nv; changed = true; }
            }
            if (changed || _dirty)
            {
                _dirty = false;
                WriteVertices();
                _mesh.vertices = _verts;
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();
            }
        }

        static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>从袋壁外侧向内探测内容物，算出每个顶点应被推出的距离。</summary>
        void Probe()
        {
            float w = _spec.Inner.x, d = _spec.Inner.z;
            var tr = transform;
            foreach (var g in _grids)
            {
                float half = g.Axis == 0 ? w / 2 : d / 2;
                float limit = half + _maxBulge;
                Vector3 outward = AxisVec(g.Axis) * g.Sign;
                // 沿 u 方向稀疏采样，之间线性插值（前后墙顶点很密）
                int step = Mathf.Max(1, (g.Nu - 1) / 14);
                var cols = new List<int>();
                for (int u = 0; u < g.Nu - 1; u += step) cols.Add(u);
                cols.Add(g.Nu - 1);
                for (int j = 1; j < g.Nv; j++)
                {
                    for (int c = 0; c < cols.Count; c++)
                        _target[g.Start + j * g.Nu + cols[c]] = ProbeVertex(tr, g, j, cols[c], half, limit, outward);
                    for (int c = 0; c + 1 < cols.Count; c++)
                    {
                        int a = cols[c], b = cols[c + 1];
                        float va = _target[g.Start + j * g.Nu + a], vb = _target[g.Start + j * g.Nu + b];
                        for (int m = a + 1; m < b; m++) _target[g.Start + j * g.Nu + m] = Mathf.Lerp(va, vb, (m - a) / (float)(b - a));
                    }
                }
                // 3×3 模糊，让鼓包过渡自然
                var tmp = new float[g.Nu * g.Nv];
                for (int j = 0; j < g.Nv; j++)
                for (int u = 0; u < g.Nu; u++)
                {
                    float s = 0; int c = 0;
                    for (int dj = -1; dj <= 1; dj++)
                    for (int du = -1; du <= 1; du++)
                    {
                        int jj = j + dj, u2 = u + du * step;
                        if (jj < 1 || jj >= g.Nv || u2 < 0 || u2 >= g.Nu) continue;
                        s += _target[g.Start + jj * g.Nu + u2]; c++;
                    }
                    float raw = _target[g.Start + j * g.Nu + u];
                    tmp[j * g.Nu + u] = c > 0 ? Mathf.Max(raw * 0.85f, s / c) : raw;
                }
                for (int q = 0; q < tmp.Length; q++) _target[g.Start + q] = q < g.Nu ? 0 : tmp[q];
            }
        }

        float ProbeVertex(Transform tr, Grid g, int j, int u, float half, float limit, Vector3 outward)
        {
            int i = g.Start + j * g.Nu + u;
            Vector3 r = _rest[i];
            float restAbs = Vector3.Dot(r, outward);
            // 起点在袋壁碰撞体内部（不会打到袋子自己），朝袋子中心发射
            Vector3 originLocal = r;
            float start = half + _spec.Stretch + 0.004f;   // 起点在袋壁碰撞体内部
            if (g.Axis == 0) originLocal.x = g.Sign * start; else originLocal.z = g.Sign * start;
            Vector3 origin = tr.TransformPoint(originLocal);
            Vector3 dir = tr.TransformDirection(-outward);
            int n = Physics.RaycastNonAlloc(origin, dir, Hits, start, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int k = 0; k < n; k++)
            {
                var rb = Hits[k].rigidbody;
                if (rb == null || rb.isKinematic) continue;
                var it = rb.GetComponent<GroceryItem>();
                if (it == null || it.Held) continue;
                if (Hits[k].distance < best) best = Hits[k].distance;
            }
            if (best == float.MaxValue) return 0f;
            float contentAbs = start - best + 0.003f; // 商品外沿离中心的距离 + 3mm 余量
            float targetAbs = Mathf.Clamp(contentAbs, restAbs, limit);
            return targetAbs - restAbs;
        }
    }
}
