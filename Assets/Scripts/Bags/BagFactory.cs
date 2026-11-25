using UnityEngine;

namespace CatMart
{
    public static class BagFactory
    {
        public static readonly Color Kraft = U.Hex("#B98D5A");
        public static readonly Color KraftDark = U.Hex("#9C7447");
        public static readonly Color PlasticWhite = U.Hex("#EEF2F4");
        public static readonly Color PlasticBlue = U.Hex("#4A90D9");
        public static readonly Color Brand = U.Hex("#F28C28");

        /// <summary>生成袋子，position 为袋底中心。</summary>
        public static Bag Create(BagSpec spec, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(spec.Name);
            root.transform.SetPositionAndRotation(position, rotation);
            var t = root.transform;

            float w = spec.Inner.x, h = spec.Inner.y, d = spec.Inner.z;
            bool paper = spec.Material == BagMaterial.Paper;
            Color wall = paper ? Kraft : PlasticWhite;
            float vis = paper ? 0.004f : 0.002f;  // 视觉厚度
            const float col = 0.02f;              // 碰撞厚度（向外）

            // 底
            Part(t, new Vector3(0, -vis / 2, 0), new Vector3(w + vis * 2, vis, d + vis * 2), wall, new Vector3(0, -col / 2, 0), new Vector3(w + col * 2, col, d + col * 2));
            // 四壁
            Part(t, new Vector3(-(w + vis) / 2, h / 2, 0), new Vector3(vis, h, d), wall, new Vector3(-(w + col) / 2, h / 2, 0), new Vector3(col, h, d + col * 2));
            Part(t, new Vector3((w + vis) / 2, h / 2, 0), new Vector3(vis, h, d), wall, new Vector3((w + col) / 2, h / 2, 0), new Vector3(col, h, d + col * 2));
            Part(t, new Vector3(0, h / 2, (d + vis) / 2), new Vector3(w + vis * 2, h, vis), wall, new Vector3(0, h / 2, (d + col) / 2), new Vector3(w + col * 2, h, col));
            Part(t, new Vector3(0, h / 2, -(d + vis) / 2), new Vector3(w + vis * 2, h, vis), wall, new Vector3(0, h / 2, -(d + col) / 2), new Vector3(w + col * 2, h, col));

            if (paper)
            {
                // 折边
                ProcMesh.Visual(t, Shape.Box, new Vector3(0, h - 0.015f, -(d / 2 + vis + 0.001f)), new Vector3(w + vis * 2, 0.03f, 0.002f), KraftDark);
                ProcMesh.Visual(t, Shape.Box, new Vector3(0, h - 0.015f, d / 2 + vis + 0.001f), new Vector3(w + vis * 2, 0.03f, 0.002f), KraftDark);
                // 前面的 logo
                ProcMesh.Visual(t, Shape.Sphere, new Vector3(0, h * 0.55f, -(d / 2 + vis + 0.002f)), new Vector3(w * 0.35f, w * 0.35f, 0.002f), Brand);
            }
            else
            {
                // 提手：两组弧形
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * w * 0.42f;
                    for (int i = 0; i < 7; i++)
                    {
                        float a = Mathf.Lerp(0, Mathf.PI, i / 6f);
                        float r = w * 0.18f;
                        Vector3 p = new Vector3(x, h + Mathf.Sin(a) * r * 0.5f, Mathf.Cos(a) * d * 0.45f);
                        ProcMesh.Visual(t, Shape.Sphere, p, new Vector3(0.012f, 0.012f, 0.03f), PlasticWhite, Quaternion.Euler(-Mathf.Rad2Deg * a + 90, 0, 0));
                    }
                }
                ProcMesh.Visual(t, Shape.Box, new Vector3(0, h * 0.55f, -(d / 2 + vis + 0.0015f)), new Vector3(w * 0.6f, h * 0.18f, 0.001f), PlasticBlue);
            }

            // 尺寸标签
            var tm = Text3D.Create(t, $"{spec.SizeLetter}", new Vector3(0, h * 0.82f, -(d / 2 + vis + 0.004f)), 0.05f,
                paper ? new Color(0.25f, 0.15f, 0.05f) : PlasticBlue, TextAnchor.MiddleCenter, Quaternion.identity);
            tm.transform.localRotation = Quaternion.identity;
            if (!paper)
                Text3D.Create(t, "CatMart", new Vector3(0, h * 0.55f, -(d / 2 + vis + 0.003f)), 0.028f, Color.white);

            // 重心标记（底部）
            var marker = ProcMesh.Visual(t, Shape.Cylinder, new Vector3(0, 0.004f, 0), new Vector3(0.025f, 0.003f, 0.025f), Color.green, null, 0.6f, "重心标记");
            // 中心十字
            ProcMesh.Visual(t, Shape.Box, new Vector3(0, 0.001f, 0), new Vector3(w * 0.3f, 0.001f, 0.003f), new Color(0, 0, 0, 1) * 0.4f);
            ProcMesh.Visual(t, Shape.Box, new Vector3(0, 0.001f, 0), new Vector3(0.003f, 0.001f, d * 0.3f), new Color(0, 0, 0, 1) * 0.4f);

            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var bag = root.AddComponent<Bag>();
            bag.Spec = spec;
            bag.ComMarker = marker.transform;
            return bag;
        }

        static void Part(Transform parent, Vector3 visPos, Vector3 visSize, Color c, Vector3 colPos, Vector3 colSize)
        {
            var v = ProcMesh.Visual(parent, Shape.Box, visPos, visSize, c, null, 0.15f, "袋壁");
            var colGo = new GameObject("袋壁碰撞");
            colGo.transform.SetParent(parent, false);
            colGo.transform.localPosition = colPos;
            var bc = colGo.AddComponent<BoxCollider>();
            bc.size = colSize;
        }

        /// <summary>袋架上的一叠袋子。</summary>
        public static GameObject CreateRackStack(Transform parent, BagSpec spec, Vector3 localPos)
        {
            var root = new GameObject("袋架_" + spec.Name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            bool paper = spec.Material == BagMaterial.Paper;
            float scale = spec.Size == BagSize.Small ? 0.7f : spec.Size == BagSize.Medium ? 0.85f : 1f;
            Vector3 size = new Vector3(0.12f * scale, 0.05f, 0.2f * scale);
            var stack = ProcMesh.Solid(root.transform, Shape.Box, new Vector3(0, size.y / 2, 0), size, paper ? Kraft : PlasticWhite, null, 0.1f, "袋堆");
            for (int i = 1; i < 5; i++)
                ProcMesh.Visual(root.transform, Shape.Box, new Vector3(0, i * 0.01f, 0), new Vector3(size.x + 0.002f, 0.001f, size.z + 0.002f),
                    paper ? KraftDark : U.Hex("#C9D3D8"));
            stack.AddComponent<BagRackButton>().Spec = spec;
            Text3D.Create(root.transform, $"{spec.SizeName}\n{spec.MaterialName}", new Vector3(0, 0.052f, 0), 0.022f,
                paper ? new Color(0.3f, 0.18f, 0.05f) : PlasticBlue, TextAnchor.MiddleCenter, Quaternion.Euler(90, 0, 0));
            return root;
        }
    }
}
