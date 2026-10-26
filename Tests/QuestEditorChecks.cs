#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>실제 의뢰 API를 에디터의 격리된 마을 씬에서 호출한다. Play 모드와 디스크 저장은 사용하지 않는다.</summary>
public static class QuestEditorChecks
{
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static void SetSession(string property, bool value)
        => typeof(GameSession).GetField("<" + property + ">k__BackingField", StaticPrivate).SetValue(null, value);

    public static void Run(Action<bool, string> check)
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/.utmp/UnityValidation/Assets", StringComparison.Ordinal)
            || EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Isolated editor only.");
        var realCatalog = Resources.Load<QuestCatalog>("QuestCatalog");
        check(realCatalog != null && realCatalog.quests.Length > 0 && realCatalog.CollectIssues().Count == 0, "registered quest assets load with valid IDs and requirements");
        foreach (var quest in realCatalog.quests) check(quest.IsValid(out _), "valid authored quest: " + quest.questId);

        var originalTown = TownProgress.Capture();
        Scene originalScene = SceneManager.GetActiveScene();
        Scene town = SceneManager.GetSceneByPath("Assets/Scenes/Town.unity");
        bool openedTown = !town.isLoaded;
        bool activeGame = GameSession.HasActiveGame, loading = GameSession.IsLoading, autosave = GameSession.AutosavePending;
        var catalogField = typeof(QuestCatalog).GetField("active", StaticPrivate);
        var originalCatalog = catalogField.GetValue(null);
        var temporaryCatalog = ScriptableObject.CreateInstance<QuestCatalog>();
        var single = CreateQuest("quest.validation.single", false, 250,
            new MaterialRequirement { kindId = LootKinds.GoblinHide, displayName = "가죽", quantity = 2 },
            new MaterialRequirement { kindId = "slime.core", displayName = "핵", quantity = 1 });
        var repeat = CreateQuest("quest.validation.repeat", true, 40,
            new MaterialRequirement { kindId = LootKinds.GoblinHide, displayName = "가죽", quantity = 1 });
        try
        {
            if (openedTown) town = EditorSceneManager.OpenScene("Assets/Scenes/Town.unity", OpenSceneMode.Additive);
            SceneManager.SetActiveScene(town);
            SetSession("HasActiveGame", true); SetSession("IsLoading", false); SetSession("AutosavePending", false);
            temporaryCatalog.quests = new[] { single, repeat }; catalogField.SetValue(null, temporaryCatalog);
            TownProgress.Reset();
            SetSession("IsLoading", true);
            check(!TownProgress.TryAcceptQuest(single, out _) && !TownProgress.HasActiveQuest, "quest acceptance cannot run during scene loading");
            SetSession("IsLoading", false);
            check(TownProgress.TryAcceptQuest(single, out _) && GameSession.AutosavePending, "accepting a selected registered quest requests an autosave");
            check(!TownProgress.TryAcceptQuest(repeat, out _) && TownProgress.ActiveQuest.Id == single.questId, "only one quest can be accepted at once");
            TownEconomyChecks.ReceiveItems(LootKinds.GoblinHide, "가죽", LootShape.Hide, 10, 2);
            check(!TownProgress.TrySubmitQuest(out _) && TownProgress.WarehouseCount == 2 && TownProgress.Gold == 0 && TownProgress.HasActiveQuest,
                "missing second objective cannot consume the first objective or award gold");
            TownEconomyChecks.ReceiveItems(LootKinds.GoblinHide, "가죽", LootShape.Hide, 90, 1);
            TownEconomyChecks.ReceiveItems("slime.core", "핵", LootShape.Core, 50, 1);
            TownProgress.SetSaleLock(LootKinds.GoblinHide, true);
            check(TownProgress.CanSubmitQuest(out _), "readiness uses all warehouse quantities even when sale-locked");
            SetSession("IsLoading", true);
            check(!TownProgress.TrySubmitQuest(out _) && TownProgress.WarehouseCount == 4 && TownProgress.Gold == 0,
                "submission cannot consume items while loading");
            SetSession("IsLoading", false);
            single.goldReward = 900; single.requirements[0].quantity = 99; single.displayName = "edited";
            check(TownProgress.ActiveQuest.GoldReward == 250 && TownProgress.ActiveQuest.Objectives[0].Quantity == 2
                && TownProgress.ActiveQuest.Title != "edited", "editing an asset cannot change accepted quest terms");
            var capture = TownProgress.Capture(); capture.activeQuest.requirements[0].quantity = 500;
            check(TownProgress.ActiveQuest.Objectives[0].Quantity == 2, "captured save DTO cannot mutate active objectives");
            var json = JsonUtility.ToJson(new GameSaveData { town = TownProgress.Capture() });
            var bytes = SaveFileProtection.Protect(json, 0);
            var loaded = JsonUtility.FromJson<GameSaveData>(SaveFileProtection.Unprotect(bytes, 0));
            check(loaded.IsValid() && loaded.town.activeQuest.goldReward == 250, "protected in-memory save roundtrip retains accepted terms");
            TownProgress.Restore(loaded.town);
            check(TownProgress.CanSubmitQuest(out _) && TownProgress.ActiveQuest.Objectives.Count == 2, "restoring accepted objectives recomputes readiness from warehouse");
            check(TownProgress.TrySubmitQuest(out _) && TownProgress.Gold == 250 && TownProgress.WarehouseCount == 1
                && TownProgress.GetStacks(LootKinds.GoblinHide)[0].unitPrice == 90 && TownProgress.HasCompletedQuest(single.questId),
                "submission atomically consumes cheapest tiers across objectives and records the reward");
            check(!TownProgress.TrySubmitQuest(out _) && TownProgress.Gold == 250 && !TownProgress.HasActiveQuest, "a completed quest cannot award twice");
            check(!TownProgress.TryAcceptQuest(single, out _), "one-time quest cannot be accepted again");
            check(TownProgress.TryAcceptQuest(repeat, out _), "another available quest can be selected after completion");
            var overflow = TownProgress.Capture(); overflow.gold = int.MaxValue; TownProgress.Restore(overflow);
            check(!TownProgress.TrySubmitQuest(out _) && TownProgress.WarehouseCount == 1 && TownProgress.HasActiveQuest,
                "reward overflow preserves both objectives and active status");
            overflow.gold = 0; TownProgress.Restore(overflow);
            check(TownProgress.TrySubmitQuest(out _) && TownProgress.TryAcceptQuest(repeat, out _), "repeatable quest becomes available after each submission");
            TownProgress.FailRun();
            check(!TownProgress.HasActiveQuest && TownProgress.HasCompletedQuest(single.questId) && TownProgress.Gold == 40,
                "death clears the active quest while preserving completion history and banked gold");
            check(TownProgress.TryAcceptQuest(repeat, out _) && TownProgress.TryAbandonQuest(out _) && !TownProgress.HasActiveQuest
                && TownProgress.Gold == 40, "abandoning a quest consumes no gold or items");
            CheckLegacyAndRemovedAssets(check, temporaryCatalog);
            CheckValidation(check, temporaryCatalog, single);
            TownProgress.Reset();
            check(!TownProgress.HasActiveQuest && !TownProgress.HasCompletedQuest("quest.legacy.goblin.dagger"), "a new game clears active quests and completion history");
        }
        finally
        {
            TownProgress.Restore(originalTown);
            SetSession("HasActiveGame", activeGame); SetSession("IsLoading", loading); SetSession("AutosavePending", autosave);
            catalogField.SetValue(null, originalCatalog);
            if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            if (openedTown && town.isLoaded) EditorSceneManager.CloseScene(town, true);
            UnityEngine.Object.DestroyImmediate(temporaryCatalog); UnityEngine.Object.DestroyImmediate(single); UnityEngine.Object.DestroyImmediate(repeat);
        }
    }

    private static QuestDefinition CreateQuest(string id, bool repeatable, int reward, params MaterialRequirement[] requirements)
    {
        var quest = ScriptableObject.CreateInstance<QuestDefinition>();
        quest.questId = id; quest.displayName = id; quest.repeatable = repeatable; quest.goldReward = reward;
        quest.requirements = new List<MaterialRequirement>(requirements);
        return quest;
    }

    private static void CheckLegacyAndRemovedAssets(Action<bool, string> check, QuestCatalog catalog)
    {
        var old = JsonUtility.FromJson<TownProgressData>("{\"version\":1,\"hasAcceptedContract\":true,\"contractTarget\":1,\"gold\":17}");
        check(old.IsValid(), "old protected-save payload remains valid without new quest fields");
        TownProgress.Restore(old);
        check(TownProgress.ActiveQuest.GoldReward == 120 && TownProgress.ActiveQuest.Objectives[0].KindId == "goblin.dagger"
            && TownProgress.ActiveQuest.Objectives[0].Quantity == 1, "old active contract migrates with its original material and reward");
        catalog.quests = Array.Empty<QuestDefinition>();
        var owner = new GameObject("Quest board checks");
        try
        {
            var panel = owner.AddComponent<TownCommercePanel>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TownCommercePanel).GetMethod("RefreshQuests", flags).Invoke(panel, null);
            var rows = (IList)typeof(TownCommercePanel).GetField("questChoices", flags).GetValue(panel);
            check(rows.Count == 1, "guild still shows a migrated or removed-asset active quest");
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
        TownEconomyChecks.ReceiveItems("goblin.dagger", "단검", LootShape.Dagger, 5, 1);
        check(TownProgress.TrySubmitQuest(out _) && TownProgress.Gold == 137, "missing original asset does not block legacy quest submission");
        var restored = JsonUtility.FromJson<TownProgressData>(JsonUtility.ToJson(TownProgress.Capture()));
        check(restored.IsValid() && !restored.hasAcceptedContract, "inactive quest JSON roundtrip accepts Unity's empty-object representation");
        TownProgress.Restore(restored);
        check(restored.questDataVersion == 1 && TownProgress.HasCompletedQuest("quest.legacy.goblin.dagger"), "completion history survives save restoration");
    }

    private static void CheckValidation(Action<bool, string> check, QuestCatalog catalog, QuestDefinition quest)
    {
        catalog.quests = new[] { quest, quest };
        check(catalog.CollectIssues().Count > 0 && !TownProgress.TryAcceptQuest(quest, out _), "duplicate catalog IDs cannot accept ambiguous quests");
        quest.requirements.Add(new MaterialRequirement { kindId = quest.requirements[0].kindId, displayName = "duplicate", quantity = 1 });
        check(!quest.IsValid(out _), "duplicate objective kinds are rejected rather than under-counted");
        quest.requirements.RemoveAt(quest.requirements.Count - 1);
        quest.requirements[0].quantity = 0;
        check(!quest.IsValid(out _), "zero required quantity cannot grant free rewards");
        var saved = TownProgress.Capture(); saved.questDataVersion = 9;
        check(!saved.IsValid(), "unsupported quest-save versions are rejected");
        saved = TownProgress.Capture(); saved.completedQuestIds.Add(saved.completedQuestIds[0]);
        check(!saved.IsValid(), "duplicate completion IDs cannot corrupt quest history");
        saved = TownProgress.Capture();
        saved.activeQuest = LegacyQuestMigration.Capture(LootShape.Dagger); saved.activeQuest.repeatable = false; saved.hasAcceptedContract = true;
        check(!saved.IsValid(), "saved one-time quests cannot be active after their recorded completion");
        saved = TownProgress.Capture(); saved.activeQuest = LegacyQuestMigration.Capture(LootShape.Dagger);
        check(!saved.IsValid(), "a populated quest snapshot cannot be hidden behind an inactive flag");
    }
}
#endif
