using System;
using System.Collections.Generic;

public sealed class QuestObjective
{
    public string KindId { get; }
    public string DisplayName { get; }
    public int Quantity { get; }
    internal QuestObjective(MaterialRequirement source)
    { KindId = source.kindId; DisplayName = source.displayName; Quantity = source.quantity; }
}

/// <summary>수락 후 바뀌지 않는 진행용 조건. 표시 계층에는 수정 가능한 저장 DTO를 노출하지 않는다.</summary>
public sealed class QuestContract
{
    private readonly QuestSaveData terms;
    public string Id => terms.questId;
    public string Title => terms.displayName;
    public string Description => terms.description;
    public int GoldReward => terms.goldReward;
    public bool Repeatable => terms.repeatable;
    public IReadOnlyList<QuestObjective> Objectives { get; }

    public QuestContract(QuestSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (!data.IsValid(out string error)) throw new ArgumentException(error);
        terms = data.Copy();
        var objectives = new QuestObjective[terms.requirements.Count];
        for (int i = 0; i < objectives.Length; i++) objectives[i] = new QuestObjective(terms.requirements[i]);
        Objectives = Array.AsReadOnly(objectives);
    }

    public QuestSaveData Capture() => terms.Copy();
}
