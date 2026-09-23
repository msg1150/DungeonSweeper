#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Town 씬을 열면 Play 전에도 런타임과 동일한 캐주얼 비주얼을 미리 보여준다.</summary>
[InitializeOnLoad]
public static class TownCasualPreview
{
    static TownCasualPreview()
    {
        EditorApplication.delayCall += ApplyIfTown;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.name == "Town") EditorApplication.delayCall += ApplyIfTown;
    }

    private static void ApplyIfTown()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (SceneManager.GetActiveScene().name == "Town") TownCasualVisuals.Apply();
    }
}
#endif
