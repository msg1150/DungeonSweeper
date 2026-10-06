using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DungeonCatalog", menuName = "Dungeon Sweeper/Dungeon Catalog")]
public sealed class DungeonCatalog : ScriptableObject
{
    public DungeonDefinition[] dungeons = Array.Empty<DungeonDefinition>();
    [Header("All prefabs · 스폰 및 기존 저장 복원용 목록")]
    [Tooltip("Register every monster prefab here. Keep entries used by older saves even after removing their dungeon spawn rows.")]
    public MonsterPrefab[] monsterPrefabs = Array.Empty<MonsterPrefab>();
    [Tooltip("Register every corpse prefab here. Assets can be outside Resources.")]
    public CorpsePrefab[] corpsePrefabs = Array.Empty<CorpsePrefab>();
    private static DungeonCatalog active;
    public static DungeonCatalog Active => active != null ? active : active = Resources.Load<DungeonCatalog>("DungeonCatalog");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => active = null;

    public DungeonDefinition FindDungeon(string id)
    {
        if (dungeons != null) foreach (var item in dungeons) if (item != null && item.dungeonId == id) return item;
        return null;
    }
    public MonsterPrefab FindMonster(string id)
    {
        if (monsterPrefabs != null) foreach (var prefab in monsterPrefabs)
            if (prefab != null && prefab.prefabId == id) return prefab;
        return null;
    }
    public CorpsePrefab FindCorpse(string id)
    {
        if (corpsePrefabs != null) foreach (var prefab in corpsePrefabs)
            if (prefab != null && prefab.prefabId == id) return prefab;
        return null;
    }
    public DungeonDefinition Pick(out string error)
    {
        error = null;
        var issues = CollectIssues();
        if (issues.Count > 0) { error = "던전 카탈로그 오류: " + string.Join("; ", issues); return null; }
        if (dungeons == null || dungeons.Length == 0) { error = "등록된 던전이 없습니다."; return null; }
        var selected = dungeons[UnityEngine.Random.Range(0, dungeons.Length)];
        if (selected == null || !selected.IsValid(out error)) { error ??= "던전 설정이 비어 있습니다."; return null; }
        return selected;
    }
    public List<string> CollectIssues()
    {
        var issues = new List<string>();
        var ids = new HashSet<string>();
        var prefabs = new Dictionary<string, UnityEngine.Object>();
        if (monsterPrefabs == null || corpsePrefabs == null) issues.Add("Missing prefab registry arrays.");
        if (monsterPrefabs != null) foreach (var prefab in monsterPrefabs)
        {
            if (prefab == null) { issues.Add("Null monster registry entry."); continue; }
            if (!prefab.IsValid()) issues.Add("Invalid registered monster prefab: " + prefab.name);
            Register(prefab.prefabId, prefab, prefabs, issues);
        }
        if (corpsePrefabs != null) foreach (var prefab in corpsePrefabs)
        {
            if (prefab == null) { issues.Add("Null corpse registry entry."); continue; }
            if (!prefab.IsValid()) issues.Add("Invalid registered corpse prefab: " + prefab.name);
            Register(prefab.prefabId, prefab, prefabs, issues);
        }
        if (dungeons == null || dungeons.Length == 0) { issues.Add("At least one dungeon definition is required."); return issues; }
        foreach (var dungeon in dungeons)
        {
            if (dungeon == null) { issues.Add("Null dungeon definition."); continue; }
            if (!ids.Add(dungeon.dungeonId)) issues.Add("Duplicate dungeon ID: " + dungeon.dungeonId);
            if (!dungeon.IsValid(out var error)) issues.Add(dungeon.name + ": " + error);
            if (dungeon.monsters != null) foreach (var row in dungeon.monsters)
                if (row?.prefab != null && FindMonster(row.prefab.prefabId) != row.prefab)
                    issues.Add("Dungeon monster must be registered in DungeonCatalog: " + row.prefab.name);
            if (dungeon.corpses != null) foreach (var row in dungeon.corpses)
                if (row?.prefab != null && FindCorpse(row.prefab.prefabId) != row.prefab)
                    issues.Add("Dungeon corpse must be registered in DungeonCatalog: " + row.prefab.name);
        }
        return issues;
    }
    private static void Register(string id, UnityEngine.Object prefab, Dictionary<string, UnityEngine.Object> known, List<string> issues)
    {
        if (!LootKinds.ValidId(id)) { issues.Add("Invalid prefab ID."); return; }
        if (known.TryGetValue(id, out var previous) && previous != prefab) issues.Add("Different prefabs share an ID: " + id);
        else known[id] = prefab;
    }
}
