using UnityEngine;

/// <summary>시체별로 유지하는 해체 진행과 포인터 상태. 화면·입력 장치와 독립적으로 갱신한다.</summary>
public sealed class DismantleSession
{
    public DismantleDifficulty Difficulty { get; }
    public int Successes { get; private set; }
    public int Failures { get; private set; }
    public float PointerPosition { get; private set; }
    public float WindowStart { get; private set; }
    public bool IsInSuccessWindow => PointerPosition >= WindowStart && PointerPosition <= WindowStart + Difficulty.SuccessWindowSize;
    public bool IsComplete => Successes >= Difficulty.RequiredSuccesses;
    public bool IsDestroyed => Failures >= Difficulty.MaxFailures;

    private float direction = 1f;

    public DismantleSession(DismantleDifficulty difficulty)
        : this(difficulty, true)
    {
    }

    private DismantleSession(DismantleDifficulty difficulty, bool randomizeWindow)
    {
        Difficulty = difficulty;
        if (difficulty == null) throw new System.ArgumentNullException(nameof(difficulty));
        // 이어하기는 저장된 창을 사용한다. 복원만으로 다른 전리품/AI의 난수 흐름을 바꾸지 않는다.
        if (!randomizeWindow) return;
        // 큰 성공 영역도 항상 막대 안에 들어가도록 한다.
        float lastStart = 1f - difficulty.SuccessWindowSize;
        // 상·하한이 같은 넓은 창도 Random.Range의 부동소수 오차로 막대 밖에 나가지 않게 한다.
        WindowStart = Mathf.Clamp(Random.Range(Mathf.Min(.16f, lastStart), Mathf.Min(.68f, lastStart)), 0f, lastStart);
    }

    public void Tick(float deltaTime)
    {
        if (!ValueValidation.Nonnegative(deltaTime)) return;
        // 프레임 지연으로 여러 번 왕복해도 초과 이동량을 버리지 않고 반사한다.
        double phase = direction < 0f ? 2d - PointerPosition : PointerPosition;
        phase = (phase + (double)Difficulty.PointerSpeed * deltaTime) % 2d;
        PointerPosition = (float)(phase <= 1d ? phase : 2d - phase);
        direction = phase < 1d ? 1f : -1f;
    }

    public void RegisterAttempt()
    {
        if (IsComplete || IsDestroyed) return;
        if (IsInSuccessWindow) Successes++;
        else Failures++;
    }

    public void AddSupplySuccess()
    {
        if (!IsComplete && !IsDestroyed) Successes++;
    }

    public DismantleSaveData Capture() => new()
    {
        successes = Successes, failures = Failures, pointer = PointerPosition,
        windowStart = WindowStart, direction = direction
    };

    public static DismantleSession Restore(DismantleDifficulty difficulty, DismantleSaveData state)
    {
        if (state == null) throw new System.ArgumentNullException(nameof(state));
        return new DismantleSession(difficulty, false)
        {
            Successes = Mathf.Clamp(state.successes, 0, difficulty.RequiredSuccesses),
            Failures = Mathf.Clamp(state.failures, 0, difficulty.MaxFailures),
            PointerPosition = Mathf.Clamp01(state.pointer),
            WindowStart = Mathf.Clamp(state.windowStart, 0f, 1f - difficulty.SuccessWindowSize),
            direction = state.direction < 0f ? -1f : 1f
        };
    }
}
