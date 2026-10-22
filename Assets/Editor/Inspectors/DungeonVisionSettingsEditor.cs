#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>제작 창과 던전 Inspector에서 같은 필드·설명을 사용한다.</summary>
public static class DungeonVisionSettingsEditor
{
    public static void Draw(SerializedProperty vision)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("시야·어둠 (던전 전체)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(vision.FindPropertyRelative("clearSightRadius"),
            new GUIContent("선명 시야 반경", "플레이어 주변에서 어둠을 적용하지 않는 월드 거리입니다."));
        EditorGUILayout.PropertyField(vision.FindPropertyRelative("darkSightRadius"),
            new GUIContent("바깥 어둠 반경", "여기까지 점차 어두워집니다. 선명 시야 반경보다 커야 합니다."));
        EditorGUILayout.PropertyField(vision.FindPropertyRelative("outerDarkness"),
            new GUIContent("바깥 어둠 강도", "바깥 반경 이후의 강도입니다. 0은 어둠 없음, 1은 완전한 검정입니다."));
        if (GUILayout.Button("현재 기본값 가져오기"))
        {
            var defaults = DungeonVisionSettings.FromDefaults();
            vision.FindPropertyRelative("clearSightRadius").floatValue = defaults.clearSightRadius;
            vision.FindPropertyRelative("darkSightRadius").floatValue = defaults.darkSightRadius;
            vision.FindPropertyRelative("outerDarkness").floatValue = defaults.outerDarkness;
        }
        EditorGUILayout.HelpBox("이 던전의 모든 층에 적용합니다. 새 던전의 초기값은 DungeonTuning에서 가져옵니다. 몬스터 인식 거리와는 별개입니다.", MessageType.None);
    }
}
#endif
