using System;
using UnityEngine;

/// <summary>Shared HUD coordinates and font; restores IMGUI state for the next component.</summary>
public readonly struct GameGuiScope : IDisposable
{
    public const float Width = 1280f, Height = 720f;
    private readonly Matrix4x4 matrix;
    private readonly Color color;
    private readonly Font font;
    private readonly GUISkin skin;
    private readonly bool enabled;
    private readonly TextAnchor labelAlignment;
    private readonly int labelSize, buttonSize;

    public GameGuiScope(bool scale)
    {
        matrix = GUI.matrix; color = GUI.color; skin = GUI.skin;
        GUI.skin = GameUiTheme.GetSkin(skin);
        font = GUI.skin.font;
        enabled = GUI.enabled;
        labelAlignment = GUI.skin.label.alignment;
        labelSize = GUI.skin.label.fontSize; buttonSize = GUI.skin.button.fontSize;
        Font gameFont = GameShell.UiFont;
        if (gameFont != null) GUI.skin.font = gameFont;
        GUI.color = Color.white;
        if (scale)
        {
            float factor = Mathf.Max(.001f, Mathf.Min(Screen.width / Width, Screen.height / Height));
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - Width * factor) * .5f,
                (Screen.height - Height * factor) * .5f, 0f), Quaternion.identity, new Vector3(factor, factor, 1f));
        }
    }

    public void Dispose()
    {
        GUI.matrix = matrix; GUI.color = color; GUI.skin.font = font;
        GUI.enabled = enabled;
        GUI.skin.label.alignment = labelAlignment;
        GUI.skin.label.fontSize = labelSize; GUI.skin.button.fontSize = buttonSize;
        GUI.skin = skin;
    }
}
