#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>플레이 없이 확률·수량·상속·저장 규칙을 데이터로 검증한다.</summary>
public static class LootRewardEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/.utmp/UnityValidation/Assets", StringComparison.Ordinal))
            throw new InvalidOperationException("Loot checks require the isolated project.");
        var randomState = UnityEngine.Random.state;
        var source = DungeonCatalog.Active.corpsePrefabs.First(item => item != null && item.loot != null && item.loot.Count > 0);
        var owner = UnityEngine.Object.Instantiate(source.gameObject);
        var corpse = owner.GetComponent<CorpsePrefab>();
        corpse.loot = new List<MonsterLootEntry> { Item("reward.test.a", "기본 재료 A", .4f), Item("reward.test.b", "기본 재료 B", .7f) };
        var dungeonTable = ScriptableObject.CreateInstance<LootRewardTable>();
        var floorTable = ScriptableObject.CreateInstance<LootRewardTable>();
        var dungeon = ScriptableObject.CreateInstance<DungeonDefinition>();
        var floors = new List<DungeonFloorDefinition>();
        try
        {
            check(dungeonTable.IsValid(out _), "empty table inherits all prefab defaults");
            var baseline = CorpseLootPlan.Capture(corpse);
            UnityEngine.Random.InitState(413);
            var expected = corpse.RollLoot(); float expectedNext = UnityEngine.Random.value;
            UnityEngine.Random.InitState(413);
            var actual = baseline.Roll();
            check(Same(expected, actual) && UnityEngine.Random.value == expectedNext, "unconfigured plans preserve default drops, prices and random consumption");

            dungeonTable.entries.Add(Row(corpse, "reward.test.a", 1f, 2, 2));
            dungeonTable.entries.Add(Row(corpse, "reward.test.b", 1f, 3, 3));
            floorTable.entries.Add(Row(corpse, "reward.test.a", 0f, 1, 1));
            check(dungeonTable.IsValid(out _) && floorTable.IsValid(out _), "valid overrides accept independent probabilities whose sum exceeds 100 percent");
            var plan = CorpseLootPlan.Capture(corpse, dungeonTable, floorTable);
            actual = plan.Roll();
            check(actual.Length == 3 && actual.All(item => item.KindId == "reward.test.b"), "floor zero percent beats dungeon rule, while missing floor row inherits dungeon rule");
            check(actual.All(item => item.Name == "기본 재료 B" && item.Value == 7 && item.Width == 1 && item.Height == 1), "override preserves base item name, price and geometry");
            check(CorpseLootPlan.Capture(corpse, dungeonTable).Roll().Length == 5, "quantity is granted per successful independent item roll");

            var variable = plan.Copy(); variable.entries[1].minQuantity = 2; variable.entries[1].maxQuantity = 4;
            int min = 99, max = 0; bool bounded = true;
            for (int i = 0; i < 80; i++) { int count = variable.Roll().Length; min = Math.Min(min, count); max = Math.Max(max, count); bounded &= count >= 2 && count <= 4; }
            check(bounded && min == 2 && max == 4, "variable quantities include both endpoints and stay within bounds");
            var saved = new CorpseSaveData { loot = actual.Select(LootSaveData.Capture).ToList() };
            var savedRoundtrip = JsonUtility.FromJson<CorpseSaveData>(JsonUtility.ToJson(saved));
            var beforeRestore = UnityEngine.Random.state; float afterRestoreExpected = UnityEngine.Random.value; UnityEngine.Random.state = beforeRestore;
            check(Same(actual, corpse.ResolveLoot(savedRoundtrip, new CorpseLootPlan())) && UnityEngine.Random.value == afterRestoreExpected,
                "saved loot restores without rerolling or reading a changed/invalid plan");
            var copy = plan.Copy(); copy.entries[1].item.lootName = "별도 사본";
            check(plan.entries[1].item.lootName == "기본 재료 B", "loot plan copy owns item definitions");

            var row = dungeonTable.entries[0];
            row.dropChance = float.NaN; check(!dungeonTable.IsValid(out _), "non-finite chance rejected"); row.dropChance = 1f;
            row.maxQuantity = 33; check(!dungeonTable.IsValid(out _), "oversized quantity rejected"); row.maxQuantity = 2;
            row.minQuantity = 3; check(!dungeonTable.IsValid(out _), "reversed quantity range rejected"); row.minQuantity = 2;
            row.kindId = "reward.missing"; check(!dungeonTable.IsValid(out _), "items outside the corpse base list rejected"); row.kindId = "reward.test.a";
            dungeonTable.entries.Add(Row(corpse, row.kindId, .5f, 1, 1));
            check(!dungeonTable.IsValid(out _), "duplicate corpse/item override rejected"); dungeonTable.entries.RemoveAt(2);
            var excessive = new CorpseLootPlan { prefabId = corpse.prefabId };
            for (int i = 0; i < 9; i++) excessive.entries.Add(new CorpseLootRule { item = Item("reward.test.a", "과다", 1f), minQuantity = 32, maxQuantity = 32 });
            check(!excessive.IsValid(), "more than 256 possible items per corpse rejected");

            var maps = DungeonMapGenerator.Generate(new DungeonGenerationSettings { floorCount = 2, width = 20, height = 12 });
            for (int i = 0; i < maps.Length; i++)
            {
                var floor = ScriptableObject.CreateInstance<DungeonFloorDefinition>(); floors.Add(floor);
                floor.floorId = "floor.loot-" + i; floor.map = maps[i];
                floor.corpses = new[] { new CorpseSpawnEntry { prefab = corpse, count = 1 } };
            }
            dungeon.floors = floors.ToArray(); dungeon.lootTable = dungeonTable; floors[0].lootTable = floorTable;
            check(dungeon.IsValid(out _), "dungeon accepts default and per-floor reward tables");
            var journey = DungeonExpeditionData.Create(dungeon);
            check(journey.IsValid() && journey.floors[0].lootPlans[0].Roll().Length == 3 && journey.floors[1].lootPlans[0].Roll().Length == 5,
                "expedition captures each floor's effective rules");
            var restored = JsonUtility.FromJson<DungeonExpeditionData>(JsonUtility.ToJson(journey));
            dungeonTable.entries[0].dropChance = 0f; corpse.loot[1].minPrice = corpse.loot[1].maxPrice = 999;
            check(restored.IsValid() && restored.floors[1].lootPlans[0].Roll().Length == 5
                && restored.floors[0].lootPlans[0].Roll().All(item => item.Value == 7), "unvisited saved floors retain pre-edit reward rules and base prices");
            var journeyCopy = journey.Copy(); journeyCopy.floors[0].lootPlans[0].entries[0].maxQuantity = 8;
            check(journey.floors[0].lootPlans[0].entries[0].maxQuantity == 1, "expedition deep copy isolates reward plans");
            restored.floors[0].lootPlans.Clear(); check(!restored.IsValid(), "missing captured rule for a spawned corpse is rejected");
            var legacy = new DungeonFloorState { corpseIds = new[] { corpse.prefabId } };
            check(legacy.HasValidLootPlans() && legacy.FindLootPlan(corpse.prefabId) == null, "old saves without reward snapshots keep their original default path");

            var catalog = ScriptableObject.CreateInstance<DungeonCatalog>();
            try
            {
                catalog.dungeons = new[] { dungeon };
                dungeonTable.entries[1].dropChance = 0f;
                check(ReleaseBuildValidator.CollectLootSources(catalog).Count == 0, "release material-source checks respect zero-percent overrides");
                floorTable.entries[0].dropChance = 1f;
                var sources = ReleaseBuildValidator.CollectLootSources(catalog);
                check(sources.SetEquals(new[] { "reward.test.a" }), "floor override can restore a source disabled at dungeon level");
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
            CheckAdoption(source, check);
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            foreach (var floor in floors) UnityEngine.Object.DestroyImmediate(floor);
            UnityEngine.Object.DestroyImmediate(dungeon);
            UnityEngine.Object.DestroyImmediate(dungeonTable); UnityEngine.Object.DestroyImmediate(floorTable);
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static MonsterLootEntry Item(string id, string name, float chance) => new() { kindId = id, lootName = name, dropChance = chance, minPrice = 7, maxPrice = 7 };
    private static LootRewardOverride Row(CorpsePrefab corpse, string kind, float chance, int min, int max)
        => new() { corpse = corpse, kindId = kind, dropChance = chance, minQuantity = min, maxQuantity = max };
    private static bool Same(IReadOnlyList<LootDefinition> a, IReadOnlyList<LootDefinition> b)
        => a.Count == b.Count && a.Select((item, index) => item.KindId == b[index].KindId && item.Value == b[index].Value && item.Name == b[index].Name
            && item.OccupiedCells.SequenceEqual(b[index].OccupiedCells)).All(value => value);

    private static void CheckAdoption(CorpsePrefab corpse, Action<bool, string> check)
    {
        string folder = "Assets/Editor/loot-checks-" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets/Editor", System.IO.Path.GetFileName(folder));
        var catalog = DungeonCatalog.Active;
        var previousDungeons = catalog.dungeons; var previousMonsters = catalog.monsterPrefabs; var previousCorpses = catalog.corpsePrefabs;
        DungeonDefinition preview = null;
        try
        {
            var table = ScriptableObject.CreateInstance<LootRewardTable>();
            table.entries.Add(Row(corpse, corpse.loot[0].kindId, 1f, 2, 2));
            AssetDatabase.CreateAsset(table, folder + "/rewards.asset");
            preview = DungeonGenerationPreview.Create(new DungeonGenerationSettings { floorCount = 2 }, "보상 연결 검사");
            preview.lootTable = table; preview.floors[1].lootTable = table;
            var saved = DungeonGenerationPreview.Adopt(preview, folder + "/dungeon.asset", out string error);
            check(saved != null && saved.lootTable == table && saved.floors[1].lootTable == table, "adoption preserves dungeon and floor table references: " + error);
            check(AssetDatabase.GetDependencies(folder + "/dungeon.asset").Contains(folder + "/rewards.asset"), "reward table is included through serialized asset dependencies");
        }
        finally
        {
            catalog.dungeons = previousDungeons; catalog.monsterPrefabs = previousMonsters; catalog.corpsePrefabs = previousCorpses;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            DungeonGenerationPreview.Discard(preview); AssetDatabase.DeleteAsset(folder);
        }
    }
}
#endif
