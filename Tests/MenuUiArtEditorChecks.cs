#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

/// <summary>실제 Unity 역직렬화 이후에도 메뉴 그림의 참조와 잘라 그릴 영역이 유효한지 확인한다.</summary>
public static class MenuUiArtEditorChecks
{
    public static void Run(Action<bool, string> check)
    {
        var config = Resources.Load<MainMenuPresentationConfig>("MainMenuPresentation");
        check(config != null && config.menuUi != null, "menu presentation loads its UI art asset");
        var art = config.menuUi;
        check(art.IsReady, "image menu theme requires usable panels and button states");
        foreach (var field in typeof(MenuUiArt).GetFields(BindingFlags.Instance | BindingFlags.Public))
        {
            if (field.FieldType != typeof(MenuUiImage)) continue;
            var image = (MenuUiImage)field.GetValue(art);
            check(image != null && image.texture != null && image.IsAvailable
                && image.uv.xMin >= 0f && image.uv.yMin >= 0f && image.uv.xMax <= 1.00001f && image.uv.yMax <= 1.00001f,
                "menu image survives Unity deserialization: " + field.Name + " UV=" + image?.uv);
        }
        var invalid = UnityEngine.Object.Instantiate(art);
        try
        {
            invalid.menuPanel.uv = Rect.zero;
            check(!invalid.IsReady, "zero-sized UV cannot enable white text on a fallback paper panel");
            invalid.menuPanel.uv = new Rect(float.NaN, 0f, 1f, 1f);
            check(!invalid.menuPanel.IsAvailable, "non-finite UV cannot be drawn");
            invalid.menuPanel.uv = new Rect(.5f, 0f, 1f, 1f);
            check(!invalid.menuPanel.IsAvailable, "out-of-bounds UV cannot be drawn");
        }
        finally { UnityEngine.Object.DestroyImmediate(invalid); }
    }
}
#endif
