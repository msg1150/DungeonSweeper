using UnityEngine;

/// <summary>전리품 종류와 아이콘 시트의 칸을 연결한다.</summary>
public static class LootIconLibrary
{
    private static Texture2D sheet;

    public static void Draw(Rect rect, LootShape shape)
    {
        if (sheet == null) sheet = Resources.Load<Texture2D>("Sprites/loot-icons");
        if (sheet == null) return;
        int index = (int)shape;
        int column = index % 3;
        int row = index / 3;
        Rect uv = new Rect(column / 3f, row == 0 ? .5f : 0f, 1f / 3f, .5f);
        GUI.DrawTextureWithTexCoords(rect, sheet, uv, true);
    }
}
