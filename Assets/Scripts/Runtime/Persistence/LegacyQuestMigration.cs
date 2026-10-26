using System;

/// <summary>이전 단일 품목·고정 보상 의뢰를 원래 조건 그대로 진행 스냅샷으로 변환한다.</summary>
public static class LegacyQuestMigration
{
    public static bool ValidTarget(LootShape target) => target == LootShape.Dagger || target == LootShape.Core
        || target == LootShape.Hide || target == LootShape.Horn;

    public static QuestSaveData Capture(LootShape target)
    {
        if (!ValidTarget(target)) throw new ArgumentException("Invalid legacy contract target.");
        string name = target switch
        {
            LootShape.Dagger => "낡은 단검", LootShape.Core => "슬라임 핵",
            LootShape.Hide => "두꺼운 가죽", LootShape.Horn => "오크 엄니", _ => "회수품"
        };
        string kind = LootKinds.LegacyId(target);
        return new QuestSaveData
        {
            questId = "quest.legacy." + kind, displayName = "기존 회수 의뢰", description = "이전 저장에서 이어받은 의뢰입니다.",
            goldReward = 120, repeatable = true,
            requirements = new() { new() { kindId = kind, displayName = name, quantity = 1 } }
        };
    }
}
