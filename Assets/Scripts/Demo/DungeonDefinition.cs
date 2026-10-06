using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MonsterSpawnEntry
{
    public MonsterPrefab prefab;
    [Min(0)] public int count = 1;
}
[Serializable]
public sealed class CorpseSpawnEntry
{
    public CorpsePrefab prefab;
    [Min(0)] public int count = 1;
}

[CreateAssetMenu(fileName = "Dungeon", menuName = "Dungeon Sweeper/Dungeon")]
public sealed class DungeonDefinition : ScriptableObject
{
    public string dungeonId = "dungeon.custom", displayName = "던전";
    [Range(0, 2)] public int layoutIndex;
    [Header("Monsters · 프리팹과 생성 수")]
    public MonsterSpawnEntry[] monsters = Array.Empty<MonsterSpawnEntry>();
    [Header("Corpses · 프리팹과 생성 수")]
    public CorpseSpawnEntry[] corpses = Array.Empty<CorpseSpawnEntry>();

    public bool IsValid(out string error)
    {
        error = null;
        if (!LootKinds.ValidId(dungeonId) || string.IsNullOrWhiteSpace(displayName) || layoutIndex < 0 || layoutIndex > 2
            || monsters == null || corpses == null) { error = "던전 기본 설정이 올바르지 않습니다."; return false; }
        long total = 0;
        foreach (var row in monsters)
        {
            if (row == null || row.count < 0 || (row.count > 0 && (row.prefab == null || !row.prefab.IsValid())))
            { error = "몬스터 프리팹 또는 생성 수가 올바르지 않습니다."; return false; }
            total += row.count;
        }
        foreach (var row in corpses)
        {
            if (row == null || row.count < 0 || (row.count > 0 && (row.prefab == null || !row.prefab.IsValid())))
            { error = "시체 프리팹 또는 생성 수가 올바르지 않습니다."; return false; }
            total += row.count;
        }
        if (total > DungeonLayoutFactory.PopulationCapacity(layoutIndex)) { error = "생성 수가 던전의 배치 가능한 바닥 수를 초과했습니다."; return false; }
        return true;
    }
    public List<MonsterPrefab> ExpandMonsters()
    {
        var result = new List<MonsterPrefab>();
        foreach (var row in monsters) for (int i = 0; i < row.count; i++) result.Add(row.prefab);
        return result;
    }
    public List<CorpsePrefab> ExpandCorpses()
    {
        var result = new List<CorpsePrefab>();
        foreach (var row in corpses) for (int i = 0; i < row.count; i++) result.Add(row.prefab);
        return result;
    }
}
