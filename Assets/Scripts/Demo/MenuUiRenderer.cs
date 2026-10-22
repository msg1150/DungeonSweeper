using UnityEngine;

/// <summary>메뉴 전용 원화 렌더러. 입력·포커스·슬라이더 드래그는 Unity의 기본 GUI 컨트롤에 맡긴다.</summary>
public sealed class MenuUiRenderer
{
    private readonly MenuUiArt art;
    private readonly GUIStyle sliderStyle = new() { fixedHeight = 30f };
    private readonly GUIStyle thumbStyle = new() { fixedWidth = 30f, fixedHeight = 30f };
    private int pressedControl;
    private Rect pressedRect;

    public MenuUiRenderer(MenuUiArt art) => this.art = art;

    public bool Button(Rect rect, string label, GUIStyle textStyle)
    {
        bool clicked = HitButton(rect, label);
        bool hover = GUI.enabled && (rect.Contains(Event.current.mousePosition) || GUI.GetNameOfFocusedControl() == label);
        bool pressed = hover && GUIUtility.hotControl != 0 && GUIUtility.hotControl == pressedControl && pressedRect == rect;
        MenuUiImage image = !GUI.enabled ? art.buttonDisabled : pressed ? art.buttonPressed : hover ? art.buttonHover : art.buttonNormal;
        if (image == null || !image.IsAvailable) image = art.buttonNormal;
        Color previous = GUI.color;
        // 배경에만 투명도를 적용하고 글자의 가독성은 유지한다.
        GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * GUI.backgroundColor.a);
        image?.DrawButton(rect);
        GUI.color = previous;
        // 모든 상태에서 같은 텍스트 레이어를 사용해 글꼴·크기·위치가 변하지 않게 한다.
        GUI.Label(rect, label, textStyle);
        return clicked;
    }

    public bool IconButton(Rect rect, string tooltip, MenuUiImage image)
    {
        bool clicked = HitButton(rect, tooltip);
        Color previous = GUI.color;
        float brightness = GUI.enabled && rect.Contains(Event.current.mousePosition) ? 1.15f : 1f;
        GUI.color = new Color(previous.r * brightness, previous.g * brightness, previous.b * brightness, previous.a);
        image?.Draw(rect);
        GUI.color = previous;
        return clicked;
    }

    private bool HitButton(Rect rect, string name)
    {
        bool mouseDown = GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 0
            && rect.Contains(Event.current.mousePosition) && GUIUtility.hotControl == 0;
        GUI.SetNextControlName(name);
        bool clicked = GUI.Button(rect, new GUIContent(string.Empty, name), GUIStyle.none);
        if (mouseDown && GUIUtility.hotControl != 0)
        {
            pressedControl = GUIUtility.hotControl;
            pressedRect = rect;
        }
        return clicked;
    }

    public float Slider(Rect rect, float value)
    {
        value = GUI.HorizontalSlider(rect, value, 0f, 1f, sliderStyle, thumbStyle);
        art.sliderTrack?.Draw(new Rect(rect.x, rect.center.y - 10f, rect.width, 20f));
        Rect fill = new(rect.x + 7f, rect.center.y - 5f, Mathf.Max(0f, rect.width - 14f), 10f);
        // 채움은 폭을 찌그러뜨리지 않고 왼쪽부터 잘라 보여준다. 0%에서는 그리지 않는다.
        if (value > 0f && art.sliderFill != null && art.sliderFill.IsAvailable && Event.current.type == EventType.Repaint)
        {
            Rect uv = art.sliderFill.uv;
            uv.width *= value;
            fill.width *= value;
            GUI.DrawTextureWithTexCoords(fill, art.sliderFill.texture, uv);
        }
        float thumbX = Mathf.Lerp(rect.x, rect.xMax - thumbStyle.fixedWidth, value);
        art.sliderThumb?.Draw(new Rect(thumbX, rect.center.y - 15f, 30f, 30f));
        return value;
    }
}
