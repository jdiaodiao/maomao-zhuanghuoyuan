using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    public enum Shape { Box, Cylinder, Sphere, Cone }

    /// <summary>
    /// 程序化网格：单位尺寸(-0.5~0.5)，细分足够多以便损坏时形变。
    /// 碰撞用低面数版本。
    /// </summary>
    public static class ProcMesh
    {
        static Mesh _box, _cyl, _sph, _cone, _disc;
        static Mesh _cylCol, _sphCol, _coneCol;

        public static Mesh Get(Shape s)
        {
            switch (s)
            {
                case Shape.Box: return _box ? _box : (_box = BuildBox(6));
                case Shape.Cylinder: return _cyl ? _cyl : (_cyl = BuildFrustum(24, 6, 1f));
                case Shape.Sphere: return _sph ? _sph : (_sph = BuildSphere(24, 16));
                case Shape.Cone: return _cone ? _cone : (_cone = BuildFrustum(20, 5, 0.02f));
            }
            return null;
        }

        public static Mesh GetCollider(Shape s)
        {
            switch (s)
            {
                case Shape.Cylinder: return _cylCol ? _cylCol : (_cylCol = BuildFrustum(12, 1, 1f));
                case Shape.Sphere: return _sphCol ? _sphCol : (_sphCol = BuildSphere(10, 8));
                case Shape.Cone: return _coneCol ? _coneCol : (_coneCol = BuildFrustum(10, 1, 0.05f));
            }
            return null;
        }

        public static Mesh Disc => _disc ? _disc : (_disc = BuildDisc(24));

        static Mesh BuildBox(int n)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var N in normals)
            {
                Vector3 a = Mathf.Abs(N.y) > 0.5f ? Vector3.forward : Vector3.up;
                Vector3 Ua = Vector3.Cross(a, N);
                Vector3 Va = Vector3.Cross(N, Ua);
                int start = v.Count;
                for (int j = 0; j <= n; j++)
                for (int i = 0; i <= n; i++)
                {
                    float fu = i / (float)n - 0.5f, fv = j / (float)n - 0.5f;
                    v.Add(N * 0.5f + Ua * fu + Va * fv);
                    uv.Add(new Vector2(i / (float)n, j / (float)n));
                }
                for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int i00 = start + j * (n + 1) + i, i10 = i00 + 1, i01 = i00 + n + 1, i11 = i01 + 1;
                    t.Add(i00); t.Add(i10); t.Add(i11);
                    t.Add(i00); t.Add(i11); t.Add(i01);
                }
            }
            return Finish("Box", v, uv, t);
        }

        /// <summary>圆柱/圆台/圆锥（沿 Y 轴，半径 0.5，高 1）。topRatio = 顶半径/底半径。</summary>
        static Mesh BuildFrustum(int radial, int hseg, float topRatio)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            // 侧面
            for (int j = 0; j <= hseg; j++)
            {
                float fy = j / (float)hseg;
                float r = 0.5f * Mathf.Lerp(1f, topRatio, fy);
                for (int i = 0; i <= radial; i++)
                {
                    float th = i / (float)radial * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(th) * r, fy - 0.5f, Mathf.Sin(th) * r));
                    uv.Add(new Vector2(i / (float)radial, fy));
                }
            }
            for (int j = 0; j < hseg; j++)
            for (int i = 0; i < radial; i++)
            {
                int a = j * (radial + 1) + i, b = a + 1, d = a + radial + 1, c = d + 1;
                t.Add(a); t.Add(d); t.Add(b);
                t.Add(b); t.Add(d); t.Add(c);
            }
            // 顶/底盖（同心环，方便形变）
            AddCap(v, uv, t, radial, 0.5f, 0.5f * topRatio, true);
            AddCap(v, uv, t, radial, -0.5f, 0.5f, false);
            return Finish("Frustum", v, uv, t);
        }

        static void AddCap(List<Vector3> v, List<Vector2> uv, List<int> t, int radial, float y, float radius, bool top)
        {
            if (radius < 0.02f) return;
            const int rings = 3;
            int start = v.Count;
            for (int k = 0; k <= rings; k++)
            {
                float r = radius * k / rings;
                for (int i = 0; i <= radial; i++)
                {
                    float th = i / (float)radial * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r));
                    uv.Add(new Vector2(0.5f + Mathf.Cos(th) * r, 0.5f + Mathf.Sin(th) * r));
                }
            }
            for (int k = 0; k < rings; k++)
            for (int i = 0; i < radial; i++)
            {
                int a = start + k * (radial + 1) + i, b = a + 1, d = a + radial + 1, c = d + 1;
                if (top) { t.Add(a); t.Add(b); t.Add(d); t.Add(b); t.Add(c); t.Add(d); }
                else { t.Add(a); t.Add(d); t.Add(b); t.Add(b); t.Add(d); t.Add(c); }
            }
        }

        static Mesh BuildSphere(int lon, int lat)
        {
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int j = 0; j <= lat; j++)
            {
                float phi = j / (float)lat * Mathf.PI;
                for (int i = 0; i <= lon; i++)
                {
                    float th = i / (float)lon * Mathf.PI * 2f;
                    v.Add(0.5f * new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th)));
                    uv.Add(new Vector2(i / (float)lon, 1f - j / (float)lat));
                }
            }
            for (int j = 0; j < lat; j++)
            for (int i = 0; i < lon; i++)
            {
                int a = j * (lon + 1) + i, b = a + 1, d = a + lon + 1, c = d + 1;
                t.Add(a); t.Add(b); t.Add(d);
                t.Add(b); t.Add(c); t.Add(d);
            }
            return Finish("Sphere", v, uv, t);
        }

        static Mesh BuildDisc(int radial)
        {
            var v = new List<Vector3> { Vector3.zero };
            var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var t = new List<int>();
            for (int i = 0; i <= radial; i++)
            {
                float th = i / (float)radial * Mathf.PI * 2f;
                float r = 0.5f * (0.85f + 0.15f * Mathf.PerlinNoise(i * 0.7f, 3.1f) * 2f);
                if (i == radial) r = v[1].magnitude;
                v.Add(new Vector3(Mathf.Cos(th) * r, 0, Mathf.Sin(th) * r));
                uv.Add(new Vector2(0.5f + Mathf.Cos(th) * 0.5f, 0.5f + Mathf.Sin(th) * 0.5f));
            }
            for (int i = 1; i <= radial; i++) { t.Add(0); t.Add(i + 1); t.Add(i); }
            return Finish("Disc", v, uv, t);
        }

        static Mesh Finish(string name, List<Vector3> v, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>创建一个纯视觉部件（无碰撞）。</summary>
        public static GameObject Visual(Transform parent, Shape shape, Vector3 localPos, Vector3 localScale, Color color,
            Quaternion? rot = null, float smooth = 0.25f, string name = null)
        {
            var go = new GameObject(name ?? shape.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = Get(shape);
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Get(color, smooth);
            return go;
        }

        /// <summary>创建一个带碰撞的部件。</summary>
        public static GameObject Solid(Transform parent, Shape shape, Vector3 localPos, Vector3 localScale, Color color,
            Quaternion? rot = null, float smooth = 0.25f, string name = null)
        {
            var go = Visual(parent, shape, localPos, localScale, color, rot, smooth, name);
            AddCollider(go, shape, localScale);
            return go;
        }

        public static Collider AddCollider(GameObject go, Shape shape, Vector3 scale)
        {
            switch (shape)
            {
                case Shape.Box:
                    return go.AddComponent<BoxCollider>();
                case Shape.Sphere when Mathf.Approximately(scale.x, scale.y) && Mathf.Approximately(scale.y, scale.z):
                    return go.AddComponent<SphereCollider>();
                default:
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = GetCollider(shape);
                    mc.convex = true;
                    return mc;
            }
        }
    }
}
