using System.Collections.Generic;
using UnityEngine;

/// <summary>밝은 UI 팔레트와 재사용하는 9-slice 스타일. 던전의 월드 조명과는 독립적이다.</summary>
public static class GameUiTheme
{
    public static readonly Color Ink = new(.16f, .24f, .22f);
    public static readonly Color MutedInk = new(.34f, .41f, .36f);
    public static readonly Color Paper = new(1f, .98f, .91f, .98f);
    public static readonly Color Mint = new(.77f, .89f, .79f);
    public static readonly Color MintHover = new(.88f, .96f, .86f);
    public static readonly Color MintPressed = new(.61f, .79f, .65f);
    public static readonly Color EmptyCell = new(.86f, .89f, .80f);
    public static readonly Color Scrim = new(.10f, .17f, .16f, .42f);
    public static readonly Color PromptPaper = new(1f, .94f, .74f, .98f);
    private static readonly List<Texture2D> textures = new();
    private static GUISkin skin;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        if (skin != null) Object.Destroy(skin);
        foreach (Texture2D texture in textures) if (texture != null) Object.Destroy(texture);
        textures.Clear(); skin = null;
    }

    // OnGUI 안에서만 호출한다. 원본 Unity skin은 복제하고, GUI scope가 원본을 복원한다.
    public static GUISkin GetSkin(GUISkin source)
    {
        if (skin != null) return skin;
        skin = Object.Instantiate(source);
        skin.name = "Dungeon Sweeper Bright UI";
        skin.hideFlags = HideFlags.HideAndDontSave;
        foreach (GUIStyle style in new[] { skin.label, skin.box, skin.toggle, skin.textField, skin.textArea })
            SetTextColors(style);
        StyleButton(skin.button);
        skin.box.normal.background = Rounded(Paper);
        skin.box.border = new RectOffset(12, 12, 12, 12);
        Texture2D field = Rounded(Color.white);
        foreach (GUIStyle style in new[] { skin.textField, skin.textArea })
        {
            style.normal.background = style.hover.background = style.focused.background = field;
            style.border = new RectOffset(12, 12, 12, 12);
            style.padding = new RectOffset(8, 8, 4, 4);
        }
        skin.horizontalSlider.normal.background = Rounded(EmptyCell);
        skin.horizontalSlider.border = new RectOffset(12, 12, 12, 12);
        skin.horizontalSlider.fixedHeight = 8f;
        skin.horizontalSliderThumb.normal.background = Rounded(MintPressed);
        skin.horizontalSliderThumb.hover.background = Rounded(MintHover);
        skin.horizontalSliderThumb.active.background = skin.horizontalSliderThumb.normal.background;
        skin.horizontalSliderThumb.border = new RectOffset(12, 12, 12, 12);
        skin.horizontalSliderThumb.fixedWidth = 18f;
        skin.horizontalSliderThumb.fixedHeight = 18f;
        skin.settings.cursorColor = Ink;
        skin.settings.selectionColor = Mint;
        return skin;
    }

    private static void SetTextColors(GUIStyle style)
    {
        style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = Ink;
        style.onNormal.textColor = style.onHover.textColor = style.onActive.textColor = style.onFocused.textColor = Ink;
    }

    private static void StyleButton(GUIStyle style)
    {
        SetTextColors(style);
        style.normal.background = Rounded(Mint);
        style.hover.background = Rounded(MintHover);
        style.active.background = Rounded(MintPressed);
        style.focused.background = style.hover.background;
        style.onNormal.background = style.active.background;
        style.onHover.background = style.hover.background;
        style.onActive.background = style.active.background;
        style.onFocused.background = style.hover.background;
        style.border = new RectOffset(12, 12, 12, 12);
        style.padding = new RectOffset(10, 10, 5, 5);
    }

    // 작은 UI 모양은 한 번만 생성한다. 화면 크기나 Repaint마다 텍스처를 만들지 않는다.
    private static Texture2D Rounded(Color fill)
    {
        const int size = 32;
        const float radius = 9f;
        Color edge = Color.Lerp(fill, Ink, .22f);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius), 0f);
            float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius), 0f);
            float coverage = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + .5f);
            Color color = y < 3 ? edge : fill;
            color.a *= coverage;
            pixels[y * size + x] = color;
        }
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        textures.Add(texture);
        return texture;
    }

    public static void Panel(Rect rect, Color? color = null)
    {
        Color previous = GUI.color;
        GUI.color = color ?? Paper;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
