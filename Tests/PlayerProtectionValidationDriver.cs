#if !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PlayerProtectionValidationDriver : MonoBehaviour
{
    private string root;
    private int count;
    private float deadline;
    private bool capture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartChecks()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-saveProtectionValidation") < 0) return;
        var driver = new GameObject("Release Validation").AddComponent<PlayerProtectionValidationDriver>();
        DontDestroyOnLoad(driver.gameObject);
    }

    private IEnumerator Start()
    {
        root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        deadline = Time.realtimeSinceStartup + 90f;
        capture = Array.IndexOf(Environment.GetCommandLineArgs(), "-captureReleaseScreens") >= 0;
        IEnumerator checks = Run();
        while (true)
        {
            object next;
            try
            {
                if (!checks.MoveNext()) yield break;
                next = checks.Current;
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(root, "player-protection-result.txt"), "FAIL: " + error);
                Debug.LogException(error); Application.Quit(1); yield break;
            }
            yield return next;
        }
    }

    private void Check(bool passed, string name)
    {
        if (!passed) throw new InvalidOperationException("FAILED: " + name);
        count++; Debug.Log("RELEASE CHECK PASS: " + name);
    }
    private void CheckTime() { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Release validation timed out."); }
    private static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
    private static GameShell Shell => UnityEngine.Object.FindAnyObjectByType<GameShell>();
    private static bool Ready(string name) => SceneManager.GetActiveScene().name == name && !GameSession.IsLoading;
    private void Snapshot(string name)
    {
        if (capture) ScreenCapture.CaptureScreenshot(Path.Combine(root, name + ".png"));
    }

    private IEnumerator Run()
    {
        if (!root.Replace('\\', '/').EndsWith("/.utmp/UnityValidation"))
            throw new InvalidOperationException("Player checks must run in the isolated project.");
        bool restart = Array.IndexOf(Environment.GetCommandLineArgs(), "-readSavedProtection") >= 0;
        if (restart)
        {
            Check(GameSaveService.TryLoad(8, out var stored, out _) && stored.town.gold == 466, "protected slot survives independent process restart");
            Check(GameSaveService.TryLoad(-1, out var quitSave, out _) && quitSave.town.gold == 466 && quitSave.area == SaveArea.Town,
                "Windows normal quit persists latest progress");
            Finish(true); yield break;
        }
        Check(!Debug.isDebugBuild, "validation uses a non-development Windows build");
        Check(Ready("MainMenu") && Shell != null && !GameSession.HasActiveGame, "release starts in main menu");
        SaveProtectionChecks.Run(Check);
        ReleaseLogicChecks.Run(Check);
        if (capture)
        {
            var display = GameSettings.Copy(); display.width = 1280; display.height = 720; display.fullscreen = false;
            GameSettings.Apply(display, false);
            float settleUntil = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < settleUntil || Screen.width != 1280 || Screen.height != 720 || Screen.fullScreen)
            { CheckTime(); yield return null; }
            Check(Screen.width == 1280 && Screen.height == 720, "release supports standard window size");
        }
        Snapshot("release-main-menu");
        yield return null; yield return null;
        Check(GameSession.StartNewGame(out _), "release new game starts");
        while (!Ready("Town")) { CheckTime(); yield return null; }
        yield return null;
        Check(TownProgress.Gold == 0 && TownProgress.SupplyKits == 0, "release town starts with fresh progress");
        Call(Shell, "Resume");
        TownProgress.BankRun(250, false);
        Check(TownProgress.TryBuySupplyKit() && TownProgress.Gold == 225, "release supply transaction");
        TownProgress.AcceptContract();
        Vector2 townPosition = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform.position;
        yield return null;
        Snapshot("release-town");
        yield return null; yield return null;
        Check(GameSession.EnterDungeon(out _) && GameSession.IsLoading, "release dungeon transition marks loading");
        Check(!(bool)Call(Shell, "SaveBeforeLeaving") && Time.timeScale == 1f,
            "quit during loading defers exit without pausing the incoming scene");
        Call(Shell, "ShowNotice", string.Empty);
        while (!Ready("Dungeon")) { CheckTime(); yield return null; }
        yield return null;
        DungeonRunController run = DungeonRunController.Instance;
        Check(run != null && run.IsRunActive && Resources.Load<Shader>("VisionOverlay") != null, "release dungeon and vision shader exist");
        Check(GameSaveService.TryLoad(-1, out var entry, out _) && entry.area == SaveArea.Dungeon, "dungeon entry checkpoint persisted");
        Call(Shell, "OpenPause");
        Check(run.Inventory.TryPlace(LootDefinition.CreateShaped("회수품", 50, TownProgress.ContractTarget,
            Vector2Int.zero, Vector2Int.right, Vector2Int.up), 1, 1), "release shaped loot fits inventory");
        run.PlayerHealth.TakeDamage(17);
        run.Player.GetComponent<PlayerMovement>().Restore(new PlayerMotionSaveData { cooldownSeconds = 2f });
        Vector2 entrance = run.Player.position;
        run.Player.position = DungeonLayoutFactory.RandomFloorPosition(18f); run.SendMessage("LateUpdate"); run.Player.position = entrance;
        int exploration = run.CaptureSave().discoveredCells.Count;
        Check(GameSession.Save(3, out _) && GameSession.LoadSlot(3, out _), "release saves and loads dungeon slot");
        while (!Ready("Dungeon")) { CheckTime(); yield return null; }
        yield return null;
        run = DungeonRunController.Instance;
        Call(Shell, "OpenPause");
        Check(run.PlayerHealth.Current == 83 && run.Inventory.TotalValue == 50, "release restores health and inventory value");
        Check(run.CaptureSave().discoveredCells.Count >= exploration && run.Player.GetComponent<PlayerMovement>().Capture().cooldownSeconds > 0f,
            "release restores exploration and dash cooldown");
        Call(Shell, "Resume");
        yield return null;
        Snapshot("release-dungeon");
        yield return null; yield return null;
        var pending = GameSession.Capture();
        pending.dungeon.lootPlacementOpen = true;
        for (int i = 0; i < 6; i++) pending.dungeon.pendingLoot.Add(LootSaveData.Capture(new LootDefinition("전리품 " + (i + 1), 1, 1, 15)));
        Check(GameSaveService.TrySave(3, pending, out _) && GameSession.LoadSlot(3, out _), "release pending loot slot loads");
        while (!Ready("Dungeon")) { CheckTime(); yield return null; }
        yield return null;
        run = DungeonRunController.Instance;
        Check(run.IsLootPlacementOpen && Time.timeScale == 0f && run.PendingLootItems.Count == 6, "release restores six-item loot modal and pause");
        Call(Shell, "OnApplicationFocus", false); Call(Shell, "OpenPause"); Call(Shell, "Resume");
        Check(Time.timeScale == 0f, "focus pause and repeated pause preserve loot pause");
        yield return null;
        Snapshot("release-loot-modal");
        yield return null; yield return null;
        if (capture)
        {
            var hud = UnityEngine.Object.FindAnyObjectByType<DungeonDemoHud>();
            typeof(DungeonDemoHud).GetField("pendingLootScroll", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, new Vector2(0f, 500f));
            Snapshot("release-loot-scrolled"); yield return null; yield return null;
            var small = GameSettings.Copy(); small.width = 640; small.height = 360;
            GameSettings.Apply(small, false);
            float settleUntil = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < settleUntil || Screen.width != 640 || Screen.height != 360)
            { CheckTime(); yield return null; }
            Check(Screen.width == 640 && Screen.height == 360, "release supports minimum window size");
            Snapshot("release-loot-small"); yield return null; yield return null;
            small.width = 1280; small.height = 720; GameSettings.Apply(small, false);
            yield return null; yield return null;
        }
        Check(GameSession.LoadSlot(3, out _), "release reloads while loot modal is paused");
        while (!Ready("Dungeon")) { CheckTime(); yield return null; }
        yield return null;
        run = DungeonRunController.Instance;
        Check(Time.timeScale == 0f && run.IsLootPlacementOpen, "old dungeon cleanup cannot unpause restored loot modal");
        while (run.PendingLootItems.Count > 0) run.DiscardPendingLoot(0);
        Check(Time.timeScale == 1f && !run.IsLootPlacementOpen, "release completing loot resumes simulation");
        Call(run, "FinishRun", "검증 귀환");
        int banked = TownProgress.Gold;
        Call(run, "FinishRun", "중복 귀환");
        Check(banked == 395 && TownProgress.Gold == banked && !run.PlayerHealth.TakeDamage(10), "escape banks reward once and prevents later damage");
        yield return null;
        Check(GameSaveService.TryLoad(-1, out var escaped, out _) && escaped.area == SaveArea.Town && escaped.town.gold == banked,
            "escape autosave contains committed town reward");
        Check(GameSession.ReturnToTown(out _), "release returns to town");
        while (!Ready("Town")) { CheckTime(); yield return null; }
        yield return null;
        Check(Vector2.Distance(UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform.position, townPosition) < .01f,
            "town return restores remembered position");
        Call(Shell, "OnApplicationFocus", false); Call(Shell, "OpenPause");
        Check(Time.timeScale == 0f && GameShell.IsGameplayInputBlocked, "release focus loss pauses gameplay");
        Call(Shell, "Resume");
        Check(Time.timeScale == 1f, "repeated pause does not lose resume time scale");
        TownProgress.AcceptContract();
        Check(GameSession.EnterDungeon(out _), "release second dungeon begins");
        while (!Ready("Dungeon")) { CheckTime(); yield return null; }
        yield return null;
        run = DungeonRunController.Instance;
        run.Inventory.TryPlace(new LootDefinition("잃는 전리품", 1, 1, 999), 0, 0);
        run.PlayerHealth.Restore(1);
        Check(run.PlayerHealth.TakeDamage(2), "release lethal damage applied");
        while (!Ready("Town")) { CheckTime(); yield return null; }
        yield return null;
        Check(TownProgress.Gold == banked && !TownProgress.HasAcceptedContract && TownProgress.LastRunGold == 0,
            "death loses unbanked loot and contract without changing banked gold");
        Call(Shell, "Resume");
        string autoPath = Path.Combine(GameSaveService.SaveDirectory, "autosave.sav");
        using (var locked = File.Open(autoPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            GameSession.RequestAutosave(); Call(Shell, "LateUpdate");
            Check(GameSession.AutosavePending, "release failed autosave remains pending");
        }
        typeof(GameShell).GetField("nextAutosaveAttempt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Shell, 0f);
        Call(Shell, "LateUpdate");
        Check(!GameSession.AutosavePending, "release autosave retries after storage recovers");
        Check(GameSaveService.TrySave(8, new GameSaveData { town = new TownProgressData { gold = 466 } }, out _), "release persistent slot written");
        TownProgress.BankRun(71, false);
        Check(TownProgress.Gold == 466, "latest quit state prepared");
        Finish(false);
    }

    private void Finish(bool restart)
    {
        File.WriteAllText(Path.Combine(root, "player-protection-result.txt"), "PASS: " + count + " release player checks." + (restart ? " (restart)" : ""));
        Application.Quit(0);
    }
}
#endif
