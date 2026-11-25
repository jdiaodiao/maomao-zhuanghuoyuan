using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CatMart.EditorTools
{
    public static class CatMartEditorTools
    {
        const string ScenePath = "Assets/Scenes/Chapter1_CatMart.unity";
        const string ResDir = "Assets/Resources/CatMart";

        [MenuItem("CatMart/创建第一章场景")]
        public static void CreateScene()
        {
            EnsureMaterials();
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("CatMart Game").AddComponent<GameRoot>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("CatMart: 场景已创建 " + ScenePath);
        }

        /// <summary>保证打包时 Standard 着色器（含自发光变体）被包含。</summary>
        public static void EnsureMaterials()
        {
            Directory.CreateDirectory(ResDir);
            var shader = Shader.Find("Standard");
            if (shader == null) { Debug.LogWarning("找不到 Standard 着色器"); return; }
            string basePath = ResDir + "/BaseMat.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(basePath) == null)
                AssetDatabase.CreateAsset(new Material(shader) { color = Color.white }, basePath);
            string emPath = ResDir + "/EmissiveMat.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(emPath) == null)
            {
                var m = new Material(shader) { color = Color.white };
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                AssetDatabase.CreateAsset(m, emPath);
            }

            // 法线贴图
            Create("NormalMat", shader, m =>
            {
                m.EnableKeyword("_NORMALMAP");
            });
            // 透明（玻璃/塑料膜）
            Create("GlassMat", shader, m => SetupTransparent(m));
            Create("GlassNormalMat", shader, m => { SetupTransparent(m); m.EnableKeyword("_NORMALMAP"); });
            // 镂空（网袋）
            Create("CutoutMat", shader, m =>
            {
                m.SetFloat("_Mode", 1);
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                m.SetInt("_ZWrite", 1);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cutoff", 0.5f);
                m.renderQueue = 2450;
            });
            // 无光照（包装贴图烘焙用）
            Create("UnlitColorMat", Shader.Find("Unlit/Color"), null);
            Create("UnlitTexMat", Shader.Find("Unlit/Texture"), null);
            // 森友会风卡通着色 + 后期
            Create("CozyMat", Shader.Find("CatMart/CozyToon"), null);
            Create("CozyTransparentMat", Shader.Find("CatMart/CozyToonTransparent"), null);
            Create("CozyCutoutMat", Shader.Find("CatMart/CozyToonCutout"), null);
            Create("CozyPostMat", Shader.Find("Hidden/CatMart/CozyPost"), null);
            Create("WorldTextMat", Shader.Find("CatMart/WorldText"), null);
            Create("ItemOutlineMat", Shader.Find("Hidden/CatMart/ItemOutline"), null);
            AssetDatabase.SaveAssets();
        }

        static void Create(string name, Shader shader, System.Action<Material> setup)
        {
            string path = ResDir + "/" + name + ".mat";
            if (shader == null || AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var m = new Material(shader) { color = Color.white };
            setup?.Invoke(m);
            AssetDatabase.CreateAsset(m, path);
        }

        static void SetupTransparent(Material m)
        {
            m.SetFloat("_Mode", 3);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        [MenuItem("CatMart/重建材质模板")]
        public static void EnsureMaterialsMenu() => EnsureMaterials();

        static void ConfigurePlayer()
        {
            PlayerSettings.runInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
        }

        [MenuItem("CatMart/打包 Windows 版")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            EnsureMaterials();
            ConfigurePlayer();
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Windows/猫猫装货员.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log("CatMart: 打包结果 " + report.summary.result + " -> " + report.summary.outputPath);
            if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
