using UnityEngine;
using UnityEngine.SceneManagement;

public static class DungeonTestBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateVerticalSlice()
    {
        if (SceneManager.GetActiveScene().name != "Dungeon_Test" || Object.FindAnyObjectByType<DungeonRunController>() != null)
            return;

        new GameObject("Dungeon Test Demo").AddComponent<DungeonRunController>();
    }
}
