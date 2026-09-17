using UnityEngine;

/// <summary>마을의 의뢰와 소모품이 실제 던전 결과에 영향을 주도록 유지하는 상태.</summary>
public static class TownProgress
{
    private const int ContractBonus = 120;
    private static readonly LootShape[] contractTargets = { LootShape.Dagger, LootShape.Core, LootShape.Hide, LootShape.Horn };

    public static int Gold { get; private set; }
    public static int SupplyKits { get; private set; }
    public static int LastRunGold { get; private set; }
    public static int LastContractBonus { get; private set; }
    public static bool HasAcceptedContract { get; private set; }
    public static LootShape ContractTarget { get; private set; }
    public static int ActiveContractBonus => HasAcceptedContract ? ContractBonus : 0;
    public static string ContractTargetName => ContractTarget switch
    {
        LootShape.Dagger => "낡은 단검",
        LootShape.Core => "슬라임 핵",
        LootShape.Hide => "두꺼운 가죽",
        LootShape.Horn => "오우거 뿔",
        _ => "회수품"
    };

    public static bool AcceptContract()
    {
        if (HasAcceptedContract) return false;
        ContractTarget = contractTargets[Random.Range(0, contractTargets.Length)];
        HasAcceptedContract = true;
        return true;
    }

    public static void BankRun(int recoveredGold, bool contractCompleted)
    {
        LastContractBonus = HasAcceptedContract && contractCompleted ? ContractBonus : 0;
        LastRunGold = recoveredGold + LastContractBonus;
        Gold += LastRunGold;
        HasAcceptedContract = false;
    }

    public static bool TryBuySupplyKit()
    {
        const int cost = 25;
        if (Gold < cost) return false;
        Gold -= cost;
        SupplyKits++;
        return true;
    }

    public static bool TryUseSupplyKit()
    {
        if (SupplyKits <= 0) return false;
        SupplyKits--;
        return true;
    }
}
