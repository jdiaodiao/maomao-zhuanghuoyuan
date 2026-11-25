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
            AssetDatabase.SaveAssets();
        }

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
