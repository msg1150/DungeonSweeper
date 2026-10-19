#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>에디터의 도면 파일 생성과 카탈로그 등록. 기존 저장용 프리팹 등록은 제거하지 않는다.</summary>
public static class DungeonAuthoringService
{
    /// <summary>수동 생성과 자동 후보 채택에 같은 신규 에셋 경로 규칙을 적용한다.</summary>
    public static bool TryNewAssetPath(string requested, out string path, out string error)
    {
        path = null; error = "Assets 아래에 존재하지 않는 새 .asset 경로를 선택하세요.";
        requested = (requested ?? "").Replace('\\', '/');
        if (!requested.StartsWith("Assets/", StringComparison.Ordinal) || !requested.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) return false;
        try
        {
            string root = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            string absolute = Path.GetFullPath(Path.Combine(Application.dataPath, "..", requested));
            if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase) || File.Exists(absolute) || Directory.Exists(absolute)) return false;
            string candidate = "Assets/" + absolute.Substring(root.Length).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(Path.GetDirectoryName(candidate).Replace('\\', '/'))) return false;
            path = candidate; error = null; return true;
        }
        catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is IOException || exception is UnauthorizedAccessException)
        { return false; }
    }

    public static DungeonDefinition Create(string path, string name)
    {
        if (!TryNewAssetPath(path, out path, out string error)) throw new InvalidOperationException(error);
        var dungeon = ScriptableObject.CreateInstance<DungeonDefinition>();
        dungeon.dungeonId = "dungeon." + Guid.NewGuid().ToString("N"); dungeon.displayName = name;
        AssetDatabase.CreateAsset(dungeon, path);
        AddFloor(dungeon);
        return dungeon;
    }
    public static void ConvertLegacy(DungeonDefinition dungeon)
    {
        if (dungeon.floors != null && dungeon.floors.Length > 0) return;
        AddFloor(dungeon, true);
    }
    public static DungeonFloorDefinition AddFloor(DungeonDefinition dungeon)
        => AddFloor(dungeon, false);

    private static DungeonFloorDefinition AddFloor(DungeonDefinition dungeon, bool copyLegacySpawns)
    {
        if (dungeon == null || !EditorUtility.IsPersistent(dungeon)) throw new InvalidOperationException("먼저 던전 에셋을 저장하세요.");
        var list = dungeon.floors == null ? new List<DungeonFloorDefinition>() : new List<DungeonFloorDefinition>(dungeon.floors);
        if (list.Count >= DungeonExpeditionData.MaximumFloors) throw new InvalidOperationException("최대 16층까지 지원합니다.");
        string path = AssetDatabase.GetAssetPath(dungeon);
        string directory = Path.GetDirectoryName(path).Replace('\\','/') + "/" + Path.GetFileNameWithoutExtension(path) + "-floors";
        if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder(Path.GetDirectoryName(directory).Replace('\\','/'), Path.GetFileName(directory));
        var floor = ScriptableObject.CreateInstance<DungeonFloorDefinition>();
        floor.floorId = "floor." + Guid.NewGuid().ToString("N"); floor.displayName = $"{list.Count + 1}층";
        floor.map = DungeonLayoutFactory.LegacyMap(list.Count == 0 ? dungeon.layoutIndex : list.Count % 3);
        floor.monsters = copyLegacySpawns ? Clone(dungeon.monsters ?? Array.Empty<MonsterSpawnEntry>())
            : (DungeonCatalog.Active?.monsterPrefabs ?? Array.Empty<MonsterPrefab>()).Select(prefab => new MonsterSpawnEntry { prefab = prefab, count = 1 }).ToArray();
        floor.corpses = copyLegacySpawns ? Clone(dungeon.corpses ?? Array.Empty<CorpseSpawnEntry>())
            : (DungeonCatalog.Active?.corpsePrefabs ?? Array.Empty<CorpsePrefab>()).Select(prefab => new CorpseSpawnEntry { prefab = prefab, count = 1 }).ToArray();
        AssetDatabase.CreateAsset(floor, AssetDatabase.GenerateUniqueAssetPath(directory + $"/floor-{list.Count + 1:00}.asset"));
        Undo.RecordObject(dungeon, "Add dungeon floor"); list.Add(floor); dungeon.floors = list.ToArray();
        if (copyLegacySpawns)
        {
            dungeon.monsters = Array.Empty<MonsterSpawnEntry>(); dungeon.corpses = Array.Empty<CorpseSpawnEntry>();
        }
        NormalizeStairs(dungeon); EditorUtility.SetDirty(dungeon);
        return floor;
    }
    private static MonsterSpawnEntry[] Clone(MonsterSpawnEntry[] rows) => rows.Select(row => row == null ? null : new MonsterSpawnEntry { prefab = row.prefab, count = row.count }).ToArray();
    private static CorpseSpawnEntry[] Clone(CorpseSpawnEntry[] rows) => rows.Select(row => row == null ? null : new CorpseSpawnEntry { prefab = row.prefab, count = row.count }).ToArray();

    public static void NormalizeStairs(DungeonDefinition dungeon)
    {
        for (int i = 0; i < dungeon.floors.Length; i++)
        {
            var floor = dungeon.floors[i]; if (floor == null || floor.map == null) continue;
            Undo.RecordObject(floor, "Connect dungeon stairs"); var map = floor.map;
            map.hasStairsUp = i > 0; map.hasStairsDown = i < dungeon.floors.Length - 1;
            var available = new List<Vector2Int>();
            for (int x = 0; x < map.width; x++) for (int y = 0; y < map.height; y++)
                if (map.Walkable(new Vector2Int(x,y)) && new Vector2Int(x,y) != map.entrance && !map.HasHidingSpot(new Vector2Int(x,y))) available.Add(new Vector2Int(x,y));
            if (available.Count >= 2)
            {
                if (!map.Walkable(map.stairsUp) || map.stairsUp == map.entrance || map.HasHidingSpot(map.stairsUp)) map.stairsUp = available[0];
                if (!map.Walkable(map.stairsDown) || map.stairsDown == map.entrance || map.stairsDown == map.stairsUp || map.HasHidingSpot(map.stairsDown))
                    map.stairsDown = available.OrderByDescending(cell => (cell - map.entrance).sqrMagnitude).First(cell => cell != map.stairsUp);
            }
            EditorUtility.SetDirty(floor);
        }
        if (dungeon.extraExitFloor >= dungeon.floors.Length) dungeon.extraExitFloor = -1;
    }
    public static bool SaveAndRegister(DungeonDefinition dungeon, out string error)
    {
        if (dungeon == null) { error = "던전을 선택하세요."; return false; }
        if (!dungeon.IsValid(out error)) return false;
        var catalog = DungeonCatalog.Active;
        if (catalog == null) { error = "Resources/DungeonCatalog.asset이 없습니다."; return false; }
        if (catalog.dungeons == null || catalog.monsterPrefabs == null || catalog.corpsePrefabs == null)
        { error = "카탈로그의 던전·프리팹 목록을 확인하세요."; return false; }
        if (catalog.dungeons.Any(item => item != null && item != dungeon && item.dungeonId == dungeon.dungeonId))
        { error = "다른 던전과 ID가 중복되었습니다."; return false; }
        var monsters = new List<MonsterPrefab>(catalog.monsterPrefabs); var corpses = new List<CorpsePrefab>(catalog.corpsePrefabs);
        foreach (var row in dungeon.MonsterSpawns()) if (row.count > 0) monsters.Add(row.prefab);
        foreach (var row in dungeon.CorpseSpawns()) if (row.count > 0) corpses.Add(row.prefab);
        var candidate = UnityEngine.Object.Instantiate(catalog);
        try
        {
            candidate.monsterPrefabs = monsters.Distinct().ToArray(); candidate.corpsePrefabs = corpses.Distinct().ToArray();
            candidate.dungeons = catalog.dungeons.Concat(new[] { dungeon }).Distinct().ToArray();
            var issues = candidate.CollectIssues();
            if (issues.Count > 0) { error = string.Join("\n", issues); return false; }
            Undo.RecordObject(catalog, "Register dungeon");
            catalog.monsterPrefabs = candidate.monsterPrefabs; catalog.corpsePrefabs = candidate.corpsePrefabs; catalog.dungeons = candidate.dungeons;
            EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(dungeon);
            if (dungeon.floors != null) foreach (var floor in dungeon.floors) if (floor != null) EditorUtility.SetDirty(floor);
            AssetDatabase.SaveAssets(); error = null; return true;
        }
        finally { UnityEngine.Object.DestroyImmediate(candidate); }
    }

    public static void EnsureDemo()
    {
        const string path = "Assets/Resources/Dungeons/deep-salvage.asset";
        var existing = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(path);
        if (existing != null)
        {
            if (!SaveAndRegister(existing, out string existingError)) throw new InvalidOperationException(existingError);
            return;
        }
        var dungeon = Create(path, "깊은 회수 유적"); dungeon.dungeonId = "dungeon.deep-salvage";
        AddFloor(dungeon); dungeon.extraExitFloor = 1;
        if (!SaveAndRegister(dungeon, out string error)) throw new InvalidOperationException(error);
    }
}
#endif
