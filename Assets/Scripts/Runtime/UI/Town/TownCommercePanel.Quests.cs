using System.Collections.Generic;
using UnityEngine;

public sealed partial class TownCommercePanel
{
    private readonly List<GuildQuestChoice> questChoices = new();
    private string selectedQuestId, abandonQuestId, questCatalogError;
    private uint questBoardRevision;
    private GUIStyle questDescriptionStyle, questTitleStyle;
    private readonly GUIContent questDescriptionContent = new();
    private sealed class GuildQuestChoice
    {
        public QuestDefinition Definition;
        public QuestContract Accepted;
        public string Error;
        public string Id => Accepted != null ? Accepted.Id : Definition.questId;
        public string Title => Accepted != null ? Accepted.Title : Definition.displayName;
    }

    private void RefreshQuests()
    {
        questChoices.Clear(); abandonQuestId = null; questCatalogError = null;
        var active = TownProgress.ActiveQuest;
        // 원본 에셋이 수정·삭제되거나 구 저장의 의뢰라도 수락 당시 조건을 계속 표시한다.
        if (active != null) questChoices.Add(new GuildQuestChoice { Accepted = active });
        var catalog = QuestCatalog.Active;
        if (catalog == null) questCatalogError = "QuestCatalog에 의뢰를 등록하세요.";
        else
        {
            var issues = catalog.CollectIssues();
            if (issues.Count > 0) questCatalogError = issues[0];
            var ids = new HashSet<string>();
            if (catalog.quests != null) foreach (var definition in catalog.quests)
            {
                if (definition == null || !ids.Add(definition.questId) || definition.questId == active?.Id) continue;
                definition.IsValid(out string error);
                questChoices.Add(new GuildQuestChoice { Definition = definition, Error = error });
            }
        }
        bool found = false;
        foreach (var choice in questChoices) if (choice.Id == selectedQuestId) { found = true; break; }
        if (!found) selectedQuestId = questChoices.Count > 0 ? questChoices[0].Id : null;
        questBoardRevision = TownProgress.QuestRevision;
    }

    private void DrawGuild()
    {
        if (questBoardRevision != TownProgress.QuestRevision) RefreshQuests();
        questDescriptionStyle ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16, alignment = TextAnchor.UpperLeft };
        questTitleStyle ??= new GUIStyle(questDescriptionStyle) { fontSize = 23, fontStyle = FontStyle.Bold };
        Text(new Rect(176, 151, 924, 24), "동시 수락 1개 · 진행도는 창고 보유량 기준입니다.", 15);
        if (questChoices.Count == 0)
        {
            GUI.Label(new Rect(176, 210, 924, 100), questCatalogError ?? "현재 등록된 의뢰가 없습니다.", questDescriptionStyle);
            return;
        }
        GuildQuestChoice selected = null;
        listScroll = GUI.BeginScrollView(new Rect(176, 185, 306, 400), listScroll, new Rect(0, 0, 286, questChoices.Count * 86));
        try
        {
            for (int i = 0; i < questChoices.Count; i++)
            {
                var choice = questChoices[i];
                Rect card = new Rect(0, i * 86, 282, 76);
                Panel(card, choice.Id == selectedQuestId ? GameUiTheme.Mint : GameUiTheme.EmptyCell);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none))
                { selectedQuestId = choice.Id; abandonQuestId = null; detailScroll = Vector2.zero; notice = null; }
                GUI.Label(new Rect(10, card.y + 7, 262, 36), choice.Title, questDescriptionStyle);
                string status = choice.Accepted != null ? TownProgress.CanSubmitQuest(out _) ? "완료 가능" : "진행 중"
                    : choice.Error != null ? "설정 확인 필요"
                    : !choice.Definition.repeatable && TownProgress.HasCompletedQuest(choice.Id) ? "완료"
                    : TownProgress.HasActiveQuest ? "다른 의뢰 진행 중" : "수락 가능";
                Text(new Rect(10, card.y + 47, 262, 24), status, 14);
                if (choice.Id == selectedQuestId) selected = choice;
            }
        }
        finally { GUI.EndScrollView(); }
        if (selected == null) return;
        DrawQuestDetails(selected);
    }

    private void DrawQuestDetails(GuildQuestChoice choice)
    {
        var contract = choice.Accepted;
        var definition = choice.Definition;
        int reward = contract != null ? contract.GoldReward : definition.goldReward;
        bool repeatable = contract != null ? contract.Repeatable : definition.repeatable;
        GUI.Label(new Rect(510, 185, 588, 52), choice.Title, questTitleStyle);
        Text(new Rect(510, 243, 588, 26), $"보상 {reward}G · {(repeatable ? "반복 가능" : "한 번만 완료 가능")}", 18);
        int count = contract != null ? contract.Objectives.Count : definition.requirements?.Count ?? 0;
        questDescriptionContent.text = (contract != null ? contract.Description : definition.description) ?? string.Empty;
        float descriptionHeight = questDescriptionContent.text.Length > 0 ? questDescriptionStyle.CalcHeight(questDescriptionContent, 565f) + 16f : 0f;
        detailScroll = GUI.BeginScrollView(new Rect(510, 283, 594, 211), detailScroll,
            new Rect(0, 0, 574, Mathf.Max(211f, descriptionHeight + count * 40f)));
        try
        {
            GUI.Label(new Rect(0, 0, 565, Mathf.Max(0f, descriptionHeight - 16f)), questDescriptionContent, questDescriptionStyle);
            for (int i = 0; i < count; i++)
            {
                string kind, name; int quantity;
                if (contract != null)
                { var row = contract.Objectives[i]; kind = row.KindId; name = row.DisplayName; quantity = row.Quantity; }
                else
                {
                    var row = definition.requirements[i];
                    if (row == null) continue;
                    kind = row.kindId; name = row.displayName; quantity = row.quantity;
                }
                Text(new Rect(0, descriptionHeight + i * 40, 565, 35), $"{name}   {System.Math.Min(TownProgress.MaterialCount(kind), quantity)}/{quantity}개", 18);
            }
        }
        finally { GUI.EndScrollView(); }
        string hint = choice.Error ?? "제출 재료는 소비됩니다. 가격이 낮은 것부터 사용하며 판매 잠금도 제출에는 사용됩니다.";
        bool wasEnabled = GUI.enabled;
        if (contract != null)
        {
            bool ready = TownProgress.CanSubmitQuest(out string error);
            if (!ready) hint = error;
            GUI.enabled = wasEnabled && ready;
            bool submit = GUI.Button(new Rect(510, 548, 290, 40), "재료 제출 · 보상 받기");
            GUI.enabled = wasEnabled;
            if (submit) notice = TownProgress.TrySubmitQuest(out error) ? $"의뢰 완료! +{reward}G" : error;
            if (GUI.Button(new Rect(820, 548, 130, 40), abandonQuestId == contract.Id ? "포기 확인" : "의뢰 포기"))
            {
                if (abandonQuestId != contract.Id) abandonQuestId = contract.Id;
                else notice = TownProgress.TryAbandonQuest(out error) ? "의뢰를 포기했습니다. 보관 재료는 유지됩니다." : error;
            }
            if (abandonQuestId == contract.Id)
            {
                hint = "포기해도 골드·보관 재료는 소비하지 않습니다. 다시 한 번 포기 확인을 눌러 주세요.";
                if (GUI.Button(new Rect(966, 548, 130, 40), "취소")) abandonQuestId = null;
            }
        }
        else
        {
            bool completed = !repeatable && TownProgress.HasCompletedQuest(choice.Id);
            bool canAccept = !completed && !TownProgress.HasActiveQuest && choice.Error == null && questCatalogError == null;
            if (questCatalogError != null) hint = questCatalogError;
            else if (completed) hint = "이미 완료한 의뢰입니다.";
            else if (TownProgress.HasActiveQuest) hint = "진행 중인 의뢰를 먼저 완료하거나 포기해 주세요.";
            GUI.enabled = wasEnabled && canAccept;
            bool accept = GUI.Button(new Rect(510, 548, 330, 40), completed ? "완료한 의뢰" : "의뢰 수락");
            GUI.enabled = wasEnabled;
            if (accept) notice = TownProgress.TryAcceptQuest(definition, out string error) ? "의뢰를 수락했습니다." : error;
        }
        GUI.Label(new Rect(510, 502, 588, 42), hint, questDescriptionStyle);
    }
}
