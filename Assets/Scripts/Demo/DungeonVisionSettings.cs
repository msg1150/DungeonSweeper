using System;
using UnityEngine;

/// <summary>던전 전체 층에 적용하는 표시 설정. AI 감지 범위와 탐색 기록에는 영향을 주지 않는다.</summary>
[Serializable]
public sealed class DungeonVisionSettings
{
    // 필드가 없는 기존 던전도 도입 시점의 3 / 5 / 0.9 표시를 유지한다.
    [Min(.1f)] public float clearSightRadius = 3f;
    [Min(.1f)] public float darkSightRadius = 5f;
    [Range(0f, 1f)] public float outerDarkness = .9f;

    public bool IsValid() => MonsterDefinition.Positive(clearSightRadius) && MonsterDefinition.Positive(darkSightRadius)
        && darkSightRadius > clearSightRadius && MonsterDefinition.Nonnegative(outerDarkness) && outerDarkness <= 1f;
    public static DungeonVisionSettings FromDefaults()
    {
        var tuning = DungeonTuning.Active;
        return new DungeonVisionSettings { clearSightRadius = tuning.clearSightRadius,
            darkSightRadius = tuning.darkSightRadius, outerDarkness = tuning.outerDarkness };
    }
}
