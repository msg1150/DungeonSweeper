using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Coordinates new games, selected saves, checkpoints, and scene restoration.</summary>
public static class GameSession
{
    public static bool HasActiveGame { get; private set; }
    public static bool IsLoading { get; private set; }
    public static bool AutosavePending { get; private set; }
    public static double PlaySeconds { get; private set; }
    private static DungeonSaveData pendingDungeon;
    private static DungeonDefinition pendingDefinition;
    private static bool restoreTownPosition, hasTownPosition, saveAfterSceneLoad;
    private static Vector2 townPosition;
    private static readonly List<string> completedEvents = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        HasActiveGame = IsLoading = AutosavePending = restoreTownPosition = hasTownPosition = saveAfterSceneLoad = false;
        PlaySeconds = 0;
        pendingDungeon = null;
        pendingDefinition = null;
        completedEvents.Clear();
    }

    public static bool StartNewGame(out string error)
    {
        if (IsLoading) { error = "현재 지역을 불러오는 중입니다."; return false; }
        string scene = GameFlowConfig.Active.townSceneName;
        if (!Application.CanStreamedLevelBeLoaded(scene)) { error = "마을 씬이 빌드 목록에 없습니다."; return false; }
        DungeonRunController.Instance?.ReleaseActors();
        Reset();
        TownProgress.Reset();
        HasActiveGame = IsLoading = saveAfterSceneLoad = true;
        Time.timeScale = 1f;
        error = null;
        SceneManager.LoadScene(scene);
        return true;
    }

    public static bool LoadSlot(int slot, out string error)
    {
        if (IsLoading) { error = "현재 지역을 불러오는 중입니다."; return false; }
        if (!GameSaveService.TryLoad(slot, out GameSaveData data, out error)) return false;
        string scene = data.area == SaveArea.Dungeon ? GameFlowConfig.Active.dungeonSceneName : GameFlowConfig.Active.townSceneName;
        if (!Application.CanStreamedLevelBeLoaded(scene)) { error = "저장된 지역의 씬이 빌드 목록에 없습니다."; return false; }
        DungeonRunController.Instance?.ReleaseActors();
        Reset();
        TownProgress.Restore(data.town);
        HasActiveGame = IsLoading = true;
        PlaySeconds = data.playSeconds;
        hasTownPosition = data.hasTownPosition;
        townPosition = data.townPosition;
        restoreTownPosition = data.area == SaveArea.Town && hasTownPosition;
        // Unity's inline serialization can materialize an empty dungeon object in town saves.
        // The saved area, not object presence, decides whether a run should be restored.
        pendingDungeon = data.area == SaveArea.Dungeon ? data.dungeon : null;
        if (data.completedEvents != null) completedEvents.AddRange(data.completedEvents);
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
        return true;
    }

    public static void BeginDirectPlay()
    {
        if (!HasActiveGame) HasActiveGame = true;
    }

    public static bool EnterDungeon(out string error, DungeonDefinition definition = null)
    {
        string scene = GameFlowConfig.Active.dungeonSceneName;
        if (!CanTravel(scene, out error)) return false;
        var catalog = DungeonCatalog.Active;
        if (catalog == null) { error = "던전 카탈로그 설정이 없습니다."; return false; }
        if (definition == null) definition = catalog.Pick(out error);
        if (definition == null) { error ??= "던전 카탈로그 설정이 없습니다."; return false; }
        if (!catalog.ValidateEntry(definition, out error)) return false;
        pendingDungeon = null; // Entering from town always starts a new run.
        pendingDefinition = definition;
        PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>();
        if (player != null) RememberTownPosition(player.transform.position);
        BeginTravel(scene);
        return true;
    }

    public static bool ReturnToTown(out string error)
    {
        string scene = GameFlowConfig.Active.townSceneName;
        if (!CanTravel(scene, out error)) return false;
        pendingDungeon = null;
        pendingDefinition = null;
        restoreTownPosition = hasTownPosition;
        BeginTravel(scene);
        return true;
    }

    private static bool CanTravel(string scene, out string error)
    {
        error = null;
        if (!HasActiveGame || IsLoading) { error = "현재 지역을 이동할 수 없습니다."; return false; }
        if (!Application.CanStreamedLevelBeLoaded(scene)) { error = "이동할 씬이 빌드 목록에 없습니다."; return false; }
        return true;
    }

    private static void BeginTravel(string scene)
    {
        DungeonRunController.Instance?.ReleaseActors();
        IsLoading = saveAfterSceneLoad = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }

    public static void FinishSceneLoad()
    {
        IsLoading = false;
        if (saveAfterSceneLoad) { saveAfterSceneLoad = false; RequestAutosave(); }
    }

    public static void Tick(float elapsed)
    {
        if (HasActiveGame && !IsLoading && Time.timeScale > 0f) PlaySeconds += elapsed;
    }

    public static DungeonSaveData TakeDungeonSave()
    {
        DungeonSaveData result = pendingDungeon;
        pendingDungeon = null;
        return result;
    }

    public static DungeonDefinition TakeDungeonDefinition()
    {
        DungeonDefinition result = pendingDefinition;
        pendingDefinition = null;
        return result;
    }

    public static void RememberTownPosition(Vector2 position)
    {
        townPosition = position;
        hasTownPosition = true;
    }

    public static bool TakeTownPosition(out Vector2 position)
    {
        position = townPosition;
        bool restore = restoreTownPosition;
        restoreTownPosition = false;
        return restore;
    }

    public static void RequestAutosave()
    {
        if (HasActiveGame) AutosavePending = true;
    }

    /// <summary>Call after future story/quest events finish changing the game state.</summary>
    public static void CompleteEvent(string eventId)
    {
        if (!HasActiveGame || string.IsNullOrWhiteSpace(eventId)) return;
        if (!completedEvents.Contains(eventId)) completedEvents.Add(eventId);
        RequestAutosave();
    }

    public static bool Save(int slot, out string error)
    {
        if (!HasActiveGame || IsLoading) { error = "현재 저장할 수 있는 게임이 없습니다."; return false; }
        GameSaveData data = Capture();
        bool success = GameSaveService.TrySave(slot, data, out error);
        if (slot == GameSaveService.AutosaveSlot && success) AutosavePending = false;
        return success;
    }

    public static GameSaveData Capture()
    {
        DungeonRunController run = DungeonRunController.Instance;
        bool inDungeon = run != null && run.IsRunActive;
        if (!inDungeon && SceneManager.GetActiveScene().name == GameFlowConfig.Active.townSceneName)
        {
            PlayerMovement player = Object.FindAnyObjectByType<PlayerMovement>();
            if (player != null) RememberTownPosition(player.transform.position);
        }
        return new GameSaveData
        {
            town = TownProgress.Capture(), playSeconds = PlaySeconds,
            area = inDungeon ? SaveArea.Dungeon : SaveArea.Town,
            hasTownPosition = hasTownPosition, townPosition = townPosition,
            completedEvents = new List<string>(completedEvents),
            dungeon = inDungeon ? run.CaptureSave() : null
        };
    }

    public static void EndGame()
    {
        DungeonRunController.Instance?.ReleaseActors();
        Reset();
        DungeonActorPool.ClearInactive();
    }
}
