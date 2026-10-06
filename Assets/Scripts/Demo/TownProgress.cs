using System;
using UnityEngine;

/// <summary>마을의 의뢰와 소모품이 실제 던전 결과에 영향을 주도록 유지하는 상태.</summary>
public static partial class TownProgress
{
    private const int ContractBonus = 120;
    private static readonly LootShape[] contractTargets = { LootShape.Dagger, LootShape.Core, LootShape.Hide, LootShape.Horn };

    public static int Gold { get; private set; }
    public static int SupplyKits { get; private set; }
    public static int LastRunGold { get; private set; }
    public static int LastContractBonus { get; private set; }
    public static bool HasAcceptedContract { get; private set; }
    public static LootShape ContractTarget { get; private set; }
    public static string ContractKindId => LootKinds.LegacyId(ContractTarget);
    public static int ActiveContractBonus => HasAcceptedContract ? ContractBonus : 0;
    public static string ContractTargetName => ContractTarget switch
    {
        LootShape.Dagger => "낡은 단검",
        LootShape.Core => "슬라임 핵",
        LootShape.Hide => "두꺼운 가죽",
        LootShape.Horn => "오크 엄니",
        _ => "회수품"
    };

    public static bool AcceptContract()
    {
        if (!InTown || HasAcceptedContract) return false;
        ContractTarget = contractTargets[UnityEngine.Random.Range(0, contractTargets.Length)];
        HasAcceptedContract = true;
        GameSession.RequestAutosave();
        return true;
    }

    public static void FailRun()
    {
        LastRunGold = 0;
        LastContractBonus = 0;
        LastRecoveredCount = LastRecoveredValue = 0;
        HasAcceptedContract = false;
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
        HasAcceptedContract = false;
        ContractTarget = LootShape.Dagger;
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
        LastContractBonus = Mathf.Clamp(data.lastContractBonus, 0, Mathf.Min(ContractBonus, LastRunGold));
        if (Array.IndexOf(contractTargets, data.contractTarget) >= 0)
        {
            ContractTarget = data.contractTarget;
            HasAcceptedContract = data.hasAcceptedContract;
        }
    }

    public static TownProgressData Capture() => new()
    {
        gold = Gold,
        supplyKits = SupplyKits,
        lastRunGold = LastRunGold,
        lastContractBonus = LastContractBonus,
        hasAcceptedContract = HasAcceptedContract,
        contractTarget = ContractTarget,
        bagLevel = BagLevel, lastRecoveredCount = LastRecoveredCount, lastRecoveredValue = LastRecoveredValue,
        nextWarehouseId = nextWarehouseId, warehouse = CopyWarehouse(), saleLockedKinds = new(saleLocks)
    };

}
