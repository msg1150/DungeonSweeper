#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>모든 몬스터의 범위 선을 공유 UI 재질과 정점 색으로 묶는다. AI나 콜라이더를 변경하지 않는다.</summary>
[RequireComponent(typeof(CanvasRenderer), typeof(RectTransform))]
public sealed class MonsterPerceptionLines : Graphic
{
    // uGUI의 16비트 정점 한도를 넘기기 전에 다음 Graphic으로 분리한다.
    public const int MaximumSegments = 16000;
    private struct Segment { public Vector2 from, to; public float width; public Color32 color; }
    private readonly List<Segment> segments = new(1024);
    public int SegmentCount => segments.Count;
    public void ClearSegments() => segments.Clear();
    public void AddSegment(Vector2 from, Vector2 to, float width, Color32 color)
    {
        if (segments.Count >= MaximumSegments || (to - from).sqrMagnitude < .0001f || width <= 0f) return;
        segments.Add(new Segment { from = from, to = to, width = width, color = color });
    }
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        foreach (var segment in segments)
        {
            Vector2 delta = segment.to - segment.from;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (segment.width * .5f);
            int index = vertices.currentVertCount;
            vertices.AddVert(segment.from - normal, segment.color, Vector2.zero);
            vertices.AddVert(segment.from + normal, segment.color, Vector2.zero);
            vertices.AddVert(segment.to + normal, segment.color, Vector2.zero);
            vertices.AddVert(segment.to - normal, segment.color, Vector2.zero);
            vertices.AddTriangle(index, index + 1, index + 2);
            vertices.AddTriangle(index + 2, index + 3, index);
        }
    }
}
#endif
