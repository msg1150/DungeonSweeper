#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>기본 시트 크기가 바뀌면 프리팹 미리보기의 영역만 갱신한다. 기존 GUID와 프리팹 설정은 보존한다.</summary>
[InitializeOnLoad]
public sealed class ConceptArtPreviewRefresh : AssetPostprocessor
{
    private static bool queued;

    static ConceptArtPreviewRefresh() => QueueRefresh();

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
            if (path.StartsWith("Assets/Resources/Sprites/Monsters/", System.StringComparison.Ordinal)
                && path.EndsWith("-sheet.png", System.StringComparison.Ordinal)) { QueueRefresh(); break; }
    }

    private static void QueueRefresh()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += Refresh;
    }

    [MenuItem("Dungeon Sweeper/Refresh Default Art Previews")]
    public static void Refresh()
    {
        queued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string[] species = { "goblin", "slime", "orc" };
        for (int i = 0; i < species.Length; i++)
        {
            UpdatePreview(species[i] + "-monster", species[i] + "-sheet", 3, 2, 3);
            UpdatePreview(species[i] + "-corpse", "corpse-sheet", 3, 1, i);
        }
    }

    private static void UpdatePreview(string name, string sheet, int columns, int rows, int index)
    {
        string path = "Assets/Prefabs/Previews/" + name + ".asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Sprites/Monsters/" + sheet + ".png");
        if (existing == null || texture == null) return;
        float width = texture.width / (float)columns, height = texture.height / (float)rows;
        Rect rect = new Rect(index % columns * width, index / columns * height, width, height);
        Vector2 pivot = new Vector2(.5f, .35f);
        if (existing.texture == texture && existing.rect == rect && Mathf.Approximately(existing.pixelsPerUnit, 180f)
            && Vector2.Distance(existing.pivot, new Vector2(width * pivot.x, height * pivot.y)) < .01f) return;
        Sprite replacement = Sprite.Create(texture, rect, pivot, 180f);
        try
        {
            replacement.name = existing.name;
            EditorUtility.CopySerialized(replacement, existing);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
        }
        finally { Object.DestroyImmediate(replacement); }
    }
}
#endif
