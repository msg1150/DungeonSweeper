using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>방문하지 않은 층의 도면·스폰 목록도 입장 시 확정한다. 계단 이동으로 재추첨하지 않는다.</summary>
[Serializable]
public sealed class DungeonFloorState
{
    public string floorId, displayName;
    public DungeonFloorMap map;
    public bool visited;
    public string[] monsterIds, corpseIds;
    public int lootPlanVersion;
    public List<CorpseLootPlan> lootPlans = new();
    public Vector2 specialGate;
    public List<CorpseSaveData> corpses = new();
    public List<EnemySaveData> enemies = new();
    public List<Vector2Int> discoveredCells = new();
    public DungeonFloorState Copy()
    {
        var copy = (DungeonFloorState)MemberwiseClone();
        copy.map = map?.Copy();
        copy.monsterIds = monsterIds == null ? null : (string[])monsterIds.Clone();
        copy.corpseIds = corpseIds == null ? null : (string[])corpseIds.Clone();
        copy.lootPlans = lootPlans?.ConvertAll(item => item?.Copy());
        copy.corpses = corpses?.ConvertAll(item => item?.Copy());
        copy.enemies = enemies?.ConvertAll(item => item?.Copy());
        copy.discoveredCells = discoveredCells == null ? null : new List<Vector2Int>(discoveredCells);
        return copy;
    }

    public CorpseLootPlan FindLootPlan(string prefabId)
    {
        if (lootPlanVersion == 1 && lootPlans != null) foreach (var plan in lootPlans)
            if (plan != null && plan.prefabId == prefabId) return plan;
        return null;
    }

    public bool HasValidLootPlans()
    {
        if (lootPlanVersion == 0) return lootPlans == null || lootPlans.Count == 0; // 이전 저장은 기존 기본 드롭 경로를 유지한다.
        if (lootPlanVersion != 1 || lootPlans == null || corpseIds == null || lootPlans.Count > 256) return false;
        var required = new HashSet<string>(corpseIds);
        foreach (var plan in lootPlans)
            if (plan == null || !plan.IsValid() || !required.Remove(plan.prefabId)) return false;
        return required.Count == 0;
    }
}

[Serializable]
public sealed class DungeonExpeditionData
{
    public const int MaximumFloors = 16;
    public int version = 1, currentFloor, exitFloor;
    public List<DungeonFloorState> floors = new();
    public DungeonFloorState Current => floors[currentFloor];
    /// <summary>JSON 문자열과 파서 없이 가변 목록·도면·능력치를 모두 독립적으로 복사한다.</summary>
    public DungeonExpeditionData Copy()
    {
        var copy = (DungeonExpeditionData)MemberwiseClone();
        copy.floors = floors?.ConvertAll(floor => floor?.Copy());
        return copy;
    }
    public static DungeonExpeditionData Create(DungeonDefinition dungeon)
    {
        if (dungeon.floors == null || dungeon.floors.Length == 0) return null; // 이전 단층 저장과 호환한다.
        var result = new DungeonExpeditionData { exitFloor = dungeon.extraExitFloor < 0 ? UnityEngine.Random.Range(0, dungeon.floors.Length) : dungeon.extraExitFloor };
        foreach (var floor in dungeon.floors)
        {
            var monsters = new List<string>(); var corpses = new List<string>();
            foreach (var row in floor.monsters) for (int i = 0; i < row.count; i++) monsters.Add(row.prefab.prefabId);
            foreach (var row in floor.corpses) for (int i = 0; i < row.count; i++) corpses.Add(row.prefab.prefabId);
            var state = new DungeonFloorState { floorId = floor.floorId, displayName = floor.displayName, map = floor.map.Copy(),
                monsterIds = monsters.ToArray(), corpseIds = corpses.ToArray(), lootPlanVersion = 1 };
            var captured = new HashSet<CorpsePrefab>();
            foreach (var row in floor.corpses)
                if (row.count > 0 && captured.Add(row.prefab)) state.lootPlans.Add(CorpseLootPlan.Capture(row.prefab, dungeon.lootTable, floor.lootTable));
            result.floors.Add(state);
        }
        return result;
    }

    public bool IsValid()
    {
        if (version != 1 || floors == null || floors.Count < 1 || floors.Count > MaximumFloors || currentFloor < 0 || currentFloor >= floors.Count || exitFloor < 0 || exitFloor >= floors.Count) return false;
        var ids = new HashSet<string>(); long population = 0;
        for (int i = 0; i < floors.Count; i++)
        {
            var floor = floors[i];
            if (floor == null || !LootKinds.ValidId(floor.floorId) || !ids.Add(floor.floorId) || string.IsNullOrWhiteSpace(floor.displayName)
                || floor.map == null || !floor.map.IsValid(out _) || floor.map.hasStairsUp != (i > 0) || floor.map.hasStairsDown != (i < floors.Count - 1)
                || floor.monsterIds == null || floor.corpseIds == null || !GameSaveData.Finite(floor.specialGate)) return false;
            int count = floor.monsterIds.Length + floor.corpseIds.Length; population += count;
            if (count > floor.map.PopulationCapacity || count > 256 || population > 1024) return false;
            foreach (string id in floor.monsterIds) if (DungeonCatalog.Active?.FindMonster(id) == null) return false;
            foreach (string id in floor.corpseIds) if (DungeonCatalog.Active?.FindCorpse(id) == null) return false;
            if (!floor.HasValidLootPlans()) return false;
            if (floor.corpses == null || floor.enemies == null || floor.discoveredCells == null) return false;
            if (!floor.visited) { if (floor.corpses.Count != 0 || floor.enemies.Count != 0 || floor.discoveredCells.Count != 0) return false; continue; }
            // 기존 검증을 공유하여 비활성 층의 적·전리품 데이터도 검사한다.
            var snapshot = new DungeonSaveData { mapSnapshot = floor.map, hasMapSnapshot = true, health = 1, corpses = floor.corpses,
                enemies = floor.enemies, discoveredCells = floor.discoveredCells, specialGate = floor.specialGate };
            if (!snapshot.IsValid()) return false;
        }
        return true;
    }
}
