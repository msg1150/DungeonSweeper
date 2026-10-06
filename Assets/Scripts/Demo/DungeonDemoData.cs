using System.Collections.Generic;
using UnityEngine;

public enum LootShape
{
    Tooth,
    Dagger,
    Gel,
    Core,
    Hide,
    Horn
}

public sealed class LootDefinition
{
    public string KindId { get; }
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public int Value { get; }
    public LootShape Shape { get; }
    public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;

    private readonly Vector2Int[] occupiedCells;

    public LootDefinition(string name, int width, int height, int value, LootShape shape = LootShape.Tooth, string kindId = null)
        : this(name, value, shape, CreateRectangle(width, height), kindId)
    {
    }

    private LootDefinition(string name, int value, LootShape shape, Vector2Int[] cells, string kindId = null)
    {
        if (value < 0) throw new System.ArgumentOutOfRangeException(nameof(value));
        KindId = string.IsNullOrEmpty(kindId) ? LootKinds.LegacyId(shape) : kindId;
        if (!LootKinds.ValidId(KindId)) throw new System.ArgumentException("Invalid loot kind ID.");
        Name = name;
        Value = value;
        Shape = shape;
        occupiedCells = Normalize(cells);
        foreach (Vector2Int cell in occupiedCells)
        {
            Width = Mathf.Max(Width, cell.x + 1);
            Height = Mathf.Max(Height, cell.y + 1);
        }
    }

    public static LootDefinition CreateShaped(string name, int value, LootShape shape, params Vector2Int[] cells)
    {
        return new LootDefinition(name, value, shape, cells);
    }

    public static LootDefinition CreateIdentified(string kindId, string name, int value, LootShape shape, Vector2Int[] cells)
        => new(name, value, shape, cells, kindId);

    public LootDefinition RotatedClockwise()
    {
        Vector2Int[] rotated = new Vector2Int[occupiedCells.Length];
        for (int i = 0; i < occupiedCells.Length; i++)
            rotated[i] = new Vector2Int(Height - 1 - occupiedCells[i].y, occupiedCells[i].x);
        return new LootDefinition(Name, Value, Shape, rotated, KindId);
    }

    private static Vector2Int[] CreateRectangle(int width, int height)
    {
        if (width < 1 || height < 1 || width > 100 || height > 100 || (long)width * height > 1024)
            throw new System.ArgumentOutOfRangeException(nameof(width), "Unsupported loot dimensions.");
        Vector2Int[] cells = new Vector2Int[width * height];
        int index = 0;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            cells[index++] = new Vector2Int(x, y);
        return cells;
    }

    private static Vector2Int[] Normalize(Vector2Int[] cells)
    {
        if (cells == null || cells.Length == 0) return new[] { Vector2Int.zero };
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        foreach (Vector2Int cell in cells)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
        }
        Vector2Int[] normalized = new Vector2Int[cells.Length];
        if (cells.Length > 1024) throw new System.ArgumentException("Too many loot cells.");
        HashSet<Vector2Int> unique = new();
        for (int i = 0; i < cells.Length; i++)
        {
            long x = (long)cells[i].x - minX, y = (long)cells[i].y - minY;
            if (x > 100 || y > 100 || !unique.Add(new Vector2Int((int)x, (int)y)))
                throw new System.ArgumentException("Invalid or duplicate loot cell.");
            normalized[i] = new Vector2Int((int)x, (int)y);
        }
        return normalized;
    }
}

public sealed class DismantleDifficulty
{
    public int RequiredSuccesses { get; }
    public int MaxFailures { get; }
    public float PointerSpeed { get; }
    public float SuccessWindowSize { get; }

    public DismantleDifficulty(int requiredSuccesses, int maxFailures, float pointerSpeed, float successWindowSize)
    {
        if (requiredSuccesses < 1 || maxFailures < 1 || !MonsterDefinition.Positive(pointerSpeed)
            || !MonsterDefinition.Positive(successWindowSize) || successWindowSize > 1f)
            throw new System.ArgumentException("Invalid dismantle difficulty.");
        RequiredSuccesses = requiredSuccesses;
        MaxFailures = maxFailures;
        PointerSpeed = pointerSpeed;
        SuccessWindowSize = successWindowSize;
    }
}

public sealed class CorpseRunData
{
    public GameObject Visual { get; }
    public string Name { get; }
    public DismantleDifficulty Difficulty { get; }
    public IReadOnlyList<LootDefinition> Loot { get; }
    public bool IsProcessed { get; private set; }
    public int MonsterIndex { get; set; }
    public string PrefabId { get; set; }
    private DismantleSession session;
    public DismantleSession Session => session ??= new DismantleSession(Difficulty);
    public DismantleSaveData CaptureSession() => session?.Capture();
    public void RestoreSession(DismantleSaveData state) => session = state == null ? null : DismantleSession.Restore(Difficulty, state);

    public CorpseRunData(GameObject visual, string name, DismantleDifficulty difficulty, params LootDefinition[] loot)
    {
        Visual = visual;
        Name = name;
        Difficulty = difficulty;
        Loot = loot;
    }

    public void MarkProcessed()
    {
        IsProcessed = true;
        // 즉시 풀에 반환하면 이 런의 저장 위치가 다음 대여 위치로 바뀐다. 런 종료까지 소유한다.
        Visual.SetActive(false);
    }
}

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
    {
        Difficulty = difficulty;
        if (difficulty == null) throw new System.ArgumentNullException(nameof(difficulty));
        // 큰 성공 영역도 항상 막대 안에 들어가도록 한다.
        float lastStart = 1f - difficulty.SuccessWindowSize;
        WindowStart = Random.Range(Mathf.Min(.16f, lastStart), Mathf.Min(.68f, lastStart));
    }

    public void Tick(float deltaTime)
    {
        if (!MonsterDefinition.Nonnegative(deltaTime)) return;
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
        return new DismantleSession(difficulty)
        {
            Successes = Mathf.Clamp(state.successes, 0, difficulty.RequiredSuccesses),
            Failures = Mathf.Clamp(state.failures, 0, difficulty.MaxFailures),
            PointerPosition = Mathf.Clamp01(state.pointer),
            WindowStart = Mathf.Clamp(state.windowStart, 0f, 1f - difficulty.SuccessWindowSize),
            direction = state.direction < 0f ? -1f : 1f
        };
    }
}
