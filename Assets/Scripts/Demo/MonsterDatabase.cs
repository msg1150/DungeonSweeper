using System;
using System.Collections.Generic;
using UnityEngine;

public enum MonsterAttackStyle { BodySlam, DaggerSlash, ClubSwing }

[Serializable]
public sealed class MonsterLootEntry
{
    public string kindId;
    public string lootName = "전리품";
    public LootShape shape;
    [Range(0f, 1f)] public float dropChance = .6f;
    [Min(0)] public int minPrice = 100;
    [Min(0)] public int maxPrice = 300;
    [Min(1)] public int width = 1;
    [Min(1)] public int height = 1;

    public MonsterLootEntry Copy() => (MonsterLootEntry)MemberwiseClone();

    public bool IsValid() => LootKinds.ValidId(kindId) && !string.IsNullOrWhiteSpace(lootName)
        && MonsterDefinition.Nonnegative(dropChance) && dropChance <= 1f && minPrice >= 0 && maxPrice >= minPrice
        && maxPrice < int.MaxValue && width >= 1 && height >= 1 && width <= GridInventory.Width
        && height <= GridInventory.Height && Enum.IsDefined(typeof(LootShape), shape);

    public LootDefinition Roll()
    {
        if (float.IsNaN(dropChance) || dropChance <= 0f || (dropChance < 1f && UnityEngine.Random.value >= dropChance)) return null;
        int low = Mathf.Clamp(Mathf.Min(minPrice, maxPrice), 0, int.MaxValue - 1);
        int high = Mathf.Clamp(Mathf.Max(minPrice, maxPrice), low, int.MaxValue - 1);
        return new LootDefinition(lootName, width, height, UnityEngine.Random.Range(low, high + 1), shape, kindId);
    }
}

[Serializable]
public sealed class MonsterDefinition
{
    public string id = "monster";
    public string displayName = "몬스터";
    public string spriteSheetResource = "Sprites/Monsters/goblin-sheet";
    public MonsterAttackStyle attackStyle;
    [Min(1)] public int attackDamage = 15;
    [Min(.1f)] public float attackRange = .9f;
    [Min(.1f)] public float attackCooldown = 1.4f;
    [Min(.1f)] public float attackAnimationSeconds = .6f;
    // Old saves lack this flag and continue using their original global movement tuning.
    [HideInInspector] public bool hasMovementStats;
    [Min(.01f)] public float patrolSpeed = 1.35f, chaseSpeed = 2.15f;
    [Tooltip("평상시 플레이어를 발견하는 반경. 벽에 가리면 발견하지 못합니다.")]
    [Min(.01f)] public float detectionRange = 3.8f;
    [Tooltip("해체 실패 소음을 들을 수 있는 반경. 소리를 들으면 조사와 일시적인 인식 반경 확대가 시작됩니다.")]
    [Min(.01f)] public float hearingRange = 8f;
    [Min(.01f)] public float investigationSeconds = 6f, patrolTravelDistance = 10f;
    [Min(0f)] public float patrolArrivalPause = .75f;
    [Header("Noise Alert")]
    [Tooltip("소음을 들은 뒤 기본 플레이어 인식 반경에 곱할 배율. 벽에 가린 플레이어는 발견하지 못합니다.")]
    [Range(1.01f, 5f)] public float noiseDetectionMultiplier = 2f;
    [Tooltip("소음을 들은 순간부터 인식 반경이 확대되는 시간(초). 다시 들으면 이 시간으로 갱신합니다.")]
    [Min(.1f)] public float noiseAlertSeconds = 6f;
    // 이전 저장에 필드가 없으면 0이다. 기존 저장을 거부하지 않고 새 기본값으로 해석한다.
    public float NoiseDetectionMultiplier => noiseDetectionMultiplier == 0f ? 2f : noiseDetectionMultiplier;
    public float NoiseAlertSeconds => noiseAlertSeconds == 0f ? 6f : noiseAlertSeconds;
    [HideInInspector]
    public List<MonsterLootEntry> loot = new();

    public bool IsValid() => LootKinds.ValidId(id) && !string.IsNullOrWhiteSpace(displayName)
        && attackDamage >= 1 && Positive(attackRange) && Nonnegative(attackCooldown) && Positive(attackAnimationSeconds)
        && Enum.IsDefined(typeof(MonsterAttackStyle), attackStyle)
        && Nonnegative(noiseDetectionMultiplier) && NoiseDetectionMultiplier >= 1.01f && NoiseDetectionMultiplier <= 5f
        && Nonnegative(noiseAlertSeconds) && Positive(NoiseAlertSeconds)
        && (!hasMovementStats || Positive(detectionRange * NoiseDetectionMultiplier))
        && (!hasMovementStats || (Positive(patrolSpeed) && Positive(chaseSpeed) && Positive(detectionRange)
            && Positive(hearingRange) && Positive(investigationSeconds) && Positive(patrolTravelDistance) && Nonnegative(patrolArrivalPause)));
    public static bool Positive(float value) => Nonnegative(value) && value > 0f;
    public static bool Nonnegative(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    /// <summary>JSON 문자열 생성 없이 런별 능력치와 기존 드롭 목록까지 독립적으로 복사한다.</summary>
    public MonsterDefinition Copy()
    {
        var copy = (MonsterDefinition)MemberwiseClone();
        copy.loot = new List<MonsterLootEntry>(loot?.Count ?? 0);
        if (loot != null) foreach (var entry in loot) copy.loot.Add(entry?.Copy());
        return copy;
    }

    public LootDefinition[] RollLoot()
    {
        List<LootDefinition> result = new();
        if (loot == null) return result.ToArray();
        foreach (MonsterLootEntry entry in loot)
        {
            if (entry == null) continue;
            LootDefinition rolled = entry.Roll();
            if (rolled != null) result.Add(rolled);
        }
        return result.ToArray();
    }
}

[CreateAssetMenu(fileName = "MonsterDatabase", menuName = "Dungeon Sweeper/Monster Database")]
public sealed class MonsterDatabase : ScriptableObject
{
    private static MonsterDatabase active;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActive() => active = null;
    public List<MonsterDefinition> monsters = new();
    public static MonsterDatabase Active
    {
        get
        {
            if (active == null) active = Resources.Load<MonsterDatabase>("MonsterDatabase");
            if (active == null) { active = CreateInstance<MonsterDatabase>(); active.ResetDefaults(); }
            return active;
        }
    }

    public void ResetDefaults()
    {
        monsters = CreateDefaults();
    }

    public List<MonsterDefinition> GetSpawnDefinitions()
    {
        var result = new List<MonsterDefinition>();
        if (monsters != null) foreach (var monster in monsters) if (monster != null) result.Add(monster);
        if (result.Count > 0) return result;
        Debug.LogWarning("MonsterDatabase has no spawn definitions. Using the default three monsters for this run.");
        return CreateDefaults();
    }

    private static List<MonsterDefinition> CreateDefaults()
    {
        var defaults = new List<MonsterDefinition>
        {
            Make("goblin", "고블린", "Sprites/Monsters/goblin-sheet", MonsterAttackStyle.DaggerSlash, 14, .85f, "고블린 이빨", LootShape.Tooth, .8f, 30, 80, "낡은 단검", LootShape.Dagger, .35f, 100, 220),
            Make("slime", "슬라임", "Sprites/Monsters/slime-sheet", MonsterAttackStyle.BodySlam, 10, 1.05f, "슬라임 젤", LootShape.Gel, .9f, 25, 70, "슬라임 핵", LootShape.Core, .25f, 120, 300),
            Make("orc", "오크", "Sprites/Monsters/orc-sheet", MonsterAttackStyle.ClubSwing, 24, 1.15f, "두꺼운 가죽", LootShape.Hide, .7f, 100, 240, "오크 엄니", LootShape.Horn, .4f, 180, 360)
        };
        defaults[0].loot.Add(new MonsterLootEntry { kindId = LootKinds.GoblinHide, lootName = "고블린 가죽",
            shape = LootShape.Hide, dropChance = .65f, minPrice = 40, maxPrice = 90, width = 2 });
        return defaults;
    }

    private static MonsterDefinition Make(string id, string name, string path, MonsterAttackStyle style, int damage, float range,
        string lootA, LootShape shapeA, float chanceA, int minA, int maxA,
        string lootB, LootShape shapeB, float chanceB, int minB, int maxB)
    {
        MonsterDefinition m = new() { id = id, displayName = name, spriteSheetResource = path, attackStyle = style, attackDamage = damage, attackRange = range };
        m.loot.Add(new MonsterLootEntry { kindId = LootKinds.LegacyId(shapeA), lootName = lootA, shape = shapeA, dropChance = chanceA, minPrice = minA, maxPrice = maxA });
        m.loot.Add(new MonsterLootEntry { kindId = LootKinds.LegacyId(shapeB), lootName = lootB, shape = shapeB, dropChance = chanceB, minPrice = minB, maxPrice = maxB, height = shapeB == LootShape.Dagger ? 3 : 1 });
        return m;
    }
}
