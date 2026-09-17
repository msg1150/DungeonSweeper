using UnityEngine;
using UnityEngine.SceneManagement;

public static class DungeonTestBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateVerticalSlice()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if ((sceneName != "Dungeon_Test" && sceneName != "Dungeon") || Object.FindAnyObjectByType<DungeonRunController>() != null)
            return;

        new GameObject("Dungeon Test Demo").AddComponent<DungeonRunController>();
    }
}
