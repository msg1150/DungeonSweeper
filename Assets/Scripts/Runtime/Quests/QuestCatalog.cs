using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestCatalog", menuName = "Dungeon Sweeper/Quests/Quest Catalog")]
public sealed class QuestCatalog : ScriptableObject
{
    public const int MaximumQuests = 512;
    public QuestDefinition[] quests = Array.Empty<QuestDefinition>();
    private static QuestCatalog active;
    public static QuestCatalog Active => active != null ? active : active = Resources.Load<QuestCatalog>("QuestCatalog");
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetActive() => active = null;

    public QuestDefinition Find(string id)
    {
        if (quests != null) foreach (var quest in quests) if (quest != null && quest.questId == id) return quest;
        return null;
    }

    public List<string> CollectIssues()
    {
        var issues = new List<string>();
        if (quests == null || quests.Length == 0 || quests.Length > MaximumQuests)
        { issues.Add("의뢰는 1~512개 등록하세요."); return issues; }
        var ids = new HashSet<string>();
        foreach (var quest in quests)
        {
            if (quest == null) { issues.Add("연결되지 않은 의뢰가 있습니다."); continue; }
            if (!quest.IsValid(out string error)) issues.Add(quest.name + ": " + error);
            if (!ids.Add(quest.questId)) issues.Add("중복 의뢰 ID: " + quest.questId);
        }
        return issues;
    }
}
