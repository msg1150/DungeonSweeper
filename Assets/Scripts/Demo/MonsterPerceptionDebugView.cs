#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>에디터 전용 Game 뷰 표시. 실제 AI의 반경을 투영하며 어둠·벽에 가려진 범위도 확인할 수 있다.</summary>
[DefaultExecutionOrder(950)]
public sealed class MonsterPerceptionDebugView : MonoBehaviour
{
    private const int Segments = 64;
    private static readonly Color BaseColor = new(1f, .83f, .2f, .9f);
    private static readonly Color HearingColor = new(.25f, .9f, 1f, .7f);
    private static readonly Color AlertColor = new(1f, .3f, .25f, .95f);
    private static readonly Color HidingColor = new(.85f, .4f, 1f, .95f);
    private readonly Vector2[] circle = new Vector2[Segments + 1];
    private DungeonRunController run;
    private GUIStyle label;
    private Canvas lineCanvas;
    private readonly List<MonsterPerceptionLines> batches = new();
    private int batchIndex;

    public void Initialize(DungeonRunController controller)
    {
        run = controller;
        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            circle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    private bool ShouldDraw => run != null && run.IsRunActive && DungeonTuning.Active.showMonsterPerceptionRanges
        && GameShell.IsGameplayHudVisible;

    private void LateUpdate()
    {
        if (!ShouldDraw) { if (lineCanvas != null) lineCanvas.enabled = false; return; }
        var world = ThreeDWorldView.Current;
        Camera camera = world != null ? world.WorldCamera : Camera.main;
        if (camera == null) { if (lineCanvas != null) lineCanvas.enabled = false; return; }
        EnsureCanvas();
        lineCanvas.enabled = true;
        batchIndex = 0;
        foreach (var batch in batches) batch.ClearSegments();
        float width = 2f * Mathf.Max(.001f, Mathf.Min(Screen.width / GameGuiScope.Width, Screen.height / GameGuiScope.Height));
        foreach (var enemy in run.Enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled) continue;
            Vector2 center = enemy.transform.position;
            float height = world != null ? world.FloorHeight(center) + .04f : 0f;
            DrawCircle(camera, center, height, enemy.HearingRange, HearingColor, true, width);
            DrawCircle(camera, center, height, enemy.BaseDetectionRange, BaseColor, false, width);
            DrawCircle(camera, center, height, enemy.HidingRevealRange, HidingColor, false, width);
            if (enemy.NoiseAlertRemaining > 0f) DrawCircle(camera, center, height, enemy.CurrentDetectionRange, AlertColor, false, width);
        }
        foreach (var batch in batches) { batch.enabled = batch.SegmentCount > 0; batch.SetVerticesDirty(); }
    }

    private void OnDisable() { if (lineCanvas != null) lineCanvas.enabled = false; }

    private void EnsureCanvas()
    {
        if (lineCanvas != null) return;
        // ScreenSpaceOverlay는 최상위 오브젝트여야 한다. 일반 HUD의 자식으로 두면
        // 메시가 있어도 Game 뷰에서 누락될 수 있으므로 수명만 이 컴포넌트가 관리한다.
        var root = new GameObject("Batched perception debug", typeof(RectTransform), typeof(Canvas));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        lineCanvas = root.GetComponent<Canvas>();
        lineCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        lineCanvas.sortingOrder = 150; // 시야 오버레이(100) 위, IMGUI HUD 아래.
    }

    private void OnDestroy()
    {
        if (lineCanvas == null) return;
        if (Application.isPlaying) Destroy(lineCanvas.gameObject); else DestroyImmediate(lineCanvas.gameObject);
    }

    private void AddSegment(Vector2 from, Vector2 to, float width, Color color)
    {
        if (batches.Count > batchIndex && batches[batchIndex].SegmentCount == MonsterPerceptionLines.MaximumSegments) batchIndex++;
        if (batchIndex == batches.Count)
        {
            var obj = new GameObject("Perception lines " + batchIndex, typeof(RectTransform));
            obj.transform.SetParent(lineCanvas.transform, false);
            var batch = obj.AddComponent<MonsterPerceptionLines>();
            batch.raycastTarget = false;
            var rect = batch.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            batches.Add(batch);
        }
        // ScreenSpaceOverlay의 중앙 피벗 기준. 색은 재질 변경 없이 정점에 저장한다.
        Vector2 origin = new(Screen.width * .5f, Screen.height * .5f);
        batches[batchIndex].AddSegment(from - origin, to - origin, width, color);
    }

    private void OnGUI()
    {
        // 원은 하나의 UI 메시로 그리고, 텍스트만 기존 HUD 좌표계를 사용한다.
        if (!ShouldDraw || Event.current.type != EventType.Repaint) return;
        var world = ThreeDWorldView.Current;
        Camera camera = world != null ? world.WorldCamera : Camera.main;
        if (camera == null) return;
        using var gui = new GameGuiScope(true);
        label ??= new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
        int depth = GUI.depth;
        // 게임 HUD 뒤에 그려 체력·해체 UI·가방을 가리지 않는다.
        GUI.depth = 10;
        try
        {
            Matrix4x4 inverse = GUI.matrix.inverse;
            foreach (var enemy in run.Enemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                Vector2 center = enemy.transform.position;
                float height = world != null ? world.FloorHeight(center) + .04f : 0f;
                if (!Project(camera, inverse, center, height, out var point)
                    || point.x < 0 || point.x > GameGuiScope.Width || point.y < 0 || point.y > GameGuiScope.Height) continue;
                string text = $"{enemy.DisplayName}  인식 {enemy.CurrentDetectionRange:0.0} / 기본 {enemy.BaseDetectionRange:0.0} / 청취 {enemy.HearingRange:0.0} / 은신 발각 {enemy.HidingRevealRange:0.0}";
                if (enemy.NoiseAlertRemaining > 0f) text += $"  경계 {enemy.NoiseAlertRemaining:0.0}초";
                DrawLabel(new Rect(Mathf.Clamp(point.x - 140f, 8f, GameGuiScope.Width - 480f), point.y + 8f, 470f, 23f), text);
            }
            DrawLabel(new Rect(8, 108, 550, 76),
                "몬스터 인식 디버그 · DungeonTuning에서 표시 전환\n노랑: 기본 인식   하늘색 점선: 소음 청취   빨강: 소음 후 실제 인식\n보라: 은신 발각 · 숨는 순간 이 반경 안이면 은신 실패 (벽과 무관)\n기본·확장 시야는 벽에 가리면 발견하지 못합니다.");
        }
        finally { GUI.depth = depth; }
    }

    private void DrawLabel(Rect rect, string text)
    {
        GUI.color = new Color(0f, 0f, 0f, .75f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 5f, rect.y + 2f, rect.width - 10f, rect.height), text, label);
    }

    private void DrawCircle(Camera camera, Vector2 center, float height, float radius, Color color, bool dashed, float width)
    {
        Vector3 previous = camera.WorldToScreenPoint(ThreeDWorldView.Project(center + circle[0] * radius, height));
        Rect viewport = camera.pixelRect;
        for (int i = 1; i <= Segments; i++)
        {
            Vector3 next = camera.WorldToScreenPoint(ThreeDWorldView.Project(center + circle[i] * radius, height));
            if (previous.z > camera.nearClipPlane && next.z > camera.nearClipPlane && (!dashed || i % 2 == 0)
                && Mathf.Max(previous.x, next.x) >= viewport.xMin && Mathf.Min(previous.x, next.x) <= viewport.xMax
                && Mathf.Max(previous.y, next.y) >= viewport.yMin && Mathf.Min(previous.y, next.y) <= viewport.yMax)
                AddSegment(previous, next, width, color);
            previous = next;
        }
    }

    private static bool Project(Camera camera, Matrix4x4 inverse, Vector2 point, float height, out Vector2 result)
    {
        Vector3 screen = camera.WorldToScreenPoint(ThreeDWorldView.Project(point, height));
        result = inverse.MultiplyPoint3x4(new Vector3(screen.x, Screen.height - screen.y, 0f));
        return screen.z > camera.nearClipPlane;
    }
}
#endif
