using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DungeonPrefabChecks
{
    private static void Shell(string method, params object[] args) => typeof(GameShell).GetMethod(method,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(UnityEngine.Object.FindAnyObjectByType<GameShell>(), args);

    public static void RunAuthoring(Action<bool, string> check)
    {
        var catalog = DungeonCatalog.Active;
        check(catalog != null && catalog.CollectIssues().Count == 0 && catalog.dungeons.Length == 3, "three independent dungeon configuration assets validate");
        check(catalog.dungeons[0] != catalog.dungeons[1] && catalog.dungeons[1] != catalog.dungeons[2], "dungeon spawn arrays belong to separate assets");
        var clone = UnityEngine.Object.Instantiate(catalog.dungeons[0]);
        var goblin = catalog.FindMonster("monster.goblin");
        var slime = catalog.FindMonster("monster.slime");
        var orcCorpse = catalog.FindCorpse("corpse.orc");
        check(catalog.monsterPrefabs.Length == 3 && catalog.corpsePrefabs.Length == 3, "all default prefabs are explicitly registered");
        var registry = UnityEngine.Object.Instantiate(catalog);
        try
        {
            registry.dungeons = Array.Empty<DungeonDefinition>();
            check(registry.FindMonster("monster.goblin") == goblin && registry.FindCorpse("corpse.orc") == orcCorpse,
                "save lookup works through registry even without any dungeon spawn rows");
            registry.dungeons = catalog.dungeons;
            registry.monsterPrefabs = Array.Empty<MonsterPrefab>();
            check(registry.CollectIssues().Count > 0 && registry.FindMonster("monster.goblin") == null,
                "unregistered dungeon prefab is rejected instead of relying on folder discovery");
        }
        finally { UnityEngine.Object.Destroy(registry); }
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var grid = (bool[,])typeof(DungeonLayoutFactory).GetField("floor", flags).GetValue(null);
        var original = (bool[,])grid.Clone(); int originalIndex = DungeonLayoutFactory.LayoutIndex;
        var random = UnityEngine.Random.state;
        try
        {
            clone.monsters = new[] { new MonsterSpawnEntry { prefab = goblin, count = 2 }, new MonsterSpawnEntry { prefab = slime, count = 1 } };
            clone.corpses = new[] { new CorpseSpawnEntry { prefab = orcCorpse, count = 4 }, new CorpseSpawnEntry { count = 0 } };
            check(clone.IsValid(out _) && clone.ExpandMonsters().Count == 3 && clone.ExpandCorpses().Count == 4
                && catalog.dungeons[0].monsters[0].count == 1, "independent prefab counts do not modify another dungeon");
            clone.monsters[0].count = -1; check(!clone.IsValid(out _), "negative spawn count is rejected"); clone.monsters[0].count = 2;
            clone.monsters[0].prefab = null; check(!clone.IsValid(out _), "positive count requires a prefab"); clone.monsters[0].prefab = goblin;
            clone.monsters[0].count = int.MaxValue; check(!clone.IsValid(out _), "excessive population is rejected without integer overflow");
            clone.monsters = Array.Empty<MonsterSpawnEntry>(); check(clone.IsValid(out _) && clone.ExpandMonsters().Count == 0, "corpse-only dungeon is allowed");
            clone.corpses = Array.Empty<CorpseSpawnEntry>(); check(clone.IsValid(out _), "explicit empty dungeon is allowed");
            for (int layout = 0; layout < 3; layout++)
            {
                Array.Clear(grid, 0, grid.Length);
                typeof(DungeonLayoutFactory).GetMethod("BuildFloorPlan", flags).Invoke(null, new object[] { layout });
                clone.layoutIndex = layout;
                int capacity = DungeonLayoutFactory.PopulationCapacity(layout);
                clone.monsters = new[] { new MonsterSpawnEntry { prefab = goblin, count = capacity } };
                check(clone.IsValid(out _) && DungeonLayoutFactory.LayoutIndex == layout, "capacity check preserves active layout " + layout);
                var plan = DungeonPopulationPlan.Create(0, clone.ExpandMonsters());
                var unique = new HashSet<Vector2>(plan.Enemies) { plan.Gate, DungeonLayoutFactory.Entrance };
                check(plan.Enemies.Count == capacity && unique.Count == capacity + 2,
                    "layout " + layout + " spawns exactly the maximum population without duplicate cells or gates");
                clone.monsters[0].count++; check(!clone.IsValid(out _), "layout " + layout + " rejects one more than capacity");
            }
        }
        finally
        {
            Array.Copy(original, grid, grid.Length); typeof(DungeonLayoutFactory).GetField("layoutIndex", flags).SetValue(null, originalIndex);
            UnityEngine.Random.state = random; UnityEngine.Object.Destroy(clone);
        }
    }

    public static void CheckLive(Action<bool, string> check)
    {
        var run = DungeonRunController.Instance;
        var state = run.CaptureSave();
        var config = DungeonCatalog.Active.FindDungeon(state.dungeonId);
        var counts = new Dictionary<string, int>();
        foreach (var row in config.monsters) if (row.count > 0) counts[row.prefab.prefabId] = counts.GetValueOrDefault(row.prefab.prefabId) + row.count;
        foreach (var enemy in state.enemies) counts[enemy.prefabId] = counts.GetValueOrDefault(enemy.prefabId) - 1;
        bool types = true; foreach (int remaining in counts.Values) types &= remaining == 0;
        counts.Clear();
        foreach (var row in config.corpses) if (row.count > 0) counts[row.prefab.prefabId] = counts.GetValueOrDefault(row.prefab.prefabId) + row.count;
        foreach (var corpse in state.corpses) counts[corpse.prefabId] = counts.GetValueOrDefault(corpse.prefabId) - 1;
        foreach (int remaining in counts.Values) types &= remaining == 0;
        check(types && state.enemies.Count == config.ExpandMonsters().Count && state.corpses.Count == config.ExpandCorpses().Count,
            "live dungeon spawns exactly each configured prefab and count");
        bool actualPrefabs = true, independentStats = true;
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyAgent>())
        {
            var settings = enemy.GetComponent<MonsterPrefab>();
            var prefab = DungeonCatalog.Active.FindMonster(enemy.PrefabId);
            actualPrefabs &= settings != null && enemy.GetComponent<BoxCollider2D>().size == Vector2.one;
            independentStats &= settings != null && !ReferenceEquals(settings.stats, prefab.stats)
                && settings.stats.attackDamage == prefab.stats.attackDamage && settings.stats.hasMovementStats;
        }
        check(actualPrefabs && UnityEngine.Object.FindObjectsByType<CorpsePrefab>().Length == state.corpses.Count,
            "live monsters and corpses instantiate actual authored prefab components");
        check(independentStats, "spawned monster stats copy their prefab without sharing mutable runtime state");
    }

    public static IEnumerator CheckConfiguredEntries(Action<bool, string> check, Action timeout)
    {
        var catalog = DungeonCatalog.Active;
        var originalMonsters = new MonsterSpawnEntry[catalog.dungeons.Length][];
        var originalCorpses = new CorpseSpawnEntry[catalog.dungeons.Length][];
        for (int i = 0; i < catalog.dungeons.Length; i++)
        { originalMonsters[i] = catalog.dungeons[i].monsters; originalCorpses[i] = catalog.dungeons[i].corpses; }
        var goblin = catalog.FindMonster("monster.goblin"); var slime = catalog.FindMonster("monster.slime");
        var orc = catalog.FindMonster("monster.orc"); var corpse = catalog.FindCorpse("corpse.orc");
        int damage = goblin.stats.attackDamage, successes = corpse.requiredSuccesses;
        try
        {
            for (int trial = 0; trial < 4; trial++)
            {
                var config = catalog.dungeons[trial % 3];
                config.monsters = trial == 0 ? new[] { new MonsterSpawnEntry { prefab = goblin, count = 2 }, new MonsterSpawnEntry { prefab = slime, count = 1 } }
                    : trial == 2 ? new[] { new MonsterSpawnEntry { prefab = orc, count = 2 } } : Array.Empty<MonsterSpawnEntry>();
                config.corpses = trial == 0 ? new[] { new CorpseSpawnEntry { prefab = corpse, count = 4 } }
                    : trial == 1 ? new[] { new CorpseSpawnEntry { prefab = corpse, count = 2 } } : Array.Empty<CorpseSpawnEntry>();
                Shell("OnApplicationFocus", true); Shell("ReturnToMain"); yield return null;
                check(GameSession.StartNewGame(out _), "configured trial starts a new game");
                while (GameSession.IsLoading) { timeout(); yield return null; }
                Shell("OnApplicationFocus", true); Shell("Resume");
                check(GameSession.EnterDungeon(out _, config), "configured trial enters selected dungeon");
                while (GameSession.IsLoading) { timeout(); yield return null; }
                yield return null; Shell("OnApplicationFocus", true); Shell("OpenPause");
                CheckLive(check);
                var snapshot = GameSession.Capture();
                var points = new HashSet<Vector2> { DungeonLayoutFactory.Entrance, snapshot.dungeon.specialGate };
                foreach (var saved in snapshot.dungeon.corpses) points.Add(saved.position);
                foreach (var saved in snapshot.dungeon.enemies) points.Add(saved.position);
                check(snapshot.IsValid() && snapshot.dungeon.layoutIndex == config.layoutIndex
                    && points.Count == snapshot.dungeon.corpses.Count + snapshot.dungeon.enemies.Count + 2,
                    "configured dungeon uses its own layout and distinct random positions");
                if (trial != 0) continue;
                snapshot.dungeon.corpses[0].processed = true;
                snapshot.dungeon.corpses[1].session = new DismantleSaveData { successes = 1, pointer = .3f, windowStart = .2f, direction = 1f };
                check(GameSaveService.TrySave(6, snapshot, out _), "configured prefab dungeon saves with stable IDs");
                config.monsters = Array.Empty<MonsterSpawnEntry>(); config.corpses = Array.Empty<CorpseSpawnEntry>();
                goblin.stats.attackDamage += 333; corpse.requiredSuccesses += 2;
                check(GameSession.LoadSlot(6, out _), "save finds library prefabs even after all spawn rows are removed");
                while (GameSession.IsLoading) { timeout(); yield return null; }
                yield return null; Shell("OpenPause");
                var restored = DungeonRunController.Instance.CaptureSave();
                check(restored.enemies.Count == 3 && restored.corpses.Count == 4 && restored.dungeonId == snapshot.dungeon.dungeonId
                    && restored.enemies[0].definition.attackDamage == damage && restored.corpses[0].requiredSuccesses == successes
                    && restored.corpses[0].processed && restored.corpses[1].session.successes == 1,
                    "saved population, stats, difficulty and corpse progress survive authoring changes");
                bool loot = true;
                for (int i = 0; i < 4; i++)
                {
                    loot &= restored.corpses[i].loot.Count == snapshot.dungeon.corpses[i].loot.Count;
                    for (int j = 0; j < restored.corpses[i].loot.Count; j++)
                        loot &= JsonUtility.ToJson(restored.corpses[i].loot[j]) == JsonUtility.ToJson(snapshot.dungeon.corpses[i].loot[j]);
                }
                check(loot, "saved corpse loot is restored without rolling again");
                goblin.stats.attackDamage = damage; corpse.requiredSuccesses = successes;
                var legacy = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(snapshot));
                legacy.dungeon.dungeonId = null;
                foreach (var saved in legacy.dungeon.corpses) saved.prefabId = null;
                foreach (var saved in legacy.dungeon.enemies) { saved.prefabId = null; saved.definition.hasMovementStats = false; }
                check(GameSaveService.TrySave(5, legacy, out _) && GameSession.LoadSlot(5, out _), "legacy save without prefab IDs still loads");
                while (GameSession.IsLoading) { timeout(); yield return null; }
                yield return null; Shell("OpenPause");
                restored = DungeonRunController.Instance.CaptureSave();
                check(restored.enemies.Count == 3 && restored.corpses.Count == 4 && restored.corpses[0].processed,
                    "legacy factory preserves saved population and progress");
                config.monsters = originalMonsters[0]; config.corpses = originalCorpses[0];
            }
        }
        finally
        {
            for (int i = 0; i < catalog.dungeons.Length; i++)
            { catalog.dungeons[i].monsters = originalMonsters[i]; catalog.dungeons[i].corpses = originalCorpses[i]; }
            goblin.stats.attackDamage = damage; corpse.requiredSuccesses = successes;
        }
    }
}
