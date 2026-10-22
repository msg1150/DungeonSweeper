using UnityEngine;

/// <summary>에셋·저장·입력 경계에서 공유하는 유한 수치 검사. 몬스터 데이터에 의존하지 않는다.</summary>
public static class ValueValidation
{
    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    public static bool Finite(Vector2 value) => Finite(value.x) && Finite(value.y);
    public static bool Nonnegative(float value) => Finite(value) && value >= 0f;
    public static bool Positive(float value) => Finite(value) && value > 0f;
}
