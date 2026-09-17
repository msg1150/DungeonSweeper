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
    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public int Value { get; }
    public LootShape Shape { get; }
    public IReadOnlyList<Vector2Int> OccupiedCells => occupiedCells;

    private readonly Vector2Int[] occupiedCells;

    public LootDefinition(string name, int width, int height, int value, LootShape shape = LootShape.Tooth)
        : this(name, value, shape, CreateRectangle(width, height))
    {
    }

    private LootDefinition(string name, int value, LootShape shape, Vector2Int[] cells)
    {
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

    public LootDefinition RotatedClockwise()
    {
        Vector2Int[] rotated = new Vector2Int[occupiedCells.Length];
        for (int i = 0; i < occupiedCells.Length; i++)
            rotated[i] = new Vector2Int(Height - 1 - occupiedCells[i].y, occupiedCells[i].x);
        return new LootDefinition(Name, Value, Shape, rotated);
    }

    private static Vector2Int[] CreateRectangle(int width, int height)
    {
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
        for (int i = 0; i < cells.Length; i++) normalized[i] = cells[i] - new Vector2Int(minX, minY);
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
        Visual.SetActive(false);
    }
}

public sealed class DismantleSession
{
    public DismantleDifficulty Difficulty { get; }
    public int Successes { get; private set; }
    public int Failures { get; private set; }
    public float PointerPosition { get; private set; }
    public float WindowStart { get; }
    public bool IsInSuccessWindow => PointerPosition >= WindowStart && PointerPosition <= WindowStart + Difficulty.SuccessWindowSize;
    public bool IsComplete => Successes >= Difficulty.RequiredSuccesses;
    public bool IsDestroyed => Failures >= Difficulty.MaxFailures;

    private float direction = 1f;

    public DismantleSession(DismantleDifficulty difficulty)
    {
        Difficulty = difficulty;
        WindowStart = Random.Range(.16f, .68f);
    }

    public void Tick(float deltaTime)
    {
        PointerPosition += direction * Difficulty.PointerSpeed * deltaTime;
        if (PointerPosition >= 1f || PointerPosition <= 0f)
        {
            PointerPosition = Mathf.Clamp01(PointerPosition);
            direction *= -1f;
        }
    }

    public void RegisterAttempt()
    {
        if (IsInSuccessWindow) Successes++;
        else Failures++;
    }
}
