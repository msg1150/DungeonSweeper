using System;
using System.Collections.Generic;
using UnityEngine;

public enum MonsterAttackStyle { BodySlam, DaggerSlash, ClubSwing }

[Serializable]
public sealed class MonsterLootEntry
{
    public string lootName = "전리품";
    public LootShape shape;
    [Range(0f, 1f)] public float dropChance = .6f;
    [Min(0)] public int minPrice = 100;
    [Min(0)] public int maxPrice = 300;
    [Min(1)] public int width = 1;
    [Min(1)] public int height = 1;

    public LootDefinition Roll()
    {
        if (UnityEngine.Random.value > dropChance) return null;
        int low = Mathf.Min(minPrice, maxPrice);
        int high = Mathf.Max(minPrice, maxPrice);
        return new LootDefinition(lootName, width, height, UnityEngine.Random.Range(low, high + 1), shape);
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
    public List<MonsterLootEntry> loot = new();

    public LootDefinition[] RollLoot()
    {
        List<LootDefinition> result = new();
        foreach (MonsterLootEntry entry in loot)
        {
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
        monsters = new List<MonsterDefinition>
        {
            Make("goblin", "고블린", "Sprites/Monsters/goblin-sheet", MonsterAttackStyle.DaggerSlash, 14, .85f, "고블린 이빨", LootShape.Tooth, .8f, 30, 80, "낡은 단검", LootShape.Dagger, .35f, 100, 220),
            Make("slime", "슬라임", "Sprites/Monsters/slime-sheet", MonsterAttackStyle.BodySlam, 10, 1.05f, "슬라임 젤", LootShape.Gel, .9f, 25, 70, "슬라임 핵", LootShape.Core, .25f, 120, 300),
            Make("orc", "오크", "Sprites/Monsters/orc-sheet", MonsterAttackStyle.ClubSwing, 24, 1.15f, "두꺼운 가죽", LootShape.Hide, .7f, 100, 240, "오크 엄니", LootShape.Horn, .4f, 180, 360)
        };
    }

    private static MonsterDefinition Make(string id, string name, string path, MonsterAttackStyle style, int damage, float range,
        string lootA, LootShape shapeA, float chanceA, int minA, int maxA,
        string lootB, LootShape shapeB, float chanceB, int minB, int maxB)
    {
        MonsterDefinition m = new() { id = id, displayName = name, spriteSheetResource = path, attackStyle = style, attackDamage = damage, attackRange = range };
        m.loot.Add(new MonsterLootEntry { lootName = lootA, shape = shapeA, dropChance = chanceA, minPrice = minA, maxPrice = maxA });
        m.loot.Add(new MonsterLootEntry { lootName = lootB, shape = shapeB, dropChance = chanceB, minPrice = minB, maxPrice = maxB, height = shapeB == LootShape.Dagger ? 3 : 1 });
        return m;
    }
}
