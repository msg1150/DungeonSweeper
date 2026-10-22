using System;
using UnityEngine;

/// <summary>메뉴 원화와 투명 여백을 제외한 UV. PNG 원본은 수정하지 않는다.</summary>
[Serializable]
public sealed class MenuUiImage
{
    public Texture2D texture;
    public Rect uv = new(0f, 0f, 1f, 1f);
    public bool IsAvailable => texture != null && uv.x >= 0f && uv.y >= 0f && uv.width > 0f && uv.height > 0f
        && uv.xMax <= 1.00001f && uv.yMax <= 1.00001f;

    public void Draw(Rect rect)
    {
        if (IsAvailable && Event.current.type == EventType.Repaint)
            GUI.DrawTextureWithTexCoords(rect, texture, uv);
    }

    public void DrawButton(Rect rect)
    {
        if (!IsAvailable || Event.current.type != EventType.Repaint) return;
        // 긴 저장 슬롯·해상도 버튼도 양 끝의 둥근 석재 테두리는 같은 비율을 유지한다.
        float cap = Mathf.Min(rect.width * .5f, rect.height * texture.width * uv.width / (texture.height * uv.height) * .12f);
        GUI.DrawTextureWithTexCoords(new Rect(rect.x, rect.y, cap, rect.height), texture,
            new Rect(uv.x, uv.y, uv.width * .12f, uv.height));
        GUI.DrawTextureWithTexCoords(new Rect(rect.x + cap, rect.y, rect.width - 2f * cap, rect.height), texture,
            new Rect(uv.x + uv.width * .12f, uv.y, uv.width * .76f, uv.height));
        GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - cap, rect.y, cap, rect.height), texture,
            new Rect(uv.x + uv.width * .88f, uv.y, uv.width * .12f, uv.height));
    }

    // 패널의 모서리는 일정한 크기로 두고 가운데만 늘려 창 크기가 달라도 테두리가 찌그러지지 않게 한다.
    public void DrawPanel(Rect rect, float opacity = 1f)
    {
        if (!IsAvailable || Event.current.type != EventType.Repaint) return;
        Color previous = GUI.color;
        GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * Mathf.Clamp01(opacity));
        float edge = Mathf.Min(32f, rect.width * .5f, rect.height * .5f);
        for (int row = 0; row < 3; row++)
        for (int column = 0; column < 3; column++)
        {
            float x = column == 0 ? rect.x : column == 1 ? rect.x + edge : rect.xMax - edge;
            float y = row == 0 ? rect.y : row == 1 ? rect.y + edge : rect.yMax - edge;
            float width = column == 1 ? rect.width - 2f * edge : edge;
            float height = row == 1 ? rect.height - 2f * edge : edge;
            if (width <= 0f || height <= 0f) continue;
            float u = uv.x + uv.width * (column == 0 ? 0f : column == 1 ? .12f : .88f);
            float v = uv.y + uv.height * (row == 0 ? .88f : row == 1 ? .12f : 0f);
            GUI.DrawTextureWithTexCoords(new Rect(x, y, width, height), texture,
                new Rect(u, v, uv.width * (column == 1 ? .76f : .12f), uv.height * (row == 1 ? .76f : .12f)));
        }
        GUI.color = previous;
    }
}

[CreateAssetMenu(fileName = "MenuUiArt", menuName = "Dungeon Sweeper/Menu UI Art")]
public sealed class MenuUiArt : ScriptableObject
{
    // 잘못된 원화가 연결돼도 밝은 기본 패널 위에 흰 글자만 남지 않도록 테마를 함께 전환한다.
    public bool IsReady => menuPanel?.IsAvailable == true && optionsPanel?.IsAvailable == true
        && dialogPanel?.IsAvailable == true && buttonNormal?.IsAvailable == true
        && buttonHover?.IsAvailable == true && buttonPressed?.IsAvailable == true && buttonDisabled?.IsAvailable == true;

    [Header("Panels")]
    public MenuUiImage menuPanel, optionsPanel, dialogPanel;
    [Header("Reusable Button States")]
    public MenuUiImage buttonNormal, buttonHover, buttonPressed, buttonDisabled;
    [Header("Audio Sliders")]
    public MenuUiImage sliderTrack, sliderFill, sliderThumb;
    [Header("Settings and Navigation")]
    public MenuUiImage toggleOn, toggleOff, checkboxOn, checkboxOff, backIcon, closeIcon;
}
