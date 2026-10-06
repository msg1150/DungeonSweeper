using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GameFlowSmokeChecks
{
    const string Active = "DungeonValidation.Active", StepKey = "DungeonValidation.Step", CountKey = "DungeonValidation.Count";
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static GameFlowSmokeChecks()
    {
        if (SessionState.GetBool(Active, false)) EditorApplication.update += Watch;
    }
    public static void Run()
    {
        if (!Root.Replace('\\', '/').EndsWith("/.utmp/UnityValidation")) throw new Exception("Validation must run in the isolated project.");
        SessionState.SetBool(Active, true);
        SessionState.SetInt(StepKey, 0);
        SessionState.SetInt(CountKey, 0);
        SessionState.SetFloat("DungeonValidation.Deadline", (float)EditorApplication.timeSinceStartup + 240f);
        Application.runInBackground = true;
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity");
        var buildIssues = ReleaseBuildValidator.CollectIssues();
        Check(buildIssues.Count == 0, "release scenes and required assets valid: " + string.Join("; ", buildIssues));
        int slotCount = GameFlowConfig.Active.manualSaveSlotCount;
        try
        {
            GameFlowConfig.Active.manualSaveSlotCount = 0;
            Check(ReleaseBuildValidator.CollectIssues().Count > 0, "build validator catches invalid authoring settings");
        }
        finally { GameFlowConfig.Active.manualSaveSlotCount = slotCount; }
        EditorApplication.update -= Watch;
        EditorApplication.update += Watch;
        EditorApplication.isPlaying = true;
    }
    static void Watch()
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (EditorApplication.isPlaying && !EditorApplication.isCompiling)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (UnityEngine.Object.FindAnyObjectByType<ValidationRuntimeDriver>() == null)
                new GameObject("Validation Driver").AddComponent<ValidationRuntimeDriver>();
            ValidationRuntimeDriver.TickAction = Tick;
        }
        else if (SessionState.GetInt(StepKey, 0) >= 11) Tick();
    }
    static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAILED: " + name);
        SessionState.SetInt(CountKey, SessionState.GetInt(CountKey, 0) + 1);
        Debug.Log("CHECK PASS: " + name);
    }
    static void Next(int step) { SessionState.SetInt(StepKey, step); SessionState.SetFloat("DungeonValidation.Wait", (float)EditorApplication.timeSinceStartup + .5f); }
    static void CallShell(string name, params object[] args)
    {
        var shell = UnityEngine.Object.FindAnyObjectByType<GameShell>();
        typeof(GameShell).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, args);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Active, false)) return;
        if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat("DungeonValidation.Deadline", 0)) throw new Exception("Validation timed out.");
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat("DungeonValidation.Wait", 0)) return;
            int step = SessionState.GetInt(StepKey, 0);
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat("DungeonValidation.NextLog", 0))
            {
                var diagnosticShell = UnityEngine.Object.FindAnyObjectByType<GameShell>();
                string diagnosticPage = diagnosticShell != null ? typeof(GameShell).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(diagnosticShell).ToString() : "missing";
                Debug.Log($"VALIDATION STATUS step={step} playing={EditorApplication.isPlaying} loading={GameSession.IsLoading} gameTime={Time.time} scale={Time.timeScale} scene={SceneManager.GetActiveScene().name} page={diagnosticPage} shellEnabled={(diagnosticShell != null && diagnosticShell.enabled)}");
                SessionState.SetFloat("DungeonValidation.NextLog", (float)EditorApplication.timeSinceStartup + 5f);
            }
            if (step < 11 && (!EditorApplication.isPlaying || EditorApplication.isCompiling || GameSession.IsLoading)) return;
            switch (step)
            {
                case 0:
                    if (UnityEngine.Object.FindAnyObjectByType<GameShell>() == null) return;
                    Check(SceneManager.GetActiveScene().name == "MainMenu", "main menu scene opens");
                    Check(!GameSession.HasActiveGame, "menu does not start or overwrite a game");
                    Check(UnityEngine.Object.FindObjectsByType<GameShell>().Length == 1, "single persistent shell");
                    Check(GameSaveService.ManualSlotCount == 10 && GameSaveService.GetSlots().Count == 11, "one auto plus ten manual slots");
                    Check(UnityEngine.Object.FindAnyObjectByType<MainMenuPresentation>() != null, "presentation works without media");
                    var mainShell = UnityEngine.Object.FindAnyObjectByType<GameShell>();
                    Check(typeof(GameShell).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mainShell).ToString() == "Main", "main menu page selected");
                    SaveProtectionChecks.Run(Check);
                    ReleaseLogicChecks.Run(Check);
                    DungeonPopulationChecks.RunLayouts(Check);
                    DungeonPrefabChecks.RunAuthoring(Check);
                    Next(1);
                    break;
                case 1:
                    Check(GameSession.StartNewGame(out string newError), "new game starts: " + newError);
                    Next(2);
                    break;
                case 2:
                    // Headless Unity has no focused window; model active play for the town checks.
                    CallShell("OnApplicationFocus", true);
                    Check(SceneManager.GetActiveScene().name == "Town" && GameSession.HasActiveGame, "new game enters town");
                    Check(TownProgress.Gold == 0 && TownProgress.SupplyKits == 0, "new game has fresh progress");
                    Check(GameSaveService.TryLoad(-1, out var initial, out _), "new game automatically saved");
                    TownEconomyChecks.Run(Check);
                    FocusPauseChecks.Run(Check);
                    TownProgress.Restore(new TownProgressData { gold = 370 });
                    Check(TownProgress.TryBuySupplyKit(), "purchase supply");
                    Check(GameSession.Save(0, out string townError), "manual town save: " + townError);
                    Check(TownProgress.Gold == 345 && TownProgress.SupplyKits == 1, "town accounting");
                    string auto = Path.Combine(GameSaveService.SaveDirectory, "autosave.sav");
                    using (var unavailable = File.Open(auto, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        GameSession.RequestAutosave();
                        CallShell("LateUpdate");
                        Check(GameSession.AutosavePending, "failed event save remains pending for retry");
                    }
                    var retryShell = UnityEngine.Object.FindAnyObjectByType<GameShell>();
                    typeof(GameShell).GetField("nextAutosaveAttempt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(retryShell, 0f);
                    CallShell("LateUpdate");
                    Check(!GameSession.AutosavePending && GameSaveService.TryLoad(-1, out var retried, out _) && retried.town.gold == 345,
                        "automatic retry commits latest state after IO recovers");
                    var options = GameSettings.Copy();
                    options.masterVolume = .4f; options.musicVolume = .25f; options.effectsVolume = .6f;
                    options.width = 1280; options.height = 720; options.fullscreen = false;
                    GameSettings.Apply(options);
                    Check(Mathf.Abs(AudioListener.volume - .4f) < .001f, "master volume applied");
                    Check(GameSettings.Current.musicVolume == .25f && GameSettings.Current.effectsVolume == .6f, "separate volume settings");
                    CallShell("OpenPause");
                    Check(Time.timeScale == 0f && GameShell.IsGameplayInputBlocked, "pause freezes and blocks game input");
                    CallShell("OpenSlots", true);
                    Next(3);
                    break;
                case 3:
                    CallShell("Resume");
                    GameSession.RememberTownPosition(UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform.position);
                    Check(GameSession.EnterDungeon(out _), "checked dungeon transition starts");
                    Check(GameSession.IsLoading && !GameSession.EnterDungeon(out _), "loading blocks a duplicate transition");
                    CallShell("OnApplicationFocus", false);
                    Next(4);
                    break;
                case 4:
                    Check(Time.timeScale == 0f && !GameShell.IsMenuOpen, "scene loaded without focus stays paused without a menu");
                    CallShell("OnApplicationFocus", true);
                    Check(Time.timeScale == 1f, "focus return resumes a scene loaded in the background");
                    var run = DungeonRunController.Instance;
                    Check(run != null && run.IsRunActive, "dungeon starts");
                    DungeonPopulationChecks.CheckLivePopulation(Check);
                    DungeonPrefabChecks.CheckLive(Check);
                    DungeonPopulationChecks.RunPatrol(Check);
                    var layoutRoot = GameObject.Find("Runtime Dungeon Layout");
                    Physics2D.SyncTransforms();
                    foreach (var wall in layoutRoot.GetComponentsInChildren<BoxCollider2D>())
                    {
                        bool vertical = wall.name.StartsWith("West") || wall.name.StartsWith("East");
                        Vector3 scale = wall.transform.lossyScale;
                        float width = wall.size.x * Mathf.Abs(scale.x), height = wall.size.y * Mathf.Abs(scale.y);
                        Check(Mathf.Abs(width - (vertical ? .18f : 1.63f)) < .001f
                            && Mathf.Abs(height - (vertical ? 1.63f : .18f)) < .001f, "native wall has intended world dimensions");
                        Vector2 axis = vertical ? Vector2.right : Vector2.up;
                        Vector2 origin = (Vector2)wall.transform.position - axis * .5f;
                        bool hitExpectedWall = false;
                        foreach (RaycastHit2D hit in Physics2D.RaycastAll(origin, axis, 1f))
                            if (hit.collider == wall && Mathf.Abs(hit.distance - .41f) < .005f) hitExpectedWall = true;
                        Check(hitExpectedWall, "native physics ray hits the visible wall boundary");
                    }
                    CallShell("OpenPause");
                    Vector2 spawn = run.Player.position;
                    run.Player.position = DungeonLayoutFactory.RandomFloorPosition(18f);
                    run.SendMessage("LateUpdate");
                    run.Player.position = spawn;
                    run.Player.GetComponent<PlayerMovement>().Restore(new PlayerMotionSaveData { cooldownSeconds = 2f });
                    var shaped = LootDefinition.CreateShaped("검사용 회수품", 83, LootShape.Hide, new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1));
                    Check(run.Inventory.TryPlace(shaped, 1, 1), "shaped inventory placement");
                    run.PlayerHealth.TakeDamage(17);
                    var corpses = (List<CorpseRunData>)typeof(DungeonRunController).GetField("corpses", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
                    var corpse = corpses[0];
                    corpse.Session.RegisterAttempt();
                    corpse.Session.AddSupplySuccess();
                    typeof(DungeonRunController).GetField("activeCorpse", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(run, corpse);
                    typeof(DungeonRunController).GetField("dismantleSession", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(run, corpse.Session);
                    run.Player.GetComponent<PlayerMovement>().SetMovementEnabled(false);
                    Check(GameSession.Save(1, out string dungeonError), "manual dungeon save: " + dungeonError);
                    SessionState.SetString("DungeonValidation.Manual1", Convert.ToBase64String(File.ReadAllBytes(Path.Combine(GameSaveService.SaveDirectory, "slot-02.sav"))));
                    Check(GameSaveService.TryLoad(1, out var saved, out _), "native JSON dungeon roundtrip");
                    Check(saved.dungeon.layoutIndex == DungeonLayoutFactory.LayoutIndex && saved.dungeon.inventory.Count == 1, "map and inventory serialized");
                    Check(saved.dungeon.corpses[0].session.failures == 1 && saved.dungeon.corpses[0].session.successes == 1, "dismantling state serialized");
                    Check(saved.dungeon.enemies.Count == 3 && saved.dungeon.health == 83, "enemy states and health serialized");
                    Check(saved.dungeon.discoveredCells.Count > 0 && saved.dungeon.motion.cooldownSeconds == 2f
                        && saved.dungeon.invulnerabilitySeconds > 0f, "exploration movement and damage timers serialized");
                    SessionState.SetInt("DungeonValidation.Explored", saved.dungeon.discoveredCells.Count);
                    SessionState.SetInt("DungeonValidation.Layout", saved.dungeon.layoutIndex);
                    run.Inventory.Clear();
                    run.PlayerHealth.Restore(12);
                    Check(GameSession.LoadSlot(1, out string loadError), "load selected dungeon slot: " + loadError);
                    Next(5);
                    break;
                case 5:
                    var resumed = DungeonRunController.Instance;
                    CallShell("OpenPause");
                    Check(resumed.PlayerHealth.Current == 83 && resumed.Inventory.TotalValue == 83, "health and rewards restored");
                    Check(resumed.Inventory.GetCell(1,1) != 0 && resumed.Inventory.GetCell(2,1) != 0 && resumed.Inventory.GetCell(1,2) != 0, "nonrectangular cells restored");
                    Check(DungeonLayoutFactory.LayoutIndex == SessionState.GetInt("DungeonValidation.Layout", -1), "same dungeon layout restored");
                    Check(resumed.ActiveSession.Successes == 1 && resumed.ActiveSession.Failures == 1, "active dismantling resumed");
                    Check(resumed.CaptureSave().discoveredCells.Count >= SessionState.GetInt("DungeonValidation.Explored", 0), "explored minimap persists across load");
                    GameSession.CompleteEvent("validation_special_event");
                    Next(6);
                    break;
                case 6:
                    Check(GameSaveService.TryLoad(-1, out var checkpoint, out _), "event produces autosave");
                    Check(checkpoint.completedEvents.Contains("validation_special_event"), "event completion persisted");
                    Check(Convert.ToBase64String(File.ReadAllBytes(Path.Combine(GameSaveService.SaveDirectory, "slot-02.sav"))) == SessionState.GetString("DungeonValidation.Manual1", ""), "autosave never overwrites manual slot");
                    var pending = GameSession.Capture();
                    pending.dungeon.activeCorpseIndex = -1;
                    pending.dungeon.lootPlacementOpen = true;
                    pending.dungeon.pendingLoot.Add(LootSaveData.Capture(new LootDefinition("보류 회수품", 2, 2, 40)));
                    Check(GameSaveService.TrySave(2, pending, out string pendingError), "pending loot save: " + pendingError);
                    Check(GameSession.LoadSlot(2, out _), "load pending loot slot");
                    Next(7);
                    break;
                case 7:
                    var pendingRun = DungeonRunController.Instance;
                    Check(pendingRun.IsLootPlacementOpen && pendingRun.PendingLootItems.Count == 1 && Time.timeScale == 0f, "loot modal and paused world restored");
                    CallShell("OpenPause"); CallShell("Resume");
                    Check(Time.timeScale == 0f, "closing menu preserves existing loot pause");
                    pendingRun.DiscardPendingLoot(0);
                    Check(Time.timeScale == 1f && !pendingRun.IsLootPlacementOpen, "loot completion resumes world");
                    var first = GameSession.Capture();
                    Check(GameSaveService.TrySave(9, first, out string lastSlotError), "last configured manual slot writable: " + lastSlotError
                        + " town=" + first.town.IsValid() + " dungeon=" + first.dungeon?.IsValid() + " health=" + first.dungeon?.health
                        + " bag=" + first.town.bagLevel + " motion=" + first.dungeon?.motion?.IsValid()
                        + " value=" + first.dungeon?.totalValue + " items=" + first.dungeon?.inventory.Count);
                    var second = GameSession.Capture(); second.town.gold = 777;
                    Check(GameSaveService.TrySave(9, second, out _), "atomic overwrite keeps backup");
                    File.WriteAllText(Path.Combine(GameSaveService.SaveDirectory, "slot-10.sav"), "{broken");
                    Check(GameSaveService.TryLoad(9, out var recovered, out string recoveredNotice) && recovered.town.gold == 345 && !string.IsNullOrEmpty(recoveredNotice), "damaged primary recovers from backup");
                    GameFlowConfig.Active.manualSaveSlotCount = 2;
                    Check(GameSaveService.GetSlots().Count == 3, "slot count can be reduced");
                    GameFlowConfig.Active.manualSaveSlotCount = 10;
                    Check(GameSaveService.GetSlots().Count == 11 && GameSaveService.TryLoad(9, out _, out _), "hidden saves survive slot count changes");
                    Check(!GameSaveService.TrySave(10, first, out _), "out of range slot rejected");
                    Check(GameSession.LoadSlot(0, out _), "load town slot");
                    Next(8);
                    break;
                case 8:
                    Check(SceneManager.GetActiveScene().name == "Town" && TownProgress.Gold == 345 && TownProgress.SupplyKits == 1, "selected town progress restored");
                    Check(GameSettings.Current.musicVolume == .25f, "options independent of saves");
                    Check(!GameSession.Capture().completedEvents.Contains("validation_special_event"), "loading restores selected event timeline");
                    CallShell("Resume");
                    Check(GameSession.EnterDungeon(out _, DungeonCatalog.Active.dungeons[1]), "continued town enters its first fresh dungeon");
                    Next(15);
                    break;
                case 15:
                    if (GameSession.IsLoading || SceneManager.GetActiveScene().name != "Dungeon") return;
                    CallShell("OnApplicationFocus", true); CallShell("OpenPause");
                    var continuedRun = DungeonRunController.Instance;
                    Check(continuedRun.PlayerHealth.Current == continuedRun.PlayerHealth.Maximum
                        && Vector2.Distance(continuedRun.Player.position, DungeonLayoutFactory.Entrance) < .2f
                        && DungeonLayoutFactory.LayoutIndex == 1, "continued town starts fresh at the chosen dungeon entrance with full health");
                    DungeonPrefabChecks.CheckLive(Check);
                    Check(GameSession.StartNewGame(out _), "second new game starts");
                    Next(9);
                    break;
                case 9:
                    Check(TownProgress.Gold == 0 && TownProgress.SupplyKits == 0 && !TownProgress.HasAcceptedContract, "new game resets all progress");
                    Check(GameSaveService.TryLoad(0, out var manual, out _) && manual.town.gold == 345, "new game retains manual saves");
                    CallShell("ReturnToMain");
                    Next(10);
                    break;
                case 10:
                    Check(SceneManager.GetActiveScene().name == "MainMenu" && !GameSession.HasActiveGame, "return to main ends active session");
                    SessionState.SetString("DungeonValidation.AutoBeforeMenuQuit", Convert.ToBase64String(File.ReadAllBytes(Path.Combine(GameSaveService.SaveDirectory, "autosave.sav"))));
                    Next(11);
                    CallShell("RequestQuit");
                    break;
                case 11:
                    if (EditorApplication.isPlaying) return;
                    Check(Convert.ToBase64String(File.ReadAllBytes(Path.Combine(Root, "ValidationSaves/autosave.sav"))) == SessionState.GetString("DungeonValidation.AutoBeforeMenuQuit", ""), "quitting idle menu does not overwrite autosave");
                    Next(12);
                    EditorApplication.isPlaying = true;
                    break;
                case 12:
                    if (!EditorApplication.isPlaying || UnityEngine.Object.FindAnyObjectByType<GameShell>() == null || GameSession.IsLoading) return;
                    Check(Mathf.Abs(GameSettings.Current.musicVolume - .25f) < .001f, "options survive restarting play");
                    Check(GameSession.StartNewGame(out _), "start active quit test");
                    Next(13);
                    break;
                case 13:
                    if (!EditorApplication.isPlaying || GameSession.IsLoading || SceneManager.GetActiveScene().name != "Town") return;
                    TownProgress.Restore(new TownProgressData { gold = 123 });
                    string autoPath = Path.Combine(GameSaveService.SaveDirectory, "autosave.sav");
                    using (var locked = File.Open(autoPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        CallShell("RequestQuit");
                        Check(EditorApplication.isPlaying, "failed save cancels normal quit");
                    }
                    Next(14);
                    CallShell("RequestQuit");
                    break;
                case 14:
                    if (EditorApplication.isPlaying) return;
                    Check(GameSaveService.TryLoad(-1, out var quitSave, out _) && quitSave.area == SaveArea.Town && quitSave.town.gold == 123, "normal quit commits latest game state");
                    string result = "PASS: " + SessionState.GetInt(CountKey, 0) + " Unity runtime checks.\n";
                    File.WriteAllText(Path.Combine(Root, "validation-result.txt"), result);
                    Debug.Log(result);
                    SessionState.SetBool(Active, false);
                    EditorApplication.Exit(0);
                    break;
            }
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(Root, "validation-result.txt"), "FAIL: " + error);
            Debug.LogException(error);
            SessionState.SetBool(Active, false);
            EditorApplication.Exit(1);
        }
    }
}
