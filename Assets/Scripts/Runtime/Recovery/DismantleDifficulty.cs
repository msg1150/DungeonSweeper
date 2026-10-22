using UnityEngine;

/// <summary>해체 성공·실패 한도와 포인터 규칙을 검증한 값 객체.</summary>
public sealed class DismantleDifficulty
{
    public int RequiredSuccesses { get; }
    public int MaxFailures { get; }
    public float PointerSpeed { get; }
    public float SuccessWindowSize { get; }

    public DismantleDifficulty(int requiredSuccesses, int maxFailures, float pointerSpeed, float successWindowSize)
    {
        if (requiredSuccesses < 1 || maxFailures < 1 || !ValueValidation.Positive(pointerSpeed)
            || !ValueValidation.Positive(successWindowSize) || successWindowSize > 1f)
            throw new System.ArgumentException("Invalid dismantle difficulty.");
        RequiredSuccesses = requiredSuccesses;
        MaxFailures = maxFailures;
        PointerSpeed = pointerSpeed;
        SuccessWindowSize = successWindowSize;
    }
}
