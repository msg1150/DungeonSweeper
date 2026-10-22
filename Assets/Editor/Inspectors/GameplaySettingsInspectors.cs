#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>선택한 표시 방식에서 실제 사용하는 항목만 편집 화면에 노출한다.</summary>
[CustomEditor(typeof(ThreeDPresentationConfig))]
public sealed class PresentationSettingsInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("use3D"), new GUIContent("입체 배경 사용"));
        if (serializedObject.FindProperty("use3D").boolValue)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useSpriteActors"), new GUIContent("2.5D 스프라이트 배우 사용"));
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("forwardRendererIndex"));
            if (serializedObject.FindProperty("useSpriteActors").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("spriteCameraElevation"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("dungeonViewSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("townViewSize"));
            }
            else
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraFieldOfView"));
                foreach (string field in ModelFields) EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
            }
        }
        serializedObject.ApplyModifiedProperties();
    }
    private static readonly string[] ModelFields = { "cat", "goblin", "slime", "orc", "town", "portal" };
    internal static bool UsesModels => ThreeDPresentationConfig.Enabled && !ThreeDPresentationConfig.SpritesEnabled;
}

[CustomEditor(typeof(MonsterPrefab))]
public sealed class MonsterPrefabInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "model3D", "stats");
        if (PresentationSettingsInspector.UsesModels) EditorGUILayout.PropertyField(serializedObject.FindProperty("model3D"));
        var stats = serializedObject.FindProperty("stats");
        EditorGUILayout.LabelField("Stats · 몬스터 능력치", EditorStyles.boldLabel);
        var property = stats.Copy(); var end = stats.GetEndProperty();
        bool enter = true;
        while (property.NextVisible(enter) && !SerializedProperty.EqualContents(property, end))
        {
            enter = false;
            if (property.name == "spriteSheetResource" && !serializedObject.FindProperty("useSpriteSheetAnimation").boolValue) continue;
            EditorGUILayout.PropertyField(property, true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}

[CustomEditor(typeof(CorpsePrefab))]
public sealed class CorpsePrefabInspector : Editor
{
    private static readonly string[] SheetFields = { "spriteSheetResource", "sheetColumns", "sheetRows", "sheetIndex", "pixelsPerUnit" };
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "model3D", "spriteSheetResource", "sheetColumns", "sheetRows", "sheetIndex", "pixelsPerUnit");
        if (PresentationSettingsInspector.UsesModels) EditorGUILayout.PropertyField(serializedObject.FindProperty("model3D"));
        if (serializedObject.FindProperty("useSpriteSheet").boolValue)
            foreach (string field in SheetFields) EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
