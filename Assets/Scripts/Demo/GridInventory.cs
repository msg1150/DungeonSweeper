using System.Collections.Generic;
using UnityEngine;

/// <summary>전리품의 크기와 배치만 담당하는 공간형 가방.</summary>
public sealed class GridInventory
{
    public const int Width = 5;
    public const int Height = 4;

    private readonly int[,] cells = new int[Width, Height];
    private readonly List<StoredLoot> items = new();

    public int TotalValue { get; private set; }
    public IReadOnlyList<StoredLoot> Items => items;

    public int GetCell(int x, int y) => cells[x, y];

    public bool TryStore(LootDefinition loot)
    {
        for (int y = 0; y <= Height - loot.Height; y++)
        for (int x = 0; x <= Width - loot.Width; x++)
        {
            if (!CanPlace(loot, x, y)) continue;
            int id = items.Count + 1;
            items.Add(new StoredLoot(id, loot, new Vector2Int(x, y)));
            FillCells(loot, x, y, id);
            TotalValue += loot.Value;
            return true;
        }

        return false;
    }

    private bool CanPlace(LootDefinition loot, int startX, int startY)
    {
        for (int y = 0; y < loot.Height; y++)
        for (int x = 0; x < loot.Width; x++)
            if (cells[startX + x, startY + y] != 0) return false;
        return true;
    }

    private void FillCells(LootDefinition loot, int startX, int startY, int id)
    {
        for (int y = 0; y < loot.Height; y++)
        for (int x = 0; x < loot.Width; x++)
            cells[startX + x, startY + y] = id;
    }
}

public sealed class StoredLoot
{
    public int Id { get; }
    public LootDefinition Definition { get; }
    public Vector2Int Position { get; }

    public StoredLoot(int id, LootDefinition definition, Vector2Int position)
    {
        Id = id;
        Definition = definition;
        Position = position;
    }
}
