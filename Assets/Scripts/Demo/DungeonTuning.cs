using UnityEngine;

/// <summary>플레이 테스트용 핵심 수치를 Inspector에서 조절하는 단일 밸런스 에셋.</summary>
[CreateAssetMenu(fileName = "DungeonTuning", menuName = "Dungeon Sweeper/Dungeon Tuning")]
public sealed class DungeonTuning : ScriptableObject
{
    private static DungeonTuning active;
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

    [Header("Monster Movement")]
    [Min(.1f)] public float patrolSpeed = 1.05f;
    [Min(.1f)] public float chaseSpeed = 2.15f;
    [Min(.1f)] public float detectionRange = 3.8f;
    [Min(.1f)] public float hearingRange = 8f;
    [Min(.1f)] public float investigationSeconds = 6f;
}
