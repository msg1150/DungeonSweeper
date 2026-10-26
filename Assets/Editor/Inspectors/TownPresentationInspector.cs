#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TownPresentationConfig))]
public sealed class TownPresentationInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (!((TownPresentationConfig)target).IsValid()) EditorGUILayout.HelpBox("배경·시설 4개·좌표·외곽선을 확인하세요. Sprite/알파/밑동 높이를 바꿨다면 아래 버튼으로 충돌을 재생성하세요.", MessageType.Warning);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("PNG 알파 기반 충돌 재생성"))
            {
                try { TownFootprintBaker.Rebuild((TownPresentationConfig)target); }
                catch (System.Exception error) { EditorUtility.DisplayDialog("충돌 생성 실패", error.Message, "확인"); }
            }
        }
        EditorGUILayout.HelpBox("Feet Anchor는 원화의 좌상단을 (0,0)으로 하는 발판 좌표입니다. 건물 크기·충돌·상호작용 위치가 함께 적용되며, 해상도만 바꿔서는 월드 위치가 바뀌지 않습니다.", MessageType.Info);
        EditorGUILayout.HelpBox("알파 외곽선은 밑동에만 적용하고 지붕과 흐린 그림자는 제외합니다. 작은 계단 모양은 단순화하며 내부 구멍은 채웁니다. 실제 접촉 처리는 PolygonCollider2D를 사용합니다.", MessageType.Info);
    }
}
#endif
