using System;
using UnityEngine;

[Serializable]
public sealed class TownFacilityArt
{
    public string objectName;
    public Sprite sprite;
    [Tooltip("배경 원화 기준 발판 위치. 좌측 상단 (0,0), 우측 하단 (1,1).")]
    public Vector2 feetAnchor;
    [Range(.01f, .5f)] public float visibleWidth = .16f;
    [Tooltip("PNG 전체 폭 중 실제 그림의 비율. 투명 여백을 제외한 크기를 맞춥니다.")]
    [Range(.01f, 1f)] public float imageWidthFraction = 1f;
    [Tooltip("불투명 그림의 아래쪽에서 충돌로 사용할 높이 비율. 지붕·처마는 제외합니다.")]
    [Range(.1f, .6f)] public float footprintBaseHeight = .25f;
    [Range(.05f, .95f)] public float footprintAlphaThreshold = .5f;
    [Tooltip("에디터의 알파 기반 충돌 재생성으로 저장합니다. 피벗 기준, PNG 전체 폭으로 정규화한 좌표입니다.")]
    public Vector2[] footprintOutline = Array.Empty<Vector2>();
    [HideInInspector] public Sprite footprintSource;
    [HideInInspector] public float bakedBaseHeight, bakedAlphaThreshold;

    public bool HasCurrentFootprint => footprintSource == sprite && footprintOutline != null
        && footprintOutline.Length >= 3 && footprintOutline.Length <= 256
        && Mathf.Approximately(bakedBaseHeight, footprintBaseHeight)
        && Mathf.Approximately(bakedAlphaThreshold, footprintAlphaThreshold);
}

/// <summary>화면 해상도와 독립적인 마을 원화 좌표. 표시와 상호작용·충돌이 같은 배치를 사용한다.</summary>
[CreateAssetMenu(fileName = "TownPresentation", menuName = "Dungeon Sweeper/Town Presentation")]
public sealed class TownPresentationConfig : ScriptableObject
{
    public Sprite background;
    [Min(.1f)] public float referenceAspect = 1746f / 901f;
    public Vector2 playerSpawn = new(.5f, .52f);
    public TownFacilityArt[] facilities = Array.Empty<TownFacilityArt>();
    [Tooltip("좌측 상단 원점의 정규화된 통행 외곽선. 마지막 점은 첫 점에 자동 연결됩니다.")]
    public Vector2[] walkableOutline = Array.Empty<Vector2>();
    private static TownPresentationConfig active;
    public static TownPresentationConfig Active => active != null ? active : active = Resources.Load<TownPresentationConfig>("TownPresentation");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetActive() => active = null;
    public float ViewHeight => (ThreeDPresentationConfig.Active != null ? ThreeDPresentationConfig.Active.townViewSize : 6.5f) * 2f;
    public float ViewWidth => ViewHeight * referenceAspect;
    public float DepthScale(bool depth) => depth ? Mathf.Max(.1f, Mathf.Sin(ThreeDPresentationConfig.Active.spriteCameraElevation * Mathf.Deg2Rad)) : 1f;
    public Vector2 ToWorld(Vector2 normalized, bool depth) => new((normalized.x - .5f) * ViewWidth, (.5f - normalized.y) * ViewHeight / DepthScale(depth));
    public Vector2 ToNormalized(Vector2 position, bool depth) => new(position.x / ViewWidth + .5f, .5f - position.y * DepthScale(depth) / ViewHeight);
    public Rect Footprint(TownFacilityArt art, bool depth)
    {
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = -min;
        foreach (var point in art.footprintOutline)
        {
            Vector2 world = FootprintPoint(art, point, depth);
            min = Vector2.Min(min, world); max = Vector2.Max(max, world);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public Vector2 FootprintPoint(TownFacilityArt art, Vector2 point, bool depth)
    {
        float canvasWidth = ViewWidth * art.visibleWidth / art.imageWidthFraction;
        return ToWorld(art.feetAnchor, depth) + new Vector2(point.x * canvasWidth, point.y * canvasWidth / DepthScale(depth));
    }

    /// <summary>저장 위치 검사도 같은 외곽선을 사용해 빈 모서리를 사각형으로 막지 않는다.</summary>
    public bool OverlapsFootprint(TownFacilityArt art, Rect rect, bool depth)
    {
        if (!Footprint(art, depth).Overlaps(rect)) return false;
        Vector2[] corners = { rect.min, new(rect.xMax, rect.yMin), rect.max, new(rect.xMin, rect.yMax) };
        bool[] inside = new bool[4];
        for (int i = 0, j = art.footprintOutline.Length - 1; i < art.footprintOutline.Length; j = i++)
        {
            Vector2 a = FootprintPoint(art, art.footprintOutline[j], depth), b = FootprintPoint(art, art.footprintOutline[i], depth);
            if (rect.Contains(a)) return true;
            for (int c = 0; c < 4; c++)
            {
                Vector2 p = corners[c];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) inside[c] = !inside[c];
                if (SegmentsCross(a, b, p, corners[(c+1)%4])) return true;
            }
        }
        foreach (bool contained in inside) if (contained) return true;
        return false;
    }

    private static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        Vector2 ab = b-a, cd = d-c;
        float denominator = ab.x*cd.y-ab.y*cd.x;
        if (Mathf.Abs(denominator) < .000001f) return false;
        Vector2 offset = c-a;
        float t = (offset.x*cd.y-offset.y*cd.x)/denominator;
        float u = (offset.x*ab.y-offset.y*ab.x)/denominator;
        return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
    }

    public bool Contains(Vector2 position, bool depth)
    {
        Vector2 point = ToNormalized(position, depth);
        if (walkableOutline == null || walkableOutline.Length < 3) return point.x > .02f && point.x < .98f && point.y > .02f && point.y < .98f;
        bool inside = false;
        for (int i = 0, j = walkableOutline.Length - 1; i < walkableOutline.Length; j = i++)
        {
            Vector2 a = walkableOutline[i], b = walkableOutline[j];
            if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    public static Rect FitViewport(float aspect, float reference)
    {
        if (aspect > reference) { float width = reference / aspect; return new Rect((1f - width) * .5f, 0f, width, 1f); }
        float height = aspect / reference; return new Rect(0f, (1f - height) * .5f, 1f, height);
    }

    public bool IsValid()
    {
        if (background == null || !ValueValidation.Positive(referenceAspect) || !ValueValidation.Positive(ViewHeight)
            || facilities == null || facilities.Length != 4 || !Normalized(playerSpawn)) return false;
        var names = new System.Collections.Generic.HashSet<string>();
        foreach (var art in facilities)
            if (art == null || art.sprite == null || !names.Add(art.objectName) || !Normalized(art.feetAnchor)
                || !ValueValidation.Positive(art.visibleWidth) || art.visibleWidth > .5f
                || !ValueValidation.Positive(art.imageWidthFraction) || art.imageWidthFraction > 1f
                || !ValueValidation.Finite(art.footprintBaseHeight) || art.footprintBaseHeight < .1f || art.footprintBaseHeight > .6f
                || !ValueValidation.Finite(art.footprintAlphaThreshold) || art.footprintAlphaThreshold < .05f || art.footprintAlphaThreshold > .95f
                || !art.HasCurrentFootprint) return false;
        foreach (var art in facilities)
            foreach (var point in art.footprintOutline) if (!ValueValidation.Finite(point) || Mathf.Abs(point.x) > 1f || Mathf.Abs(point.y) > 1f) return false;
        foreach (string required in new[] { "Salvager Guild", "Supply Shop", "Town Warehouse", "Dungeon Entrance" }) if (!names.Contains(required)) return false;
        if (walkableOutline == null || walkableOutline.Length < 3 || walkableOutline.Length > 64) return false;
        foreach (var point in walkableOutline) if (!Normalized(point)) return false;
        return Contains(ToWorld(playerSpawn, false), false);
    }

    private static bool Normalized(Vector2 value) => ValueValidation.Finite(value) && value.x >= 0f && value.x <= 1f && value.y >= 0f && value.y <= 1f;
}
