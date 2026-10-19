#if UNITY_EDITOR
using UnityEngine;

/// <summary>에디터 전용 Game 뷰 표시. 실제 AI의 반경을 투영하며 어둠·벽에 가려진 범위도 확인할 수 있다.</summary>
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

    public void Initialize(DungeonRunController controller)
    {
        run = controller;
        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            circle[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }

    private void OnGUI()
    {
        // 꺼진 상태에는 투영·그리기·문자열 생성을 하지 않는다. 개체 검색이나 별도 렌더링 자원도 필요 없다.
        if (run == null || !run.IsRunActive || !DungeonTuning.Active.showMonsterPerceptionRanges
            || !GameShell.IsGameplayHudVisible || Event.current.type != EventType.Repaint) return;
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
                DrawCircle(camera, inverse, center, height, enemy.HearingRange, HearingColor, true);
                DrawCircle(camera, inverse, center, height, enemy.BaseDetectionRange, BaseColor, false);
                DrawCircle(camera, inverse, center, height, enemy.HidingRevealRange, HidingColor, false);
                if (enemy.NoiseAlertRemaining > 0f)
                    DrawCircle(camera, inverse, center, height, enemy.CurrentDetectionRange, AlertColor, false);
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

    private void DrawCircle(Camera camera, Matrix4x4 inverse, Vector2 center, float height, float radius, Color color, bool dashed)
    {
        GUI.color = color;
        Matrix4x4 matrix = GUI.matrix;
        bool previousVisible = Project(camera, inverse, center + circle[0] * radius, height, out var previous);
        for (int i = 1; i <= Segments; i++)
        {
            bool visible = Project(camera, inverse, center + circle[i] * radius, height, out var next);
            if (previousVisible && visible && (!dashed || i % 2 == 0)
                && Mathf.Max(previous.x, next.x) >= 0f && Mathf.Min(previous.x, next.x) <= GameGuiScope.Width
                && Mathf.Max(previous.y, next.y) >= 0f && Mathf.Min(previous.y, next.y) <= GameGuiScope.Height)
            {
                Vector2 delta = next - previous;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, previous);
                GUI.DrawTexture(new Rect(previous.x, previous.y - 1f, delta.magnitude, 2f), Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
            previous = next;
            previousVisible = visible;
        }
        GUI.color = Color.white;
    }

    private static bool Project(Camera camera, Matrix4x4 inverse, Vector2 point, float height, out Vector2 result)
    {
        Vector3 screen = camera.WorldToScreenPoint(ThreeDWorldView.Project(point, height));
        result = inverse.MultiplyPoint3x4(new Vector3(screen.x, Screen.height - screen.y, 0f));
        return screen.z > camera.nearClipPlane;
    }
}
#endif
