#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(QuestDefinition))]
public sealed class QuestDefinitionInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!((QuestDefinition)target).IsValid(out string error)) EditorGUILayout.HelpBox(error, MessageType.Warning);
        EditorGUILayout.HelpBox("Quest ID는 저장에 사용하므로 배포 후 바꾸지 마세요. 만든 의뢰를 Resources/QuestCatalog 목록에 연결하면 길드에 표시됩니다. 진행 중인 의뢰는 수락 당시의 조건을 유지합니다.", MessageType.Info);
    }
}

[CustomEditor(typeof(QuestCatalog))]
public sealed class QuestCatalogInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        foreach (string issue in ((QuestCatalog)target).CollectIssues()) EditorGUILayout.HelpBox(issue, MessageType.Warning);
    }
}
#endif
