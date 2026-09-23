using UnityEngine;
using UnityEngine.SceneManagement;

public static class DungeonTestBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateVerticalSlice()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        GameFlowConfig flow = GameFlowConfig.Active;
        if ((sceneName != flow.dungeonTestSceneName && sceneName != flow.dungeonSceneName) || Object.FindAnyObjectByType<DungeonRunController>() != null)
            return;

        new GameObject("Dungeon Test Demo").AddComponent<DungeonRunController>();
    }
}
