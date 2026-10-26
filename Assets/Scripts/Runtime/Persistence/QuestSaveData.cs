using System;
using System.Collections.Generic;

/// <summary>에셋 수정·제거와 독립적으로 저장하는 수락 당시의 의뢰 조건.</summary>
[Serializable]
public sealed class QuestSaveData
{
    public const int MaximumRequirements = 16, MaximumCompletedQuests = 4096, MaximumQuantity = 1000000;
    public string questId, displayName, description;
    public int goldReward;
    public bool repeatable;
    public List<MaterialRequirement> requirements = new();
    // Unity는 null인 직렬화 클래스도 JSON 복원 시 빈 객체로 만들 수 있다. 의뢰 유무는 별도 플래그로 판별한다.
    public bool IsEmpty => string.IsNullOrEmpty(questId) && string.IsNullOrEmpty(displayName) && string.IsNullOrEmpty(description)
        && goldReward == 0 && !repeatable && (requirements == null || requirements.Count == 0);

    public bool IsValid(out string error)
        => ValidateTerms(questId, displayName, description, goldReward, requirements, out error);

    public static bool ValidateTerms(string questId, string displayName, string description, int goldReward,
        IReadOnlyList<MaterialRequirement> requirements, out string error)
    {
        error = null;
        if (!LootKinds.ValidId(questId) || string.IsNullOrWhiteSpace(displayName) || displayName.Length > 120
            || (description?.Length ?? 0) > 2000 || goldReward < 0)
        { error = "의뢰 ID·이름·설명·보상 골드를 확인하세요."; return false; }
        if (requirements == null || requirements.Count < 1 || requirements.Count > MaximumRequirements)
        { error = "요구 재료는 1~16종 등록하세요."; return false; }
        var kinds = new HashSet<string>();
        foreach (var requirement in requirements)
            if (requirement == null || !LootKinds.ValidId(requirement.kindId) || !kinds.Add(requirement.kindId)
                || string.IsNullOrWhiteSpace(requirement.displayName) || requirement.displayName.Length > 120
                || requirement.quantity < 1 || requirement.quantity > MaximumQuantity)
            { error = "재료 ID·이름·수량을 확인하세요. 같은 종류를 중복 등록할 수 없습니다."; return false; }
        return true;
    }

    public QuestSaveData Copy() => new()
    {
        questId = questId, displayName = displayName, description = description, goldReward = goldReward,
        repeatable = repeatable, requirements = CopyRequirements(requirements)
    };

    public static QuestSaveData Capture(QuestDefinition definition) => new()
    {
        questId = definition.questId, displayName = definition.displayName, description = definition.description,
        goldReward = definition.goldReward, repeatable = definition.repeatable, requirements = CopyRequirements(definition.requirements)
    };

    private static List<MaterialRequirement> CopyRequirements(List<MaterialRequirement> source)
    {
        if (source == null) return null;
        var copy = new List<MaterialRequirement>(source.Count);
        foreach (var row in source) copy.Add(row == null ? null : new MaterialRequirement
            { kindId = row.kindId, displayName = row.displayName, quantity = row.quantity });
        return copy;
    }
}
