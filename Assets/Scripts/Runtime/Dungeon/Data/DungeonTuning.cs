using UnityEngine;

/// <summary>플레이어·전리품·시야 기본값과 풀 보관 한도를 조절하는 공용 설정.</summary>
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

    [Header("New Dungeon Vision Defaults · 새 던전의 초기값")]
    [Tooltip("새 던전의 초기값입니다. 기존 던전은 DungeonDefinition의 시야·어둠 설정을 수정하세요.")]
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

    // 구 저장 복원과 기본 프리팹 최초 생성에서만 사용한다. 현재 몬스터 조절은 프리팹 Stats가 담당한다.
    [HideInInspector, Min(.1f)] public float patrolSpeed = 1.35f;
    [HideInInspector, Min(1f)] public float patrolTravelDistance = 10f;
    [HideInInspector, Min(0f)] public float patrolArrivalPause = .75f;
    [HideInInspector, Min(.1f)] public float chaseSpeed = 2.15f;
    [HideInInspector, Min(.1f)] public float detectionRange = 3.8f;
    [HideInInspector, Min(.1f)] public float hearingRange = 8f;
    [HideInInspector, Min(.1f)] public float investigationSeconds = 6f;

#if UNITY_EDITOR
    [Header("Editor Debug")]
    [Tooltip("에디터에서만 몬스터 기본 인식·소음 청취·경계 중 인식·은신 발각 반경을 표시합니다. 플레이 중에도 전환 가능합니다.")]
    public bool showMonsterPerceptionRanges;
#endif

    [Header("Inactive Actor Pool")]
    [Tooltip("Cached instances per prefab. Zero disables retention; this does not limit active spawns.")]
    [Min(0)] public int poolCapacityPerPrefab = 32;
    [Tooltip("Total cached monster/corpse instances across prefabs. Excess returns are destroyed.")]
    [Min(0)] public int poolTotalCapacity = 128;
}
