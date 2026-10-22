using System;
using UnityEngine;

/// <summary>지도 바닥·계단·은신처를 한 장으로 캐시한다. 플레이어 표식과 지도 이동은 별도로 그린다.</summary>
public sealed class DungeonMinimapTexture : IDisposable
{
    private DungeonFloorMap cachedMap;
    private int cachedCount = -1, cachedColumns, cachedRows;
    private float cachedCell;
    private Color32[] pixels;
    public Texture2D Texture { get; private set; }

    public bool Refresh(DungeonFloorMap map, int columns, int rows, int discoveredCount, float cell, Func<int, int, bool> discovered)
    {
        if (Texture != null && ReferenceEquals(cachedMap, map) && cachedColumns == columns && cachedRows == rows
            && cachedCount == discoveredCount && cachedCell == cell) return false;
        int width = Mathf.Max(1, Mathf.CeilToInt(columns * cell)), height = Mathf.Max(1, Mathf.CeilToInt(rows * cell));
        if (Texture == null || Texture.width != width || Texture.height != height)
        {
            Dispose();
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                { name = "Dungeon minimap cache", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            pixels = new Color32[width * height];
        }
        else Array.Clear(pixels, 0, pixels.Length);
        float gap = Mathf.Min(.6f, cell * .12f);
        float scaleX = width / (columns * cell), scaleY = height / (rows * cell);
        // 텍스처의 y=0도 도면의 아래쪽이다. 화면의 위/아래 변환은 GUI.DrawTexture가 처리한다.
        for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
        {
            if (!discovered(x, y)) continue;
            var location = new Vector2Int(x, y);
            Color32 color = new Color(.57f, .68f, .75f);
            if (map != null)
            {
                if (map.hasStairsUp && map.stairsUp == location) color = new Color(.4f, .65f, 1f);
                if (map.hasStairsDown && map.stairsDown == location) color = new Color(1f, .73f, .3f);
            }
            FillCell(x, y, color);
        }
        // 적은 수의 표식을 덧그려 바닥 칸마다 은신처 목록을 검색하지 않는다.
        if (map?.hidingSpots != null) foreach (var location in map.hidingSpots)
            if (location.x >= 0 && location.x < columns && location.y >= 0 && location.y < rows && discovered(location.x, location.y))
                FillCell(location.x, location.y, new Color(.15f, .85f, .75f));
        Texture.SetPixels32(pixels); Texture.Apply(false, false);
        cachedMap = map; cachedColumns = columns; cachedRows = rows; cachedCount = discoveredCount; cachedCell = cell;
        return true;

        void FillCell(int x, int y, Color32 color)
        {
            int left = Mathf.Clamp(Mathf.CeilToInt((x * cell + gap * .5f) * scaleX - .5f), 0, width);
            int right = Mathf.Clamp(Mathf.CeilToInt(((x + 1) * cell - gap * .5f) * scaleX - .5f), 0, width);
            int bottom = Mathf.Clamp(Mathf.CeilToInt((y * cell + gap * .5f) * scaleY - .5f), 0, height);
            int top = Mathf.Clamp(Mathf.CeilToInt(((y + 1) * cell - gap * .5f) * scaleY - .5f), 0, height);
            for (int row = bottom; row < top; row++) Array.Fill(pixels, color, row * width + left, right - left);
        }
    }

    public void Dispose()
    {
        if (Texture != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(Texture); else UnityEngine.Object.DestroyImmediate(Texture);
        }
        Texture = null; pixels = null; cachedMap = null; cachedCount = -1;
    }
}
