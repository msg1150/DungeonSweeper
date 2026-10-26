using System;
using System.Collections.Generic;

/// <summary>의뢰 수락 조건과 완료 이력. 제출은 재료·골드·이력을 함께 커밋한다.</summary>
public static partial class TownProgress
{
    private static readonly HashSet<string> completedQuests = new();
    public static QuestContract ActiveQuest { get; private set; }
    public static bool HasActiveQuest => ActiveQuest != null;
    public static uint QuestRevision { get; private set; }
    public static bool HasCompletedQuest(string id) => id != null && completedQuests.Contains(id);

    private static void ClearActiveQuest()
    {
        ActiveQuest = null;
        QuestRevision = unchecked(QuestRevision + 1);
    }

    private static void ResetQuests() { completedQuests.Clear(); ClearActiveQuest(); }

    private static void RestoreQuests(TownProgressData data)
    {
        if (data.questDataVersion == 0)
        {
            if (data.hasAcceptedContract) ActiveQuest = new QuestContract(LegacyQuestMigration.Capture(data.contractTarget));
        }
        else
        {
            if (data.hasAcceptedContract) ActiveQuest = new QuestContract(data.activeQuest);
            if (data.completedQuestIds != null) foreach (string id in data.completedQuestIds) completedQuests.Add(id);
        }
        QuestRevision = unchecked(QuestRevision + 1);
    }

    private static List<string> CaptureCompletedQuests()
    {
        var ids = new List<string>(completedQuests);
        ids.Sort(StringComparer.Ordinal);
        return ids;
    }

    public static bool TryAcceptQuest(QuestDefinition definition, out string error)
    {
        error = null;
        if (!InTown) { error = "마을 길드에서 수락해 주세요."; return false; }
        if (HasActiveQuest) { error = "진행 중인 의뢰를 완료하거나 포기해 주세요."; return false; }
        var catalog = QuestCatalog.Active;
        if (definition == null || catalog == null || catalog.Find(definition.questId) != definition)
        { error = "목록에 등록된 의뢰를 선택하세요."; return false; }
        var issues = catalog.CollectIssues();
        if (issues.Count > 0) { error = issues[0]; return false; }
        if (!definition.repeatable && HasCompletedQuest(definition.questId))
        { error = "이미 완료한 의뢰입니다."; return false; }
        if (!HasCompletedQuest(definition.questId) && completedQuests.Count >= QuestSaveData.MaximumCompletedQuests)
        { error = "기록 가능한 의뢰 수를 초과했습니다."; return false; }
        ActiveQuest = new QuestContract(QuestSaveData.Capture(definition));
        QuestRevision = unchecked(QuestRevision + 1);
        GameSession.RequestAutosave();
        return true;
    }

    public static bool CanSubmitQuest(out string error)
    {
        error = null;
        if (!InTown || !HasActiveQuest) { error = "마을 길드에서 진행 중인 의뢰를 제출하세요."; return false; }
        foreach (var objective in ActiveQuest.Objectives)
            if (MaterialCount(objective.KindId) < objective.Quantity)
            { error = objective.DisplayName + " 수량이 부족합니다."; return false; }
        if ((long)Gold + ActiveQuest.GoldReward > int.MaxValue)
        { error = "골드를 더 받을 수 없습니다."; return false; }
        if (!HasCompletedQuest(ActiveQuest.Id) && completedQuests.Count >= QuestSaveData.MaximumCompletedQuests)
        { error = "기록 가능한 의뢰 수를 초과했습니다."; return false; }
        return true;
    }

    public static bool TrySubmitQuest(out string error)
    {
        if (!CanSubmitQuest(out error)) return false;
        var contract = ActiveQuest;
        var staged = CopyWarehouse();
        staged.Sort((a, b) => a.unitPrice != b.unitPrice ? a.unitPrice.CompareTo(b.unitPrice) : a.id.CompareTo(b.id));
        foreach (var objective in contract.Objectives) ConsumeSorted(staged, objective.KindId, objective.Quantity);
        // 실패 가능한 검사가 모두 끝난 뒤 원본을 교체한다. 판매 잠금은 제출을 막지 않는다.
        warehouse = staged;
        Gold += contract.GoldReward;
        LastContractBonus = LastRunGold = contract.GoldReward;
        completedQuests.Add(contract.Id);
        ClearActiveQuest();
        WarehouseChanged();
        GameSession.RequestAutosave();
        return true;
    }

    public static bool TryAbandonQuest(out string error)
    {
        error = null;
        if (!InTown || !HasActiveQuest) { error = "마을 길드에서 진행 중인 의뢰를 포기할 수 있습니다."; return false; }
        ClearActiveQuest();
        GameSession.RequestAutosave();
        return true;
    }

    /// <summary>진행도는 창고 기준이다. 들고 있는 가방의 재료는 귀환 후에 반영한다.</summary>
    public static string QuestProgressText
    {
        get
        {
            if (!HasActiveQuest) return string.Empty;
            if (ActiveQuest.Objectives.Count != 1) return ActiveQuest.Title;
            var objective = ActiveQuest.Objectives[0];
            return $"{objective.DisplayName} {Math.Min(MaterialCount(objective.KindId), objective.Quantity)}/{objective.Quantity}";
        }
    }
}
