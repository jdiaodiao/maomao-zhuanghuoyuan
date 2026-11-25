using UnityEngine;

namespace CatMart
{
    /// <summary>用简单几何体拼出的猫猫 NPC（拟人站立）。</summary>
    public class CatNpc : MonoBehaviour
    {
        public Transform Head, TailRoot, ArmL, ArmR;
        public TextMesh Speech;
        public Transform SpeechRoot;
        public Transform PatienceFill;
        public Transform PatienceRoot;
        public Renderer[] FurRenderers;
        public Transform MouthHappy, MouthSad;
        float _t;
        public float ScanAnim; // >0 时做扫描动作

        void Update()
        {
            _t += Time.deltaTime;
            if (TailRoot != null) TailRoot.localRotation = Quaternion.Euler(-30 + Mathf.Sin(_t * 1.7f) * 12f, Mathf.Sin(_t * 1.1f) * 25f, 0);
            if (Head != null) Head.localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.7f) * 3f, Mathf.Sin(_t * 0.5f) * 6f, Mathf.Sin(_t * 0.9f) * 2f);
            if (ArmR != null && ScanAnim > 0)
            {
                ScanAnim -= Time.deltaTime;
                ArmR.localRotation = Quaternion.Euler(-60 + Mathf.Sin(_t * 14f) * 20f, 0, 10);
            }
            else if (ArmR != null) ArmR.localRotation = Quaternion.Slerp(ArmR.localRotation, Quaternion.Euler(0, 0, 8), Time.deltaTime * 4);
        }

        public void SetFur(Color c)
        {
            if (FurRenderers == null) return;
            var m = Mats.Get(c, 0.1f);
            foreach (var r in FurRenderers) if (r != null) r.sharedMaterial = m;
        }

        public void SetPatience(float v)
        {
            if (PatienceFill == null) return;
            v = Mathf.Clamp01(v);
            PatienceFill.localScale = new Vector3(0.4f * v, 0.025f, 0.012f);
            PatienceFill.localPosition = new Vector3(-0.2f + 0.2f * v, 0, -0.003f);
            PatienceFill.GetComponent<Renderer>().sharedMaterial = Mats.Get(Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.3f, 0.9f, 0.35f), v), 0.3f);
        }

        public void SetMood(bool happy)
        {
            if (MouthHappy != null) MouthHappy.gameObject.SetActive(happy);
            if (MouthSad != null) MouthSad.gameObject.SetActive(!happy);
        }
    }

    public static class CatBuilder
    {
        static readonly Color White = U.Hex("#FAFAFA");
        static readonly Color Pink = U.Hex("#F4A6B8");
        static readonly Color Dark = U.Hex("#1A1A1A");

        public static CatNpc Build(Transform parent, Vector3 pos, float yaw, Color fur, Color clothes, bool withSpeech)
        {
            var root = new GameObject("猫猫NPC");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var t = root.transform;
            var npc = root.AddComponent<CatNpc>();
            var furList = new System.Collections.Generic.List<Renderer>();

            GameObject F(Transform p, Shape s, Vector3 lp, Vector3 sc, Quaternion? r = null)
            {
                var g = ProcMesh.Visual(p, s, lp, sc, fur, r, 0.1f);
                furList.Add(g.GetComponent<Renderer>());
                return g;
            }

            // 腿
            F(t, Shape.Cylinder, new Vector3(-0.1f, 0.22f, 0), new Vector3(0.12f, 0.44f, 0.12f));
            F(t, Shape.Cylinder, new Vector3(0.1f, 0.22f, 0), new Vector3(0.12f, 0.44f, 0.12f));
            ProcMesh.Visual(t, Shape.Sphere, new Vector3(-0.1f, 0.03f, 0.04f), new Vector3(0.13f, 0.07f, 0.18f), White);
            ProcMesh.Visual(t, Shape.Sphere, new Vector3(0.1f, 0.03f, 0.04f), new Vector3(0.13f, 0.07f, 0.18f), White);
            // 身体
            F(t, Shape.Sphere, new Vector3(0, 0.78f, 0), new Vector3(0.46f, 0.72f, 0.36f));
            ProcMesh.Visual(t, Shape.Sphere, new Vector3(0, 0.78f, 0.1f), new Vector3(0.3f, 0.5f, 0.2f), White); // 肚皮
            // 衣服（围裙/马甲）
            ProcMesh.Visual(t, Shape.Box, new Vector3(0, 0.72f, 0.155f), new Vector3(0.34f, 0.42f, 0.06f), clothes, null, 0.2f);
            // 手臂
            var armL = new GameObject("ArmL").transform; armL.SetParent(t, false); armL.localPosition = new Vector3(-0.24f, 1.0f, 0);
            var armR = new GameObject("ArmR").transform; armR.SetParent(t, false); armR.localPosition = new Vector3(0.24f, 1.0f, 0);
            armL.localRotation = Quaternion.Euler(0, 0, -8);
            armR.localRotation = Quaternion.Euler(0, 0, 8);
            F(armL, Shape.Cylinder, new Vector3(0, -0.18f, 0), new Vector3(0.1f, 0.36f, 0.1f));
            F(armR, Shape.Cylinder, new Vector3(0, -0.18f, 0), new Vector3(0.1f, 0.36f, 0.1f));
            ProcMesh.Visual(armL, Shape.Sphere, new Vector3(0, -0.38f, 0), new Vector3(0.12f, 0.1f, 0.12f), White);
            ProcMesh.Visual(armR, Shape.Sphere, new Vector3(0, -0.38f, 0), new Vector3(0.12f, 0.1f, 0.12f), White);
            npc.ArmL = armL; npc.ArmR = armR;

            // 头
            var head = new GameObject("Head").transform; head.SetParent(t, false); head.localPosition = new Vector3(0, 1.3f, 0);
            F(head, Shape.Sphere, Vector3.zero, new Vector3(0.42f, 0.36f, 0.36f));
            ProcMesh.Visual(head, Shape.Sphere, new Vector3(0, -0.06f, 0.14f), new Vector3(0.18f, 0.12f, 0.1f), White); // 嘴套
            ProcMesh.Visual(head, Shape.Sphere, new Vector3(0, -0.02f, 0.19f), new Vector3(0.04f, 0.03f, 0.03f), Pink);  // 鼻子
            for (int s = -1; s <= 1; s += 2)
            {
                F(head, Shape.Cone, new Vector3(s * 0.12f, 0.19f, 0), new Vector3(0.13f, 0.15f, 0.07f), Quaternion.Euler(0, 0, -s * 18));
                ProcMesh.Visual(head, Shape.Cone, new Vector3(s * 0.12f, 0.18f, 0.012f), new Vector3(0.08f, 0.1f, 0.04f), Pink, Quaternion.Euler(0, 0, -s * 18));
                ProcMesh.Visual(head, Shape.Sphere, new Vector3(s * 0.085f, 0.04f, 0.16f), new Vector3(0.06f, 0.075f, 0.04f), Dark, null, 0.9f);
                ProcMesh.Visual(head, Shape.Sphere, new Vector3(s * 0.08f, 0.055f, 0.18f), new Vector3(0.018f, 0.018f, 0.01f), Color.white, null, 0.9f);
                // 胡须
                for (int k = -1; k <= 1; k++)
                    ProcMesh.Visual(head, Shape.Box, new Vector3(s * 0.15f, -0.04f + k * 0.015f, 0.15f), new Vector3(0.12f, 0.002f, 0.002f), Dark, Quaternion.Euler(0, 0, s * k * 8));
            }
            npc.MouthHappy = ProcMesh.Visual(head, Shape.Box, new Vector3(0, -0.075f, 0.185f), new Vector3(0.05f, 0.008f, 0.01f), Dark, Quaternion.Euler(0, 0, 0)).transform;
            npc.MouthSad = ProcMesh.Visual(head, Shape.Box, new Vector3(0, -0.085f, 0.185f), new Vector3(0.05f, 0.008f, 0.01f), U.Hex("#B71C1C")).transform;
            npc.MouthSad.gameObject.SetActive(false);
            npc.Head = head;

            // 尾巴
            var tail = new GameObject("Tail").transform; tail.SetParent(t, false); tail.localPosition = new Vector3(0, 0.55f, -0.16f);
            Transform seg = tail;
            for (int i = 0; i < 5; i++)
            {
                var s = new GameObject("seg").transform; s.SetParent(seg, false);
                s.localPosition = i == 0 ? Vector3.zero : new Vector3(0, 0.1f, 0);
                s.localRotation = Quaternion.Euler(-12, 0, 0);
                F(s, Shape.Cylinder, new Vector3(0, 0.05f, 0), new Vector3(0.06f, 0.12f, 0.06f));
                seg = s;
            }
            npc.TailRoot = tail;
            npc.FurRenderers = furList.ToArray();

            if (withSpeech)
            {
                var sr = new GameObject("Speech").transform;
                sr.SetParent(t, false);
                sr.localPosition = new Vector3(0, 1.95f, 0);
                sr.gameObject.AddComponent<Billboard>();
                var bubble = new GameObject("Bubble").transform; bubble.SetParent(sr, false);
                ProcMesh.Visual(bubble, Shape.Box, Vector3.zero, new Vector3(0.95f, 0.2f, 0.01f), White, null, 0.1f);
                ProcMesh.Visual(bubble, Shape.Box, new Vector3(0, -0.11f, 0), new Vector3(0.04f, 0.04f, 0.01f), White, Quaternion.Euler(0, 0, 45));
                npc.Speech = Text3D.Create(sr, "", new Vector3(0, 0, -0.008f), 0.042f, Dark);
                npc.SpeechRoot = sr;

                var pr = new GameObject("Patience").transform; pr.SetParent(t, false);
                pr.localPosition = new Vector3(0, 1.66f, 0);
                pr.gameObject.AddComponent<Billboard>();
                var bg = ProcMesh.Visual(pr, Shape.Box, Vector3.zero, new Vector3(0.42f, 0.04f, 0.01f), U.Hex("#333333"));
                var fill = ProcMesh.Visual(pr, Shape.Box, new Vector3(0, 0, -0.003f), new Vector3(0.4f, 0.025f, 0.012f), Color.green);
                fill.transform.localRotation = Quaternion.identity;
                npc.PatienceFill = fill.transform;
                npc.PatienceRoot = pr;
                // Billboard 让 +z 背对相机，所以把填充放在 -z 一侧前面
                bg.transform.localRotation = Quaternion.identity;
            }
            return npc;
        }
    }
}
