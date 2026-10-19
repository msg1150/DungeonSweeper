#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LootRewardTable))]
public sealed class LootRewardTableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("시체의 기본 전리품 중 변경할 항목만 추가하세요. 행마다 독립 추첨하며 확률 합계는 100%일 필요가 없습니다. 0%는 해당 항목을 제외합니다. 적용 순서: 층 → 던전 → 시체 기본값.", MessageType.Info);
        var entries = serializedObject.FindProperty("entries");
        int remove = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var row = entries.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"보상 항목 {i + 1}", EditorStyles.boldLabel);
                    if (GUILayout.Button("삭제", GUILayout.Width(52))) remove = i;
                }
                var corpse = row.FindPropertyRelative("corpse");
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(corpse, new GUIContent("시체 프리팹"));
                bool changed = EditorGUI.EndChangeCheck();
                var kind = row.FindPropertyRelative("kindId");
                DrawKind(kind, (CorpsePrefab)corpse.objectReferenceValue, changed);
                var chance = row.FindPropertyRelative("dropChance");
                chance.floatValue = EditorGUILayout.Slider("드롭 확률 (%)", chance.floatValue * 100f, 0f, 100f) / 100f;
                EditorGUILayout.PropertyField(row.FindPropertyRelative("minQuantity"), new GUIContent("최소 수량"));
                EditorGUILayout.PropertyField(row.FindPropertyRelative("maxQuantity"), new GUIContent("최대 수량"));
            }
        }
        if (remove >= 0) entries.DeleteArrayElementAtIndex(remove);
        using (new EditorGUI.DisabledScope(entries.arraySize >= 1024))
            if (GUILayout.Button("보상 항목 추가"))
            {
                int index = entries.arraySize++;
                var row = entries.GetArrayElementAtIndex(index);
                row.FindPropertyRelative("corpse").objectReferenceValue = null;
                row.FindPropertyRelative("kindId").stringValue = "";
                row.FindPropertyRelative("dropChance").floatValue = .5f;
                row.FindPropertyRelative("minQuantity").intValue = row.FindPropertyRelative("maxQuantity").intValue = 1;
            }
        serializedObject.ApplyModifiedProperties();
        if (!((LootRewardTable)target).IsValid(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
    }

    private static void DrawKind(SerializedProperty property, CorpsePrefab corpse, bool changedCorpse)
    {
        var ids = new List<string>(); var labels = new List<string>();
        if (corpse?.loot != null) foreach (var item in corpse.loot)
            if (item != null && !ids.Contains(item.kindId)) { ids.Add(item.kindId); labels.Add($"{item.lootName} ({item.kindId})"); }
        if (changedCorpse) property.stringValue = ids.Count > 0 ? ids[0] : "";
        int index = ids.IndexOf(property.stringValue);
        if (index < 0)
        {
            ids.Insert(0, property.stringValue); labels.Insert(0, string.IsNullOrEmpty(property.stringValue) ? "전리품을 선택하세요" : "기본 목록에 없음: " + property.stringValue);
            index = 0;
        }
        using (new EditorGUI.DisabledScope(corpse == null || corpse.loot == null || corpse.loot.Count == 0))
            property.stringValue = ids[EditorGUILayout.Popup("기본 전리품", index, labels.ToArray())];
    }

    public static void DrawReference(SerializedProperty property, string label)
    {
        EditorGUILayout.PropertyField(property, new GUIContent(label));
        if (property.objectReferenceValue != null && GUILayout.Button("보상 테이블 편집")) Selection.activeObject = property.objectReferenceValue;
    }
}
#endif
