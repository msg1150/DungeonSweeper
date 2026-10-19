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
        EditorGUILayout.HelpBox("기본 콘텐츠 최초 생성과 이전 저장 복원에 사용하는 내부 데이터입니다. 현재 몬스터 능력치와 전리품은 프리팹에서만 수정합니다.", MessageType.Info);
        EditorGUILayout.LabelField("기본 종류 수", (((MonsterDatabase)target).monsters?.Count ?? 0).ToString());
        if (GUILayout.Button("몬스터 프리팹 폴더 열기"))
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>("Assets/Prefabs/Monsters");
        if (GUILayout.Button("시체 · 전리품 프리팹 폴더 열기"))
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>("Assets/Prefabs/Corpses");
    }
}
