using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DungeonBuilderChecks
{
    private static void Shell(string method) => typeof(GameShell).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(UnityEngine.Object.FindAnyObjectByType<GameShell>(), null);
    private static void PlacePlayer(DungeonRunController run, Vector2 position)
    {
        run.Player.position = position;
        var body = run.Player.GetComponent<Rigidbody2D>(); body.position = position; body.linearVelocity = Vector2.zero;
    }
    public static void RunData(Action<bool,string> check)
    {
        var demo = DungeonCatalog.Active.FindDungeon("dungeon.deep-salvage");
        check(demo != null && demo.floors.Length == 2 && DungeonCatalog.Active.ValidateEntry(demo, out _), "authored two-floor sample is registered and playable");
        var map = new DungeonFloorMap { width = 32, height = 20, tiles = new int[640] };
        for (int x = 1; x < 31; x++) for (int y = 1; y < 19; y++) map.tiles[y * 32 + x] = 1;
        check(map.IsValid(out _), "authoring supports maps beyond the original 20 by 12 grid");
        for (int y = 0; y < 20; y++) map.tiles[y * 32 + 15] = 2;
        check(!map.IsValid(out _), "wall brush cannot save disconnected walkable areas");
        map.tiles[5 * 32 + 15] = 1;
        check(map.IsValid(out _), "opening a passage reconnects painted rooms");
        map.hasStairsDown = true; map.stairsDown = new Vector2Int(15,1);
        check(!map.IsValid(out _), "stairs placed inside a wall are rejected");
        map.stairsDown = new Vector2Int(29,17);
        check(map.IsValid(out _), "stairs on a reachable floor validate");
        var legacy = new DungeonSaveData { health = 100 };
        var roundtrip = JsonUtility.FromJson<DungeonSaveData>(JsonUtility.ToJson(legacy));
        check(roundtrip.IsValid() && roundtrip.ResolvedExpedition == null && roundtrip.ResolvedMap == null, "legacy single-floor JSON ignores materialized empty optional objects");
        var journey = DungeonExpeditionData.Create(demo);
        check(journey.IsValid() && journey.exitFloor == 1 && journey.floors[0].map != demo.floors[0].map, "run snapshots copy authored maps and select exactly one extra exit floor");
        journey.Current.enemies.Add(new EnemySaveData { definition = DungeonCatalog.Active.monsterPrefabs[0].stats.Copy() });
        journey.Current.corpses.Add(new CorpseSaveData { session = new DismantleSaveData { successes = 1 },
            loot = new List<LootSaveData> { LootSaveData.Capture(new LootDefinition("복사 검사", 1, 1, 7)) } });
        var copied = journey.Copy();
        copied.Current.map.tiles[0] = 99; copied.Current.monsterIds[0] = "changed";
        copied.Current.enemies[0].definition.attackDamage++;
        copied.Current.corpses[0].session.successes++;
        copied.Current.corpses[0].loot[0].cells[0] = Vector2Int.one;
        copied.Current.discoveredCells.Add(Vector2Int.one);
        check(journey.Current.map.tiles[0] != 99 && journey.Current.monsterIds[0] != "changed"
            && copied.Current.enemies[0].definition.attackDamage != journey.Current.enemies[0].definition.attackDamage
            && journey.Current.corpses[0].session.successes == 1 && journey.Current.corpses[0].loot[0].cells[0] == Vector2Int.zero
            && journey.Current.discoveredCells.Count == 0, "typed expedition copies isolate maps, spawn IDs, stats, loot geometry, sessions and exploration");
        journey.Current.enemies.Clear(); journey.Current.corpses.Clear();
        journey.floors[1].map.tiles[0] = 99;
        check(!journey.IsValid() && demo.floors[1].map.tiles[0] != 99, "corrupt inactive maps are rejected without changing source assets");
        check(DungeonLayoutFactory.LegacyMap(0).IsValid(out _), "legacy template remains available for conversion");
    }

    public static IEnumerator RunTransitions(Action<bool,string> check, Action timeout, Action<string> snapshot = null)
    {
        check(GameSession.StartNewGame(out _), "floor test starts a separate fresh expedition");
        while (GameSession.IsLoading) { timeout(); yield return null; }
        Shell("OnApplicationFocus", true);
        Shell("Resume");
        var dungeon = DungeonCatalog.Active.FindDungeon("dungeon.deep-salvage");
        var sourceMap = dungeon.floors[1].map;
        var expanded = sourceMap.Copy(); expanded.width = 32; expanded.height = 20; expanded.tiles = new int[640];
        var shift = new Vector2Int(6,4);
        for (int x = 0; x < sourceMap.width; x++) for (int y = 0; y < sourceMap.height; y++) expanded.tiles[(y+shift.y)*32+x+shift.x] = sourceMap.tiles[y*sourceMap.width+x];
        expanded.entrance += shift; expanded.stairsUp += shift; expanded.stairsDown += shift;
        try
        {
            dungeon.floors[1].map = expanded;
            check(GameSession.EnterDungeon(out _, dungeon), "authoring-tool sample enters from town");
            while (GameSession.IsLoading) { timeout(); yield return null; }
        }
        finally { dungeon.floors[1].map = sourceMap; }
        yield return null; Shell("OnApplicationFocus", true); Shell("OpenPause");
        var run = DungeonRunController.Instance;
        check(run.FloorIndex == 0 && run.FloorCount == 2 && GameObject.Find("입구") != null && GameObject.Find("특수 탈출 게이트") == null,
            "first floor contains original portal and stairs but no second exit portal");
        var first = run.CaptureSave();
        var liveJourney = (DungeonExpeditionData)typeof(DungeonRunController)
            .GetField("expedition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(run);
        check(!ReferenceEquals(first.corpses, liveJourney.Current.corpses)
            && !ReferenceEquals(first.enemies, liveJourney.Current.enemies)
            && !ReferenceEquals(first.discoveredCells, liveJourney.Current.discoveredCells),
            "capturing the active floor cannot attach caller-owned lists to the live expedition");
        first.health = 61; first.corpses[0].processed = true; first.enemies[0].attackCooldown = 2.75f;
        check(GameSaveService.TrySave(6, new GameSaveData { area = SaveArea.Dungeon, dungeon = first, town = TownProgress.Capture() }, out _)
            && GameSession.LoadSlot(6, out _), "multifloor checkpoint uses the protected save service");
        while (GameSession.IsLoading) { timeout(); yield return null; }
        yield return null; Shell("OpenPause"); run = DungeonRunController.Instance;
        check(run.Inventory.TryPlace(new LootDefinition("계단 검증 전리품",1,1,10,LootShape.Core),0,0), "floor test carries unbanked loot");
        var before = run.CaptureSave();
        PlacePlayer(run, before.mapSnapshot.Center(before.mapSnapshot.stairsDown));
        check(run.TryTravelFloor(1, out _), "downstairs changes the active floor");
        while (GameSession.IsLoading) { timeout(); yield return null; }
        yield return null; Shell("OpenPause"); run = DungeonRunController.Instance;
        check(run.FloorIndex == 1 && run.PlayerHealth.Current == 61 && run.Inventory.Items.Count == 1,
            "stairs preserve health and expedition bag instead of banking rewards");
        check(GameObject.Find("입구") == null && GameObject.Find("특수 탈출 게이트") != null && GameObject.Find("Dungeon Stairs") != null,
            "deeper floor contains the sole extra portal and rendered stairs");
        check(DungeonLayoutFactory.Width == 32 && DungeonLayoutFactory.Height == 20 && run.CaptureSave().mapSnapshot.width == 32,
            "runtime and saved map keep authored variable dimensions even after source asset changes");
        check(run.CaptureSave().IsValid(), "visited and unvisited floor snapshots validate after a transition");
        Shell("Resume"); yield return null;
        snapshot?.Invoke("release-authored-floor"); yield return null; yield return null;
        Shell("OpenPause");
        PlacePlayer(run, run.CaptureSave().mapSnapshot.Center(run.CaptureSave().mapSnapshot.stairsUp));
        check(run.TryTravelFloor(-1, out _), "upstairs returns to the explored floor");
        while (GameSession.IsLoading) { timeout(); yield return null; }
        yield return null; Shell("OpenPause"); run = DungeonRunController.Instance;
        var returned = run.CaptureSave();
        check(returned.corpses[0].processed && Mathf.Abs(returned.enemies[0].attackCooldown - before.enemies[0].attackCooldown) < .25f
            && returned.specialGate == before.specialGate && returned.inventory.Count == 1 && returned.health == 61,
            $"returning preserves floor state: processed={returned.corpses[0].processed}, cooldown={returned.enemies[0].attackCooldown}/{before.enemies[0].attackCooldown}, gate={returned.specialGate == before.specialGate}, bag={returned.inventory.Count}, health={returned.health}");
        check(returned.discoveredCells.Count >= before.discoveredCells.Count, "each floor retains its explored minimap");
        PlacePlayer(run, returned.mapSnapshot.Center(returned.mapSnapshot.stairsDown));
        check(run.TryTravelFloor(1, out _), "second downstairs visit restores the same deeper floor");
        while (GameSession.IsLoading) { timeout(); yield return null; }
        yield return null; Shell("OpenPause"); run = DungeonRunController.Instance;
        check(GameSession.Save(6,out _) && GameSession.LoadSlot(6,out _), "saving on a deeper floor can be continued");
        while (GameSession.IsLoading) { timeout(); yield return null; }
        yield return null; Shell("OpenPause"); run = DungeonRunController.Instance;
        check(run.FloorIndex == 1 && run.Inventory.Items.Count == 1 && run.PlayerHealth.Current == 61
            && run.CaptureSave().expedition.floors[0].corpses[0].processed, "continue restores current floor and previously visited floor progress");
        var corrupt = GameSession.Capture(); corrupt.dungeon.expedition.floors[0].enemies[0].definition.attackRange = -1;
        check(!corrupt.IsValid() && run.CaptureSave().IsValid(), "inactive actor corruption is rejected and save snapshots do not mutate live exploration");
        long stored = TownProgress.WarehouseCount;
        run.HandlePlayerDeath();
        while (GameSession.IsLoading) { timeout(); yield return null; }
        yield return null;
        check(TownProgress.WarehouseCount == stored && GameSession.Capture().area == SaveArea.Town,
            "death on a deeper floor discards all expedition loot and returns to town");
        check(GameSession.LoadSlot(6,out _), "exit trials restore the deeper-floor checkpoint");
        while (GameSession.IsLoading) { timeout(); yield return null; }
        yield return null; Shell("OpenPause"); run = DungeonRunController.Instance;
        PlacePlayer(run, run.CaptureSave().specialGate);
        check(run.GetContextPrompt()?.Contains("특수 게이트") == true, "extra portal offers extraction only on its selected floor");
        typeof(DungeonRunController).GetMethod("TryInteract", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, null);
        check(run.HasEscaped && run.Inventory.Items.Count == 0 && TownProgress.WarehouseCount == stored + 1,
            "deeper extra portal banks expedition loot exactly once");
        check(GameSession.LoadSlot(6,out _), "original portal trial restores the same checkpoint");
        while (GameSession.IsLoading) { timeout(); yield return null; }
        yield return null; Shell("OpenPause"); run = DungeonRunController.Instance;
        PlacePlayer(run, run.CaptureSave().mapSnapshot.Center(run.CaptureSave().mapSnapshot.stairsUp));
        check(run.TryTravelFloor(-1,out _), "explorer can climb back to the original portal floor");
        while (GameSession.IsLoading) { timeout(); yield return null; }
        yield return null; Shell("OpenPause"); run = DungeonRunController.Instance;
        PlacePlayer(run, DungeonLayoutFactory.Entrance);
        typeof(DungeonRunController).GetMethod("TryInteract", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(run, null);
        check(run.HasEscaped && TownProgress.WarehouseCount == stored + 1, "original entry portal also extracts the entire expedition bag");
        check(GameSession.ReturnToTown(out _), "floor extraction trials return to town");
        while (GameSession.IsLoading) { timeout(); yield return null; }
    }
    private static void Shell(string method, bool value) => typeof(GameShell).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
        .Invoke(UnityEngine.Object.FindAnyObjectByType<GameShell>(), new object[] { value });
}
