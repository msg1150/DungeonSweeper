using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>탐험 상태를 그리는 표시 계층. 게임 규칙을 변경하지 않는다.</summary>
public sealed partial class DungeonHud : MonoBehaviour
{
    private DungeonRunController run;
    private readonly Color mint = new(.2f, .9f, .62f);
    private readonly Color danger = new(.92f, .27f, .3f);
    // 회전 미리보기는 클릭할 때만 계산한다. IMGUI의 Layout/Repaint마다 도형을 새로 만들지 않는다.
    private readonly Dictionary<LootDefinition, LootDefinition> rotatedLoot = new();
    private int draggingLootIndex = -1;
    private int draggingStoredItemId;
    private Vector2 dragMousePosition;
    private Rect bagGrid;
    private float bagCell;
    private Vector2 pendingLootScroll;
    private const float MinimapViewportSize = 210f;
    private readonly DungeonMinimapTexture minimap = new();
    private System.Func<int, int, bool> isDiscovered;
    // 층 이동 때 HUD가 다시 생성되어도 사용자가 고른 내부 지도 배율은 유지한다.
    private static readonly float[] minimapZoomLevels = { 1f, 1.5f, 2f, 3f };
    private static int minimapZoomIndex = 1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMinimapZoom() => minimapZoomIndex = 1;

    public void Initialize(DungeonRunController controller)
    {
        minimap.Dispose();
        run = controller; isDiscovered = controller.IsCellDiscovered;
    }
    private void OnDestroy() => minimap.Dispose();
    private void OnDisable() => minimap.Dispose();

    private void OnGUI()
    {
        if (run == null || !GameShell.IsGameplayHudVisible) return;
        // Keep Layout/Repaint available while paused or unfocused; ignore UI input events.
        if (GameShell.IsGameplayInputBlocked && Event.current.type != EventType.Layout
            && Event.current.type != EventType.Repaint) return;
        using var gui = new GameGuiScope(true);
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

    private void Update()
    {
        UpdateMinimapZoom();
        if (run == null || !run.IsLootPlacementOpen || GameShell.IsGameplayInputBlocked)
        {
            draggingLootIndex = -1;
            draggingStoredItemId = 0;
            if (run == null || !run.IsLootPlacementOpen) { rotatedLoot.Clear(); pendingLootScroll = Vector2.zero; }
        }
    }

    private void UpdateMinimapZoom()
    {
        if (run == null || !run.IsRunActive || run.IsLootPlacementOpen || GameShell.IsGameplayInputBlocked) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        int direction = (keyboard.rightBracketKey.wasPressedThisFrame ? 1 : 0)
            - (keyboard.leftBracketKey.wasPressedThisFrame ? 1 : 0);
        minimapZoomIndex = Mathf.Clamp(minimapZoomIndex + direction, 0, minimapZoomLevels.Length - 1);
    }

    private static float MinimapCellSize(int columns, int rows)
    {
        return Mathf.Min(MinimapViewportSize / columns, MinimapViewportSize / rows) * minimapZoomLevels[minimapZoomIndex];
    }

    private static Vector2 MinimapOrigin(int columns, int rows, float cell, Vector2Int playerCell)
    {
        // 전체 지도가 들어가는 축은 중앙 정렬한다. 확대된 축은 플레이어를 따라가되
        // 지도 끝에서 멈춰, 빈 공간보다 실제 던전이 많이 보이도록 한다.
        float width = columns * cell, height = rows * cell;
        return new Vector2(
            width <= MinimapViewportSize ? (MinimapViewportSize - width) * .5f
                : Mathf.Clamp(MinimapViewportSize * .5f - (playerCell.x + .5f) * cell, MinimapViewportSize - width, 0f),
            height <= MinimapViewportSize ? (MinimapViewportSize - height) * .5f
                : Mathf.Clamp(MinimapViewportSize * .5f - (rows - playerCell.y - .5f) * cell, MinimapViewportSize - height, 0f));
    }

    private void DrawHeader()
    {
        GUI.skin.label.fontSize = 17;
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GameUiTheme.Panel(new Rect(8, 8, 1000, 58));
        GUI.color = Color.white;
        GUI.Label(new Rect(16, 14, 740, 25), "DUNGEON SWEEPER  ·  던전 회수 작업");
        string contract = TownProgress.HasActiveQuest ? $"  |  의뢰: {TownProgress.ActiveQuest.Title} +{TownProgress.ActiveQuest.GoldReward}G" : string.Empty;
        GUI.Label(new Rect(16, 39, 900, 24), $"{run.DungeonName}  |  회수 가치 {run.Inventory.TotalValue}G{contract}    |    [WASD] 이동 / [Space] 대시 / [E] 상호작용");
    }

    private void DrawHealth()
    {
        if (run.PlayerHealth == null) return;
        GUI.skin.label.fontSize = 14;
        GUI.skin.label.alignment = TextAnchor.MiddleLeft;
        float ratio = run.PlayerHealth.Maximum <= 0 ? 0f : (float)run.PlayerHealth.Current / run.PlayerHealth.Maximum;
        Rect background = new Rect(16f, 68f, 230f, 22f);
        GUI.color = GameUiTheme.EmptyCell; GUI.DrawTexture(background, Texture2D.whiteTexture);
        GUI.color = Color.Lerp(danger, mint, ratio); GUI.DrawTexture(new Rect(background.x + 2f, background.y + 2f, (background.width - 4f) * ratio, background.height - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white; GUI.Label(background, $"  체력 {run.PlayerHealth.Current} / {run.PlayerHealth.Maximum}");
    }

    private void DrawMinimap()
    {
        // 지도에는 입력 컨트롤이 없다. Layout/마우스 이벤트마다 수천 칸을 다시 조회하지 않는다.
        if (run.Player == null || Event.current.type != EventType.Repaint) return;
        Vector2Int playerCell = DungeonLayoutFactory.WorldToCell(run.Player.position);

        float panelY = Mathf.Max(210f, CompactBagCell * run.Inventory.Rows + 120f);
        float cell = MinimapCellSize(DungeonLayoutFactory.Width, DungeonLayoutFactory.Height);
        float panelWidth = MinimapViewportSize + 16f;
        Rect panel = new Rect(GameGuiScope.Width - panelWidth - 14f, panelY, panelWidth, MinimapViewportSize + 64f);
        GUI.color = GameUiTheme.Paper;
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.skin.label.fontSize = 13;
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.Label(new Rect(panel.x + 8f, panel.y + 6f, panel.width - 16f, 18f), $"탐색 지도 · {run.FloorIndex + 1}층");
        Rect mapArea = new Rect(panel.x + 8f, panel.y + 28f, MinimapViewportSize, MinimapViewportSize);
        GUI.color = new Color(.08f, .13f, .17f);
        GUI.DrawTexture(mapArea, Texture2D.whiteTexture);
        // 지도 내용만 잘라내며 외곽 UI는 고정한다. 예외 시에도 IMGUI 영역을 닫는다.
        GUI.BeginGroup(mapArea);
        try
        {
            DrawMinimapContents(playerCell, cell);
        }
        finally { GUI.EndGroup(); }
        GUI.color = Color.white;
        GUI.skin.label.fontSize = 12;
        GUI.Label(new Rect(panel.x + 8f, mapArea.yMax + 5f, panel.width - 16f, 22f),
            $"[ 축소   ·   ] 확대   {Mathf.RoundToInt(minimapZoomLevels[minimapZoomIndex] * 100f)}%");
    }

    private void DrawMinimapContents(Vector2Int playerCell, float cell)
    {
        int columns = DungeonLayoutFactory.Width, rows = DungeonLayoutFactory.Height;
        Vector2 origin = MinimapOrigin(columns, rows, cell, playerCell);
        var map = DungeonLayoutFactory.ActiveMap;
        float gap = Mathf.Min(.6f, cell * .12f);
        // 플레이어 이동·지도 스크롤만으로는 업로드하지 않는다. 탐색·층·배율이 바뀔 때만 갱신한다.
        minimap.Refresh(map, columns, rows, run.DiscoveredCellCount, cell, isDiscovered);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(origin.x, origin.y, columns * cell, rows * cell), minimap.Texture);
        // 작은 지도에서도 위치를 찾을 수 있게 플레이어 표식을 마지막에 덧그린다.
        float markerSize = Mathf.Max(5f, cell - gap);
        Vector2 markerCenter = new(origin.x + (playerCell.x + .5f) * cell,
            origin.y + (rows - playerCell.y - .5f) * cell);
        Rect marker = new Rect(markerCenter.x - markerSize * .5f, markerCenter.y - markerSize * .5f, markerSize, markerSize);
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(marker.x - 1f, marker.y - 1f, marker.width + 2f, marker.height + 2f), Texture2D.whiteTexture);
        GUI.color = mint;
        GUI.DrawTexture(marker, Texture2D.whiteTexture);
    }

    private float CompactBagCell => Mathf.Min(24f, 144f / run.Inventory.Columns, 144f / run.Inventory.Rows);

    private void DrawInventory()
    {
        float cell = CompactBagCell;
        float x = GameGuiScope.Width - 160f;
        float y = 18f;
        GameUiTheme.Panel(new Rect(x - 8f, y - 10f, 160f, 95f + cell * run.Inventory.Rows));
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, 145f, 23f), $"작업 가방  {run.Inventory.Columns} × {run.Inventory.Rows}");
        for (int row = 0; row < run.Inventory.Rows; row++)
        for (int col = 0; col < run.Inventory.Columns; col++)
        {
            int id = run.Inventory.GetCell(col, row);
            GUI.color = id == 0 ? GameUiTheme.EmptyCell : LootColor(id);
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
            GUI.Label(new Rect(x, y + 35 + cell * run.Inventory.Rows, 150f, 55f), "해체 후 전리품을 직접\n배치할 수 있습니다.");
            return;
        }

        int width = pending.Width;
        int height = pending.Height;
        GUI.Label(new Rect(x, y + 126, 150f, 20f), $"{pending.Name}  {width}×{height}  {pending.Value}G");
        if (GUI.Button(new Rect(x + 149, y + 149, 68, 22), "포기")) run.DiscardPendingLoot(0);
    }

    private void DrawContextPrompt()
    {
        if (run.IsLootPlacementOpen || Event.current.type != EventType.Repaint) return;
        string prompt = run.GetContextPrompt();
        if (string.IsNullOrEmpty(prompt)) return;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GameUiTheme.Panel(new Rect(GameGuiScope.Width * .5f - 250f, GameGuiScope.Height - 84, 500f, 38f), GameUiTheme.PromptPaper);
        GUI.color = Color.white;
        GUI.Label(new Rect(GameGuiScope.Width * .5f - 230f, GameGuiScope.Height - 78, 460f, 26f), prompt);
    }

    private void DrawToast()
    {
        if (string.IsNullOrEmpty(run.Toast)) return;
        GameUiTheme.Panel(new Rect(8, GameGuiScope.Height - 46, GameGuiScope.Width - 176, 36));
        GUI.color = Color.white;
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
        GUI.Label(new Rect(16, GameGuiScope.Height - 42, GameGuiScope.Width - 32, 28), run.Toast);
    }

    private void DrawSkillCheck(DismantleSession session)
    {
        float width = 560f;
        float x = (GameGuiScope.Width - width) * .5f;
        float y = GameGuiScope.Height - 220f;
        GUI.color = GameUiTheme.Paper;
        GUI.DrawTexture(new Rect(x, y, width, 180), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 19;
        GUI.Label(new Rect(x, y + 14, width, 28), $"{run.ActiveCorpseName} 해체  ·  성공 {session.Successes}/{session.Difficulty.RequiredSuccesses}  ·  훼손 {session.Failures}/{session.Difficulty.MaxFailures}");
        GUI.skin.label.fontSize = 14;
        GUI.Label(new Rect(x, y + 45, width, 24), TownProgress.SupplyKits > 0 ? "[E] 정밀 해체  ·  [R] 보급 도구 사용: 즉시 성공 1회" : "포인터가 초록색 영역에 있을 때 [E]를 누르세요");
        Rect bar = new Rect(x + 42, y + 91, width - 84, 28);
        GUI.color = new Color(.94f, .74f, .70f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = mint; GUI.DrawTexture(new Rect(bar.x + bar.width * session.WindowStart, bar.y, bar.width * session.Difficulty.SuccessWindowSize, bar.height), Texture2D.whiteTexture);
        GUI.color = danger; GUI.DrawTexture(new Rect(bar.x + bar.width * session.PointerPosition - 3f, bar.y - 7f, 6f, bar.height + 14f), Texture2D.whiteTexture);
        GUI.color = Color.white; GUI.skin.label.fontSize = 13;
        GUI.Label(new Rect(x, y + 133, width, 22), "[ESC] 작업 취소");
    }

    private void DrawResult()
    {
        GUI.color = GameUiTheme.Paper;
        GUI.DrawTexture(new Rect(GameGuiScope.Width * .5f - 250f, GameGuiScope.Height * .5f - 95f, 500f, 190f), Texture2D.whiteTexture);
        GUI.color = mint;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;
        GUI.skin.label.fontSize = 29;
        GUI.Label(new Rect(GameGuiScope.Width * .5f - 230f, GameGuiScope.Height * .5f - 56f, 460f, 40f), "RUN COMPLETE");
        GUI.color = Color.white;
        GUI.skin.label.fontSize = 18;
        GUI.Label(new Rect(GameGuiScope.Width * .5f - 225f, GameGuiScope.Height * .5f - 5f, 450f, 65f), $"전리품 {TownProgress.LastRecoveredCount}개를 창고에 보관했습니다.\n거래소에서 판매하거나 재료로 사용하세요.\n[E]를 눌러 마을로 돌아갑니다.");
    }

    private static Color LootColor(int id) => Color.HSVToRGB((id * .19f) % 1f, .62f, .9f);
}
