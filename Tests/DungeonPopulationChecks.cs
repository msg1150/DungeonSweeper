using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class DungeonPopulationChecks
{
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);

    public static void RunLayouts(Action<bool, string> check)
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var grid = (bool[,])typeof(DungeonLayoutFactory).GetField("floor", flags).GetValue(null);
        var originalGrid = (bool[,])grid.Clone();
        int originalIndex = DungeonLayoutFactory.LayoutIndex;
        var random = UnityEngine.Random.state;
        var database = MonsterDatabase.Active;
        var definitions = database.monsters;
        try
        {
            database.monsters = new List<MonsterDefinition>();
            check(database.GetSpawnDefinitions().Count == 3 && database.monsters.Count == 0,
                "empty spawn data falls back without modifying authored monster definitions");
            database.monsters = definitions;
            for (int layout = 0; layout < 3; layout++)
            {
                Array.Clear(grid, 0, grid.Length);
                typeof(DungeonLayoutFactory).GetMethod("BuildFloorPlan", flags).Invoke(null, new object[] { layout });
                var reached = new HashSet<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                var start = DungeonLayoutFactory.WorldToCell(DungeonLayoutFactory.Entrance);
                queue.Enqueue(start); reached.Add(start);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var direction in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        var next = cell + direction;
                        if (DungeonLayoutFactory.IsWalkableCell(next.x, next.y) && reached.Add(next)) queue.Enqueue(next);
                    }
                }
                int floorCount = 0; foreach (bool cell in grid) if (cell) floorCount++;
                check(reached.Count == floorCount, "layout " + layout + " connects every floor tile to the entrance");
                bool spawnsValid = true, pathsValid = true, widePatrol = true;
                var path = new List<Vector2>();
                for (int seed = 0; seed < 100; seed++)
                {
                    UnityEngine.Random.InitState(seed + layout * 1000);
                    var plan = DungeonPopulationPlan.Create(3);
                    spawnsValid &= plan.Corpses.Count == 3 && plan.Enemies.Count == 3
                        && Vector2.Distance(plan.Corpses[0], DungeonLayoutFactory.Entrance) <= 7.01f;
                    var positions = new List<Vector2>(plan.Corpses); positions.AddRange(plan.Enemies); positions.Add(plan.Gate);
                    foreach (var point in positions)
                        pathsValid &= DungeonLayoutFactory.IsWalkablePosition(point)
                            && DungeonLayoutFactory.TryBuildPath(DungeonLayoutFactory.Entrance, point, path);
                    for (int i = 0; i < 3; i++)
                    {
                        for (int j = i + 1; j < 3; j++) spawnsValid &= Vector2.Distance(plan.Corpses[i], plan.Corpses[j]) >= 4.49f;
                        foreach (var corpse in plan.Corpses) spawnsValid &= Vector2.Distance(plan.Enemies[i], corpse) >= Mathf.Max(4.5f, DungeonTuning.Active.detectionRange + 1f) - .01f;
                        widePatrol &= Vector2.Distance(plan.Enemies[i], plan.PatrolTargets[i]) >= DungeonTuning.Active.patrolTravelDistance - .01f;
                        pathsValid &= DungeonLayoutFactory.TryBuildPath(plan.Enemies[i], plan.PatrolTargets[i], path);
                        for (int p = 1; p < path.Count; p++)
                        {
                            Vector2 delta = path[p] - path[p - 1];
                            pathsValid &= Mathf.Abs(delta.x) < .01f || Mathf.Abs(delta.y) < .01f;
                        }
                    }
                }
                check(spawnsValid, "layout " + layout + " spreads corpses and enemies across 100 seeds with an early recovery target");
                check(pathsValid, "layout " + layout + " reaches every spawn and follows corridors without cutting corners");
                check(widePatrol, "layout " + layout + " starts every patrol with a distant destination");
            }
        }
        finally
        {
            database.monsters = definitions;
            Array.Copy(originalGrid, grid, grid.Length);
            typeof(DungeonLayoutFactory).GetField("layoutIndex", flags).SetValue(null, originalIndex);
            UnityEngine.Random.state = random;
        }
    }

    public static void CheckLivePopulation(Action<bool, string> check)
    {
        var run = DungeonRunController.Instance;
        var state = run.CaptureSave();
        check(state.corpses.Count == 3 && state.enemies.Count == 3, "fresh dungeon contains three corpses and three enemies");
        bool visibleObjects = true, reachable = true;
        var path = new List<Vector2>();
        foreach (var corpse in state.corpses)
        {
            var renderer = GameObject.Find(corpse.name)?.GetComponent<SpriteRenderer>();
            visibleObjects &= renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null;
            reachable &= DungeonLayoutFactory.TryBuildPath(DungeonLayoutFactory.Entrance, corpse.position, path);
        }
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyAgent>())
        {
            var renderer = enemy.GetComponent<SpriteRenderer>();
            visibleObjects &= renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sprite != null;
            reachable &= DungeonLayoutFactory.TryBuildPath(DungeonLayoutFactory.Entrance, enemy.transform.position, path);
        }
        check(visibleObjects, "fresh dungeon recovery targets and enemies have active sprite renderers");
        check(reachable, "fresh dungeon recovery targets and enemies are accessible from the entrance");
    }

    public static void RunPatrol(Action<bool, string> check)
    {
        var run = DungeonRunController.Instance;
        Vector3 playerPosition = run.Player.position;
        var random = UnityEngine.Random.state;
        EnemyAgent actor = null;
        try
        {
            run.Player.position = new Vector3(1000, 1000, 0);
            DungeonLayoutFactory.RandomPatrolPoints(8f, out var origin, out var goal);
            actor = new DungeonWorldFactory().CreateEnemy(MonsterDatabase.Active.GetSpawnDefinitions()[0], run.Player, origin, goal);
            Vector2 previousGoal = goal;
            bool heldDestination = true, stayedOnFloor = true, crossedWall = false;
            int completedLegs = 0;
            float greatestDistance = 0f;
            for (int i = 0; i < 1200; i++)
            {
                Vector2 before = actor.transform.position;
                bool arrivedBefore = Vector2.Distance(before, previousGoal) < .18f;
                Call(actor, "Tick", .1f);
                var state = actor.Capture();
                if (state.roamTarget != previousGoal)
                {
                    heldDestination &= arrivedBefore;
                    completedLegs++; previousGoal = state.roamTarget;
                }
                stayedOnFloor &= DungeonLayoutFactory.IsWalkablePosition(actor.transform.position);
                Vector2 delta = (Vector2)actor.transform.position - before;
                if (delta.magnitude > .001f)
                    foreach (var hit in Physics2D.RaycastAll(before, delta.normalized, delta.magnitude))
                        if (hit.collider != null && hit.collider.gameObject.scene == actor.gameObject.scene
                            && hit.collider.GetComponentInParent<EnemyAgent>() == null && hit.collider.GetComponentInParent<PlayerMovement>() == null)
                            crossedWall = true;
                greatestDistance = Mathf.Max(greatestDistance, Vector2.Distance(origin, actor.transform.position));
            }
            check(heldDestination && completedLegs >= 2, "patrol completes distant journeys before selecting the next destination");
            check(greatestDistance >= DungeonTuning.Active.patrolTravelDistance - .2f && stayedOnFloor && !crossedWall,
                "patrol covers distant areas while staying inside real dungeon corridors");
            var saved = actor.Capture();
            actor.Restore(saved);
            check(actor.Capture().roamTarget == saved.roamTarget && actor.Capture().previousPatrolOrigin == saved.previousPatrolOrigin,
                "patrol destination and previous region survive state restoration");
            var path = new List<Vector2>();
            DungeonLayoutFactory.TryBuildPath(actor.transform.position, saved.roamTarget, path);
            Vector2 noise = path[Mathf.Min(3, path.Count - 1)];
            actor.HearNoise(noise, 8f);
            check(actor.Capture().investigationSeconds > 0f, "nearby noise interrupts patrol for investigation");
            bool arrived = false;
            for (int i = 0; i < 200; i++)
            {
                Call(actor, "Tick", .1f);
                if (Vector2.Distance(actor.transform.position, noise) < .18f) { arrived = true; break; }
            }
            check(arrived && actor.Capture().investigationSeconds > 0f, "noise investigation reaches its destination before the wait expires");
            var investigation = actor.Capture(); actor.Restore(investigation);
            check(actor.Capture().investigationSeconds == investigation.investigationSeconds, "noise investigation countdown restores");
            for (int i = 0; i < 100; i++) Call(actor, "Tick", .1f);
            check(actor.Capture().investigationSeconds == 0f && Vector2.Distance(actor.transform.position, noise) > 1f,
                "enemy leaves the noise location and returns to a wide patrol");
            saved.patrolStateVersion = 0; saved.roamSeconds = 5f;
            actor.Restore(saved);
            check(actor.Capture().roamSeconds == 0f, "legacy short retarget timers do not interrupt restored patrols");
        }
        finally
        {
            run.Player.position = playerPosition;
            if (actor != null) { actor.gameObject.SetActive(false); UnityEngine.Object.Destroy(actor.gameObject); }
            UnityEngine.Random.state = random;
            Physics2D.SyncTransforms();
        }
    }

    public static IEnumerator CheckFreshEntries(Action<bool, string> check, Action timeout, Action<int> snapshot)
    {
        var seen = new HashSet<int>();
        var originalDefinitions = MonsterDatabase.Active.monsters;
        var random = UnityEngine.Random.state;
        try
        {
            for (int attempt = 0; attempt < 12 && (attempt < 6 || seen.Count < 3); attempt++)
            {
                var shell = UnityEngine.Object.FindAnyObjectByType<GameShell>();
                Call(shell, "OnApplicationFocus", true);
                Call(shell, "ReturnToMain");
                yield return null; yield return null;
                check(GameSession.StartNewGame(out _), "fresh-entry trial starts from the main menu");
                while (GameSession.IsLoading) { timeout(); yield return null; }
                Call(shell, "OnApplicationFocus", true); Call(shell, "Resume");
                MonsterDatabase.Active.monsters = attempt == 0 ? new List<MonsterDefinition>() : originalDefinitions;
                for (int seed = attempt + 20261007; ; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    var seeded = UnityEngine.Random.state;
                    if (UnityEngine.Random.Range(0, 3) != attempt % 3) continue;
                    UnityEngine.Random.state = seeded;
                    break;
                }
                check(GameSession.EnterDungeon(out _), "fresh-entry trial enters dungeon");
                while (GameSession.IsLoading) { timeout(); yield return null; }
                MonsterDatabase.Active.monsters = originalDefinitions;
                yield return null;
                CheckLivePopulation(check);
                if (seen.Add(DungeonLayoutFactory.LayoutIndex))
                {
                    snapshot(DungeonLayoutFactory.LayoutIndex);
                    yield return null; yield return null;
                }
            }
            check(seen.Count == 3, "fresh-entry trials populate all three dungeon layouts");
        }
        finally { MonsterDatabase.Active.monsters = originalDefinitions; UnityEngine.Random.state = random; }
    }
}
