using UnityEngine;

/// <summary>플레이 테스트용 핵심 수치를 Inspector에서 조절하는 단일 밸런스 에셋.</summary>
[CreateAssetMenu(fileName = "DungeonTuning", menuName = "Dungeon Sweeper/Dungeon Tuning")]
public sealed class DungeonTuning : ScriptableObject
{
    private static DungeonTuning active;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActive() => active = null;
    public static DungeonTuning Active
    {
        get
        {
            if (active == null) active = Resources.Load<DungeonTuning>("DungeonTuning");
            if (active == null) active = CreateInstance<DungeonTuning>();
            return active;
        }
    }

    [Header("Loot Economy")]
    [Min(.1f)] public float lootValueMultiplier = 1f;

    [Header("Player Vision")]
    [Min(10f)] public float fogWorldSize = 32f;
    [Min(.1f)] public float clearSightRadius = 4.5f;
    [Min(.1f)] public float darkSightRadius = 11f;
    [Range(0f, 1f)] public float outerDarkness = .5f;

    [Header("Player Combat")]
    [Min(1)] public int playerMaxHealth = 100;
    [Min(0f)] public float playerHitInvulnerability = .55f;
    [Min(.1f)] public float playerMoveSpeed = 5f;
    [Min(.1f)] public float playerDashSpeed = 12f;
    [Min(.02f)] public float playerDashDuration = .15f;
    [Min(0f)] public float playerDashCooldown = 1f;

    [Header("Monster Movement")]
    [Min(.1f)] public float patrolSpeed = 1.35f;
    [Min(1f)] public float patrolTravelDistance = 10f;
    [Min(0f)] public float patrolArrivalPause = .75f;
    [Min(.1f)] public float chaseSpeed = 2.15f;
    [Min(.1f)] public float detectionRange = 3.8f;
    [Min(.1f)] public float hearingRange = 8f;
    [Min(.1f)] public float investigationSeconds = 6f;

    [Header("Inactive Actor Pool")]
    [Tooltip("Cached instances per prefab. Zero disables retention; this does not limit active spawns.")]
    [Min(0)] public int poolCapacityPerPrefab = 32;
    [Tooltip("Total cached monster/corpse instances across prefabs. Excess returns are destroyed.")]
    [Min(0)] public int poolTotalCapacity = 128;
}
