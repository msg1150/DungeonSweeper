using System;
using System.Collections.Generic;
using UnityEngine;

public enum DungeonDifficulty
{
    [InspectorName("쉬움")] Easy,
    [InspectorName("보통")] Normal,
    [InspectorName("어려움")] Hard,
    [InspectorName("매우 어려움")] VeryHard
}

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

public sealed class DungeonDefinition : ScriptableObject
{
    [HideInInspector] public int generationHidingSpotsPerFloor;
    public string dungeonId = "dungeon.custom", displayName = "던전";
    public LootRewardTable lootTable;
    public DungeonVisionSettings vision = new();
    [Header("Dungeon selection · 입장 선택창")]
    [Tooltip("선택창에 표시할 난이도입니다. 몬스터 능력치와 스폰 수는 별도로 설정합니다.")]
    public DungeonDifficulty difficulty = DungeonDifficulty.Normal;
    [Tooltip("선택창 카드에 표시할 이미지입니다. 비워 두어도 입장할 수 있습니다.")]
    public Texture2D selectionImage;
    [TextArea(2, 4)] public string selectionDescription = "";
    public string DifficultyLabel => difficulty switch
    {
        DungeonDifficulty.Easy => "쉬움", DungeonDifficulty.Normal => "보통",
        DungeonDifficulty.Hard => "어려움", DungeonDifficulty.VeryHard => "매우 어려움", _ => "미설정"
    };
    // 구 단층 에셋의 실행·도면 변환에만 필요하다. 현재 제작은 floors에서만 설정한다.
    [HideInInspector, Range(0, 2)] public int layoutIndex;
    [HideInInspector]
    public MonsterSpawnEntry[] monsters = Array.Empty<MonsterSpawnEntry>();
    [HideInInspector]
    public CorpseSpawnEntry[] corpses = Array.Empty<CorpseSpawnEntry>();
    [Header("Authoring tool · 층별 도면과 스폰 설정 (비어 있으면 이전 단층 맵)")]
    public DungeonFloorDefinition[] floors = Array.Empty<DungeonFloorDefinition>();
    [Tooltip("추가 탈출 포탈이 나올 층. -1은 입장 시 전체 층 중 하나를 무작위 선택합니다.")]
    public int extraExitFloor = -1;
    [HideInInspector] public int generationVersion, generationSeed;

    /// <summary>층이 있으면 층별 설정만 사용한다. 남아 있는 구 단층 설정은 스폰·등록·출시 검사에서 제외한다.</summary>
    public IEnumerable<MonsterSpawnEntry> MonsterSpawns()
    {
        if (floors != null && floors.Length > 0)
        {
            foreach (var floor in floors) if (floor?.monsters != null)
                foreach (var row in floor.monsters) if (row != null) yield return row;
        }
        else if (monsters != null) foreach (var row in monsters) if (row != null) yield return row;
    }

    public IEnumerable<CorpseSpawnEntry> CorpseSpawns()
    {
        if (floors != null && floors.Length > 0)
        {
            foreach (var floor in floors) if (floor?.corpses != null)
                foreach (var row in floor.corpses) if (row != null) yield return row;
        }
        else if (corpses != null) foreach (var row in corpses) if (row != null) yield return row;
    }

    public bool IsValid(out string error)
    {
        error = null;
        if (vision != null && !vision.IsValid())
        { error = "던전 시야 반경은 양수이며 바깥 반경이 선명 반경보다 커야 합니다. 어둠 강도는 0~1 사이여야 합니다."; return false; }
        if (!Enum.IsDefined(typeof(DungeonDifficulty), difficulty))
        { error = "던전 난이도 설정이 올바르지 않습니다."; return false; }
        if (lootTable != null && !lootTable.IsValid(out error)) return false;
        if (floors != null && floors.Length > 0)
        {
            if (!LootKinds.ValidId(dungeonId) || string.IsNullOrWhiteSpace(displayName) || floors.Length > DungeonExpeditionData.MaximumFloors
                || extraExitFloor < -1 || extraExitFloor >= floors.Length) { error = "던전 ID, 층 수 또는 추가 포탈 층 설정이 올바르지 않습니다."; return false; }
            var ids = new HashSet<string>(); long population = 0;
            for (int i = 0; i < floors.Length; i++)
            {
                var floor = floors[i];
                if (floor == null || !LootKinds.ValidId(floor.floorId) || !ids.Add(floor.floorId) || string.IsNullOrWhiteSpace(floor.displayName))
                { error = "층 에셋이 없거나 층 ID가 중복되었습니다."; return false; }
                if (floor.map == null || !floor.map.IsValid(out error)) return false;
                if (floor.map.hasStairsUp != (i > 0) || floor.map.hasStairsDown != (i < floors.Length - 1))
                { error = $"{i + 1}층 계단 연결을 확인하세요. 첫 층은 아래로, 마지막 층은 위로, 중간 층은 양쪽 계단이 필요합니다."; return false; }
                if (!ValidateSpawns(floor.monsters, floor.corpses, floor.map.PopulationCapacity, out error, out long count)) return false;
                if (!ValidateRewards(floor.corpses, floor.lootTable, out error)) return false;
                population += count;
            }
            if (population > 1024) { error = "한 탐험의 전체 몬스터·시체 수는 1024개 이하여야 합니다."; return false; }
            return true;
        }
        if (!LootKinds.ValidId(dungeonId) || string.IsNullOrWhiteSpace(displayName) || layoutIndex < 0 || layoutIndex > 2
            || monsters == null || corpses == null) { error = "던전 기본 설정이 올바르지 않습니다."; return false; }
        return ValidateSpawns(monsters, corpses, DungeonLayoutFactory.PopulationCapacity(layoutIndex), out error, out _)
            && ValidateRewards(corpses, null, out error);
    }
    private bool ValidateRewards(CorpseSpawnEntry[] spawns, LootRewardTable floorTable, out string error)
    {
        error = null;
        if (floorTable != null && !floorTable.IsValid(out error)) return false;
        foreach (var row in spawns)
            if (row.count > 0 && !CorpseLootPlan.Capture(row.prefab, lootTable, floorTable).IsValid())
            { error = "보상 설정이 유효하지 않거나 시체 하나의 최대 전리품 수가 256개를 초과합니다."; return false; }
        return true;
    }
    public static bool ValidateSpawns(MonsterSpawnEntry[] monsters, CorpseSpawnEntry[] corpses, int capacity, out string error, out long total)
    {
        error = null; total = 0;
        if (monsters == null || corpses == null) { error = "스폰 목록이 없습니다."; return false; }
        foreach (var row in monsters)
        {
            if (row == null || row.count < 0 || (row.count > 0 && (row.prefab == null || !row.prefab.IsValid())))
            { error = "몬스터 프리팹 또는 수량이 올바르지 않습니다."; return false; }
            total += row.count;
        }
        foreach (var row in corpses)
        {
            if (row == null || row.count < 0 || (row.count > 0 && (row.prefab == null || !row.prefab.IsValid())))
            { error = "시체 프리팹 또는 수량이 올바르지 않습니다."; return false; }
            total += row.count;
        }
        if (total > capacity || total > 256) { error = "층의 생성 수가 배치 가능한 바닥 수 또는 256개를 초과했습니다."; return false; }
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
