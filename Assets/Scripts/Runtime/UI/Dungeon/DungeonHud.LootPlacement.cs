using UnityEngine;

/// <summary>가방·회수품 카드와 드래그·회전 입력을 그리는 표시 계층.</summary>
public sealed partial class DungeonHud
{
    private void DrawLootPlacementModal()
    {
        GUI.color = GameUiTheme.Scrim;
        GUI.DrawTexture(new Rect(0, 0, GameGuiScope.Width, GameGuiScope.Height), Texture2D.whiteTexture);
        const float panelWidth = 800f;
        const float panelHeight = 500f;
        float panelX = (GameGuiScope.Width - panelWidth) * .5f;
        float panelY = (GameGuiScope.Height - panelHeight) * .5f;
        GUI.color = GameUiTheme.Paper;
        GUI.DrawTexture(new Rect(panelX, panelY, panelWidth, panelHeight), Texture2D.whiteTexture);
        GUI.color = mint;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 25;
        GUI.Label(new Rect(panelX + 20f, panelY + 18f, panelWidth - 40f, 34f), "회수품 정리");
        GUI.color = Color.white;
        GUI.skin.label.fontSize = 14;
        GUI.Label(new Rect(panelX + 20f, panelY + 52f, panelWidth - 40f, 24f), "전리품 카드를 잡아 가방 위로 드래그해 놓으세요.");
        DrawLargeBag(panelX + 52f, panelY + 118f);
        DrawPendingLootCards(panelX + 395f, panelY + 105f);
        DrawDragGhost();
        HandleLootDragAndDrop();
    }

    private void DrawLargeBag(float x, float y)
    {
        float cell = bagCell = Mathf.Min(54f, 300f / run.Inventory.Columns, 300f / run.Inventory.Rows);
        bagGrid = new Rect(x, y, run.Inventory.Columns * cell, run.Inventory.Rows * cell);
        GUI.color = Color.white;
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.skin.label.fontSize = 18;
        GUI.Label(new Rect(x, y - 32f, 300f, 26f), $"플레이어 가방  {run.Inventory.Columns} × {run.Inventory.Rows}");
        for (int row = 0; row < run.Inventory.Rows; row++)
        for (int col = 0; col < run.Inventory.Columns; col++)
        {
            int id = run.Inventory.GetCell(col, row);
            Rect cellRect = new Rect(x + col * cell, y + row * cell, cell - 3f, cell - 3f);
            GUI.color = id == 0 ? GameUiTheme.EmptyCell : LootColor(id);
            GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
            if (id != 0) HandleStoredDragStart(id, cellRect);
        }
        foreach (StoredLoot item in run.Inventory.Items)
        {
            Rect iconRect = new Rect(x + item.Position.x * cell + 7f, y + item.Position.y * cell + 7f,
                item.Definition.Width * cell - 14f, item.Definition.Height * cell - 14f);
            GUI.color = Color.white;
            LootIconLibrary.Draw(iconRect, item.Definition.Shape);
        }
    }

    private void DrawPendingLootCards(float x, float y)
    {
        GUI.color = Color.white;
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.skin.label.fontSize = 18;
        GUI.Label(new Rect(x, y - 30f, 350f, 24f), $"회수한 전리품  {run.PendingLootItems.Count}개");
        pendingLootScroll = GUI.BeginScrollView(new Rect(x, y, 365f, 350f), pendingLootScroll,
            new Rect(0f, 0f, 345f, run.PendingLootItems.Count * 132f));
        for (int i = 0; i < run.PendingLootItems.Count; i++)
        {
            LootDefinition loot = run.PendingLootItems[i];
            Rect card = new Rect(0f, i * 132f, 345f, 115f);
            LootDefinition displayLoot = DisplayLoot(loot);
            int width = displayLoot.Width;
            int height = displayLoot.Height;
            GUI.color = i == draggingLootIndex ? GameUiTheme.Mint : new Color(.93f, .94f, .86f);
            GUI.DrawTexture(card, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.skin.label.fontSize = 18;
            GUI.Label(new Rect(card.x + 14f, card.y + 12f, 195f, 25f), loot.Name);
            GUI.skin.label.fontSize = 13;
            GUI.Label(new Rect(card.x + 14f, card.y + 40f, 195f, 20f), $"공간 {width} × {height}  ·  {loot.Value}G");

            Rect iconRect = new Rect(card.x + 220f, card.y + 28f, 55f, 55f);
            GUI.color = Color.white;
            LootIconLibrary.Draw(iconRect, loot.Shape);
            float previewCell = Mathf.Min(72f / height, 52f / width);
            float previewWidth = width * previewCell;
            float previewHeight = height * previewCell;
            Rect preview = new Rect(card.x + 282f + (52f - previewWidth) * .5f, card.y + 20f + (72f - previewHeight) * .5f, previewWidth, previewHeight);
            GUI.color = LootColor((int)loot.Shape + 1);
            DrawLootShape(displayLoot, preview);
            GUI.color = Color.white;
            Rect rotateButton = new Rect(card.x + 14f, card.y + 72f, 66f, 26f);
            Rect discardButton = new Rect(card.x + 87f, card.y + 72f, 70f, 26f);
            if (GUI.Button(rotateButton, "회전")) ToggleRotation(loot);
            if (GUI.Button(discardButton, "포기"))
            {
                rotatedLoot.Remove(loot);
                run.DiscardPendingLoot(i);
                draggingLootIndex = -1; draggingStoredItemId = 0;
                break;
            }
            HandleDragStart(i, card, rotateButton, discardButton, new Vector2(x, y) - pendingLootScroll, new Rect(x, y, 345f, 350f));
        }
        GUI.EndScrollView();
    }

    private void HandleDragStart(int index, Rect card, Rect rotateButton, Rect discardButton, Vector2 screenOffset, Rect viewport)
    {
        Event current = Event.current;
        if (current.type != EventType.MouseDown || current.button != 0 || !card.Contains(current.mousePosition)) return;
        if (!viewport.Contains(current.mousePosition + screenOffset)) return;
        if (rotateButton.Contains(current.mousePosition) || discardButton.Contains(current.mousePosition)) return;
        draggingLootIndex = index;
        draggingStoredItemId = 0;
        dragMousePosition = current.mousePosition + screenOffset;
        current.Use();
    }

    private void HandleStoredDragStart(int itemId, Rect cell)
    {
        Event current = Event.current;
        if (current.type != EventType.MouseDown || current.button != 0 || !cell.Contains(current.mousePosition)) return;
        draggingStoredItemId = itemId;
        draggingLootIndex = -1;
        dragMousePosition = current.mousePosition;
        current.Use();
    }

    private void HandleLootDragAndDrop()
    {
        Event current = Event.current;
        if (draggingLootIndex < 0 && draggingStoredItemId == 0) return;
        if (current.type == EventType.MouseDrag)
        {
            dragMousePosition = current.mousePosition;
            current.Use();
            return;
        }

        if (current.type != EventType.MouseUp || current.button != 0) return;
        if (bagGrid.Contains(current.mousePosition))
        {
            int column = Mathf.FloorToInt((current.mousePosition.x - bagGrid.x) / bagCell);
            int row = Mathf.FloorToInt((current.mousePosition.y - bagGrid.y) / bagCell);
            if (draggingLootIndex >= 0 && draggingLootIndex < run.PendingLootItems.Count)
            {
                LootDefinition loot = run.PendingLootItems[draggingLootIndex];
                if (run.TryPlacePendingLoot(draggingLootIndex, column, row, rotatedLoot.ContainsKey(loot))) rotatedLoot.Remove(loot);
            }
            else run.TryMoveStoredLoot(draggingStoredItemId, column, row);
        }
        draggingLootIndex = -1;
        draggingStoredItemId = 0;
        current.Use();
    }

    private void DrawDragGhost()
    {
        LootDefinition loot = GetDraggedLoot();
        if (loot == null) return;
        LootDefinition displayLoot = draggingLootIndex >= 0 ? DisplayLoot(loot) : loot;
        float cell = bagCell;
        GUI.color = new Color(LootColor((int)loot.Shape + 1).r, LootColor((int)loot.Shape + 1).g, LootColor((int)loot.Shape + 1).b, .7f);
        DrawLootShape(displayLoot, new Rect(dragMousePosition.x - 12f, dragMousePosition.y - 12f, displayLoot.Width * cell - 11f, displayLoot.Height * cell - 11f));
    }

    private LootDefinition GetDraggedLoot()
    {
        if (draggingLootIndex >= 0 && draggingLootIndex < run.PendingLootItems.Count)
            return run.PendingLootItems[draggingLootIndex];
        StoredLoot stored = run.Inventory.GetItem(draggingStoredItemId);
        return stored?.Definition;
    }

    private void ToggleRotation(LootDefinition loot)
    {
        if (!rotatedLoot.Remove(loot)) rotatedLoot.Add(loot, loot.RotatedClockwise());
    }

    private LootDefinition DisplayLoot(LootDefinition loot) => rotatedLoot.TryGetValue(loot, out var rotated) ? rotated : loot;

    private static void DrawLootShape(LootDefinition loot, Rect area)
    {
        float cellWidth = area.width / loot.Width;
        float cellHeight = area.height / loot.Height;
        foreach (Vector2Int cell in loot.OccupiedCells)
        {
            GUI.DrawTexture(new Rect(area.x + cell.x * cellWidth, area.y + cell.y * cellHeight,
                cellWidth - 3f, cellHeight - 3f), Texture2D.whiteTexture);
        }
    }
}
