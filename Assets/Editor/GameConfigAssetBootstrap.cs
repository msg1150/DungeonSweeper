#if UNITY_EDITOR
using UnityEditor;
using System.IO;
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
        Ensure<TownEconomyConfig>("Assets/Resources/TownEconomyConfig.asset");
    }

    private static void Ensure<T>(string path) where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return;
        // GUID·타입이 손상된 기존 에셋도 자동 생성으로 덮어쓰지 않는다.
        if (File.Exists(path)) { Debug.LogError("Existing config has an unexpected type; preserving the asset: " + path); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        AssetDatabase.SaveAssets();
    }
}
#endif
