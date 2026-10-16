#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>타일 도면을 편집하는 제작 창. 몬스터·시체의 좌표는 그리지 않고 층별 스폰 규칙만 편집한다.</summary>
public sealed class DungeonBuilderWindow : EditorWindow
{
    private enum Brush { Floor, Wall, Empty, Entrance, Up, Down }
    [SerializeField] private DungeonDefinition dungeon;
    [SerializeField] private int floorIndex;
    [SerializeField] private float cellSize = 24;
    private Brush brush = Brush.Floor;
    private Vector2 sidebarScroll, gridScroll;
    private int desiredWidth = 20, desiredHeight = 12, strokeGroup = -1;
    private Vector2Int? lastCell;
    private string status;
    private DungeonFloorDefinition Floor => dungeon != null && dungeon.floors != null && floorIndex >= 0 && floorIndex < dungeon.floors.Length ? dungeon.floors[floorIndex] : null;

    [MenuItem("Dungeon Sweeper/Dungeon Builder")]
    public static void Open() => GetWindow<DungeonBuilderWindow>("던전 제작");
    public static void Open(DungeonDefinition selected)
    {
        var window = GetWindow<DungeonBuilderWindow>("던전 제작"); window.dungeon = selected; window.floorIndex = 0; window.SelectFloor(); window.Show();
    }
    private void OnEnable() { minSize = new Vector2(850, 500); Undo.undoRedoPerformed += Repaint; }
    private void OnDisable() { EndStroke(); Undo.undoRedoPerformed -= Repaint; }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var selected = (DungeonDefinition)EditorGUILayout.ObjectField(dungeon, typeof(DungeonDefinition), false, GUILayout.Width(280));
            if (selected != dungeon) { EndStroke(); dungeon = selected; floorIndex = 0; SelectFloor(); status = null; }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("새 던전", EditorStyles.toolbarButton)) CreateDungeon();
                if (GUILayout.Button("검증", EditorStyles.toolbarButton)) status = dungeon != null && dungeon.IsValid(out _) ? "도면과 층 연결이 정상입니다." : dungeon == null ? "던전을 선택하세요." : ValidateError();
                if (GUILayout.Button("저장 + 게임에 등록", EditorStyles.toolbarButton))
                    status = DungeonAuthoringService.SaveAndRegister(dungeon, out string error) ? "저장 완료. 다음 새 탐험부터 게임에서 사용합니다." : error;
            }
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || !GameSession.HasActiveGame || GameSession.IsLoading || DungeonRunController.Instance != null || dungeon == null))
                if (GUILayout.Button("선택 던전 입장", EditorStyles.toolbarButton)) status = GameSession.EnterDungeon(out string error, dungeon) ? "선택한 던전으로 입장했습니다." : error;
        }
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        if (dungeon == null)
        {
            EditorGUILayout.HelpBox("새 던전을 만들거나 Project의 DungeonDefinition 에셋을 선택하세요. 벽·바닥을 그린 뒤 저장하면 카탈로그에 자동 등록됩니다.\n테스트: MainMenu에서 새 게임으로 마을에 간 뒤 '선택 던전 입장'을 누르세요.", MessageType.Info);
            if (DungeonCatalog.Active != null) foreach (var entry in DungeonCatalog.Active.dungeons)
                if (entry != null && GUILayout.Button(entry.displayName)) { dungeon = entry; floorIndex = 0; SelectFloor(); }
            return;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) DrawSidebar();
            DrawGrid();
        }
        if (Event.current.rawType == EventType.MouseUp) EndStroke();
    }
    private string ValidateError() { dungeon.IsValid(out string error); return error; }
    private void DrawSidebar()
    {
        using var column = new EditorGUILayout.VerticalScope(GUILayout.Width(310));
        sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);
        EditorGUILayout.LabelField("던전", EditorStyles.boldLabel);
        var settings = new SerializedObject(dungeon); settings.Update();
        EditorGUILayout.PropertyField(settings.FindProperty("displayName"), new GUIContent("표시 이름"));
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(settings.FindProperty("dungeonId"), new GUIContent("저장 식별 ID"));
        EditorGUILayout.PropertyField(settings.FindProperty("extraExitFloor"), new GUIContent("추가 포탈 층 (-1 무작위)"));
        EditorGUILayout.LabelField("층 번호 설정은 0부터 시작합니다.", EditorStyles.miniLabel);
        settings.ApplyModifiedProperties();
        if (dungeon.floors == null || dungeon.floors.Length == 0)
        {
            EditorGUILayout.HelpBox("이전 코드 기반 단층 던전입니다. 도면으로 변환하면 이 창에서 편집할 수 있습니다. 이전 저장은 유지됩니다.", MessageType.Info);
            if (GUILayout.Button("기존 맵을 도면으로 변환")) { DungeonAuthoringService.ConvertLegacy(dungeon); SelectFloor(); }
            EditorGUILayout.EndScrollView(); return;
        }
        int next = EditorGUILayout.Popup("편집할 층", Mathf.Clamp(floorIndex, 0, dungeon.floors.Length - 1),
            dungeon.floors.Select((floor,index) => $"{index+1}층 · {(floor != null ? floor.displayName : "누락")}").ToArray());
        if (next != floorIndex) { EndStroke(); floorIndex = next; SelectFloor(); }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(dungeon.floors.Length >= DungeonExpeditionData.MaximumFloors))
                if (GUILayout.Button("층 추가")) { DungeonAuthoringService.AddFloor(dungeon); floorIndex = dungeon.floors.Length - 1; SelectFloor(); }
            using (new EditorGUI.DisabledScope(dungeon.floors.Length <= 1))
                if (GUILayout.Button("층 제외"))
                {
                    Undo.RecordObject(dungeon, "Remove dungeon floor reference");
                    dungeon.floors = dungeon.floors.Where((_,index) => index != floorIndex).ToArray();
                    floorIndex = Mathf.Min(floorIndex, dungeon.floors.Length - 1);
                    DungeonAuthoringService.NormalizeStairs(dungeon); EditorUtility.SetDirty(dungeon); SelectFloor();
                }
        }
        if (Floor == null) { EditorGUILayout.EndScrollView(); return; }
        var floorSettings = new SerializedObject(Floor); floorSettings.Update();
        EditorGUILayout.PropertyField(floorSettings.FindProperty("displayName"), new GUIContent("층 이름"));
        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("층 에셋", Floor, typeof(DungeonFloorDefinition), false);
        EditorGUILayout.Space(); EditorGUILayout.LabelField("도면", EditorStyles.boldLabel);
        desiredWidth = EditorGUILayout.IntField("가로 칸 수", desiredWidth);
        desiredHeight = EditorGUILayout.IntField("세로 칸 수", desiredHeight);
        if (GUILayout.Button("크기 변경 (기존 칸 유지)")) Resize();
        cellSize = EditorGUILayout.Slider("확대", cellSize, 12, 48);
        brush = (Brush)GUILayout.SelectionGrid((int)brush, new[] { "바닥", "벽", "빈 공간", "입구 기준", "위층 계단", "아래층 계단" }, 2);
        EditorGUILayout.HelpBox("왼쪽 드래그: 선택 도구\n오른쪽 드래그: 빈 공간\nCtrl+Z: 실행 취소\n첫 층의 입구 기준이 귀환 포탈입니다. 다른 층에서는 스폰 거리의 기준으로만 사용합니다.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("전체 바닥")) Fill(false);
            if (GUILayout.Button("외곽 벽")) Fill(true);
        }
        EditorGUILayout.Space(); EditorGUILayout.LabelField("무작위 스폰 구성", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(floorSettings.FindProperty("monsters"), new GUIContent("몬스터 · 프리팹 / 수량"), true);
        EditorGUILayout.PropertyField(floorSettings.FindProperty("corpses"), new GUIContent("시체 · 프리팹 / 수량"), true);
        floorSettings.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("위치는 알고리즘이 결정합니다. 계단·입구 칸에는 스폰하지 않습니다. 층 제외는 참조만 제거하며 에셋 파일을 지우지 않습니다.", MessageType.None);
        EditorGUILayout.EndScrollView();
    }
    private void DrawGrid()
    {
        if (Floor == null || Floor.map?.tiles == null) return;
        var map = Floor.map;
        using var panel = new EditorGUILayout.VerticalScope();
        EditorGUILayout.LabelField($"{map.width} × {map.height}  |  파랑: 입구 / 보라: 위층 / 초록: 아래층", EditorStyles.boldLabel);
        gridScroll = EditorGUILayout.BeginScrollView(gridScroll);
        Rect area = GUILayoutUtility.GetRect(map.width * cellSize + 16, map.height * cellSize + 16, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
        Vector2 origin = area.position + new Vector2(8,8);
        for (int x = 0; x < map.width; x++) for (int y = 0; y < map.height; y++)
        {
            var cell = new Vector2Int(x,y); var tile = map.Tile(x,y);
            var rect = new Rect(origin.x + x * cellSize, origin.y + (map.height - 1 - y) * cellSize, cellSize - 1, cellSize - 1);
            Color color = tile == DungeonTile.Floor ? new Color(.35f,.43f,.45f) : tile == DungeonTile.Wall ? new Color(.75f,.68f,.51f) : new Color(.12f,.14f,.16f);
            string marker = null;
            if (cell == map.entrance) { color = new Color(.25f,.55f,.95f); marker = "E"; }
            if (map.hasStairsUp && cell == map.stairsUp) { color = new Color(.65f,.4f,.95f); marker = "↑"; }
            if (map.hasStairsDown && cell == map.stairsDown) { color = new Color(.2f,.75f,.45f); marker = "↓"; }
            EditorGUI.DrawRect(rect, color); if (marker != null) GUI.Label(rect, marker, EditorStyles.whiteBoldLabel);
        }
        Event current = Event.current;
        if (!EditorApplication.isPlaying && area.Contains(current.mousePosition) && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag) && current.button <= 1)
        {
            var cell = new Vector2Int(Mathf.FloorToInt((current.mousePosition.x - origin.x) / cellSize), map.height - 1 - Mathf.FloorToInt((current.mousePosition.y - origin.y) / cellSize));
            if (cell.x >= 0 && cell.y >= 0 && cell.x < map.width && cell.y < map.height)
            {
                if (strokeGroup < 0) { Undo.IncrementCurrentGroup(); strokeGroup = Undo.GetCurrentGroup(); Undo.RegisterCompleteObjectUndo(Floor, "Paint dungeon tiles"); }
                var selected = current.button == 1 ? Brush.Empty : brush;
                if (lastCell.HasValue && (int)selected <= (int)Brush.Empty)
                {
                    Vector2Int from = lastCell.Value; int steps = Mathf.Max(Mathf.Abs(cell.x-from.x), Mathf.Abs(cell.y-from.y));
                    for (int i = 0; i <= steps; i++) Paint(Vector2Int.RoundToInt(Vector2.Lerp(from,cell,steps == 0 ? 1 : (float)i/steps)), selected);
                }
                else Paint(cell, selected);
                lastCell = cell; EditorUtility.SetDirty(Floor); Repaint(); current.Use();
            }
        }
        EditorGUILayout.EndScrollView();
    }
    private void Paint(Vector2Int cell, Brush selected)
    {
        var map = Floor.map;
        if ((int)selected <= (int)Brush.Empty) { map.tiles[cell.y * map.width + cell.x] = selected == Brush.Floor ? 1 : selected == Brush.Wall ? 2 : 0; return; }
        if (selected == Brush.Up && floorIndex == 0) { status = "첫 층에는 위층 계단이 없습니다."; return; }
        if (selected == Brush.Down && floorIndex == dungeon.floors.Length - 1) { status = "먼저 다음 층을 추가하세요."; return; }
        map.tiles[cell.y * map.width + cell.x] = 1;
        if (selected == Brush.Entrance) map.entrance = cell;
        else if (selected == Brush.Up) map.stairsUp = cell;
        else map.stairsDown = cell;
    }
    private void EndStroke() { if (strokeGroup >= 0) Undo.CollapseUndoOperations(strokeGroup); strokeGroup = -1; lastCell = null; }
    private void SelectFloor() { if (Floor != null) { desiredWidth = Floor.map.width; desiredHeight = Floor.map.height; } }
    private void Resize()
    {
        int width = Mathf.Clamp(desiredWidth, 4, DungeonFloorMap.MaximumSize), height = Mathf.Clamp(desiredHeight, 4, DungeonFloorMap.MaximumSize);
        var map = Floor.map; Undo.RecordObject(Floor, "Resize dungeon floor"); var tiles = new int[width * height];
        for (int y = 0; y < Mathf.Min(height,map.height); y++) for (int x = 0; x < Mathf.Min(width,map.width); x++) tiles[y * width + x] = map.tiles[y * map.width + x];
        map.width = width; map.height = height; map.tiles = tiles; EditorUtility.SetDirty(Floor); SelectFloor();
    }
    private void Fill(bool borderOnly)
    {
        var map = Floor.map; Undo.RecordObject(Floor, "Fill dungeon floor");
        for (int x = 0; x < map.width; x++) for (int y = 0; y < map.height; y++)
            if (!borderOnly || x == 0 || y == 0 || x == map.width - 1 || y == map.height - 1) map.tiles[y * map.width + x] = borderOnly ? 2 : 1;
        EditorUtility.SetDirty(Floor);
    }
    private void CreateDungeon()
    {
        string path = EditorUtility.SaveFilePanelInProject("새 던전", "new-dungeon", "asset", "던전 데이터 저장 위치", "Assets/Resources/Dungeons");
        if (string.IsNullOrEmpty(path)) return;
        dungeon = DungeonAuthoringService.Create(path, System.IO.Path.GetFileNameWithoutExtension(path)); floorIndex = 0; SelectFloor();
        status = DungeonAuthoringService.SaveAndRegister(dungeon, out string error) ? "새 던전을 만들고 게임에 등록했습니다." : error;
    }
}

[CustomEditor(typeof(DungeonDefinition))]
public sealed class DungeonDefinitionInspector : Editor
{
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("던전 제작 툴에서 열기")) DungeonBuilderWindow.Open((DungeonDefinition)target);
        DrawDefaultInspector();
    }
}

[CustomEditor(typeof(DungeonFloorDefinition))]
public sealed class DungeonFloorInspector : Editor
{
    public override void OnInspectorGUI()
    {
        var floor = (DungeonFloorDefinition)target;
        EditorGUILayout.HelpBox($"{floor.map.width} × {floor.map.height} 도면 · Dungeon Sweeper > Dungeon Builder에서 편집하세요.", MessageType.Info);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("monsters"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("corpses"), true);
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
