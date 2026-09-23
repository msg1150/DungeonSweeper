using System.Collections.Generic;
using UnityEngine;

/// <summary>데모의 상태를 그리는 표시 계층. 게임 규칙을 변경하지 않는다.</summary>
public class DungeonDemoHud : MonoBehaviour
{
    private DungeonRunController run;
    private readonly Color mint = new(.2f, .9f, .62f);
    private readonly Color danger = new(.92f, .27f, .3f);
    private readonly HashSet<LootDefinition> rotatedLoot = new();
    private int draggingLootIndex = -1;
    private int draggingStoredItemId;
    private Vector2 dragMousePosition;
    private Rect bagGrid;
    private bool[,] discovered;
    private bool legacyFogRemoved;

    public void Initialize(DungeonRunController controller) => run = controller;

    private void OnGUI()
    {
        if (run == null) return;
        RemoveLegacyWorldFog();
        DrawHeader();
        DrawHealth();
        DrawMinimap();
        if (run.IsLootPlacementOpen) DrawLootPlacementModal();
        else DrawInventory();
        DrawContextPrompt();
        DrawToast();
        if (run.ActiveSession != null) DrawSkillCheck(run.ActiveSession);
        if (run.HasEscaped) DrawResult();
    }

    private void RemoveLegacyWorldFog()
    {
        if (legacyFogRemoved) return;
        foreach (DungeonVisionFog fog in FindObjectsByType<DungeonVisionFog>())
            fog.gameObject.SetActive(false);
        legacyFogRemoved = true;
    }

    private void DrawHeader()
    {
        GUI.skin.label.fontSize = 17;
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.color = Color.white;
        GUI.Label(new Rect(16, 14, 740, 25), "DUNGEON SWEEPER  ·  비전투 회수 작업 데모");
        string contract = TownProgress.HasAcceptedContract ? $"  |  의뢰: {TownProgress.ContractTargetName} +{TownProgress.ActiveContractBonus}G" : string.Empty;
        GUI.Label(new Rect(16, 39, 900, 24), $"{DungeonLayoutFactory.LayoutName}  |  회수 가치 {run.Inventory.TotalValue}G{contract}    |    [WASD] 이동 / [Space] 대시 / [E] 상호작용");
    }

    private void DrawHealth()
    {
        if (run.PlayerHealth == null) return;
        float ratio = run.PlayerHealth.Maximum <= 0 ? 0f : (float)run.PlayerHealth.Current / run.PlayerHealth.Maximum;
        Rect background = new Rect(16f, 68f, 230f, 22f);
        GUI.color = new Color(.08f, .08f, .1f, .92f); GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(danger, mint, ratio); GUI.DrawTexture(new Rect(background.x + 2f, background.y + 2f, (background.width - 4f) * ratio, background.height - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white; GUI.Label(background, $"  체력 {run.PlayerHealth.Current} / {run.PlayerHealth.Maximum}");
    }

    private void DrawMinimap()
    {
        if (run.Player == null) return;
        if (discovered == null) discovered = new bool[DungeonLayoutFactory.Width, DungeonLayoutFactory.Height];
        Vector2Int playerCell = DungeonLayoutFactory.WorldToCell(run.Player.position);
        for (int x = 0; x < DungeonLayoutFactory.Width; x++)
        for (int y = 0; y < DungeonLayoutFactory.Height; y++)
            if (DungeonLayoutFactory.IsWalkableCell(x, y) && Mathf.Abs(x - playerCell.x) + Mathf.Abs(y - playerCell.y) <= 3)
                discovered[x, y] = true;

        const float cell = 7f;
        float width = DungeonLayoutFactory.Width * cell;
        float height = DungeonLayoutFactory.Height * cell;
        Rect panel = new Rect(Screen.width - width - 30f, 155f, width + 16f, height + 38f);
        GUI.color = new Color(.02f, .035f, .06f, .9f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = Color.white;
        GUI.skin.label.fontSize = 13;
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.Label(new Rect(panel.x + 8f, panel.y + 6f, width, 18f), "탐색 지도");
        for (int x = 0; x < DungeonLayoutFactory.Width; x++)
        for (int y = 0; y < DungeonLayoutFactory.Height; y++)
        {
            if (!discovered[x, y]) continue;
            Rect tile = new Rect(panel.x + 8f + x * cell, panel.y + 28f + (DungeonLayoutFactory.Height - 1 - y) * cell, cell - 1f, cell - 1f);
            GUI.color = new Vector2Int(x, y) == playerCell ? mint : new Color(.3f, .43f, .52f);
            GUI.DrawTexture(tile, Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    private void DrawInventory()
    {
        const float cell = 24f;
        float x = Screen.width - 160f;
        float y = 18f;
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 145f, 23f), "작업 가방  5 × 4");
        for (int row = 0; row < GridInventory.Height; row++)
        for (int col = 0; col < GridInventory.Width; col++)
        {
            int id = run.Inventory.GetCell(col, row);
            GUI.color = id == 0 ? new Color(.12f, .15f, .2f) : LootColor(id);
            Rect cellRect = new Rect(x + col * cell, y + 25f + row * cell, cell - 2f, cell - 2f);
            GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
            if (id == 0 && run.PendingLoot != null && GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                run.TryPlacePendingLoot(0, col, row, false);
        }
        GUI.color = Color.white;
        GUI.skin.label.fontSize = 12;
        LootDefinition pending = run.PendingLoot;
        if (pending == null)
        {
            GUI.Label(new Rect(x, y + 126, 150f, 55f), "해체 후 전리품을 직접\n배치할 수 있습니다.");
            return;
        }

        int width = pending.Width;
        int height = pending.Height;
        GUI.Label(new Rect(x, y + 126, 150f, 20f), $"{pending.Name}  {width}×{height}  {pending.Value}G");
        if (GUI.Button(new Rect(x + 149, y + 149, 68, 22), "포기")) run.DiscardPendingLoot(0);
    }

    private void DrawLootPlacementModal()
    {
        GUI.color = new Color(0f, 0f, 0f, .68f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        const float panelWidth = 800f;
        const float panelHeight = 500f;
        float panelX = (Screen.width - panelWidth) * .5f;
        float panelY = (Screen.height - panelHeight) * .5f;
        GUI.color = new Color(.035f, .055f, .09f, .99f);
        GUI.Box(new Rect(panelX, panelY, panelWidth, panelHeight), string.Empty);
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
        const float cell = 54f;
        bagGrid = new Rect(x, y, GridInventory.Width * cell, GridInventory.Height * cell);
        GUI.color = Color.white;
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.skin.label.fontSize = 18;
        GUI.Label(new Rect(x, y - 32f, 300f, 26f), "플레이어 가방  5 × 4");
        for (int row = 0; row < GridInventory.Height; row++)
        for (int col = 0; col < GridInventory.Width; col++)
        {
            int id = run.Inventory.GetCell(col, row);
            Rect cellRect = new Rect(x + col * cell, y + row * cell, cell - 3f, cell - 3f);
            GUI.color = id == 0 ? new Color(.1f, .13f, .18f) : LootColor(id);
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

        for (int i = 0; i < run.PendingLootItems.Count; i++)
        {
            LootDefinition loot = run.PendingLootItems[i];
            Rect card = new Rect(x, y + i * 132f, 350f, 115f);
            bool rotated = rotatedLoot.Contains(loot);
            LootDefinition displayLoot = rotated ? loot.RotatedClockwise() : loot;
            int width = displayLoot.Width;
            int height = displayLoot.Height;
            GUI.color = i == draggingLootIndex ? new Color(.28f, .38f, .5f, 1f) : new Color(.12f, .16f, .24f, 1f);
            GUI.Box(card, string.Empty);
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
            if (GUI.Button(discardButton, "포기")) run.DiscardPendingLoot(i);
            HandleDragStart(i, card, rotateButton, discardButton);
        }
    }

    private void HandleDragStart(int index, Rect card, Rect rotateButton, Rect discardButton)
    {
        Event current = Event.current;
        if (current.type != EventType.MouseDown || current.button != 0 || !card.Contains(current.mousePosition)) return;
        if (rotateButton.Contains(current.mousePosition) || discardButton.Contains(current.mousePosition)) return;
        draggingLootIndex = index;
        dragMousePosition = current.mousePosition;
        current.Use();
    }

    private void HandleStoredDragStart(int itemId, Rect cell)
    {
        Event current = Event.current;
        if (current.type != EventType.MouseDown || current.button != 0 || !cell.Contains(current.mousePosition)) return;
        draggingStoredItemId = itemId;
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
            int column = Mathf.FloorToInt((current.mousePosition.x - bagGrid.x) / 54f);
            int row = Mathf.FloorToInt((current.mousePosition.y - bagGrid.y) / 54f);
            if (draggingLootIndex >= 0)
            {
                LootDefinition loot = run.PendingLootItems[draggingLootIndex];
                run.TryPlacePendingLoot(draggingLootIndex, column, row, rotatedLoot.Contains(loot));
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
        bool rotated = draggingLootIndex >= 0 && rotatedLoot.Contains(loot);
        LootDefinition displayLoot = rotated ? loot.RotatedClockwise() : loot;
        const float cell = 54f;
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
        if (!rotatedLoot.Add(loot)) rotatedLoot.Remove(loot);
    }

    private static void DrawLootShape(LootDefinition loot, Rect area)
    {
        float cellWidth = area.width / loot.Width;
        float cellHeight = area.height / loot.Height;
        foreach (Vector2Int cell in loot.OccupiedCells)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(area.x + cell.x * cellWidth, area.y + cell.y * cellHeight,
                cellWidth - 3f, cellHeight - 3f), Texture2D.whiteTexture);
        }
    }

    private void DrawContextPrompt()
    {
        if (run.IsLootPlacementOpen) return;
        string prompt = run.GetContextPrompt();
        if (string.IsNullOrEmpty(prompt)) return;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.color = new Color(1f, .86f, .35f);
        GUI.Label(new Rect(Screen.width * .5f - 230f, Screen.height - 78, 460f, 26f), prompt);
    }

    private void DrawToast()
    {
        if (string.IsNullOrEmpty(run.Toast)) return;
        GUI.color = new Color(.85f, .96f, 1f);
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.Label(new Rect(16, Screen.height - 42, Screen.width - 32, 28), run.Toast);
    }

    private void DrawSkillCheck(DismantleSession session)
    {
        float width = 560f;
        float x = (Screen.width - width) * .5f;
        float y = Screen.height - 220f;
        GUI.color = new Color(.025f, .04f, .07f, .96f);
        GUI.Box(new Rect(x, y, width, 180), string.Empty);
        GUI.color = Color.white;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 19;
        GUI.Label(new Rect(x, y + 14, width, 28), $"{run.ActiveCorpseName} 해체  ·  성공 {session.Successes}/{session.Difficulty.RequiredSuccesses}  ·  훼손 {session.Failures}/{session.Difficulty.MaxFailures}");
        GUI.skin.label.fontSize = 14;
        GUI.Label(new Rect(x, y + 45, width, 24), TownProgress.SupplyKits > 0 ? "[E] 정밀 해체  ·  [R] 보급 도구 사용: 즉시 성공 1회" : "포인터가 초록색 영역에 있을 때 [E]를 누르세요");
        Rect bar = new Rect(x + 42, y + 91, width - 84, 28);
        GUI.color = new Color(.4f, .09f, .11f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = mint; GUI.DrawTexture(new Rect(bar.x + bar.width * session.WindowStart, bar.y, bar.width * session.Difficulty.SuccessWindowSize, bar.height), Texture2D.whiteTexture);
        GUI.color = danger; GUI.DrawTexture(new Rect(bar.x + bar.width * session.PointerPosition - 3f, bar.y - 7f, 6f, bar.height + 14f), Texture2D.whiteTexture);
        GUI.color = Color.white; GUI.skin.label.fontSize = 13;
        GUI.Label(new Rect(x, y + 133, width, 22), "[ESC] 작업 취소");
    }

    private void DrawResult()
    {
        GUI.color = new Color(.02f, .04f, .07f, .94f);
        GUI.Box(new Rect(Screen.width * .5f - 250f, Screen.height * .5f - 95f, 500f, 190f), string.Empty);
        GUI.color = mint;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 29;
        GUI.Label(new Rect(Screen.width * .5f - 230f, Screen.height * .5f - 56f, 460f, 40f), "RUN COMPLETE");
        GUI.color = Color.white;
        GUI.skin.label.fontSize = 18;
        GUI.Label(new Rect(Screen.width * .5f - 225f, Screen.height * .5f - 5f, 450f, 54f), $"{run.Inventory.TotalValue}G 상당의 전리품을 회수했습니다.\n[E]를 눌러 마을로 돌아갑니다.");
    }

    private static Color LootColor(int id) => Color.HSVToRGB((id * .19f) % 1f, .62f, .9f);
}
