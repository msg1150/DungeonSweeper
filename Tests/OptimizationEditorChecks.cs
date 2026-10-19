#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>플레이·씬 전환·저장 없이 생성 알고리즘의 기존 결과와 표시 갱신 순서를 검사한다.</summary>
public static class OptimizationEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        var addHiding = typeof(DungeonMapGenerator).GetMethod("AddHidingSpots", BindingFlags.Static | BindingFlags.NonPublic);
        var sizes = new[] { new Vector2Int(4, 4), new Vector2Int(4, 64), new Vector2Int(64, 4),
            new Vector2Int(20, 12), new Vector2Int(32, 20), new Vector2Int(64, 64) };
        foreach (var size in sizes)
        for (int seed = -5; seed < 5; seed++)
        {
            var maps = DungeonMapGenerator.Generate(new DungeonGenerationSettings
                { width = size.x, height = size.y, floorCount = 2, seed = seed, hidingSpotsPerFloor = 0 });
            foreach (var map in maps)
            foreach (int requested in new[] { 0, 2, 16 })
            {
                var expected = map.Copy(); var actual = map.Copy();
                var referenceRandom = new System.Random(seed); var optimizedRandom = new System.Random(seed);
                AddHidingReference(expected, requested, referenceRandom);
                addHiding.Invoke(null, new object[] { actual, requested, optimizedRandom });
                check(actual.hidingSpots.SequenceEqual(expected.hidingSpots) && actual.IsValid(out _)
                    && optimizedRandom.Next() == referenceRandom.Next(),
                    $"{size.x}x{size.y}, seed {seed}, hiding {requested}: positions and subsequent random state preserved");
            }
        }
        int cameraOrder = Order(typeof(ThreeDWorldView)), spriteOrder = Order(typeof(SpriteDepthView));
        check(cameraOrder < spriteOrder && spriteOrder < Order(typeof(MonsterAwarenessIndicator))
            && spriteOrder < Order(typeof(DungeonVisionOverlayRenderer)),
            "camera moves before sprite, awareness and vision overlay updates");
    }

    private static int Order(Type type) => ((DefaultExecutionOrder)Attribute.GetCustomAttribute(type, typeof(DefaultExecutionOrder))).order;

    // 최적화 이전 기준 구현. 점수·동점 처리·난수 소비까지 비교하여 생성 버전 2의 호환성을 보호한다.
    private static void AddHidingReference(DungeonFloorMap map, int requested, System.Random random)
    {
        var directions = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
        var candidates = new List<Vector2Int>();
        for (int y = 0; y < map.height; y++) for (int x = 0; x < map.width; x++)
        {
            var cell = new Vector2Int(x, y);
            if (map.Walkable(cell) && !map.Reserved(cell)) candidates.Add(cell);
        }
        int count = Mathf.Min(requested, Mathf.Max(0, candidates.Count - 1));
        for (int i = 0; i < count; i++)
        {
            int bestIndex = -1; float bestScore = float.NegativeInfinity;
            for (int j = 0; j < candidates.Count; j++)
            {
                var cell = candidates[j];
                float separation = Vector2Int.Distance(cell, map.entrance);
                foreach (var existing in map.hidingSpots) separation = Mathf.Min(separation, Vector2Int.Distance(cell, existing));
                int edges = 0; foreach (var delta in directions) if (!map.Walkable(cell + delta)) edges++;
                float score = separation + edges * .75f + (float)random.NextDouble() * .3f;
                if (score > bestScore) { bestScore = score; bestIndex = j; }
            }
            map.hidingSpots.Add(candidates[bestIndex]); candidates.RemoveAt(bestIndex);
        }
    }

    public static void RunStandalone()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
        if (!root.EndsWith("/.utmp/UnityValidation", StringComparison.Ordinal) || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Data checks require the isolated project outside Play mode.");
        int count = 0;
        Run((passed, message) => { if (!passed) throw new InvalidOperationException(message); count++; });
        File.WriteAllText(Path.Combine(root, "optimization-data-result.txt"), $"PASS: {count} data checks; Play mode was not entered.");
    }
}
#endif
