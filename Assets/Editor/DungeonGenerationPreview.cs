#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>후보는 메모리에만 만들고 채택 시에만 에셋과 카탈로그를 기록한다.</summary>
public static class DungeonGenerationPreview
{
    public static DungeonDefinition Create(DungeonGenerationSettings settings, string name)
    {
        var maps = DungeonMapGenerator.Generate(settings);
        var draft = ScriptableObject.CreateInstance<DungeonDefinition>();
        draft.hideFlags = HideFlags.HideAndDontSave; draft.name = "자동 생성 미리보기";
        draft.dungeonId = "dungeon." + Guid.NewGuid().ToString("N"); draft.displayName = name;
        draft.generationVersion = DungeonMapGenerator.Version; draft.generationSeed = settings.seed;
        draft.generationHidingSpotsPerFloor = settings.hidingSpotsPerFloor;
        draft.floors = new DungeonFloorDefinition[maps.Length];
        try
        {
            for (int i = 0; i < maps.Length; i++)
            {
                var floor = ScriptableObject.CreateInstance<DungeonFloorDefinition>(); draft.floors[i] = floor;
                floor.hideFlags = HideFlags.HideAndDontSave;
                floor.floorId = "floor." + Guid.NewGuid().ToString("N"); floor.displayName = $"{i+1}층"; floor.map = maps[i];
                var catalog = DungeonCatalog.Active;
                floor.monsters = (catalog?.monsterPrefabs ?? Array.Empty<MonsterPrefab>()).Select(prefab => new MonsterSpawnEntry { prefab = prefab, count = 0 }).ToArray();
                floor.corpses = (catalog?.corpsePrefabs ?? Array.Empty<CorpsePrefab>()).Select(prefab => new CorpseSpawnEntry { prefab = prefab, count = 0 }).ToArray();
                // 작은 도면은 용량 안에서만 기본 스폰을 채운다. 미리보기에서도 층별 수량을 바꿀 수 있다.
                int budget = Mathf.Min(256,1024/maps.Length,maps[i].PopulationCapacity);
                for (int row = 0; row < Mathf.Max(floor.monsters.Length,floor.corpses.Length) && budget > 0; row++)
                {
                    if (row < floor.corpses.Length && floor.corpses[row].prefab != null) { floor.corpses[row].count = 1; budget--; }
                    if (budget > 0 && row < floor.monsters.Length && floor.monsters[row].prefab != null) { floor.monsters[row].count = 1; budget--; }
                }
            }
            return draft;
        }
        catch { Discard(draft); throw; }
    }
    public static void Discard(DungeonDefinition draft)
    {
        if (draft == null || EditorUtility.IsPersistent(draft)) return;
        if (draft.floors != null) foreach (var floor in draft.floors)
            if (floor != null && !EditorUtility.IsPersistent(floor)) UnityEngine.Object.DestroyImmediate(floor);
        UnityEngine.Object.DestroyImmediate(draft);
    }
    public static DungeonDefinition Adopt(DungeonDefinition draft, string path, out string error)
    {
        error = null;
        if (draft == null || EditorUtility.IsPersistent(draft)) { error = "자동 생성 후보를 먼저 만드세요."; return null; }
        if (!draft.IsValid(out error)) return null;
        var catalog = DungeonCatalog.Active;
        if (catalog == null || catalog.FindDungeon(draft.dungeonId) != null) { error = "카탈로그가 없거나 이미 채택한 후보입니다."; return null; }
        if (!DungeonAuthoringService.TryNewAssetPath(path, out path, out error)) return null;
        string folder = AssetDatabase.GenerateUniqueAssetPath(Path.GetDirectoryName(path).Replace('\\','/') + "/" + Path.GetFileNameWithoutExtension(path) + "-floors");
        var oldDungeons = catalog.dungeons; var oldMonsters = catalog.monsterPrefabs; var oldCorpses = catalog.corpsePrefabs;
        var created = new List<string>(); DungeonDefinition saved = null; bool ownsFolder = false;
        try
        {
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\','/'),Path.GetFileName(folder)); ownsFolder = true;
            saved = UnityEngine.Object.Instantiate(draft); saved.hideFlags = HideFlags.None;
            saved.name = Path.GetFileNameWithoutExtension(path); saved.floors = new DungeonFloorDefinition[draft.floors.Length];
            for (int i = 0; i < saved.floors.Length; i++)
            {
                var floor = UnityEngine.Object.Instantiate(draft.floors[i]); floor.hideFlags = HideFlags.None; floor.name = $"floor-{i+1:00}";
                saved.floors[i] = floor;
                string floorPath = folder + $"/floor-{i+1:00}.asset"; created.Add(floorPath); AssetDatabase.CreateAsset(floor,floorPath);
            }
            created.Add(path); AssetDatabase.CreateAsset(saved,path);
            if (!DungeonAuthoringService.SaveAndRegister(saved,out error)) throw new InvalidOperationException(error);
            return saved;
        }
        catch (Exception exception)
        {
            error = "후보를 저장하지 못했습니다: " + exception.Message;
            catalog.dungeons = oldDungeons; catalog.monsterPrefabs = oldMonsters; catalog.corpsePrefabs = oldCorpses;
            EditorUtility.SetDirty(catalog);
            // 이 호출에서 새로 만든 경로만 정리한다. 기존 던전·층 에셋은 건드리지 않는다.
            foreach (string asset in created) AssetDatabase.DeleteAsset(asset);
            if (ownsFolder) AssetDatabase.DeleteAsset(folder);
            if (saved != null && saved.floors != null) foreach (var floor in saved.floors)
                if (floor != null && !EditorUtility.IsPersistent(floor)) UnityEngine.Object.DestroyImmediate(floor);
            if (saved != null && !EditorUtility.IsPersistent(saved)) UnityEngine.Object.DestroyImmediate(saved);
            AssetDatabase.SaveAssets(); return null;
        }
    }
}
#endif
