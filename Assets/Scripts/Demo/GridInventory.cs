using System.Collections.Generic;
using UnityEngine;

/// <summary>전리품의 크기와 배치만 담당하는 공간형 가방.</summary>
public sealed class GridInventory
{
    public const int Width = 5;
    public const int Height = 4;

    private readonly int[,] cells = new int[Width, Height];
    private readonly List<StoredLoot> items = new();
    private int nextId = 1;

    public int TotalValue { get; private set; }
    public IReadOnlyList<StoredLoot> Items => items;

    public int GetCell(int x, int y) => cells[x, y];

    public StoredLoot GetItem(int id)
    {
        foreach (StoredLoot item in items)
            if (item.Id == id) return item;
        return null;
    }

    public bool TryPlace(LootDefinition loot, int startX, int startY)
    {
        if (!CanPlace(loot, startX, startY)) return false;
        int id = nextId++;
        items.Add(new StoredLoot(id, loot, new Vector2Int(startX, startY)));
        FillCells(loot, startX, startY, id);
        TotalValue += loot.Value;
        return true;
    }

    public bool CanPlace(LootDefinition loot, int startX, int startY)
    {
        if (startX < 0 || startY < 0 || startX + loot.Width > Width || startY + loot.Height > Height)
            return false;
        foreach (Vector2Int cell in loot.OccupiedCells)
            if (cells[startX + cell.x, startY + cell.y] != 0) return false;
        return true;
    }

    public bool TryMove(int itemId, int startX, int startY)
    {
        StoredLoot item = GetItem(itemId);
        if (item == null) return false;
        ClearCells(item.Definition, item.Position.x, item.Position.y);
        if (!CanPlace(item.Definition, startX, startY))
        {
            FillCells(item.Definition, item.Position.x, item.Position.y, item.Id);
            return false;
        }
        FillCells(item.Definition, startX, startY, item.Id);
        item.MoveTo(new Vector2Int(startX, startY));
        return true;
    }

    private void FillCells(LootDefinition loot, int startX, int startY, int id)
    {
        foreach (Vector2Int cell in loot.OccupiedCells)
            cells[startX + cell.x, startY + cell.y] = id;
    }

    private void ClearCells(LootDefinition loot, int startX, int startY)
    {
        foreach (Vector2Int cell in loot.OccupiedCells)
            cells[startX + cell.x, startY + cell.y] = 0;
    }
}

public sealed class StoredLoot
{
    public int Id { get; }
    public LootDefinition Definition { get; }
    public Vector2Int Position { get; private set; }

    public StoredLoot(int id, LootDefinition definition, Vector2Int position)
    {
        Id = id;
        Definition = definition;
        Position = position;
    }

    public void MoveTo(Vector2Int position) => Position = position;
}
