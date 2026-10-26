using System.Collections.Generic;
using UnityEngine;

/// <summary>길드가 제시하는 의뢰 원본. 수락할 때 진행용 스냅샷으로 복사한다.</summary>
[CreateAssetMenu(fileName = "Quest", menuName = "Dungeon Sweeper/Quests/Quest Definition")]
public sealed class QuestDefinition : ScriptableObject
{
    public string questId = "quest.custom";
    public string displayName = "회수 의뢰";
    [TextArea(2, 6)] public string description = "";
    [Tooltip("같은 종류는 한 행만 등록하세요. 수량은 창고 보유량을 기준으로 확인합니다.")]
    public List<MaterialRequirement> requirements = new();
    [Min(0)] public int goldReward = 120;
    public bool repeatable = true;

    public bool IsValid(out string error)
        => QuestSaveData.ValidateTerms(questId, displayName, description, goldReward, requirements, out error);
}
