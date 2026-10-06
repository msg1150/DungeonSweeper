using UnityEditor;
using System.IO;
using UnityEngine;

[CustomEditor(typeof(MonsterDatabase))]
public sealed class MonsterDatabaseEditor : Editor
{
    [InitializeOnLoadMethod]
    private static void EnsureDefaultAsset()
    {
        const string path = "Assets/Resources/MonsterDatabase.asset";
        if (AssetDatabase.LoadAssetAtPath<MonsterDatabase>(path) != null) return;
        if (File.Exists(path)) { Debug.LogError("Existing monster database has an unexpected type; preserving the asset: " + path); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        MonsterDatabase database = CreateInstance<MonsterDatabase>();
        database.ResetDefaults();
        AssetDatabase.CreateAsset(database, path);
        AssetDatabase.SaveAssets();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty monsters = serializedObject.FindProperty("monsters");
        EditorGUILayout.HelpBox("기존 저장 호환과 기본 콘텐츠 생성용 데이터입니다. 새 던전의 능력치는 Assets/Prefabs/Monsters, 해체·드롭은 Assets/Prefabs/Corpses, 던전별 생성 수는 Resources/Dungeons에서 수정하세요.", MessageType.Info);
        for (int i = 0; i < monsters.arraySize; i++)
        {
            SerializedProperty m = monsters.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"몬스터 {i + 1}", EditorStyles.boldLabel);
            if (GUILayout.Button("몬스터 제거", GUILayout.Width(100))) { monsters.DeleteArrayElementAtIndex(i); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); break; }
            EditorGUILayout.EndHorizontal();
            Draw(m, "id", "ID"); Draw(m, "displayName", "표시 이름"); Draw(m, "spriteSheetResource", "스프라이트 시트(Resources 경로)");
            Draw(m, "attackStyle", "공격 방식"); Draw(m, "attackDamage", "공격 피해"); Draw(m, "attackRange", "공격 범위"); Draw(m, "attackCooldown", "공격 간격"); Draw(m, "attackAnimationSeconds", "공격 애니메이션 시간");
            SerializedProperty loot = m.FindPropertyRelative("loot");
            EditorGUILayout.LabelField("전리품 테이블", EditorStyles.boldLabel);
            for (int j = 0; j < loot.arraySize; j++)
            {
                SerializedProperty item = loot.GetArrayElementAtIndex(j);
                EditorGUILayout.PropertyField(item.FindPropertyRelative("kindId"), new GUIContent("전리품 종류 ID (이름·아이콘과 독립)"));
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(item.FindPropertyRelative("lootName"), GUIContent.none, GUILayout.MinWidth(100));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("shape"), GUIContent.none, GUILayout.Width(80));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("dropChance"), new GUIContent("확률"), GUILayout.MinWidth(100));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("minPrice"), new GUIContent("최소"), GUILayout.MinWidth(80));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("maxPrice"), new GUIContent("최대"), GUILayout.MinWidth(80));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("width"), new GUIContent("가로"), GUILayout.MinWidth(70));
                EditorGUILayout.PropertyField(item.FindPropertyRelative("height"), new GUIContent("세로"), GUILayout.MinWidth(70));
                if (GUILayout.Button("-", GUILayout.Width(24))) { loot.DeleteArrayElementAtIndex(j); EditorGUILayout.EndHorizontal(); break; }
                EditorGUILayout.EndHorizontal();
            }
            if (GUILayout.Button("+ 전리품 추가")) loot.InsertArrayElementAtIndex(loot.arraySize);
            EditorGUILayout.EndVertical();
        }
        if (GUILayout.Button("+ 몬스터 추가", GUILayout.Height(28))) monsters.InsertArrayElementAtIndex(monsters.arraySize);
        if (GUILayout.Button("기본 3종으로 초기화")) { ((MonsterDatabase)target).ResetDefaults(); EditorUtility.SetDirty(target); }
        serializedObject.ApplyModifiedProperties();
    }

    private static void Draw(SerializedProperty parent, string name, string label) => EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), new GUIContent(label));
}
