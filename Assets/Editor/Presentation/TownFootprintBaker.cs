#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>원본 PNG의 알파에서 밑동 외곽선을 추출해 저장한다. 런타임에는 픽셀 읽기가 없다.</summary>
public static class TownFootprintBaker
{
    [MenuItem("Dungeon Sweeper/Art/Rebuild Town Footprints")]
    public static void RebuildDefault()
    {
        Rebuild(Resources.Load<TownPresentationConfig>("TownPresentation"));
    }

    public static void Rebuild(TownPresentationConfig config)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Play 종료 후 충돌을 생성하세요.");
        if (config == null || config.facilities == null || config.facilities.Length != 4) throw new InvalidOperationException("마을 시설 설정 4개가 필요합니다.");
        var outlines = new Vector2[config.facilities.Length][];
        for (int i = 0; i < outlines.Length; i++)
        {
            var row = config.facilities[i];
            if (row == null || row.sprite == null) throw new InvalidOperationException("시설 Sprite를 지정하세요.");
            string path = AssetDatabase.GetAssetPath(row.sprite);
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("원본 PNG Sprite만 지원합니다: " + path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) throw new InvalidOperationException("PNG를 읽지 못했습니다: " + path);
                // 단일 Sprite를 전제로 한다. 아틀라스/분할 그림에 엉뚱한 좌표를 저장하지 않는다.
                if (row.sprite.rect.size != new Vector2(texture.width, texture.height)) throw new InvalidOperationException("원본 크기의 Single Sprite가 필요합니다: " + path);
                outlines[i] = Extract(texture.GetPixels32(), texture.width, texture.height, row.sprite.pivot,
                    row.footprintBaseHeight, row.footprintAlphaThreshold);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
        // 한 시설이라도 실패하면 기존 충돌 데이터를 유지한다.
        Undo.RecordObject(config, "Rebuild town alpha footprints");
        for (int i = 0; i < outlines.Length; i++)
        {
            var row = config.facilities[i];
            row.footprintOutline = outlines[i]; row.footprintSource = row.sprite;
            row.bakedBaseHeight = row.footprintBaseHeight; row.bakedAlphaThreshold = row.footprintAlphaThreshold;
        }
        EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config);
        Debug.Log("Town alpha footprints baked for four facilities; Play mode was not entered.");
    }

    public static Vector2[] Extract(Color32[] pixels, int width, int height, Vector2 pivot, float baseHeight, float alpha)
    {
        if (width < 1 || height < 1 || pixels == null || pixels.Length != (long)width * height
            || !ValueValidation.Finite(pivot) || !ValueValidation.Finite(baseHeight) || baseHeight < .1f || baseHeight > .6f
            || !ValueValidation.Finite(alpha) || alpha < .05f || alpha > .95f) throw new ArgumentException("Invalid town alpha mask.");
        int threshold = Mathf.CeilToInt(alpha * 255f), bottom = height, top = -1;
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (pixels[y*width+x].a >= threshold) { bottom = Mathf.Min(bottom,y); top = Mathf.Max(top,y); }
        if (top < bottom) throw new InvalidOperationException("충돌을 만들 불투명 픽셀이 없습니다.");
        int cutoff = Mathf.Min(top, bottom + Mathf.Max(1, Mathf.CeilToInt((top-bottom+1)*baseHeight)) - 1);
        var left = new List<Vector2>(); var right = new List<Vector2>();
        for (int y = bottom; y <= cutoff; y++)
        {
            int first = -1, last = -1;
            for (int x = 0; x < width; x++)
                if (pixels[y*width+x].a >= threshold) { if (first < 0) first=x; last=x; }
            if (first < 0) continue;
            // 행의 양쪽 픽셀 경계를 따라간다. 내부 구멍은 채워 문양·창문에 걸리지 않는다.
            left.Add(new Vector2(first,y)); left.Add(new Vector2(first,y+1));
            right.Add(new Vector2(last+1,y)); right.Add(new Vector2(last+1,y+1));
        }
        float tolerance = 2f;
        List<Vector2> outline;
        do
        {
            outline = Simplify(left,tolerance);
            var other = Simplify(right,tolerance); other.Reverse(); outline.AddRange(other);
            tolerance *= 1.5f;
        } while (outline.Count > 256);
        if (outline.Count < 3) throw new InvalidOperationException("충돌 외곽선이 너무 작습니다.");
        var result = new Vector2[outline.Count];
        for (int i = 0; i < result.Length; i++) result[i] = (outline[i]-pivot)/width;
        return result;
    }

    // Ramer–Douglas–Peucker: 1픽셀 계단을 단순화해 물리 꼭짓점 수를 제한한다.
    private static List<Vector2> Simplify(List<Vector2> points, float tolerance)
    {
        var keep = new bool[points.Count]; keep[0] = keep[points.Count-1] = true;
        var pending = new Stack<Vector2Int>(); pending.Push(new Vector2Int(0,points.Count-1));
        while (pending.Count > 0)
        {
            var range = pending.Pop(); Vector2 a = points[range.x], delta = points[range.y]-a;
            float distance = tolerance*tolerance; int split = -1;
            for (int i = range.x+1; i < range.y; i++)
            {
                Vector2 nearest = a + delta * (delta.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(points[i]-a,delta)/delta.sqrMagnitude) : 0f);
                float square = (points[i]-nearest).sqrMagnitude;
                if (square > distance) { distance=square; split=i; }
            }
            if (split < 0) continue;
            keep[split]=true; pending.Push(new Vector2Int(range.x,split)); pending.Push(new Vector2Int(split,range.y));
        }
        var result = new List<Vector2>();
        for (int i = 0; i < points.Count; i++) if (keep[i]) result.Add(points[i]);
        return result;
    }
}
#endif
