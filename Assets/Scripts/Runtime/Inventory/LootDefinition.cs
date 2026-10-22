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

/// <summary>회수품의 종류·가격·점유 도형. 회전 시 새 값을 만들고 기존 정의는 변경하지 않는다.</summary>
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
