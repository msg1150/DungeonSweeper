using System.Collections.Generic;
using UnityEngine;

/// <summary>전리품의 크기와 배치만 담당하는 공간형 가방.</summary>
public sealed class GridInventory
{
    public const int Width = 5;
    public const int Height = 4;
    public const int MaximumWidth = 8, MaximumHeight = 8;
    public int Columns { get; }
    public int Rows { get; }

    private readonly int[,] cells;
    private readonly List<StoredLoot> items = new();
    private int nextId = 1;

    public int TotalValue { get; private set; }
    public IReadOnlyList<StoredLoot> Items => items;

    public GridInventory(int width = Width, int height = Height)
    {
        if (width < 1 || height < 1 || width > MaximumWidth || height > MaximumHeight)
            throw new System.ArgumentOutOfRangeException(nameof(width));
        Columns = width; Rows = height; cells = new int[width, height];
    }

    public int GetCell(int x, int y) => cells[x, y];

    public StoredLoot GetItem(int id)
    {
        foreach (StoredLoot item in items)
            if (item.Id == id) return item;
        return null;
    }

    public bool ContainsShape(LootShape shape)
    {
        foreach (StoredLoot item in items)
            if (item.Definition.Shape == shape) return true;
        return false;
    }

    public bool TryPlace(LootDefinition loot, int startX, int startY)
    {
        if (loot == null) return false;
        return Place(loot, startX, startY, Price(loot));
    }

    private static int Price(LootDefinition loot)
    {
        double multiplier = DungeonTuning.Active.lootValueMultiplier;
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier)) multiplier = 1d;
        double value = System.Math.Round(loot.Value * System.Math.Max(0d, multiplier));
        return (int)System.Math.Min(int.MaxValue, System.Math.Max(0d, value));
    }

    private bool Place(LootDefinition loot, int startX, int startY, int reward)
    {
        if (!CanPlace(loot, startX, startY)) return false;
        int id = nextId++;
        items.Add(new StoredLoot(id, loot, new Vector2Int(startX, startY), reward));
        FillCells(loot, startX, startY, id);
        TotalValue = (int)System.Math.Min(int.MaxValue, (long)TotalValue + reward);
        return true;
    }

    public bool CanPlace(LootDefinition loot, int startX, int startY)
    {
        if (loot == null || startX < 0 || startY < 0 || startX > Columns - loot.Width || startY > Rows - loot.Height)
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

    public void Clear()
    {
        System.Array.Clear(cells, 0, cells.Length);
        items.Clear();
        TotalValue = 0;
        nextId = 1;
    }

    public List<StoredLootSaveData> Capture()
    {
        List<StoredLootSaveData> result = new();
        foreach (StoredLoot item in items)
            result.Add(new StoredLootSaveData { loot = LootSaveData.Capture(item.Definition), position = item.Position,
                hasSaleValue = true, saleValue = item.SaleValue });
        return result;
    }

    public void Restore(List<StoredLootSaveData> savedItems, int totalValue)
    {
        Clear();
        foreach (StoredLootSaveData item in savedItems)
        {
            var loot = item.loot.Restore();
            if (!Place(loot, item.position.x, item.position.y, item.hasSaleValue ? item.saleValue : Price(loot)))
                throw new System.ArgumentException("Saved inventory has an invalid placement.");
        }
        TotalValue = Mathf.Max(0, totalValue);
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
    public int SaleValue { get; }

    public StoredLoot(int id, LootDefinition definition, Vector2Int position, int saleValue)
    {
        Id = id;
        Definition = definition;
        Position = position;
        SaleValue = saleValue;
    }

    public void MoveTo(Vector2Int position) => Position = position;
}
