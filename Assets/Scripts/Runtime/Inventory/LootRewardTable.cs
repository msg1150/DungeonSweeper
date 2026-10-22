using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class LootRewardOverride
{
    public CorpsePrefab corpse;
    public string kindId;
    [Range(0f, 1f)] public float dropChance = .5f;
    [Min(1)] public int minQuantity = 1, maxQuantity = 1;
}

/// <summary>같은 시체의 기본 전리품 중 확률·수량을 변경할 항목만 지정한다.</summary>
[CreateAssetMenu(fileName = "LootRewardTable", menuName = "Dungeon Sweeper/Loot Reward Table")]
public sealed class LootRewardTable : ScriptableObject
{
    public const int MaximumEntries = 1024;
    // Inspector의 시체별 묶음을 기억한다. 개별 설정을 켠 항목만 entries에 저장하므로 상속 의미는 유지된다.
    [SerializeField, HideInInspector] private List<CorpsePrefab> displayedCorpses = new();
    public List<LootRewardOverride> entries = new();

    public LootRewardOverride Find(CorpsePrefab corpse, string kindId)
    {
        if (entries != null) foreach (var entry in entries)
            if (entry != null && entry.corpse == corpse && entry.kindId == kindId) return entry;
        return null;
    }

    public static LootRewardOverride Resolve(CorpsePrefab corpse, string kindId, LootRewardTable dungeon, LootRewardTable floor)
        => floor?.Find(corpse, kindId) ?? dungeon?.Find(corpse, kindId);

    public bool IsValid(out string error)
    {
        error = null;
        if (entries == null || entries.Count > MaximumEntries) { error = "보상 테이블은 최대 1024행입니다."; return false; }
        var seen = new Dictionary<CorpsePrefab, HashSet<string>>();
        foreach (var entry in entries)
        {
            if (entry?.corpse == null || !entry.corpse.IsValid() || !LootKinds.ValidId(entry.kindId)
                || !ValueValidation.Nonnegative(entry.dropChance) || entry.dropChance > 1f
                || entry.minQuantity < 1 || entry.maxQuantity < entry.minQuantity || entry.maxQuantity > CorpseLootPlan.MaximumQuantity)
            { error = "보상 행의 시체·확률·수량을 확인하세요. 수량은 1~32이며 최대 수량은 최소 수량 이상이어야 합니다."; return false; }
            int matches = 0;
            foreach (var loot in entry.corpse.loot) if (loot.kindId == entry.kindId) matches++;
            if (matches != 1) { error = $"{entry.corpse.displayName}: 전리품 ID가 기본 목록에 없거나 중복입니다: {entry.kindId}"; return false; }
            if (!seen.TryGetValue(entry.corpse, out var kinds)) seen.Add(entry.corpse, kinds = new HashSet<string>());
            if (!kinds.Add(entry.kindId)) { error = "같은 시체·전리품 조합을 중복 지정할 수 없습니다."; return false; }
        }
        return true;
    }
}

[Serializable]
public sealed class CorpseLootRule
{
    public MonsterLootEntry item;
    public int minQuantity = 1, maxQuantity = 1;
    public CorpseLootRule Copy() => new() { item = item?.Copy(), minQuantity = minQuantity, maxQuantity = maxQuantity };
}

/// <summary>에셋 참조 없는 입장 시점의 보상 규칙. 미방문 층도 원본 설정 변경의 영향을 받지 않는다.</summary>
[Serializable]
public sealed class CorpseLootPlan
{
    public const int MaximumQuantity = 32, MaximumDrops = 256;
    public string prefabId;
    public List<CorpseLootRule> entries = new();
    public CorpseLootPlan Copy() => new() { prefabId = prefabId, entries = entries?.ConvertAll(entry => entry?.Copy()) };

    public static CorpseLootPlan Capture(CorpsePrefab corpse, LootRewardTable dungeon = null, LootRewardTable floor = null)
    {
        if (corpse == null || corpse.loot == null) throw new ArgumentException("A loot plan requires a corpse and its base loot list.");
        var plan = new CorpseLootPlan { prefabId = corpse.prefabId };
        foreach (var loot in corpse.loot)
        {
            var item = loot?.Copy();
            var change = LootRewardTable.Resolve(corpse, item?.kindId, dungeon, floor);
            if (item != null && change != null) item.dropChance = change.dropChance;
            plan.entries.Add(new CorpseLootRule { item = item, minQuantity = change?.minQuantity ?? 1, maxQuantity = change?.maxQuantity ?? 1 });
        }
        return plan;
    }

    public bool IsValid()
    {
        if (!LootKinds.ValidId(prefabId) || entries == null || entries.Count > MaximumDrops) return false;
        long maximum = 0;
        foreach (var entry in entries)
        {
            if (entry?.item == null || !entry.item.IsValid() || entry.minQuantity < 1 || entry.maxQuantity < entry.minQuantity
                || entry.maxQuantity > MaximumQuantity) return false;
            if (entry.item.dropChance > 0f) maximum += entry.maxQuantity;
        }
        return maximum <= MaximumDrops;
    }

    public LootDefinition[] Roll()
    {
        if (!IsValid()) throw new InvalidOperationException("Invalid corpse loot plan.");
        var result = new List<LootDefinition>();
        foreach (var entry in entries)
        {
            float chance = entry.item.dropChance;
            if (chance <= 0f || (chance < 1f && UnityEngine.Random.value >= chance)) continue;
            // 항목당 한 번 추첨하고, 성공했을 때만 수량을 결정한다. 고정 수량은 난수를 소비하지 않는다.
            int quantity = entry.minQuantity == entry.maxQuantity ? entry.minQuantity : UnityEngine.Random.Range(entry.minQuantity, entry.maxQuantity + 1);
            for (int i = 0; i < quantity; i++) result.Add(entry.item.CreateItem());
        }
        return result.ToArray();
    }
}
