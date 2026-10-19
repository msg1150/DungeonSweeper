#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LootRewardTable))]
public sealed class LootRewardTableEditor : Editor
{
    private readonly HashSet<CorpsePrefab> collapsedCorpses = new();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("시체 프리팹을 한 번 추가하면 모든 기본 전리품이 표시됩니다. 변경할 전리품의 '개별 설정'을 켜세요. 끈 항목은 상속하며, 0%는 드롭 제외입니다. 적용 순서: 층 → 던전 → 시체 기본값. 각 전리품은 독립 추첨합니다.", MessageType.Info);
        var groups = LootRewardTableAuthoring.GetCorpses(serializedObject);
        foreach (var corpse in groups) DrawCorpse(corpse);
        DrawMissingReferences();
        EditorGUILayout.Space();
        var added = (CorpsePrefab)EditorGUILayout.ObjectField("시체 프리팹 추가", null, typeof(CorpsePrefab), false);
        if (added != null)
        {
            LootRewardTableAuthoring.AddCorpse(serializedObject, added);
            collapsedCorpses.Remove(added);
        }
        if (groups.Count == 0) EditorGUILayout.HelpBox("위 필드에 시체 프리팹을 넣으세요. 전리품을 하나씩 추가할 필요가 없습니다.", MessageType.None);
        if (serializedObject.FindProperty("entries").arraySize >= LootRewardTable.MaximumEntries)
            EditorGUILayout.HelpBox("개별 설정은 테이블당 최대 1024개입니다. 기존 설정을 상속으로 바꾸면 더 추가할 수 있습니다.", MessageType.Warning);
        serializedObject.ApplyModifiedProperties();
        if (!((LootRewardTable)target).IsValid(out string error)) EditorGUILayout.HelpBox(error, MessageType.Error);
    }

    private void DrawCorpse(CorpsePrefab corpse)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            bool expanded = !collapsedCorpses.Contains(corpse);
            bool remove;
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = EditorGUILayout.Foldout(expanded, corpse.displayName + " · " + corpse.name, true);
                remove = GUILayout.Button("시체 제거", GUILayout.Width(76));
            }
            if (expanded) collapsedCorpses.Remove(corpse); else collapsedCorpses.Add(corpse);
            if (remove) { LootRewardTableAuthoring.RemoveCorpse(serializedObject, corpse); return; }
            if (!expanded) return;
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField("시체 프리팹", corpse, typeof(CorpsePrefab), false);
            var items = LootRewardTableAuthoring.GetBaseLoot(corpse);
            if (items.Count == 0) EditorGUILayout.HelpBox("이 시체의 기본 전리품 목록이 비어 있습니다. 시체 프리팹에서 전리품을 추가하세요.", MessageType.Warning);
            if (items.Count > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    int missing = items.FindAll(item => LootRewardTableAuthoring.FindEntry(serializedObject, corpse, item.kindId) < 0).Count;
                    using (new EditorGUI.DisabledScope(serializedObject.FindProperty("entries").arraySize + missing > LootRewardTable.MaximumEntries))
                        if (GUILayout.Button("모두 개별 설정")) foreach (var item in items) LootRewardTableAuthoring.SetOverride(serializedObject, corpse, item.kindId, true);
                    if (GUILayout.Button("모두 상속")) foreach (var item in items) LootRewardTableAuthoring.SetOverride(serializedObject, corpse, item.kindId, false);
                }
            }
            foreach (var item in items) DrawItem(corpse, item);
            DrawStaleEntries(corpse, items);
        }
    }

    private void DrawItem(CorpsePrefab corpse, MonsterLootEntry item)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(item.lootName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(item.kindId, EditorStyles.miniLabel);
            int index = LootRewardTableAuthoring.FindEntry(serializedObject, corpse, item.kindId);
            bool enabled = index >= 0;
            bool requested;
            using (new EditorGUI.DisabledScope(!enabled && serializedObject.FindProperty("entries").arraySize >= LootRewardTable.MaximumEntries))
                requested = EditorGUILayout.Toggle("개별 설정", enabled);
            if (enabled != requested)
            {
                LootRewardTableAuthoring.SetOverride(serializedObject, corpse, item.kindId, requested);
                index = LootRewardTableAuthoring.FindEntry(serializedObject, corpse, item.kindId);
            }
            if (index >= 0)
            {
                var row = serializedObject.FindProperty("entries").GetArrayElementAtIndex(index);
                var chance = row.FindPropertyRelative("dropChance");
                EditorGUI.BeginChangeCheck();
                float percent = EditorGUILayout.Slider("드롭 확률 (%)", chance.floatValue * 100f, 0f, 100f);
                if (EditorGUI.EndChangeCheck()) chance.floatValue = percent / 100f;
                EditorGUILayout.PropertyField(row.FindPropertyRelative("minQuantity"), new GUIContent("최소 수량"));
                EditorGUILayout.PropertyField(row.FindPropertyRelative("maxQuantity"), new GUIContent("최대 수량"));
            }
            else EditorGUILayout.LabelField($"상속 사용 (상위 테이블 우선) · 시체 기본값 {item.dropChance * 100f:0.##}% / 1개", EditorStyles.wordWrappedMiniLabel);
        }
    }

    private void DrawStaleEntries(CorpsePrefab corpse, List<MonsterLootEntry> items)
    {
        var entries = serializedObject.FindProperty("entries");
        var seen = new HashSet<string>();
        for (int i = entries.arraySize - 1; i >= 0; i--)
        {
            var row = entries.GetArrayElementAtIndex(i);
            if (row.FindPropertyRelative("corpse").objectReferenceValue != corpse) continue;
            string kind = row.FindPropertyRelative("kindId").stringValue;
            bool exists = items.Exists(item => item.kindId == kind);
            bool duplicate = !seen.Add(kind);
            if (exists && !duplicate) continue;
            EditorGUILayout.HelpBox((duplicate ? "중복 설정: " : "기본 전리품 목록에서 찾을 수 없는 설정: ") + kind, MessageType.Error);
            if (GUILayout.Button("이 잘못된 설정 삭제")) entries.DeleteArrayElementAtIndex(i);
        }
    }

    private void DrawMissingReferences()
    {
        var entries = serializedObject.FindProperty("entries");
        for (int i = entries.arraySize - 1; i >= 0; i--)
        {
            var row = entries.GetArrayElementAtIndex(i);
            if (row.FindPropertyRelative("corpse").objectReferenceValue != null) continue;
            EditorGUILayout.HelpBox("시체 참조가 없는 기존 설정: " + row.FindPropertyRelative("kindId").stringValue, MessageType.Error);
            if (GUILayout.Button("참조 없는 설정 삭제")) entries.DeleteArrayElementAtIndex(i);
        }
    }

    public static void DrawReference(SerializedProperty property, string label)
    {
        EditorGUILayout.PropertyField(property, new GUIContent(label));
        if (property.objectReferenceValue != null && GUILayout.Button("보상 테이블 편집")) Selection.activeObject = property.objectReferenceValue;
    }
}

/// <summary>그룹 표시는 기존 항목 저장 형식을 재사용한다. 단순 조회·다시 그리기는 에셋을 변경하지 않는다.</summary>
public static class LootRewardTableAuthoring
{
    public static List<CorpsePrefab> GetCorpses(SerializedObject settings)
    {
        var result = new List<CorpsePrefab>();
        var groups = settings.FindProperty("displayedCorpses");
        for (int i = 0; i < groups.arraySize; i++) Add((CorpsePrefab)groups.GetArrayElementAtIndex(i).objectReferenceValue);
        // 이전 테이블도 변환 작업 없이 같은 프리팹의 설정을 한 묶음으로 보여준다.
        var entries = settings.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++) Add((CorpsePrefab)entries.GetArrayElementAtIndex(i).FindPropertyRelative("corpse").objectReferenceValue);
        return result;

        void Add(CorpsePrefab corpse) { if (corpse != null && !result.Contains(corpse)) result.Add(corpse); }
    }

    public static List<MonsterLootEntry> GetBaseLoot(CorpsePrefab corpse)
    {
        var result = new List<MonsterLootEntry>();
        var seen = new HashSet<string>();
        if (corpse != null && corpse.loot != null) foreach (var item in corpse.loot)
            if (item != null && seen.Add(item.kindId)) result.Add(item);
        return result;
    }

    public static void AddCorpse(SerializedObject settings, CorpsePrefab corpse)
    {
        if (corpse == null) return;
        var groups = settings.FindProperty("displayedCorpses");
        for (int i = 0; i < groups.arraySize; i++) if (groups.GetArrayElementAtIndex(i).objectReferenceValue == corpse) return;
        groups.GetArrayElementAtIndex(groups.arraySize++).objectReferenceValue = corpse;
    }

    public static void RemoveCorpse(SerializedObject settings, CorpsePrefab corpse)
    {
        var entries = settings.FindProperty("entries");
        for (int i = entries.arraySize - 1; i >= 0; i--)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("corpse").objectReferenceValue == corpse) entries.DeleteArrayElementAtIndex(i);
        var groups = settings.FindProperty("displayedCorpses");
        for (int i = groups.arraySize - 1; i >= 0; i--)
            if (groups.GetArrayElementAtIndex(i).objectReferenceValue == corpse)
            {
                // Unity 객체 참조 배열은 먼저 null로 만들어야 한 번의 삭제로 배열 길이가 줄어든다.
                groups.GetArrayElementAtIndex(i).objectReferenceValue = null;
                groups.DeleteArrayElementAtIndex(i);
            }
    }

    public static int FindEntry(SerializedObject settings, CorpsePrefab corpse, string kindId)
    {
        var entries = settings.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var row = entries.GetArrayElementAtIndex(i);
            if (row.FindPropertyRelative("corpse").objectReferenceValue == corpse && row.FindPropertyRelative("kindId").stringValue == kindId) return i;
        }
        return -1;
    }

    public static void SetOverride(SerializedObject settings, CorpsePrefab corpse, string kindId, bool enabled)
    {
        AddCorpse(settings, corpse); // 마지막 개별 설정을 꺼도 프리팹 묶음은 화면에 남긴다.
        var entries = settings.FindProperty("entries");
        if (!enabled)
        {
            for (int i = entries.arraySize - 1; i >= 0; i--)
            {
                var row = entries.GetArrayElementAtIndex(i);
                if (row.FindPropertyRelative("corpse").objectReferenceValue == corpse && row.FindPropertyRelative("kindId").stringValue == kindId)
                    entries.DeleteArrayElementAtIndex(i);
            }
            return;
        }
        if (FindEntry(settings, corpse, kindId) >= 0 || entries.arraySize >= LootRewardTable.MaximumEntries) return;
        var item = GetBaseLoot(corpse).Find(loot => loot.kindId == kindId);
        if (item == null) return;
        var added = entries.GetArrayElementAtIndex(entries.arraySize++);
        added.FindPropertyRelative("corpse").objectReferenceValue = corpse;
        added.FindPropertyRelative("kindId").stringValue = kindId;
        added.FindPropertyRelative("dropChance").floatValue = item.dropChance;
        added.FindPropertyRelative("minQuantity").intValue = added.FindPropertyRelative("maxQuantity").intValue = 1;
    }
}
#endif
