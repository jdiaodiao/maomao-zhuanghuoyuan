using UnityEditor;
using UnityEditor.SceneManagement;

namespace CatMart.EditorTools
{
    /// <summary>打开项目时，如果当前是空场景，自动打开第一章场景。</summary>
    [InitializeOnLoad]
    static class OpenChapterSceneOnLoad
    {
        const string ScenePath = "Assets/Scenes/Chapter1_CatMart.unity";

        static OpenChapterSceneOnLoad()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                var active = EditorSceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(active.path) && System.IO.File.Exists(ScenePath))
                    EditorSceneManager.OpenScene(ScenePath);
            };
        }
    }
}
