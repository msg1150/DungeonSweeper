using System;
using UnityEngine;

/// <summary>마을의 의뢰와 소모품이 실제 던전 결과에 영향을 주도록 유지하는 상태.</summary>
public static partial class TownProgress
{
    public static int Gold { get; private set; }
    public static int SupplyKits { get; private set; }
    public static int LastRunGold { get; private set; }
    public static int LastContractBonus { get; private set; }
    public static void FailRun()
    {
        LastRunGold = 0;
        LastContractBonus = 0;
        LastRecoveredCount = LastRecoveredValue = 0;
        ClearActiveQuest();
        GameSession.RequestAutosave();
    }

    public static bool TryBuySupplyKit()
    {
        const int cost = 25;
        if (!InTown || Gold < cost || SupplyKits == int.MaxValue) return false;
        Gold -= cost;
        SupplyKits++;
        GameSession.RequestAutosave();
        return true;
    }

    public static bool TryUseSupplyKit()
    {
        if (SupplyKits <= 0) return false;
        SupplyKits--;
        GameSession.RequestAutosave();
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Gold = 0;
        SupplyKits = 0;
        LastRunGold = 0;
        LastContractBonus = 0;
        ResetQuests();
        ResetWarehouse();
    }

    public static void Restore(TownProgressData data)
    {
        if (data != null && !data.IsValid()) throw new ArgumentException("Invalid town progression.");
        Reset();
        if (data == null) return;
        RestoreWarehouse(data);
        Gold = Mathf.Max(0, data.gold);
        SupplyKits = Mathf.Max(0, data.supplyKits);
        LastRunGold = Mathf.Max(0, data.lastRunGold);
        LastContractBonus = data.lastContractBonus;
        RestoreQuests(data);
    }

    public static TownProgressData Capture() => new()
    {
        gold = Gold,
        supplyKits = SupplyKits,
        lastRunGold = LastRunGold,
        lastContractBonus = LastContractBonus,
        hasAcceptedContract = HasActiveQuest, questDataVersion = 1,
        activeQuest = ActiveQuest?.Capture(), completedQuestIds = CaptureCompletedQuests(),
        bagLevel = BagLevel, lastRecoveredCount = LastRecoveredCount, lastRecoveredValue = LastRecoveredValue,
        nextWarehouseId = nextWarehouseId, warehouse = CopyWarehouse(), saleLockedKinds = new(saleLocks)
    };

}
