#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>밸런스 에셋의 각 수치가 게임에 미치는 영향을 Inspector에 상시 표시한다.</summary>
[CustomEditor(typeof(DungeonTuning))]
public sealed class DungeonTuningEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        DungeonTuning tuning = (DungeonTuning)target;
        if (tuning.darkSightRadius <= tuning.clearSightRadius)
            EditorGUILayout.HelpBox("Dark Sight Radius는 Clear Sight Radius보다 커야 원형 그라데이션이 정상적으로 보입니다.", MessageType.Error);
        if (tuning.outerDarkness >= .95f && tuning.darkSightRadius - tuning.clearSightRadius < 3f)
            EditorGUILayout.HelpBox("바깥 어둠이 거의 완전한 검정이고 감쇠 구간도 좁아 화면 대부분이 갑자기 어두워집니다. Outer Darkness 0.75~0.9, 반경 차이 4 이상을 권장합니다.", MessageType.Warning);
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("수치 조절 가이드", EditorStyles.boldLabel);

        Help("Loot Value Multiplier",
            "높이면 모든 회수품의 정산 골드가 함께 증가합니다. 낮추면 회수 한 번의 보상이 줄어들어 의뢰 보너스의 비중이 커집니다.");
        Help("Clear Sight Radius",
            "월드 단위 반경입니다. 높이면 플레이어 주변의 완전히 밝은 원이 넓어집니다. 4.5는 현재 화면에서 중심부가 선명하게 보이는 기준값입니다.");
        Help("Dark Sight Radius / Outer Darkness",
            "Dark Sight Radius는 월드 단위로 최대 암전에 도달하는 거리이며, Clear Sight Radius보다 커야 합니다. Outer Darkness를 높이면 원 바깥이 더 검게, 낮추면 더 희미하게 보입니다.");
        Help("몬스터 능력치 설정 위치",
            "이동 속도·순찰·인식·소음 조사·공격은 Assets/Prefabs/Monsters의 Monster Prefab > Stats에서 종류별로 설정합니다. 현재 프리팹에 적용되지 않는 구 전역 조절 항목은 이 화면에서 제거했습니다.");
        Help("Show Monster Perception Ranges (에디터 전용)",
            "플레이 중 체크하면 Game 뷰에 모든 몬스터의 기본 인식(노랑), 소음 청취(하늘색 점선), 소음 후 인식(빨강), 은신 발각(보라)을 표시합니다. 실제 게임 빌드에는 표시 코드가 포함되지 않습니다. 개별 수치는 Assets/Prefabs/Monsters의 Monster Prefab > Stats에서 설정합니다. Hiding Reveal Range는 숨는 순간 은신을 발각하는 반경입니다. Noise Detection Multiplier와 Noise Alert Seconds는 소음 후 시야 확대 배율·시간입니다.");
        Help("Player Move / Dash",
            "Move Speed는 평상시 속도, Dash Speed는 대시 속도입니다. Dash Duration은 대시 유지 시간, Dash Cooldown은 다시 사용할 때까지의 대기 시간입니다.");
        Help("Pool Capacity Per Prefab / Pool Total Capacity",
            "몬스터·시체의 비활성 인스턴스를 프리팹별·전체 한도까지 보관합니다. 마을 귀환 후 재입장 시 재사용하고 메인 화면으로 돌아가면 해제합니다. 0으로 설정하면 반환 시 모두 삭제합니다. 활성 스폰 수에는 영향을 주지 않습니다.");
    }

    private static void Help(string title, string text)
    {
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        EditorGUILayout.HelpBox(text, MessageType.Info);
    }
}
#endif
