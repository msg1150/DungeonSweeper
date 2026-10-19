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
        int originalWidth = DungeonLayoutFactory.Width, originalHeight = DungeonLayoutFactory.Height;
        var originalMap = DungeonLayoutFactory.ActiveMap;
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
                var occupied = new List<Vector2>();
                for (int x = 0; x < DungeonLayoutFactory.Width; x++) for (int y = 0; y < DungeonLayoutFactory.Height; y++)
                {
                    Vector2 point = DungeonLayoutFactory.CellCenter(x, y);
                    if (DungeonLayoutFactory.IsWalkableCell(x, y) && !DungeonLayoutFactory.IsReserved(point)) occupied.Add(point);
                }
                Vector2 onlyFree = occupied[0]; occupied.RemoveAt(0);
                check(DungeonLayoutFactory.PickSpawnPosition(0f, float.PositiveInfinity, occupied, 0f) == onlyFree,
                    "dense spawn sampling finds the only unoccupied cell");
                Vector2 newlyFree = occupied[0]; occupied[0] = onlyFree;
                check(DungeonLayoutFactory.PickSpawnPosition(0f, float.PositiveInfinity, occupied, 0f) == newlyFree,
                    "replacing an occupied position invalidates cached spawn clearance");
                occupied.Clear();
                var cleared = DungeonLayoutFactory.PickSpawnPosition(0f, float.PositiveInfinity, occupied, 0f);
                check(DungeonLayoutFactory.IsWalkablePosition(cleared) && !DungeonLayoutFactory.IsReserved(cleared),
                    "clearing occupied positions resets the spawn clearance cache");
            }
            foreach (Vector2Int size in new[] { new Vector2Int(4, 64), new Vector2Int(64, 4) })
            {
                Array.Clear(grid, 0, grid.Length);
                typeof(DungeonLayoutFactory).GetField("Columns", flags).SetValue(null, size.x);
                typeof(DungeonLayoutFactory).GetField("Rows", flags).SetValue(null, size.y);
                int x = size.x > size.y ? 60 : 1, y = size.y > size.x ? 60 : 1;
                for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++) grid[x + dx, y + dy] = true;
                var expected = new Vector2Int(x, y);
                var found = DungeonLayoutFactory.WorldToCell(DungeonLayoutFactory.CellCenter(0, 0));
                check(found == expected && DungeonLayoutFactory.IsWalkableCell(found.x, found.y),
                    "nearest floor search reaches distant rooms in a " + size.x + " by " + size.y + " map");
            }
        }
        finally
        {
            database.monsters = definitions;
            Array.Copy(originalGrid, grid, grid.Length);
            typeof(DungeonLayoutFactory).GetField("layoutIndex", flags).SetValue(null, originalIndex);
            typeof(DungeonLayoutFactory).GetField("Columns", flags).SetValue(null, originalWidth);
            typeof(DungeonLayoutFactory).GetField("Rows", flags).SetValue(null, originalHeight);
            typeof(DungeonLayoutFactory).GetField("activeMap", flags).SetValue(null, originalMap);
            typeof(DungeonLayoutFactory).GetField("spawnPointsReady", flags).SetValue(null, false);
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
            RunDiagonalMovement(check);
        }
        finally
        {
            run.Player.position = playerPosition;
            if (actor != null) { actor.gameObject.SetActive(false); UnityEngine.Object.Destroy(actor.gameObject); }
            UnityEngine.Random.state = random;
            Physics2D.SyncTransforms();
        }
    }

    private static void RunDiagonalMovement(Action<bool, string> check)
    {
        Vector2 start = default, goal = default;
        bool found = false;
        for (int x = 0; x < DungeonLayoutFactory.Width - 1 && !found; x++)
        for (int y = 0; y < DungeonLayoutFactory.Height - 1 && !found; y++)
        {
            if (!DungeonLayoutFactory.IsWalkableCell(x, y) || !DungeonLayoutFactory.IsWalkableCell(x + 1, y)
                || !DungeonLayoutFactory.IsWalkableCell(x, y + 1) || !DungeonLayoutFactory.IsWalkableCell(x + 1, y + 1)) continue;
            start = DungeonLayoutFactory.CellCenter(x, y) + Vector2.one * .12f;
            goal = DungeonLayoutFactory.CellCenter(x + 1, y + 1) - Vector2.one * .12f;
            found = true;
        }
        // 좁은 통로만으로 제작한 층은 대각선 이동을 검증할 방이 없을 수 있다.
        if (!found) return;
        var obj = new GameObject("Diagonal pursuit validation");
        var blocker = new GameObject("Diagonal sweep blocker");
        try
        {
            obj.transform.position = start;
            obj.AddComponent<BoxCollider2D>().size = Vector2.one * .4f;
            var actor = obj.AddComponent<EnemyAgent>();
            actor.Initialize(DungeonRunController.Instance.Player, start, goal, new MonsterDefinition(), null);
            Physics2D.SyncTransforms();
            bool progressed = true;
            for (int i = 0; i < 20; i++)
            {
                Vector2 target = goal + Vector2.right * (i % 2 == 0 ? .03f : -.03f);
                Vector2 before = actor.transform.position;
                Call(actor, "FollowPath", target, .04f, 2);
                Vector2 delta = (Vector2)actor.transform.position - before;
                progressed &= delta.x > 0f && delta.y > 0f
                    && Vector2.Distance(actor.transform.position, target) < Vector2.Distance(before, target) - .039f;
            }
            check(progressed, "diagonal pursuit continuously approaches a moving target without returning to cell centers");
            Call(actor, "FollowPath", goal, 10f, 2);
            Vector2 movedGoal = goal + new Vector2(-.04f, .03f);
            float beforeDistance = Vector2.Distance(actor.transform.position, movedGoal);
            Call(actor, "FollowPath", movedGoal, .04f, 2);
            check(Vector2.Distance(actor.transform.position, movedGoal) < beforeDistance - .039f,
                "a completed path follows a moved endpoint inside the same cell without rewinding to its origin");
            actor.transform.position = start;
            blocker.transform.position = (start + goal) * .5f + Vector2.up * .25f;
            blocker.AddComponent<BoxCollider2D>().size = Vector2.one * .1f;
            Physics2D.SyncTransforms();
            check(!(bool)Call(actor, "CanMoveDirectly", goal), "diagonal shortcuts respect body width even when the center line misses a wall");
        }
        finally
        {
            obj.SetActive(false); blocker.SetActive(false);
            UnityEngine.Object.Destroy(obj); UnityEngine.Object.Destroy(blocker);
            Physics2D.SyncTransforms();
        }
    }

    public static void RunPerception(Action<bool, string> check)
    {
        var run = DungeonRunController.Instance;
        Vector3 playerPosition = run.Player.position;
        var obj = new GameObject("Perception validation");
        var wall = new GameObject("Perception sight blocker");
        var other = new GameObject("Independent perception validation");
        try
        {
            // 도면 밖의 빈 공간을 사용해 랜덤 던전의 벽·배치와 독립적으로 실제 시야 판정을 검증한다.
            Vector2 origin = new(1000f, 1000f);
            obj.transform.position = origin;
            run.Player.position = origin + Vector2.right * 4f;
            var data = new MonsterDefinition { hasMovementStats = true, detectionRange = 2f, hearingRange = 6f,
                noiseDetectionMultiplier = 3f, noiseAlertSeconds = 4f };
            var actor = obj.AddComponent<EnemyAgent>();
            actor.Initialize(run.Player, origin, origin, data, null);
            var idle = actor.Capture(); idle.roamSeconds = 1000f; actor.Restore(idle);
            Physics2D.SyncTransforms();
            Call(actor, "Tick", .1f);
            check(!actor.Capture().wasChasing && actor.CurrentDetectionRange == 2f, "base detection does not acquire a player outside its radius");
            actor.HearNoise(origin + Vector2.up * 7f);
            check(actor.NoiseAlertRemaining == 0f && actor.Capture().investigationSeconds == 0f,
                "inaudible noise neither expands perception nor starts investigation");
            actor.HearNoise(origin + Vector2.up * 6f);
            check(actor.CurrentDetectionRange == 6f && actor.NoiseAlertRemaining == 4f && actor.Capture().investigationSeconds > 0f,
                "noise at the hearing boundary starts investigation and expands actual perception by the configured multiplier");
            Call(actor, "Tick", .1f);
            check(actor.Capture().wasChasing, "noise alert actually detects a player outside the normal radius");
            check(actor.Awareness == MonsterAwareness.Detected, "a visible acquired player selects the exclamation indicator");
            Vector2 lastSeen = run.Player.position;
            check(actor.Capture().hasLastSeenPlayerPosition && actor.Capture().lastSeenPlayerPosition == lastSeen,
                "visible players provide a remembered pursuit destination");
            wall.transform.position = origin + Vector2.right * 2f;
            wall.AddComponent<BoxCollider2D>();
            Physics2D.SyncTransforms();
            Call(actor, "Tick", .1f);
            check(!actor.Capture().wasChasing && actor.CurrentDetectionRange == 6f, "expanded visual perception still respects blocking walls");
            check(actor.Awareness == MonsterAwareness.Searching, "lost line of sight selects the question indicator while pursuing the last sighting");
            check(actor.Capture().hasLastSeenPlayerPosition && actor.Capture().lastSeenPlayerPosition == lastSeen,
                "losing line of sight retains the last actually observed player position");
            wall.SetActive(false);
            actor.HearNoise(origin);
            check(actor.NoiseAlertRemaining == 4f && actor.CurrentDetectionRange == 6f,
                "repeated audible noise refreshes the timer without stacking the radius");
            Call(actor, "Tick", 1f);
            var snapshot = JsonUtility.FromJson<EnemySaveData>(JsonUtility.ToJson(actor.Capture()));
            Call(actor, "Tick", 10f);
            check(actor.CurrentDetectionRange == 2f && actor.NoiseAlertRemaining == 0f,
                "perception returns to its base radius when the alert expires");
            actor.Restore(snapshot);
            check(actor.NoiseAlertRemaining == 3f && actor.CurrentDetectionRange == 6f,
                "serialized enemy state restores the remaining alert and actual radius");
            check(actor.Capture().hasLastSeenPlayerPosition == snapshot.hasLastSeenPlayerPosition
                && actor.Capture().lastSeenPlayerPosition == snapshot.lastSeenPlayerPosition,
                "serialized enemy state restores the last observed player position");
            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f; Call(actor, "Update");
                check(actor.NoiseAlertRemaining == 3f, "paused gameplay does not consume the remaining noise alert");
            }
            finally { Time.timeScale = originalTimeScale; }
            snapshot.attackTimer = 1f; snapshot.noiseAlertSeconds = .2f;
            actor.Restore(snapshot); Call(actor, "Tick", .25f);
            check(actor.IsAttacking && actor.NoiseAlertRemaining == 0f && actor.CurrentDetectionRange == 2f,
                "noise alert expires even during an attack animation");
            var second = other.AddComponent<EnemyAgent>();
            other.transform.position = origin;
            var secondData = data.Copy(); secondData.detectionRange = 1f; secondData.noiseDetectionMultiplier = 4f; secondData.noiseAlertSeconds = 2f;
            second.Initialize(run.Player, origin, origin, secondData, null); second.HearNoise(origin);
            check(second.CurrentDetectionRange == 4f && second.NoiseAlertRemaining == 2f && actor.BaseDetectionRange == 2f,
                "individual monsters use independent base radii, alert multipliers and durations");
            secondData.noiseDetectionMultiplier = secondData.noiseAlertSeconds = 0f;
            check(secondData.IsValid() && secondData.NoiseDetectionMultiplier == 2f && secondData.NoiseAlertSeconds == 6f,
                "old monster snapshots without alert settings retain compatible defaults");
            secondData.noiseDetectionMultiplier = float.NaN;
            check(!secondData.IsValid(), "non-finite perception settings are rejected");
            secondData.noiseDetectionMultiplier = 1f;
            check(!secondData.IsValid(), "configured noise perception must expand beyond the base radius");
            var beforeForget = actor.Capture();
            beforeForget.attackTimer = .3f; beforeForget.investigationSeconds = 4f;
            beforeForget.hasLastSeenPlayerPosition = true; actor.Restore(beforeForget);
            check(actor.CanRevealHidingAt((Vector2)actor.transform.position + Vector2.right * actor.HidingRevealRange)
                && !actor.CanRevealHidingAt((Vector2)actor.transform.position + Vector2.right * (actor.HidingRevealRange + .1f)),
                "hiding reveal uses the individual monster radius including its boundary");
            actor.ForgetHiddenPlayer();
            var forgotten = actor.Capture();
            check(actor.Awareness == MonsterAwareness.None, "successful hiding removes the awareness indicator immediately");
            check(!forgotten.hasLastSeenPlayerPosition && !forgotten.wasChasing && !forgotten.wasInvestigating
                && forgotten.attackTimer == 0f && forgotten.investigationSeconds == 0f,
                "successful hiding immediately clears attacks, pursuit memories and pending noise investigation");
            var saved = run.CaptureSave(); saved.enemies[0].noiseAlertSeconds = float.NaN;
            check(!saved.IsValid(), "saved non-finite alert timers are rejected");
            saved.enemies[0].noiseAlertSeconds = -1f;
            check(!saved.IsValid(), "saved negative alert timers are rejected");
            saved.enemies[0].noiseAlertSeconds = 0f;
            check(saved.IsValid(), "old enemy snapshots without an alert timer remain loadable");
            saved.enemies[0].hasLastSeenPlayerPosition = true;
            saved.enemies[0].lastSeenPlayerPosition = new Vector2(float.NaN, 0f);
            check(!saved.IsValid(), "non-finite active pursuit memories are rejected");
            saved.enemies[0].hasLastSeenPlayerPosition = false;
            saved.enemies[0].lastSeenPlayerPosition = Vector2.zero;
            check(saved.IsValid(), "old snapshots without pursuit memories remain loadable");
            var liveStates = new List<EnemySaveData>();
            foreach (var enemy in run.Enemies) liveStates.Add(enemy.Capture());
            try
            {
                Vector2 failurePosition = run.Enemies[0].transform.position;
                Call(run, "AlertNearbyMonsters", failurePosition);
                bool matchesHearing = true;
                for (int i = 0; i < run.Enemies.Count; i++)
                {
                    var enemy = run.Enemies[i];
                    bool heard = Vector2.Distance(enemy.transform.position, failurePosition) <= enemy.HearingRange;
                    matchesHearing &= heard
                        ? enemy.NoiseAlertRemaining == liveStates[i].definition.NoiseAlertSeconds && enemy.CurrentDetectionRange > enemy.BaseDetectionRange
                        : enemy.NoiseAlertRemaining == liveStates[i].noiseAlertSeconds;
                }
                check(matchesHearing, "the dismantle-failure dispatch expands only monsters that can hear the failure location");
            }
            finally { for (int i = 0; i < run.Enemies.Count; i++) run.Enemies[i].Restore(liveStates[i]); }
#if UNITY_EDITOR
            check(UnityEngine.Object.FindAnyObjectByType<MonsterPerceptionDebugView>() != null && !DungeonTuning.Active.showMonsterPerceptionRanges,
                "editor perception view is attached with debug rendering disabled by default");
#endif
        }
        finally
        {
            run.Player.position = playerPosition;
            obj.SetActive(false); wall.SetActive(false); other.SetActive(false);
            UnityEngine.Object.Destroy(obj); UnityEngine.Object.Destroy(wall); UnityEngine.Object.Destroy(other);
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
