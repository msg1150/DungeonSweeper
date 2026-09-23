using System.Collections.Generic;
using UnityEngine;

/// <summary>생성된 캐주얼 아트를 한 번만 읽고 런타임 Sprite로 캐시한다.</summary>
public static class CasualArtLibrary
{
    private static readonly Dictionary<string, Sprite[]> sheets = new();

    // Domain Reload가 꺼져 있어도 이전 Play 세션의 파괴된 Unity 객체를 재사용하지 않는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => sheets.Clear();

    public static Sprite LoadFull(string path, float pixelsPerUnit = 100f)
    {
        Sprite[] result = LoadSheetInternal(path, 1, 1, pixelsPerUnit, false, .5f);
        return result.Length == 0 ? null : result[0];
    }

    public static Sprite[] LoadSheet(string path, int columns, int rows, float pixelsPerUnit, bool removeBackdrop)
        => LoadSheetInternal(path, columns, rows, pixelsPerUnit, removeBackdrop, .35f);

    private static Sprite[] LoadSheetInternal(string path, int columns, int rows, float pixelsPerUnit, bool removeBackdrop, float pivotY)
    {
        string key = $"{path}:{columns}:{rows}:{pixelsPerUnit}:{removeBackdrop}:{pivotY}";
        if (sheets.TryGetValue(key, out Sprite[] cached) && cached != null && cached.Length > 0 && cached[0] != null) return cached;
        sheets.Remove(key);
        Texture2D source = Resources.Load<Texture2D>(path);
        if (source == null) return System.Array.Empty<Sprite>();
        Texture2D texture = source;
        if (removeBackdrop)
        {
            texture = CreateReadableCopy(source);
            Color[] pixels = texture.GetPixels();
            RemoveConnectedBackdrop(pixels, source.width, source.height);
            texture.SetPixels(pixels); texture.Apply(false, false);
        }
        Sprite[] result = new Sprite[columns * rows];
        float width = texture.width / (float)columns;
        float height = texture.height / (float)rows;
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
            result[y * columns + x] = Sprite.Create(texture, new Rect(x * width, y * height, width, height), new Vector2(.5f, pivotY), pixelsPerUnit);
        sheets[key] = result;
        return result;
    }

    /// <summary>Texture Importer의 Read/Write 설정과 무관하게 GPU 복사를 통해 읽기 가능한 사본을 만든다.</summary>
    public static Texture2D CreateReadableCopy(Texture2D source)
    {
        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;
        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
        copy.Apply(false, false);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(temporary);
        return copy;
    }

    // 배경과 연결된 픽셀만 제거하므로 눈동자·외곽선 같은 검은 내부 디테일은 보존된다.
    private static void RemoveConnectedBackdrop(Color[] pixels, int width, int height)
    {
        Color corner = pixels[0];
        bool transparentBackdrop = corner.a < .1f;
        bool magentaBackdrop = corner.r > .65f && corner.b > .65f && corner.g < .55f;
        bool blackBackdrop = corner.r < .12f && corner.g < .12f && corner.b < .12f;
        if (!transparentBackdrop && !magentaBackdrop && !blackBackdrop) return;
        bool[] visited = new bool[pixels.Length];
        Queue<int> queue = new();
        void Add(int index) { if (!visited[index] && IsBackdrop(pixels[index], transparentBackdrop, magentaBackdrop, blackBackdrop)) { visited[index] = true; queue.Enqueue(index); } }
        for (int x = 0; x < width; x++) { Add(x); Add((height - 1) * width + x); }
        for (int y = 0; y < height; y++) { Add(y * width); Add(y * width + width - 1); }
        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            Color color = pixels[index]; color.a = 0f; pixels[index] = color;
            int x = index % width; int y = index / width;
            if (x > 0) Add(index - 1); if (x + 1 < width) Add(index + 1);
            if (y > 0) Add(index - width); if (y + 1 < height) Add(index + width);
        }
    }

    private static bool IsBackdrop(Color c, bool transparent, bool magenta, bool black)
    {
        if (transparent && c.a < .15f) return true;
        if (magenta && c.r > .6f && c.b > .6f && c.g < .58f) return true;
        return black && c.r < .07f && c.g < .07f && c.b < .07f;
    }
}
