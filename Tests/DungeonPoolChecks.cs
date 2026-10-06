using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>풀 재사용이 이전 런의 전투·해체 상태와 저장 데이터를 섞지 않는지 검사한다.</summary>
public static class DungeonPoolChecks
{
    private static void Shell(string name, params object[] args) => typeof(GameShell).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(UnityEngine.Object.FindAnyObjectByType<GameShell>(), args);
    private static bool Ready(string scene) => !GameSession.IsLoading && SceneManager.GetActiveScene().name == scene;

    public static void RunLocal(Action<bool, string> check)
    {
        var tuning = DungeonTuning.Active;
        int originalPerPrefab = tuning.poolCapacityPerPrefab, originalTotal = tuning.poolTotalCapacity;
        var player = new GameObject("Pool Validation Player");
        var objects = new List<GameObject>();
        var monster = DungeonCatalog.Active.FindMonster("monster.goblin");
        bool originalAnimation = monster.useSpriteSheetAnimation;
        try
        {
            player.AddComponent<PlayerHealth>().Initialize();
            DungeonActorPool.ClearInactive();
            tuning.poolCapacityPerPrefab = 2; tuning.poolTotalCapacity = 3;
            var first = monster.Spawn(player.transform, Vector2.zero, Vector2.one); objects.Add(first.gameObject);
            check(!first.GetComponentInChildren<SpriteRenderer>().sprite.texture.isReadable,
                "processed monster textures release CPU pixel buffers");
            var second = monster.Spawn(player.transform, Vector2.zero, Vector2.one); objects.Add(second.gameObject);
            var state = second.Capture();
            state.attackTimer = .3f; state.attackCooldown = 2f; state.investigationSeconds = 4f;
            state.wasChasing = state.wasInvestigating = state.damageApplied = true;
            second.Restore(state);
            second.GetComponentInChildren<SpriteRenderer>().flipX = true;
            second.GetComponentInChildren<SpriteRenderer>().enabled = false;
            second.GetComponent<BoxCollider2D>().enabled = false;
            second.transform.localScale = Vector3.one * 9f;
            second.Capture().definition.attackDamage++;
            check(first.Capture().definition.attackDamage == monster.stats.attackDamage,
                "pooled monster stats remain independent from prefab and other actors");
            check(DungeonActorPool.Return(first.gameObject) && DungeonActorPool.Return(second.gameObject), "actors return to their own prefab bucket");
            check(!DungeonActorPool.Return(first.gameObject) && DungeonActorPool.InactiveCount == 2, "duplicate return does not add another cached actor");
            check(second.Capture().definition == null, "return clears references to old stats and player state");
            monster.useSpriteSheetAnimation = false;
            var reused = monster.Spawn(player.transform, Vector2.up, Vector2.right);
            objects.Add(reused.gameObject);
            var fresh = reused.Capture();
            check(reused == second && fresh.attackTimer == 0f && fresh.attackCooldown == 0f && fresh.investigationSeconds == 0f
                && !fresh.wasChasing && !fresh.wasInvestigating && !fresh.damageApplied, "reuse resets combat, patrol and investigation state");
            var renderer = reused.GetComponentInChildren<SpriteRenderer>();
            var sprite = renderer.sprite;
            reused.GetComponent<MonsterVisualAnimator>().Tick(true, .8f, true, Vector2.left);
            check(renderer.enabled && renderer.flipX == monster.GetComponentInChildren<SpriteRenderer>().flipX
                && reused.GetComponent<BoxCollider2D>().enabled && reused.transform.localScale == monster.transform.localScale
                && renderer.sprite == sprite, "reuse restores visuals and collider while honoring disabled sheet animation");
            monster.useSpriteSheetAnimation = originalAnimation;
            DungeonActorPool.Return(reused.gameObject);
            var corpsePrefab = DungeonCatalog.Active.FindCorpse("corpse.orc");
            var corpse = corpsePrefab.Spawn(Vector2.zero); objects.Add(corpse.Visual);
            check(!corpse.Visual.GetComponentInChildren<SpriteRenderer>().sprite.texture.isReadable,
                "processed corpse textures release CPU pixel buffers");
            corpse.Session.AddSupplySuccess(); corpse.MarkProcessed();
            check(DungeonActorPool.Return(corpse.Visual), "processed corpse remains owned until explicit return");
            var freshCorpse = corpsePrefab.Spawn(Vector2.one); objects.Add(freshCorpse.Visual);
            check(freshCorpse.Visual == corpse.Visual && !freshCorpse.IsProcessed && freshCorpse.CaptureSession() == null
                && freshCorpse.Visual.activeInHierarchy, "corpse reuse creates fresh dismantle and loot run data");
            DungeonActorPool.Return(freshCorpse.Visual);
            // Active spawns can exceed retention limits; excess instances are destroyed on return.
            var burst = new List<EnemyAgent>();
            for (int i = 0; i < 5; i++) { var actor = monster.Spawn(player.transform, Vector2.zero, Vector2.one); burst.Add(actor); objects.Add(actor.gameObject); }
            check(burst.Count == 5 && DungeonActorPool.LeasedCount == 5, "pool retention limits do not reduce active spawn counts");
            foreach (var actor in burst) DungeonActorPool.Return(actor.gameObject);
            check(DungeonActorPool.InactiveCount == 3 && DungeonActorPool.LeasedCount == 0, "per-prefab and global retention limits bound cached actors");
            DungeonActorPool.ClearInactive();
            tuning.poolTotalCapacity = 0;
            var uncached = monster.Spawn(player.transform, Vector2.zero, Vector2.one); objects.Add(uncached.gameObject);
            DungeonActorPool.Return(uncached.gameObject);
            check(DungeonActorPool.InactiveCount == 0 && DungeonActorPool.LeasedCount == 0, "zero capacity disables retention without disabling spawns");
        }
        finally
        {
            foreach (var obj in objects) if (obj != null) DungeonActorPool.Return(obj);
            DungeonActorPool.ClearInactive();
            tuning.poolCapacityPerPrefab = originalPerPrefab; tuning.poolTotalCapacity = originalTotal;
            monster.useSpriteSheetAnimation = originalAnimation;
            UnityEngine.Object.Destroy(player);
        }
    }

    public static IEnumerator RunTransitions(Action<bool, string> check, Action timeout)
    {
        check(GameSession.StartNewGame(out _), "pool transition test starts a fresh game");
        while (!Ready("Town")) { timeout(); yield return null; }
        DungeonActorPool.ClearInactive();
        var seen = new HashSet<UnityEngine.Object>();
        for (int round = 0; round < 3; round++)
        {
            Shell("OnApplicationFocus", true); Shell("Resume");
            check(GameSession.EnterDungeon(out _, DungeonCatalog.Active.dungeons[round]), "pool test enters layout " + round);
            while (!Ready("Dungeon")) { timeout(); yield return null; }
            Shell("OnApplicationFocus", true); Shell("OpenPause");
            var run = DungeonRunController.Instance;
            DungeonPrefabChecks.CheckLive(check);
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyAgent>();
            var corpses = UnityEngine.Object.FindObjectsByType<CorpsePrefab>();
            bool reused = true;
            foreach (var enemy in enemies)
            {
                if (round > 0) reused &= seen.Contains(enemy);
                seen.Add(enemy);
                var state = enemy.Capture();
                check(state.attackTimer == 0f && state.investigationSeconds == 0f, "fresh entry cannot inherit previous actor timers");
            }
            foreach (var corpse in corpses)
            { if (round > 0) reused &= seen.Contains(corpse); seen.Add(corpse); }
            check(reused && DungeonActorPool.LeasedCount == enemies.Length + corpses.Length, "layout " + round + " uses retained actors with exact lease counts");
            if (round == 0)
            {
                var state = run.CaptureSave();
                state.enemies[0].attackCooldown = 3f; state.enemies[0].investigationSeconds = 4f;
                state.corpses[0].processed = true;
                state.corpses[0].session = new DismantleSaveData { successes = 1, direction = 1f };
                var saved = GameSession.Capture(); saved.dungeon = state;
                check(GameSaveService.TrySave(9, saved, out _) && GameSession.LoadSlot(9, out _), "pooled actors reload a dungeon snapshot");
                while (!Ready("Dungeon")) { timeout(); yield return null; }
                Shell("OnApplicationFocus", true); Shell("OpenPause");
                run = DungeonRunController.Instance;
                var restored = run.CaptureSave();
                check(restored.enemies[0].attackCooldown == 3f && restored.enemies[0].investigationSeconds == 4f
                    && restored.corpses[0].processed && restored.corpses[0].session.successes == 1,
                    "pooled save restoration retains saved timers and processed corpse progress");
            }
            check(GameSession.ReturnToTown(out _), "pool test returns to town");
            while (!Ready("Town")) { timeout(); yield return null; }
            check(DungeonActorPool.LeasedCount == 0 && DungeonActorPool.InactiveCount == 6
                && UnityEngine.Object.FindObjectsByType<EnemyAgent>().Length == 0,
                "town has six retained inactive actors and no active dungeon AI");
        }
        // 맵 최대 수량도 실제 씬에서 생성해 비활성 보관 한도와 구별한다.
        DungeonActorPool.ClearInactive();
        var definition = DungeonCatalog.Active.dungeons[0];
        var originalMonsters = definition.monsters; var originalCorpses = definition.corpses;
        int capacity = DungeonLayoutFactory.PopulationCapacity(definition.layoutIndex);
        try
        {
            definition.monsters = new[] { new MonsterSpawnEntry { prefab = DungeonCatalog.Active.FindMonster("monster.goblin"), count = capacity / 2 } };
            definition.corpses = new[] { new CorpseSpawnEntry { prefab = DungeonCatalog.Active.FindCorpse("corpse.orc"), count = capacity - capacity / 2 } };
            Shell("OnApplicationFocus", true); Shell("Resume");
            check(GameSession.EnterDungeon(out _, definition), "maximum population enters without a pool spawn limit");
            while (!Ready("Dungeon")) { timeout(); yield return null; }
            Shell("OnApplicationFocus", true); Shell("OpenPause");
            var state = DungeonRunController.Instance.CaptureSave();
            var unique = new HashSet<Vector2> { DungeonLayoutFactory.Entrance, state.specialGate };
            foreach (var enemy in state.enemies) unique.Add(enemy.position);
            foreach (var corpse in state.corpses) unique.Add(corpse.position);
            check(state.enemies.Count + state.corpses.Count == capacity && unique.Count == capacity + 2
                && DungeonActorPool.LeasedCount == capacity, "all " + capacity + " actors spawn in distinct cells above retention limit");
            check(GameSession.ReturnToTown(out _), "maximum population returns to town");
            while (!Ready("Town")) { timeout(); yield return null; }
            int retained = Math.Min(DungeonTuning.Active.poolTotalCapacity, 2 * DungeonTuning.Active.poolCapacityPerPrefab);
            check(DungeonActorPool.InactiveCount == retained && DungeonActorPool.LeasedCount == 0,
                "maximum population retains only " + retained + " actors after return");
        }
        finally { definition.monsters = originalMonsters; definition.corpses = originalCorpses; }
        Shell("ReturnToMain");
        while (!Ready("MainMenu")) { timeout(); yield return null; }
        check(DungeonActorPool.InactiveCount == 0 && DungeonActorPool.LeasedCount == 0, "main menu releases dungeon actor cache");
    }
}
