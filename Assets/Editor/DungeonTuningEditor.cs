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
        Help("Fog World Size",
            "안개가 덮는 전체 월드 크기입니다. 카메라 화면보다 충분히 크게 두면 됩니다. 보통 32~48을 권장합니다.");
        Help("Clear Sight Radius",
            "월드 단위 반경입니다. 높이면 플레이어 주변의 완전히 밝은 원이 넓어집니다. 4.5는 현재 화면에서 중심부가 선명하게 보이는 기준값입니다.");
        Help("Dark Sight Radius / Outer Darkness",
            "Dark Sight Radius는 월드 단위로 최대 암전에 도달하는 거리이며, Clear Sight Radius보다 커야 합니다. Outer Darkness를 높이면 원 바깥이 더 검게, 낮추면 더 희미하게 보입니다.");
        Help("Patrol Speed / Chase Speed",
            "높이면 배회 또는 추적 중인 적이 빨라집니다. 낮추면 회피와 유인에 여유가 생깁니다. Chase Speed는 보통 Patrol Speed보다 높게 둡니다.");
        Help("Detection Range",
            "높이면 벽에 가리지 않은 적이 더 먼 거리에서 플레이어를 발견합니다. 낮추면 가까이 접근해야 추적을 시작합니다.");
        Help("Hearing Range / Investigation Seconds",
            "Hearing Range를 높이면 해체 실패 소리를 듣는 적의 범위가 넓어집니다. Investigation Seconds를 높이면 적이 소리 난 시체를 더 오래 조사합니다.");
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
