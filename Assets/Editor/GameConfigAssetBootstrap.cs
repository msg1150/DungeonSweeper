#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GameConfigAssetBootstrap
{
    static GameConfigAssetBootstrap() => EditorApplication.delayCall += EnsureAssets;

    private static void EnsureAssets()
    {
        Ensure<GameFlowConfig>("Assets/Resources/GameFlowConfig.asset");
        Ensure<PlayerVisualProfile>("Assets/Resources/PlayerVisualProfile.asset");
        Ensure<MainMenuPresentationConfig>("Assets/Resources/MainMenuPresentation.asset");
    }

    private static void Ensure<T>(string path) where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        AssetDatabase.SaveAssets();
    }
}
#endif
