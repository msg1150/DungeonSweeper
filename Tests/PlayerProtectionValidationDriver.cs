#if !UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

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
            Check(stored.town.bagLevel == 1 && stored.town.warehouse.Count == 1 && stored.town.warehouse[0].quantity == 3
                && stored.town.warehouse[0].unitPrice == 90 && stored.town.saleLockedKinds.Contains(LootKinds.GoblinHide),
                "warehouse quantities price tiers sale locks and bag upgrades survive process restart");
            Check(GameSession.LoadSlot(8, out _), "restart continues the protected town slot");
            while (!Ready("Town")) { CheckTime(); yield return null; }
            Call(Shell, "OnApplicationFocus", true); Call(Shell, "Resume");
            Check(GameSession.EnterDungeon(out _, DungeonCatalog.Active.dungeons[1]), "restart enters the first dungeon after continuing town");
            while (!Ready("Dungeon")) { CheckTime(); yield return null; }
            Call(Shell, "OnApplicationFocus", true); Call(Shell, "OpenPause");
            var restartedRun = DungeonRunController.Instance;
            Check(restartedRun.PlayerHealth.Current == restartedRun.PlayerHealth.Maximum
                && Vector2.Distance(restartedRun.Player.position, DungeonLayoutFactory.Entrance) < .2f
                && DungeonLayoutFactory.LayoutIndex == 1, "restart starts at chosen entrance with full health");
            DungeonPrefabChecks.CheckLive(Check);
            Check(GameSession.LoadSlot(6, out _), "restart loads the protected deeper-floor checkpoint");
            while (!Ready("Dungeon")) { CheckTime(); yield return null; }
            Call(Shell, "OnApplicationFocus", true); Call(Shell, "OpenPause");
            var deepRun = DungeonRunController.Instance;
            Check(deepRun.FloorIndex == 1 && deepRun.PlayerHealth.Current == 61 && deepRun.Inventory.Items.Count == 1
                && deepRun.CaptureSave().expedition.floors[0].corpses[0].processed,
                "independent process restores both the active floor and previous floor progress");
            Check(GameSession.LoadSlot(8, out _), "restart restores quit state after entry regression check");
            while (!Ready("Town")) { CheckTime(); yield return null; }
            Finish(true); yield break;
        }
        Check(!Debug.isDebugBuild, "validation uses a non-development Windows build");
        Check(Ready("MainMenu") && Shell != null && !GameSession.HasActiveGame, "release starts in main menu");
        SaveProtectionChecks.Run(Check);
        ReleaseLogicChecks.Run(Check);
        DungeonPopulationChecks.RunLayouts(Check);
        DungeonPrefabChecks.RunAuthoring(Check);
        DungeonPoolChecks.RunLocal(Check);
        DungeonBuilderChecks.RunData(Check);
        DungeonGenerationChecks.Run(Check);
        if (capture)
        {
            // 숨겨진 Windows 창도 실제 backbuffer를 생성하도록 먼저 다른 크기로 갱신한다.
            var display = GameSettings.Copy(); display.width = 800; display.height = 450; display.fullscreen = false;
            GameSettings.Apply(display, false);
            float initializeUntil = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < initializeUntil || Screen.width != 800 || Screen.height != 450)
            { CheckTime(); yield return null; }
            display.width = 1280; display.height = 720;
            GameSettings.Apply(display, false);
            float settleUntil = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < settleUntil || Screen.width != 1280 || Screen.height != 720 || Screen.fullScreen)
            { CheckTime(); yield return null; }
            Check(Screen.width == 1280 && Screen.height == 720, "release supports standard window size");
            // 숨겨진 테스트 창의 포커스 상태 대신, 사용자가 조작 중인 메뉴 모습을 캡처한다.
            Call(Shell, "OnApplicationFocus", true);
            yield return null;
        }
        Snapshot("release-main-menu");
        yield return null; yield return null;
        Check(GameSession.StartNewGame(out _), "release new game starts");
        while (!Ready("Town")) { CheckTime(); yield return null; }
        yield return null;
        Check(TownProgress.Gold == 0 && TownProgress.SupplyKits == 0, "release town starts with fresh progress");
        Call(Shell, "OnApplicationFocus", true);
        Call(Shell, "Resume");
        TownEconomyChecks.Run(Check);
        FocusPauseChecks.Run(Check);
        var artChecks = CheckConceptArt();
        while (artChecks.MoveNext()) yield return artChecks.Current;
        TownProgress.Restore(new TownProgressData { gold = 250 });
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
        Call(Shell, "OnApplicationFocus", false);
        while (!Ready("Dungeon")) { CheckTime(); yield return null; }
        yield return null;
        Check(Time.timeScale == 0f && !GameShell.IsMenuOpen, "release background scene load pauses without a menu");
        Call(Shell, "OnApplicationFocus", true);
        Check(Time.timeScale == 1f, "release background scene resumes on focus return");
        DungeonPopulationChecks.CheckLivePopulation(Check);
        DungeonPrefabChecks.CheckLive(Check);
        DungeonPopulationChecks.RunPatrol(Check);
        DungeonRunController run = DungeonRunController.Instance;
        Check(run != null && run.IsRunActive && Resources.Load<Shader>("VisionOverlay") != null, "release dungeon and vision shader exist");
        if (ThreeDPresentationConfig.SpritesEnabled)
        {
            CheckSpriteDepth();
            var wallContacts = CheckSpriteWallContacts();
            while (wallContacts.MoveNext()) yield return wallContacts.Current;
        }
        Check(GameSaveService.TryLoad(-1, out var entry, out _) && entry.area == SaveArea.Dungeon, "dungeon entry checkpoint persisted");
        Call(Shell, "OpenPause");
        Check(run.Inventory.TryPlace(LootDefinition.CreateShaped("회수품", 50, TownProgress.ContractTarget,
            Vector2Int.zero, Vector2Int.right, Vector2Int.up), 1, 1), "release shaped loot fits inventory");
        Check(run.Inventory.TryPlace(new LootDefinition("판매용 이빨", 1, 1, 70), 4, 0), "release separately priced trade loot fits inventory");
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
        Check(run.PlayerHealth.Current == 83 && run.Inventory.TotalValue == 120, "release restores health and inventory value");
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
        Call(Shell, "OnApplicationFocus", false); Call(Shell, "OnApplicationFocus", true);
        Check(Time.timeScale == 0f && run.IsLootPlacementOpen && !GameShell.IsMenuOpen,
            "focus return keeps loot placement paused without a menu");
        Call(Shell, "OpenPause"); Call(Shell, "OpenPause"); Call(Shell, "Resume");
        Check(Time.timeScale == 0f, "repeated manual pause preserves loot pause");
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
        Check(banked == 225 && TownProgress.Gold == banked && TownProgress.WarehouseCount == 2 && run.Inventory.Items.Count == 0
            && TownProgress.HasAcceptedContract && !run.PlayerHealth.TakeDamage(10), "escape stores loot once without auto-selling or auto-submitting quest");
        yield return null;
        Check(GameSaveService.TryLoad(-1, out var escaped, out _) && escaped.area == SaveArea.Town && escaped.town.gold == banked,
            "escape autosave contains committed town reward");
        Check(GameSession.ReturnToTown(out _), "release returns to town");
        while (!Ready("Town")) { CheckTime(); yield return null; }
        yield return null;
        Check(Vector2.Distance(UnityEngine.Object.FindAnyObjectByType<PlayerMovement>().transform.position, townPosition) < .01f,
            "town return restores remembered position");
        Call(Shell, "OnApplicationFocus", false); Call(Shell, "OnApplicationFocus", false);
        Check(Time.timeScale == 0f && GameShell.IsGameplayInputBlocked && !GameShell.IsMenuOpen,
            "release focus loss silently pauses gameplay");
        Call(Shell, "OnApplicationFocus", true);
        Check(Time.timeScale == 1f && !GameShell.IsMenuOpen, "release focus return automatically resumes");
        Check(TownProgress.TrySubmitContract(out _) && TownProgress.Gold == 345 && TownProgress.WarehouseCount == 1,
            "release guild submission consumes recovered quest loot and rewards once");
        Check(TownProgress.TrySellOne(TownProgress.GetStacks("goblin.tooth")[0].id, out _) && TownProgress.Gold == 415,
            "release market sells recovered item at its preserved price");
        TownEconomyChecks.ReceiveItems(LootKinds.GoblinHide, "고블린 가죽", LootShape.Hide, 40, 5);
        TownEconomyChecks.ReceiveItems(LootKinds.GoblinHide, "고블린 가죽", LootShape.Hide, 90, 3);
        TownProgress.SetSaleLock(LootKinds.GoblinHide, true);
        var commerce = UnityEngine.Object.FindAnyObjectByType<TownCommercePanel>();
        commerce.Open(TownFacility.Warehouse); yield return null;
        Snapshot("release-warehouse"); yield return null; yield return null;
        if (capture)
        {
            var smallTown = GameSettings.Copy(); smallTown.width = 640; smallTown.height = 360;
            GameSettings.Apply(smallTown, false);
            float settleUntil = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < settleUntil || Screen.width != 640 || Screen.height != 360)
            { CheckTime(); yield return null; }
            Snapshot("release-warehouse-small"); yield return null; yield return null;
            smallTown.width = 1280; smallTown.height = 720; GameSettings.Apply(smallTown, false);
            while (Screen.width != 1280 || Screen.height != 720) { CheckTime(); yield return null; }
        }
        Call(Shell, "OnApplicationFocus", false); Call(Shell, "OnApplicationFocus", true);
        Check(Time.timeScale == 0f && TownCommercePanel.IsOpen && !GameShell.IsMenuOpen,
            "focus return preserves warehouse pause without a menu");
        commerce.Close(); commerce.Open(TownFacility.Market); yield return null;
        Snapshot("release-market"); yield return null; yield return null;
        commerce.Close(); commerce.Open(TownFacility.Upgrades); yield return null;
        Snapshot("release-bag-upgrade"); yield return null; yield return null;
        Check(TownProgress.TryUpgradeBag(out _) && TownProgress.Gold == 115 && TownProgress.BagLevel == 1
            && TownProgress.GetStacks(LootKinds.GoblinHide)[0].unitPrice == 90 && TownProgress.WarehouseCount == 3,
            "release recipe consumes low-priced materials and gold atomically");
        commerce.Close(); banked = TownProgress.Gold;
        TownProgress.AcceptContract();
        Check(GameSession.EnterDungeon(out _), "release second dungeon begins");
        while (!Ready("Dungeon")) { CheckTime(); yield return null; }
        yield return null;
        run = DungeonRunController.Instance;
        Check(run.Inventory.Columns == 6 && run.Inventory.Rows == 4
            && run.Inventory.TryPlace(new LootDefinition("잃는 전리품", 1, 1, 999), 5, 3), "release upgraded bag accepts expanded edge cell");
        Call(Shell, "OpenPause");
        Check(GameSession.Save(4, out _) && GameSession.LoadSlot(4, out _), "release upgraded dungeon slot loads");
        while (!Ready("Dungeon")) { CheckTime(); yield return null; }
        yield return null; run = DungeonRunController.Instance;
        Check(run.Inventory.Columns == 6 && run.Inventory.GetCell(5, 3) != 0 && TownProgress.BagLevel == 1,
            "release upgraded dimensions and edge placement restore together");
        var expanded = GameSession.Capture(); expanded.dungeon.lootPlacementOpen = true;
        expanded.dungeon.pendingLoot.Add(LootSaveData.Capture(new LootDefinition("확장 가방 검사용", 1, 1, 15)));
        Check(GameSaveService.TrySave(4, expanded, out _) && GameSession.LoadSlot(4, out _), "release upgraded loot modal slot loads");
        while (!Ready("Dungeon")) { CheckTime(); yield return null; }
        yield return null; run = DungeonRunController.Instance;
        Snapshot("release-expanded-bag"); yield return null; yield return null;
        Check(run.IsLootPlacementOpen && run.Inventory.Columns == 6 && Time.timeScale == 0f
            && run.TryPlacePendingLoot(0, 0, 0, false), "release expanded loot grid places items and restores pause");
        run.PlayerHealth.Restore(1);
        Check(run.PlayerHealth.TakeDamage(2), "release lethal damage applied");
        while (!Ready("Town")) { CheckTime(); yield return null; }
        yield return null;
        Check(TownProgress.Gold == banked && !TownProgress.HasAcceptedContract && TownProgress.LastRunGold == 0 && TownProgress.WarehouseCount == 3,
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
        TownEconomyChecks.ReceiveItems("goblin.tooth", "정산 검사용", LootShape.Tooth, 351, 1);
        Check(TownProgress.TrySellOne(TownProgress.GetStacks("goblin.tooth")[0].id, out _), "release final manual sale");
        Check(GameSession.Save(8, out _), "release persistent slot written");
        Check(TownProgress.Gold == 466, "latest quit state prepared");
        var populationTrials = DungeonPopulationChecks.CheckFreshEntries(Check, CheckTime, layout =>
        {
            if (!capture) return;
            if (ThreeDWorldView.Current != null) ThreeDWorldView.Current.SetOverview(true);
            else
            {
            Camera.main.GetComponent<DungeonCameraFollow>().enabled = false;
            Camera.main.transform.position = new Vector3(0, 0, -10);
            Camera.main.orthographicSize = 10f;
            }
            var overlay = UnityEngine.Object.FindAnyObjectByType<DungeonVisionOverlayRenderer>();
            if (overlay != null) overlay.gameObject.SetActive(false);
            UnityEngine.Object.FindAnyObjectByType<DungeonDemoHud>().enabled = false;
            Snapshot("release-spawn-layout-" + layout);
        });
        while (populationTrials.MoveNext()) yield return populationTrials.Current;
        var configuredTrials = DungeonPrefabChecks.CheckConfiguredEntries(Check, CheckTime);
        while (configuredTrials.MoveNext()) yield return configuredTrials.Current;
        var continueTrials = ContinueEntryChecks.Run(Check, CheckTime);
        while (continueTrials.MoveNext()) yield return continueTrials.Current;
        var poolTrials = DungeonPoolChecks.RunTransitions(Check, CheckTime);
        while (poolTrials.MoveNext()) yield return poolTrials.Current;
        var floorTrials = DungeonBuilderChecks.RunTransitions(Check, CheckTime, Snapshot);
        while (floorTrials.MoveNext()) yield return floorTrials.Current;
        Check(GameSession.LoadSlot(8, out _), "fresh-entry trials restore the original quit state");
        while (!Ready("Town")) { CheckTime(); yield return null; }
        yield return null;
        Check(TownProgress.Gold == 466, "quit state remains unchanged after fresh-entry trials");
        Finish(false);
    }

    private IEnumerator CheckConceptArt()
    {
        if (ThreeDWorldView.Current != null && !ThreeDPresentationConfig.SpritesEnabled)
        {
            var models = CheckBlenderModels();
            while (models.MoveNext()) yield return models.Current;
            yield break;
        }
        if (!ThreeDPresentationConfig.SpritesEnabled)
        {
            var flat = CheckFlatConceptArt();
            while (flat.MoveNext()) yield return flat.Current;
            yield break;
        }
        CheckSpriteDepth();
        // 원본 시트 갤러리는 XY 카메라에서 검사하고 실제 플레이 화면은 다시 2.5D로 캡처한다.
        var world = ThreeDWorldView.Current;
        var camera = world.WorldCamera;
        Vector3 position = camera.transform.position; Quaternion rotation = camera.transform.rotation;
        float size = camera.orthographicSize;
        world.PreviewCamera(true);
        camera.GetUniversalAdditionalCameraData().SetRenderer(0);
        camera.cullingMask = 1; camera.transform.SetPositionAndRotation(new Vector3(0,0,-10), Quaternion.identity);
        camera.orthographicSize = 6.5f;
        try
        {
            var sprites = CheckFlatConceptArt();
            while (sprites.MoveNext()) yield return sprites.Current;
        }
        finally
        {
            camera.GetUniversalAdditionalCameraData().SetRenderer(ThreeDPresentationConfig.Active.forwardRendererIndex);
            camera.cullingMask = 1 << ThreeDWorldView.RenderLayer;
            camera.orthographicSize = size; camera.transform.SetPositionAndRotation(position, rotation);
            world.PreviewCamera(false);
        }
        yield return null;
    }

    private void CheckSpriteDepth()
    {
        var world = ThreeDWorldView.Current;
        Check(world != null && world.WorldCamera.orthographic, "2.5D uses orthographic projection without perspective distortion");
        Check(world.SpriteMaterial != null && world.SpriteMaterial.shader.isSupported, "sprite material survives native shader stripping");
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        var view = player.GetComponent<SpriteDepthView>();
        Check(view != null && view.Display != null && view.Display.GetComponentInChildren<SkinnedMeshRenderer>() == null,
            "2.5D cat uses original illustration instead of the experimental low-poly model");
        view.Sync();
        Check(Vector3.Distance(view.Display.transform.position, ThreeDWorldView.Map(player.transform.position,.025f)) < .001f,
            "depth display remains aligned with logical player and save coordinates");
        var source = player.transform.Find("Animated Player Visual").GetComponent<SpriteRenderer>();
        bool copied = false;
        foreach(var copy in view.Copies)
            if(copy.enabled && copy.sprite == source.sprite && copy.flipX == source.flipX && copy.color.a == source.color.a)
                copied = true;
        Check(copied, "billboard preserves sprite pose, facing and dash cross-fade alpha");
        foreach (var copy in view.Copies) if (copy.enabled)
        {
            Check(Vector3.Dot(copy.transform.up, Vector3.up) > .999f,
                "actor sprite stands upright instead of leaning its upper body into walls");
            Vector3 actual = world.WorldCamera.WorldToViewportPoint(copy.transform.position + copy.transform.up * copy.transform.lossyScale.y)
                - world.WorldCamera.WorldToViewportPoint(copy.transform.position);
            Vector3 expected = world.WorldCamera.WorldToViewportPoint(copy.transform.position + world.WorldCamera.transform.up * source.transform.lossyScale.y)
                - world.WorldCamera.WorldToViewportPoint(copy.transform.position);
            Check(Mathf.Abs(actual.y - expected.y) < .0001f,
                "upright sprite compensation preserves its projected artwork height");
        }
        if (!world.IsDungeon) return;
        Check(GameObject.Find("Dungeon 3D Walls")?.GetComponent<MeshRenderer>() != null,
            "2.5D dungeon retains actual wall thickness");
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyAgent>())
            Check(enemy.GetComponent<SpriteDepthView>()?.Display != null && enemy.GetComponent<ThreeDActorView>() == null,
                "dungeon monster uses animated sprite billboard");
        var overlay = UnityEngine.Object.FindAnyObjectByType<DungeonVisionOverlayRenderer>();
        Call(overlay, "LateUpdate");
        var material = (Material)typeof(DungeonVisionOverlayRenderer).GetField("material", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(overlay);
        Vector3 bottom = material.GetVector("_Ground00"), top = material.GetVector("_Ground01");
        Check(material.GetFloat("_Orthographic") == 1 && Mathf.Abs(bottom.y) < .001f && top.z > bottom.z,
            "darkness projects onto orthographic ground in the correct orientation");
    }

    private IEnumerator CheckSpriteWallContacts()
    {
        var run = DungeonRunController.Instance;
        var player = run.Player;
        var body = player.GetComponent<Rigidbody2D>();
        var shape = player.GetComponent<Collider2D>();
        var view = player.GetComponent<SpriteDepthView>();
        var world = ThreeDWorldView.Current;
        Vector2 originalPosition = body.position;
        float originalScale = Time.timeScale;
        var originalInterpolation = body.interpolation;
        var originalSimulation = Physics2D.simulationMode;
        var walls = GameObject.Find("Runtime Dungeon Layout").GetComponentsInChildren<BoxCollider2D>();
        var hits = new System.Collections.Generic.List<RaycastHit2D>();
        // AI와 이동은 멈추고 HUD를 표시한 상태로 실제 물리 경계에 붙여 촬영한다.
        Time.timeScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.None;
        Physics2D.simulationMode = SimulationMode2D.Script;
        world.PreviewCamera(true);
        try
        {
            foreach (var side in new[] { "North", "West", "East" })
            {
                Vector2 direction = side == "North" ? Vector2.up : side == "West" ? Vector2.left : Vector2.right;
                var wall = Array.Find(walls, item => item.name.StartsWith(side, StringComparison.Ordinal));
                Check(wall != null, "wall contact fixture contains " + side + " boundary");
                Vector2 center = (Vector2)wall.transform.position - direction * .725f;
                player.position = center; body.position = center; body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms(); hits.Clear(); shape.Cast(direction, hits, 1.45f);
                int contact = hits.FindIndex(item => item.collider == wall);
                Check(contact >= 0, "player capsule detects " + side + " wall before crossing it");
                foreach (float speed in new[] { DungeonTuning.Active.playerMoveSpeed, DungeonTuning.Active.playerDashSpeed })
                {
                    player.position = center; body.position = center; Physics2D.SyncTransforms();
                    for (int step = 0; step < 12; step++)
                    {
                        body.linearVelocity = direction * speed;
                        Physics2D.Simulate(.02f);
                    }
                    body.linearVelocity = Vector2.zero;
                    var distance = shape.Distance(wall);
                    // Box2D의 접촉 해석기는 작은 접촉 여유를 허용한다. 깊은 관통은 거부한다.
                    Check(distance.isValid && distance.distance >= -.025f && distance.distance < .05f,
                        $"player movement at {speed:F1} stops against {side} wall (gap {distance.distance:F4})");
                }
                Call(world, "UpdateCamera", true); view.Sync();
                if (side == "North")
                {
                    float wallFront = wall.bounds.min.y;
                    bool outsideWall = true;
                    foreach (var copy in view.Copies) if (copy.enabled)
                        foreach (var vertex in copy.sprite.vertices)
                            outsideWall &= copy.transform.TransformPoint(vertex).z < wallFront;
                    Check(outsideWall, "entire player sprite stays in front of the north wall at physical contact");
                }
                yield return null;
                Snapshot("release-wall-" + side.ToLowerInvariant());
                yield return null; yield return null;
            }
        }
        finally
        {
            player.position = originalPosition; body.position = originalPosition; body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms(); body.interpolation = originalInterpolation; Physics2D.simulationMode = originalSimulation;
            world.PreviewCamera(false); view.Sync(); Time.timeScale = originalScale;
        }
    }

    private IEnumerator CheckFlatConceptArt()
    {
        Call(Shell, "OnApplicationFocus", true); Call(Shell, "Resume");
        // 포커스 복귀/메뉴 닫기 프레임에는 정상적으로 입력과 시각 이동을 막는다.
        yield return null;
        var movement = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        var animator = movement.GetComponent<PlayerVisualAnimator>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var renderer = movement.transform.Find("Animated Player Visual").GetComponent<SpriteRenderer>();
        var walk = (Sprite[])typeof(PlayerVisualAnimator).GetField("walkFrames", flags).GetValue(animator);
        var dash = (Sprite[])typeof(PlayerVisualAnimator).GetField("dashFrames", flags).GetValue(animator);
        Sprite idle = Resources.Load<Sprite>("Sprites/player");
        movement.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        Call(animator, "LateUpdate");
        Check(renderer.sprite == idle && idle != null, "cat shows its separate idle pose when stopped");
        Check(walk.Length == 4 && dash.Length == 4 && walk[0].rect.y > walk[2].rect.y && dash[0].rect.y > dash[2].rect.y,
            "four walk and dash poses read from top left in artwork order");
        movement.GetComponent<Rigidbody2D>().linearVelocity = Vector2.left;
        Call(animator, "LateUpdate");
        Check(Array.IndexOf(walk, renderer.sprite) >= 0 && renderer.flipX, "moving cat animates and faces left");
        movement.Restore(new PlayerMotionSaveData { isDashing = true, dashSeconds = DungeonTuning.Active.playerDashDuration * .15f,
            dashDirection = Vector2.right, lastDirection = Vector2.right });
        Call(animator, "LateUpdate");
        Check(renderer.sprite == dash[2] && !renderer.flipX, "dash uses saved movement progress and its actual direction");
        Call(movement, "EndDash");
        movement.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        Call(animator, "LateUpdate");
        Check(!movement.IsDashing && renderer.sprite == dash[3], "landing recovery persists visually after physical dash ends");
        float recoveryUntil = Time.realtimeSinceStartup + PlayerVisualProfile.Active.dashRecoverySeconds
            + PlayerVisualProfile.Active.poseBlendSeconds + .04f;
        while (Time.realtimeSinceStartup < recoveryUntil) { CheckTime(); yield return null; }
        Check(renderer.sprite == idle && !movement.IsDashing && Mathf.Approximately(renderer.color.a, 1f),
            "landing fades back to idle without extending movement or leaving transparent poses");
        Sprite[] buildings = CasualArtLibrary.LoadSheet("Sprites/Environment/town-structures", 2, 2, 180f, true);
        string[] names = { "Dungeon Entrance", "Town Warehouse", "Salvager Guild", "Supply Shop" };
        for (int i = 0; i < names.Length; i++)
            Check(GameObject.Find(names[i]).GetComponent<SpriteRenderer>().sprite == buildings[i], "correct town building: " + names[i]);
        string[] species = { "goblin", "slime", "orc" };
        Sprite[] corpses = CasualArtLibrary.LoadSheet("Sprites/Monsters/corpse-sheet", 3, 1, 180f, true);
        Check(Mathf.Approximately(corpses[0].rect.width, 724f) && corpses.Length == 3,
            "corpse sheet keeps its original dimensions instead of power-of-two resizing");
        var stage = new GameObject("Validation Art Gallery");
        var hub = UnityEngine.Object.FindAnyObjectByType<TownHubController>();
        bool hubEnabled = hub.enabled;
        try
        {
            Vector3 origin = Camera.main.transform.position; origin.z = 0f;
            AddGallerySprite(stage, CasualArtLibrary.WhiteSprite, origin, new Vector3(19, 11, 1), 40);
            AddGallerySprite(stage, idle, origin + new Vector3(-6.5f, 2.5f, 0), Vector3.one * 1.7f, 41);
            for (int i = 0; i < 4; i++)
            {
                AddGallerySprite(stage, walk[i], origin + new Vector3(-3.9f + i * 2.6f, 2.5f, 0), Vector3.one * 1.7f, 41);
                AddGallerySprite(stage, dash[i], origin + new Vector3(-3.9f + i * 2.6f, 0f, 0), Vector3.one * 1.7f, 41);
            }
            for (int i = 0; i < species.Length; i++)
            {
                var obj = new GameObject(species[i]); obj.transform.SetParent(stage.transform);
                obj.transform.position = origin + new Vector3(-6.5f + i * 5.2f, -2.5f, 0);
                obj.transform.localScale = Vector3.one * .55f;
                var sprite = obj.AddComponent<SpriteRenderer>(); sprite.sortingOrder = 41;
                var monster = obj.AddComponent<MonsterVisualAnimator>();
                monster.Initialize("Sprites/Monsters/" + species[i] + "-sheet", sprite);
                Check(sprite.sprite != null && Mathf.Approximately(sprite.sprite.rect.width, 512f), "monster sheet cell stays square: " + species[i]);
                var source = CasualArtLibrary.CreateReadableCopy(sprite.sprite.texture);
                try { Check(source.GetPixel(0, 0).a < .1f, "transparent monster backdrop: " + species[i]); }
                finally { UnityEngine.Object.Destroy(source); }
                AddGallerySprite(stage, corpses[i], origin + new Vector3(-3.9f + i * 5.2f, -2.5f, 0), Vector3.one * .4f, 41);
            }
            if (capture)
            {
                hub.enabled = false;
                yield return null;
                Snapshot("release-art-gallery"); yield return null; yield return null;
            }
        }
        finally { hub.enabled = hubEnabled; UnityEngine.Object.Destroy(stage); }
        yield return null;
        var attackChecks = CheckAttackArt();
        while (attackChecks.MoveNext()) yield return attackChecks.Current;
    }

    private IEnumerator CheckBlenderModels()
    {
        Call(Shell, "OnApplicationFocus", true); Call(Shell, "Resume");
        yield return null;
        var world = ThreeDWorldView.Current;
        Check(world != null && !world.WorldCamera.orthographic && world.WorldCamera.cullingMask == 1 << ThreeDWorldView.RenderLayer,
            "gameplay uses a perspective camera and actual 3D render layer");
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        var playerView = player.GetComponent<ThreeDActorView>();
        Check(playerView != null && playerView.Model.GetComponentInChildren<SkinnedMeshRenderer>() != null,
            "white janitor cat is a skinned Blender mesh rather than a sprite billboard");
        Check(Vector3.Distance(playerView.Model.transform.position, ThreeDWorldView.Map(player.transform.position)) < .1f,
            "3D player follows logical save coordinates without moving the physics owner");
        var stage = new GameObject("Blender Model Validation Stage");
        var hub = UnityEngine.Object.FindAnyObjectByType<TownHubController>();
        bool hubEnabled = hub.enabled;
        try
        {
            world.PreviewCamera(true); hub.enabled = false;
            var camera = world.WorldCamera;
            camera.transform.position = new Vector3(40, 4.4f, -9);
            camera.transform.LookAt(new Vector3(40,.7f,0));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(stage.transform);
            floor.transform.position = new Vector3(40,-.1f,0); floor.transform.localScale = new Vector3(15,.2f,7);
            floor.layer = ThreeDWorldView.RenderLayer;
            floor.GetComponent<MeshRenderer>().sharedMaterial = ThreeDPresentationConfig.Active.town.GetComponentInChildren<Renderer>().sharedMaterials[0];
            var models = new System.Collections.Generic.List<GameObject>();
            string[] species = { "cat", "goblin", "slime", "orc" };
            for (int i=0;i<4;i++)
            {
                var owner=new GameObject(species[i]);owner.transform.SetParent(stage.transform);
                owner.transform.position=new Vector2(36.4f+i*2.4f,0);
                var view=ThreeDActorView.Attach(owner,species[i]);view.enabled=false;view.Model.SetActive(true);
                models.Add(view.Model);
                var animation=view.Model.GetComponentInChildren<Animation>();
                foreach(string clip in new[] {"Idle","Walk","Dash","Attack","Loot","Defeated"})
                    Check(animation[clip]!=null && animation[clip].clip.legacy && animation[clip].length>0,
                        "Blender clip included in build: "+species[i]+"/"+clip);
                Check(view.Model.GetComponentInChildren<SkinnedMeshRenderer>().bones.Length>0,
                    "armature bone weights survive FBX import: "+species[i]);
                if(i==0)
                {
                    var leg=Array.Find(view.Model.GetComponentsInChildren<Transform>(),item=>item.name=="Leg_L");
                    Check(leg!=null,"cat has an actual leg bone");
                    animation.Play("Idle"); animation["Idle"].normalizedTime=0; animation.Sample();
                    var initial=leg.localRotation;
                    animation.Play("Walk"); animation["Walk"].normalizedTime=.25f; animation.Sample();
                    Check(Quaternion.Angle(initial,leg.localRotation)>10,"walking deforms the leg bone instead of swapping pictures");
                    animation.Play("Idle");animation["Idle"].normalizedTime=0;animation.Sample();
                }
            }
            yield return null; Snapshot("release-art-gallery");yield return null;yield return null;
            // Three poses, two species, facing both ways with the same actual rig.
            foreach(var model in models) model.SetActive(false);
            camera.transform.position = new Vector3(40,11,-13);camera.transform.LookAt(new Vector3(40,.4f,0));
            for(int row=0;row<4;row++)
            for(int frame=0;frame<3;frame++)
            {
                var owner=new GameObject("attack pose");owner.transform.SetParent(stage.transform);
                owner.transform.position=new Vector2(36+frame*4,row*2.5f-3.75f);
                var view=ThreeDActorView.Attach(owner,row<2?"goblin":"orc");view.enabled=false;view.Model.SetActive(true);
                Vector2 facing = row%2==0 ? Vector2.right : Vector2.left;
                view.Face(facing,true);
                var animation=view.Model.GetComponentInChildren<Animation>();
                animation.Play("Attack");animation["Attack"].normalizedTime=frame*.45f;animation.Sample();animation.Stop();
                Check(Vector2.Dot(view.VisibleForward,facing)>.99f,"Blender face markers agree with attack direction after sampling");
            }
            yield return null;Snapshot("release-attack-gallery");yield return null;yield return null;
        }
        finally { world.PreviewCamera(false);hub.enabled=hubEnabled;UnityEngine.Object.Destroy(stage); }
        yield return null;
    }

    private IEnumerator CheckAttackArt()
    {
        var stage = new GameObject("Validation Attack Gallery");
        var hub = UnityEngine.Object.FindAnyObjectByType<TownHubController>();
        bool hubEnabled = hub.enabled;
        try
        {
            Vector3 origin = Camera.main.transform.position; origin.z = 0f;
            AddGallerySprite(stage, CasualArtLibrary.WhiteSprite, origin, new Vector3(19, 11, 1), 40);
            string[] species = { "goblin", "orc" };
            for (int row = 0; row < 4; row++)
            for (int frame = 0; frame < 3; frame++)
            {
                bool left = row % 2 == 1;
                var obj = new GameObject(species[row / 2] + " attack"); obj.transform.SetParent(stage.transform);
                obj.transform.position = origin + new Vector3(-4.5f + frame * 4.5f, 3f - row * 2f, 0);
                obj.transform.localScale = Vector3.one * .55f;
                var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sortingOrder = 41;
                var animator = obj.AddComponent<MonsterVisualAnimator>();
                animator.Initialize("Sprites/Monsters/" + species[row / 2] + "-sheet", renderer);
                animator.Tick(true, frame / 3f + .01f, false, left ? Vector2.left : Vector2.right);
                Check(renderer.flipX == left, "attack pose faces its target: " + species[row / 2] + " frame " + frame + " left=" + left);
                if (frame == 2)
                {
                    Check(renderer.sprite.vertices.Length == 6 && Mathf.Approximately(renderer.sprite.pivot.x + renderer.sprite.rect.x, 1280f),
                        "recovery excludes neighboring slash fragments and preserves body pivot: " + species[row / 2]);
                }
                else if (frame == 1)
                    Check(renderer.sprite.rect.width > 570 && renderer.sprite.vertices.Length == 6
                        && Mathf.Approximately(renderer.sprite.pivot.x, 256f),
                        "entire forward weapon and arc fit without borrowing recovery clothing: " + species[row / 2]);
            }
            if (capture)
            {
                hub.enabled = false;
                yield return null;
                Snapshot("release-attack-gallery"); yield return null; yield return null;
            }
        }
        finally { hub.enabled = hubEnabled; UnityEngine.Object.Destroy(stage); }
        yield return null;
    }

    private static void AddGallerySprite(GameObject stage, Sprite sprite, Vector3 position, Vector3 scale, int order)
    {
        var obj = new GameObject("Art Preview"); obj.transform.SetParent(stage.transform);
        obj.transform.position = position; obj.transform.localScale = scale;
        var renderer = obj.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = order;
    }

    private void Finish(bool restart)
    {
        File.WriteAllText(Path.Combine(root, "player-protection-result.txt"), "PASS: " + count + " release player checks." + (restart ? " (restart)" : ""));
        Application.Quit(0);
    }
}
#endif
