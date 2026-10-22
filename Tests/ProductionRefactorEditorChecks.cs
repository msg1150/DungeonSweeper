#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>플레이 없이 실행하는 구조 변경·복원 원자성·캐시·난수 호환성 검사.</summary>
public static class ProductionRefactorEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        check(!Directory.Exists("Assets/Scripts/Demo") && Directory.Exists("Assets/Scripts/Runtime"), "runtime sources no longer depend on a demo folder");
        check(AssetDatabase.AssetPathToGUID("Assets/Scripts/Runtime/UI/Dungeon/DungeonHud.cs") == "f688f9dbfc1d4c928f1af77c155f7b08",
            "renamed dungeon HUD retains the serialized script GUID");
        using (var flow = new SerializedObject(GameFlowConfig.Active))
            check(flow.FindProperty("dungeonTestSceneName") == null, "production scene settings do not expose a test scene");
        var issues = ReleaseBuildValidator.CollectIssues();
        check(issues.Count == 0, "shipping scenes, prefab references and configs pass build preflight: " + string.Join("; ", issues));
        CheckInventoryRestore(check);
        CheckDismantleRestore(check);
        CheckWarehouseCounts(check);
        CheckLegacyIsolation(check);
        CheckLegacyBaseline(check);
    }

    private static bool Rejected(Action operation)
    {
        try { operation(); return false; }
        catch (ArgumentException) { return true; }
    }

    private static void CheckInventoryRestore(Action<bool, string> check)
    {
        var bag = new GridInventory();
        var loot = new LootDefinition("kept", 1, 1, 7);
        bag.TryPlace(loot, 0, 0);
        int oldId = bag.GetCell(0, 0);
        var oldItem = bag.GetItem(oldId);
        var saved = bag.Capture();
        saved.Add(new StoredLootSaveData { loot = LootSaveData.Capture(loot), position = Vector2Int.zero, hasSaleValue = true, saleValue = 9 });
        check(Rejected(() => bag.Restore(saved, 16)) && ReferenceEquals(bag.GetItem(oldId), oldItem)
            && bag.Items.Count == 1 && bag.TotalValue == 7 && bag.GetCell(0, 0) == oldId,
            "overlapping restore cannot erase or partially replace the current bag");
        saved[1].position = Vector2Int.right;
        saved[1].saleValue = -1;
        check(Rejected(() => bag.Restore(saved, 6)) && ReferenceEquals(bag.GetItem(oldId), oldItem), "negative saved price cannot commit an inventory restore");
        check(Rejected(() => bag.Restore(null, 0)) && bag.TotalValue == 7, "null restore cannot clear the inventory");
        saved[1].saleValue = 9;
        bag.Restore(saved, 16);
        check(bag.Items.Count == 2 && bag.TotalValue == 16 && bag.GetCell(0, 0) != bag.GetCell(1, 0)
            && bag.GetItem(bag.GetCell(1, 0)).SaleValue == 9, "valid restore commits all items, grid IDs and preserved sale prices together");
    }

    private static void CheckDismantleRestore(Action<bool, string> check)
    {
        var random = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(1937);
            float expected = UnityEngine.Random.value;
            UnityEngine.Random.InitState(1937);
            var difficulty = new DismantleDifficulty(4, 3, 1f, .2f);
            var restored = DismantleSession.Restore(difficulty, new DismantleSaveData
                { successes = 2, failures = 1, pointer = .3f, windowStart = .4f, direction = -1f });
            check(UnityEngine.Random.value == expected, "loading a dismantle session does not consume gameplay random values");
            restored.Tick(.1f);
            check(restored.Successes == 2 && restored.Failures == 1 && Mathf.Abs(restored.PointerPosition - .2f) < .0001f
                && Mathf.Abs(restored.WindowStart - .4f) < .0001f, "dismantle restore preserves progress, window and pointer direction");
        }
        finally { UnityEngine.Random.state = random; }
    }

    private static void CheckWarehouseCounts(Action<bool, string> check)
    {
        var original = TownProgress.Capture();
        try
        {
            var data = new TownProgressData { nextWarehouseId = 4 };
            var first = LootSaveData.Capture(new LootDefinition("hide", 1, 1, 7, LootShape.Hide, LootKinds.GoblinHide));
            var second = LootSaveData.Capture(new LootDefinition("core", 1, 1, 9, LootShape.Core, LootKinds.LegacyId(LootShape.Core)));
            data.warehouse.Add(new WarehouseStackData { id = 1, loot = first, quantity = int.MaxValue, unitPrice = 7 });
            data.warehouse.Add(new WarehouseStackData { id = 2, loot = first.Copy(), quantity = 3, unitPrice = 11 });
            data.warehouse.Add(new WarehouseStackData { id = 3, loot = second, quantity = 5, unitPrice = 9 });
            TownProgress.Restore(data);
            check(TownProgress.WarehouseCount == (long)int.MaxValue + 8 && TownProgress.MaterialCount(LootKinds.GoblinHide) == (long)int.MaxValue + 3,
                "warehouse aggregates preserve large quantities across price tiers");
            check(TownProgress.MaterialCount(LootKinds.LegacyId(LootShape.Core)) == 5 && TownProgress.MaterialCount(null) == 0
                && TownProgress.MaterialCount("unknown") == 0, "material lookup uses normalized IDs and handles missing materials");
            TownProgress.Reset();
            check(TownProgress.WarehouseCount == 0 && TownProgress.MaterialCount(LootKinds.GoblinHide) == 0, "reset invalidates all warehouse count caches");
            data.warehouse.RemoveAt(0);
            TownProgress.Restore(data);
            check(TownProgress.WarehouseCount == 8 && TownProgress.MaterialCount(LootKinds.GoblinHide) == 3, "restoring another save replaces rather than accumulates counts");
        }
        finally { TownProgress.Restore(original); }
    }

    private static void CheckLegacyIsolation(Action<bool, string> check)
    {
        var ready = typeof(DungeonLayoutFactory).GetField("spawnPointsReady", BindingFlags.Static | BindingFlags.NonPublic);
        bool originalReady = (bool)ready.GetValue(null);
        var map = DungeonLayoutFactory.ActiveMap;
        int width = DungeonLayoutFactory.Width, height = DungeonLayoutFactory.Height, index = DungeonLayoutFactory.LayoutIndex;
        try
        {
            ready.SetValue(null, true);
            var legacy = DungeonLayoutFactory.LegacyMap(0);
            int capacity = DungeonLayoutFactory.PopulationCapacity(0);
            check((bool)ready.GetValue(null) && ReferenceEquals(map, DungeonLayoutFactory.ActiveMap)
                && width == DungeonLayoutFactory.Width && height == DungeonLayoutFactory.Height && index == DungeonLayoutFactory.LayoutIndex,
                "legacy conversion and capacity checks cannot invalidate the live map or spawn cache");
            legacy.tiles[0] = 99;
            check(DungeonLayoutFactory.LegacyMap(0).tiles[0] != 99 && capacity == DungeonLayoutFactory.PopulationCapacity(0),
                "legacy templates return independent snapshots with cached capacities");
        }
        finally { ready.SetValue(null, originalReady); }
    }

    private static void CheckLegacyBaseline(Action<bool, string> check)
    {
        // Frozen tile rows from the pre-refactor compatibility layouts; never regenerate from the new implementation at test time.
        string[][] expected = LegacyBaselineRows;
        for (int index = 0; index < expected.Length; index++)
        {
            var map = DungeonLayoutFactory.LegacyMap(index);
            bool same = map.width == 20 && map.height == 12 && map.IsValid(out _);
            for (int y = 0; same && y < 12; y++) for (int x = 0; x < 20; x++)
                same &= map.tiles[y * 20 + x] == expected[index][y][x] - '0';
            check(same, "legacy layout " + index + " preserves every tile for old save compatibility");
        }
    }

    private static readonly string[][] LegacyBaselineRows = {
        new[] { "11111011111001111110", "11111111111111111110", "11111011111001111110", "11111011111001111110", "00010000010001111110", "00010000111111001000", "01111110111111001000", "01111111111111011111", "01111110111111111111", "01111110111111011111", "01111110000000011111", "00000000000000011111" },
        new[] { "11111001111110000000", "11111111111110000000", "11111001111111110000", "11111001111111111111", "00001000001011111111", "00001001111000111111", "01111111111000111111", "01111111111000111111", "01111110011111101000", "01111111111111111000", "01111110011111100000", "00000000011111100000" },
        new[] { "11111000000000000000", "11111000111110111111", "11111000111111111111", "11111111111110111111", "00010000111110111111", "01111111000100111111", "01111111110100001000", "01111111011111111111", "01111111011111111111", "00000000011111111111", "00000000011111111111", "00000000011111110000" }
    };
}
#endif
