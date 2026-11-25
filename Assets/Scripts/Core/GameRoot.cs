using UnityEngine;

namespace CatMart
{
    /// <summary>
    /// 场景入口：放在场景里的唯一物体上。运行时程序化搭建整个第一章。
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        void Awake()
        {
            Application.targetFrameRate = 120;
            Time.fixedDeltaTime = 0.01f;
            Physics.defaultSolverIterations = 12;
            Physics.defaultSolverVelocityIterations = 4;
            Physics.bounceThreshold = 1f;
            Physics.defaultContactOffset = 0.004f;

            // 去掉场景里默认的相机和灯
            foreach (var c in FindObjectsByType<Camera>()) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>()) Destroy(l.gameObject);

            Shapes.ApplyStyle();
            Sfx.Init(gameObject);
            gameObject.AddComponent<BgmPlayer>();

            var store = StoreBuilder.Build();
            StoreBuilder.SetupLighting(store.Root);

            // 预生成全部商品外观（包装贴图烘焙 + 网格），避免游戏中卡顿
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ItemLooks.WarmAll();
            Debug.Log($"商品外观生成完成: {ItemCatalog.All.Count} 件, {sw.ElapsedMilliseconds} ms");

            // 玩家
            var player = new GameObject("玩家");
            player.transform.position = new Vector3(Layout.PlayerStart.x, 0, Layout.PlayerStart.z);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0, Layout.PlayerStart.y, 0);
            camGo.transform.localRotation = Quaternion.Euler(35, 0, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 68;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.9f, 0.9f, 0.88f);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ItemOutlines>();   // 商品属性光边（在柔光之前，让光边也微微发光）
            if (ArtStyle.Cozy)
            {
                cam.backgroundColor = new Color(1f, 0.95f, 0.88f);
                camGo.AddComponent<CozyPost>();
            }

            var pc = player.AddComponent<PlayerController>();
            pc.Cam = cam;
            pc.Paws = PawRig.Build(cam);

            var gm = gameObject.AddComponent<GameManager>();
            gm.Init(store, pc);
            gameObject.AddComponent<HUD>();

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-catmartAutotest") >= 0)
                gameObject.AddComponent<DevAutoPilot>();
        }
    }
}
