using System.Collections.Generic;
using UnityEngine;

namespace CatMart
{
    /// <summary>猫猫 NPC（顾客、收银员）。</summary>
    public class CatNpc : MonoBehaviour
    {
        public Transform Head, TailRoot, ArmL, ArmR;
        public TextMesh Speech;
        public Transform SpeechRoot;
        public Transform PatienceFill;
        public Transform PatienceRoot;
        public Renderer[] FurRenderers;
        public Transform MouthHappy, MouthSad;
        public Transform[] Eyelids;
        float _t, _blink = 2f;
        public float ScanAnim; // >0 时做扫描动作

        void Update()
        {
            _t += Time.deltaTime;
            if (TailRoot != null) TailRoot.localRotation = Quaternion.Euler(-20 + Mathf.Sin(_t * 1.7f) * 10f, Mathf.Sin(_t * 1.1f) * 28f, 0);
            if (Head != null) Head.localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.7f) * 3f, Mathf.Sin(_t * 0.5f) * 7f, Mathf.Sin(_t * 0.9f) * 3f);
            // 呼吸
            transform.localScale = new Vector3(1, 1 + Mathf.Sin(_t * 2.2f) * 0.006f, 1);
            if (ArmR != null && ScanAnim > 0)
            {
                ScanAnim -= Time.deltaTime;
                ArmR.localRotation = Quaternion.Euler(-60 + Mathf.Sin(_t * 14f) * 20f, 0, 10);
            }
            else if (ArmR != null) ArmR.localRotation = Quaternion.Slerp(ArmR.localRotation, Quaternion.Euler(0, 0, 8), Time.deltaTime * 4);
            // 眨眼
            _blink -= Time.deltaTime;
            bool closed = _blink < 0.12f;
            if (_blink < 0) _blink = 2f + Random.value * 3f;
            if (Eyelids != null) foreach (var e in Eyelids) if (e != null) e.gameObject.SetActive(closed);
        }

        public void SetFur(Color c)
        {
            if (FurRenderers == null) return;
            var m = CatBuilder.FurMat(c);
            foreach (var r in FurRenderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                mats[0] = m;
                r.sharedMaterials = mats;
            }
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

    /// <summary>用 SDF 雕出的猫：身体、头、手臂、尾巴分开（方便动画）。</summary>
    public static class CatBuilder
    {
        static readonly Color White = U.Hex("#FBF8F2");
        static readonly Color Pink = U.Hex("#F2A0B2");
        static readonly Color Dark = U.Hex("#141414");

        public static Material FurMat(Color c) => Mats.Lit(c, 0.22f, 0, TexGen.Fur, TexGen.FurN, 0.7f, new Vector2(1, 1));
        static Material WhiteMat => Mats.Lit(White, 0.22f, 0, TexGen.Fur, TexGen.FurN, 0.6f);
        static Material PinkMat => Mats.Lit(Pink, 0.5f, 0);

        // 网格只建一次，颜色通过材质替换
        static MB[] _body, _head, _arm;
        public static void ClearCache() { _body = null; _head = null; _arm = null; _tail = null; _headSdf = null; }
        static MB _tail;
        static Sdf _headSdf;

        /// <summary>头部表面在 (x,y) 处的 z（从前方往后找），五官贴在这里，不会埋进头里。</summary>
        static float FaceZ(float x, float y, float fallback)
        {
            if (_headSdf == null) return fallback;
            for (float z = 0.35f; z > 0; z -= 0.002f)
                if (_headSdf.Eval(new Vector3(x, y, z)) <= 0) return z;
            return fallback;
        }

        static void EnsureMeshes()
        {
            if (_body != null) return;
            // ---- 身体（0=毛色 1=白 2=粉）
            var b = new Sdf { DefaultBlend = 0.05f };
            b.Ellipsoid(new Vector3(0, 0.82f, 0), new Vector3(0.22f, 0.32f, 0.18f));
            b.Ellipsoid(new Vector3(0, 0.55f, -0.01f), new Vector3(0.21f, 0.2f, 0.18f));
            b.Capsule(new Vector3(0, 1.0f, 0), new Vector3(0, 1.18f, 0.01f), 0.11f, 0.09f);
            foreach (int s in new[] { -1, 1 })
            {
                b.Capsule(new Vector3(s * 0.1f, 0.48f, 0), new Vector3(s * 0.1f, 0.1f, 0.01f), 0.085f, 0.06f, 0, 0.04f);
                b.Ellipsoid(new Vector3(s * 0.1f, 0.045f, 0.05f), new Vector3(0.07f, 0.045f, 0.11f), 1, 0.03f);
            }
            b.Ellipsoid(new Vector3(0, 0.76f, 0.1f), new Vector3(0.14f, 0.26f, 0.08f), 1, 0.03f);
            _body = b.Mesh(new Bounds(new Vector3(0, 0.62f, 0), new Vector3(0.52f, 1.26f, 0.44f)), 0.011f, 3, 6);

            // ---- 头（原点在脖子上方）
            if (ArtStyle.Cozy)
            {
                // 森友会风：圆滚滚的头、小小的嘴、大而圆的耳朵
                var ch = new Sdf { DefaultBlend = 0.04f };
                ch.Ellipsoid(Vector3.zero, new Vector3(0.2f, 0.175f, 0.18f));
                ch.Ellipsoid(new Vector3(0, -0.04f, 0.03f), new Vector3(0.19f, 0.14f, 0.16f));
                foreach (int s in new[] { -1, 1 })
                {
                    ch.Capsule(new Vector3(s * 0.1f, 0.09f, -0.01f), new Vector3(s * 0.16f, 0.27f, -0.02f), 0.085f, 0.022f, 0, 0.035f);
                    ch.Ellipsoid(new Vector3(s * 0.13f, 0.19f, 0.035f), new Vector3(0.04f, 0.065f, 0.015f), 2, 0.004f, Quaternion.Euler(0, 0, -s * 16));
                    ch.Ellipsoid(new Vector3(s * 0.025f, -0.068f, 0.175f), new Vector3(0.032f, 0.026f, 0.026f), 1, 0.012f);
                }
                ch.Ellipsoid(new Vector3(0, -0.045f, 0.19f), new Vector3(0.018f, 0.013f, 0.012f), 2, 0.006f);
                _head = ch.Mesh(new Bounds(new Vector3(0, 0.04f, 0.02f), new Vector3(0.5f, 0.52f, 0.44f)), 0.0042f, 3, 10);
                _headSdf = ch;
            }
            else
            {
            var h = new Sdf { DefaultBlend = 0.035f };
            h.Ellipsoid(Vector3.zero, new Vector3(0.21f, 0.175f, 0.18f));
            h.Sphere(new Vector3(-0.1f, -0.05f, 0.06f), 0.105f);
            h.Sphere(new Vector3(0.1f, -0.05f, 0.06f), 0.105f);
            foreach (int s in new[] { -1, 1 })
            {
                h.Capsule(new Vector3(s * 0.1f, 0.1f, -0.01f), new Vector3(s * 0.15f, 0.28f, -0.02f), 0.075f, 0.01f, 0, 0.03f);
                h.Ellipsoid(new Vector3(s * 0.125f, 0.19f, 0.03f), new Vector3(0.035f, 0.06f, 0.015f), 2, 0.005f, Quaternion.Euler(0, 0, -s * 14));
                h.Ellipsoid(new Vector3(s * 0.045f, -0.06f, 0.16f), new Vector3(0.055f, 0.045f, 0.045f), 1, 0.02f);
            }
            h.Ellipsoid(new Vector3(0, -0.1f, 0.14f), new Vector3(0.05f, 0.035f, 0.04f), 1, 0.02f);
            h.Ellipsoid(new Vector3(0, -0.02f, 0.185f), new Vector3(0.022f, 0.016f, 0.014f), 2, 0.008f);
            _head = h.Mesh(new Bounds(new Vector3(0, 0.05f, 0.02f), new Vector3(0.5f, 0.52f, 0.44f)), 0.0065f, 3, 10);
            }

            // ---- 手臂（原点在肩膀，向下）
            var a = new Sdf { DefaultBlend = 0.03f };
            a.Capsule(Vector3.zero, new Vector3(0, -0.32f, 0.03f), 0.065f, 0.05f);
            a.Ellipsoid(new Vector3(0, -0.37f, 0.035f), new Vector3(0.06f, 0.055f, 0.065f), 1, 0.025f);
            _arm = a.Mesh(new Bounds(new Vector3(0, -0.2f, 0.02f), new Vector3(0.18f, 0.55f, 0.2f)), 0.008f, 2, 8);

            // ---- 尾巴
            var path = new List<Vector3>(); var rad = new List<float>();
            for (int k = 0; k <= 16; k++)
            {
                float t = k / 16f;
                path.Add(new Vector3(Mathf.Sin(t * 2.2f) * 0.08f, t * 0.55f, -t * 0.12f - Mathf.Sin(t * 3f) * 0.05f));
                rad.Add(Mathf.Lerp(0.045f, 0.03f, t) * (t > 0.92f ? Mathf.Lerp(1, 0.4f, (t - 0.92f) / 0.08f) : 1));
            }
            _tail = Shapes.Tube(path, rad, 10, true);
        }

        static GameObject Piece(Transform parent, string name, MB[] regions, Material fur, Vector3 pos, List<Renderer> furList)
        {
            var v = new VisualSet();
            Material[] mats = { fur, WhiteMat, PinkMat };
            for (int r = 0; r < regions.Length; r++) if (regions[r].V.Count > 0) v.Add(regions[r], mats[r]);
            var go = v.Spawn(parent, name, pos);
            furList.Add(go.GetComponent<Renderer>());
            return go;
        }

        public static CatNpc Build(Transform parent, Vector3 pos, float yaw, Color fur, Color clothes, bool withSpeech)
        {
            EnsureMeshes();
            var root = new GameObject("猫猫NPC");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var t = root.transform;
            var npc = root.AddComponent<CatNpc>();
            var furList = new List<Renderer>();
            var furMat = FurMat(fur);

            // 比例：森友会风是大头小身体（Q 版）
            bool cozy = ArtStyle.Cozy;
            float bodyScale = cozy ? 0.75f : 1f, headScale = cozy ? 1.45f : 1f;
            var torso = new GameObject("Torso").transform;
            torso.SetParent(t, false);
            torso.localScale = Vector3.one * bodyScale;
            Piece(torso, "身体", _body, furMat, Vector3.zero, furList);

            // 围裙
            var apron = Shapes.RoundedBox(new Vector3(0.3f, 0.44f, 0.02f), 0.008f, 2, 4);
            apron.Displace((p, n) => new Vector3(p.x, p.y, p.z - p.x * p.x * 2.2f + (p.y < 0 ? p.y * p.y * 0.3f : 0)));
            var av = new VisualSet();
            var clothMat = Mats.Lit(clothes, 0.2f, 0, TexGen.Kraft, TexGen.KraftN, 0.4f, new Vector2(4, 4));
            av.Add(apron, new Vector3(0, 0.74f, 0.195f), Quaternion.Euler(-6, 0, 0), clothMat);
            var strap = new List<Vector3>(); var sr = new List<float>();
            for (int k = 0; k <= 12; k++) { float a = Mathf.PI * k / 12f; strap.Add(new Vector3(Mathf.Cos(a) * 0.12f, 0.96f + Mathf.Sin(a) * 0.18f, 0.13f - Mathf.Sin(a) * 0.12f)); sr.Add(0.008f); }
            av.Add(Shapes.Tube(strap, sr, 6), clothMat);
            var badge = new LabelStyle { Brand = "CatMart", Title = withSpeech ? "顾客" : "收银员 橘座", Bg = Color.white, Bg2 = U.Hex("#F0F0F0"), Accent = U.Hex("#F28C28"), Ink = U.Hex("#24364B"), Icon = LabelIcon.Cat, IconColor = U.Hex("#F28C28"), Seed = 1 };
            if (!withSpeech)
                av.Add(Shapes.Quad(0.09f, 0.05f), new Vector3(0.07f, 0.86f, 0.218f), Quaternion.Euler(6, 180, 0), Mats.Label(LabelBaker.Sticker(new Vector2(0.09f, 0.05f), badge, true)));
            av.Spawn(torso, "围裙");

            // 手臂
            var armL = new GameObject("ArmL").transform; armL.SetParent(torso, false); armL.localPosition = new Vector3(-0.2f, 1.04f, 0.0f);
            var armR = new GameObject("ArmR").transform; armR.SetParent(torso, false); armR.localPosition = new Vector3(0.2f, 1.04f, 0.0f);
            armL.localRotation = Quaternion.Euler(0, 0, -8);
            armR.localRotation = Quaternion.Euler(0, 0, 8);
            Piece(armL, "左臂", _arm, furMat, Vector3.zero, furList);
            Piece(armR, "右臂", _arm, furMat, Vector3.zero, furList);
            npc.ArmL = armL; npc.ArmR = armR;

            // 头
            var head = new GameObject("Head").transform; head.SetParent(t, false);
            head.localPosition = cozy ? new Vector3(0, 1.18f * bodyScale + 0.2f, 0.01f) : new Vector3(0, 1.32f, 0.01f);
            head.localScale = Vector3.one * headScale;
            Piece(head, "头", _head, furMat, Vector3.zero, furList);
            var fv = new VisualSet();
            var eyeBlack = Mats.Lit(Dark, 0.95f);
            var iris = Mats.Lit(withSpeech ? U.Hex("#7CB342") : U.Hex("#F9A825"), 0.9f);
            var lidList = new List<Transform>();
            foreach (int s in new[] { -1, 1 })
            {
                Vector3 e = cozy ? new Vector3(s * 0.078f, 0.0f, FaceZ(s * 0.078f, 0f, 0.158f) - 0.005f) : new Vector3(s * 0.078f, 0.035f, 0.152f);
                if (cozy)
                {
                    // 森友会风：黑豆豆眼 + 大高光 + 腮红
                    fv.Add(Shapes.Ellipsoid(new Vector3(0.05f, 0.068f, 0.034f)), e, Quaternion.Euler(0, s * 20, 0), eyeBlack);
                    fv.Add(Shapes.Ellipsoid(new Vector3(0.02f, 0.022f, 0.01f)), e + new Vector3(s * 0.006f + 0.006f, 0.016f, 0.016f), Quaternion.identity, Mats.Emissive(Color.white, new Color(0.7f, 0.7f, 0.7f)));
                    fv.Add(Shapes.Ellipsoid(new Vector3(0.055f, 0.03f, 0.016f)), new Vector3(s * 0.118f, -0.05f, FaceZ(s * 0.118f, -0.05f, 0.146f) - 0.003f), Quaternion.Euler(0, s * 32, 0), Mats.Lit(U.Hex("#FF8FAB"), 0.3f));
                }
                else
                {
                    fv.Add(Shapes.Ellipsoid(new Vector3(0.062f, 0.07f, 0.04f)), e, Quaternion.Euler(0, s * 18, 0), iris);
                    fv.Add(Shapes.Ellipsoid(new Vector3(0.026f, 0.06f, 0.02f)), e + new Vector3(0, 0, 0.012f), Quaternion.Euler(0, s * 18, 0), eyeBlack);
                    fv.Add(Shapes.Ellipsoid(new Vector3(0.016f, 0.016f, 0.008f)), e + new Vector3(s * 0.008f + 0.006f, 0.016f, 0.02f), Quaternion.identity, Mats.Emissive(Color.white, new Color(0.6f, 0.6f, 0.6f)));
                }
                // 胡须（森友会风没有）
                for (int k = -1; k <= 1 && !cozy; k++)
                {
                    var w = new List<Vector3> { new Vector3(s * 0.06f, -0.045f + k * 0.012f, 0.18f), new Vector3(s * 0.15f, -0.04f + k * 0.022f, 0.17f), new Vector3(s * 0.22f, -0.045f + k * 0.04f, 0.14f) };
                    fv.Add(Shapes.Tube(w, new[] { 0.0018f, 0.0012f, 0.0006f }, 4), Mats.Lit(Color.white, 0.6f));
                }
                var lid = new VisualSet();
                lid.Add(Shapes.Ellipsoid(cozy ? new Vector3(0.056f, 0.074f, 0.04f) : new Vector3(0.068f, 0.076f, 0.044f)), Vector3.zero, Quaternion.Euler(0, s * 18, 0), furMat);
                var lidGo = lid.Spawn(head, "眼皮", e + new Vector3(0, 0, 0.002f));
                lidGo.SetActive(false);
                furList.Add(lidGo.GetComponent<Renderer>());
                lidList.Add(lidGo.transform);
            }
            fv.Spawn(head, "五官");
            npc.Eyelids = lidList.ToArray();
            // 嘴（开心 ω / 不开心 ︵）
            var happy = new VisualSet();
            var mouthPath = new List<Vector3>(); var mr = new List<float>();
            for (int k = 0; k <= 10; k++)
            {
                float u = k / 10f * 2 - 1;
                mouthPath.Add(new Vector3(u * (cozy ? 0.024f : 0.04f), (cozy ? -0.093f : -0.085f) - Mathf.Abs(Mathf.Sin(u * Mathf.PI)) * (cozy ? 0.008f : 0.012f), (cozy ? FaceZ(u * 0.024f, -0.093f, 0.172f) + 0.002f : 0.183f - u * u * 0.008f)));
                mr.Add(0.0025f);
            }
            happy.Add(Shapes.Tube(mouthPath, mr, 5), Mats.Lit(U.Hex("#4A2A2A"), 0.5f));
            npc.MouthHappy = happy.Spawn(head, "嘴_开心").transform;
            var sad = new VisualSet();
            var sp = new List<Vector3>();
            for (int k = 0; k <= 10; k++) { float u = k / 10f * 2 - 1; sp.Add(new Vector3(u * 0.035f, -0.1f + (1 - u * u) * 0.012f, 0.18f - u * u * 0.01f)); }
            sad.Add(Shapes.Tube(sp, mr, 5), Mats.Lit(U.Hex("#8B1A1A"), 0.5f));
            npc.MouthSad = sad.Spawn(head, "嘴_生气").transform;
            npc.MouthSad.gameObject.SetActive(false);
            npc.Head = head;

            // 尾巴
            var tail = new GameObject("Tail").transform; tail.SetParent(torso, false); tail.localPosition = new Vector3(0, 0.5f, -0.16f);
            var tv = new VisualSet();
            tv.Add(_tail, furMat);
            var tailGo = tv.Spawn(tail, "尾巴");
            furList.Add(tailGo.GetComponent<Renderer>());
            npc.TailRoot = tail;
            npc.FurRenderers = furList.ToArray();

            if (withSpeech)
            {
                var srt = new GameObject("Speech").transform;
                srt.SetParent(t, false);
                srt.localPosition = new Vector3(0, cozy ? 1.62f : 1.98f, 0);
                srt.gameObject.AddComponent<Billboard>();
                var bubble = new VisualSet();
                bubble.Add(Shapes.RoundedBox(new Vector3(0.98f, 0.22f, 0.02f), 0.05f, 4, 2), Mats.Lit(Color.white, 0.4f));
                bubble.Add(Shapes.Prism(Shapes.EnsureCCW(new[] { new Vector2(-0.03f, 0), new Vector2(0.03f, 0), new Vector2(0, -0.06f) }), 0.02f), new Vector3(0, -0.105f, 0.01f), Quaternion.Euler(-90, 0, 0), Mats.Lit(Color.white, 0.4f));
                bubble.Spawn(srt, "气泡");
                npc.Speech = Text3D.Create(srt, "", new Vector3(0, 0, -0.012f), 0.042f, Dark);
                npc.SpeechRoot = srt;

                var pr = new GameObject("Patience").transform; pr.SetParent(t, false);
                pr.localPosition = new Vector3(0, cozy ? 1.44f : 1.73f, 0);
                pr.gameObject.AddComponent<Billboard>();
                ProcMesh.Visual(pr, Shape.Box, Vector3.zero, new Vector3(0.42f, 0.04f, 0.01f), U.Hex("#333333"));
                var fill = ProcMesh.Visual(pr, Shape.Box, new Vector3(0, 0, -0.003f), new Vector3(0.4f, 0.025f, 0.012f), Color.green);
                npc.PatienceFill = fill.transform;
                npc.PatienceRoot = pr;
            }
            return npc;
        }
    }
}
