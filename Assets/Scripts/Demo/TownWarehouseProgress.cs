using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WarehouseGroup
{
    public string KindId, Name;
    public LootShape Shape;
    public long Quantity;
    public int MinimumPrice = int.MaxValue, MaximumPrice;
    public bool SaleLocked;
}

/// <summary>Currency and item changes commit together, then request one save.</summary>
public static partial class TownProgress
{
    private static List<WarehouseStackData> warehouse = new();
    private static readonly HashSet<string> saleLocks = new();
    private static int nextWarehouseId = 1;
    // UI는 창고 변경 때만 그룹화·정렬·깊은 복사를 다시 수행한다.
    public static uint WarehouseRevision { get; private set; }
    private static void WarehouseChanged() => WarehouseRevision = unchecked(WarehouseRevision + 1);
    public static int BagLevel { get; private set; }
    public static int LastRecoveredCount { get; private set; }
    public static int LastRecoveredValue { get; private set; }
    public static Vector2Int BagSize => TownEconomyConfig.Active.BagSize(BagLevel);
    public static long WarehouseCount { get { long count = 0; foreach (var stack in warehouse) count += stack.quantity; return count; } }
    private static bool InTown => GameSession.HasActiveGame && !GameSession.IsLoading
        && SceneManager.GetActiveScene().name == GameFlowConfig.Active.townSceneName;

    private static void ResetWarehouse()
    {
        warehouse.Clear(); saleLocks.Clear(); nextWarehouseId = 1;
        BagLevel = LastRecoveredCount = LastRecoveredValue = 0;
        WarehouseChanged();
    }
    private static List<WarehouseStackData> CopyWarehouse()
    {
        List<WarehouseStackData> copy = new(); foreach (var item in warehouse) copy.Add(item.Copy()); return copy;
    }
    private static void RestoreWarehouse(TownProgressData data)
    {
        warehouse = new();
        if (data.warehouse != null) foreach (var stack in data.warehouse) warehouse.Add(stack.Copy());
        if (data.saleLockedKinds != null) foreach (string id in data.saleLockedKinds) saleLocks.Add(id);
        foreach (var stack in warehouse) if (stack.saleLocked) saleLocks.Add(stack.loot.Restore().KindId);
        BagLevel = data.bagLevel; LastRecoveredCount = data.lastRecoveredCount; LastRecoveredValue = data.lastRecoveredValue;
        nextWarehouseId = Mathf.Max(1, data.nextWarehouseId);
        WarehouseChanged();
    }

    public static long MaterialCount(string kindId)
    {
        long count = 0; foreach (var item in warehouse) if (item.loot.kindId == kindId) count += item.quantity; return count;
    }
    public static bool IsSaleLocked(string kindId) => saleLocks.Contains(kindId);
    public static bool SetSaleLock(string kindId, bool locked)
    {
        if (!InTown || !LootKinds.ValidId(kindId)) return false;
        if (locked) saleLocks.Add(kindId); else saleLocks.Remove(kindId);
        foreach (var item in warehouse) if (item.loot.kindId == kindId) item.saleLocked = locked;
        WarehouseChanged();
        GameSession.RequestAutosave(); return true;
    }
    public static List<WarehouseStackData> GetStacks(string kindId)
    {
        var result = new List<WarehouseStackData>();
        foreach (var item in warehouse) if (item.loot.kindId == kindId) result.Add(item.Copy());
        result.Sort((a, b) => a.unitPrice != b.unitPrice ? a.unitPrice.CompareTo(b.unitPrice) : a.id.CompareTo(b.id));
        return result;
    }
    public static List<WarehouseGroup> GetWarehouseGroups()
    {
        var groups = new Dictionary<string, WarehouseGroup>();
        foreach (var item in warehouse)
        {
            string kind = item.loot.kindId;
            if (!groups.TryGetValue(kind, out var group)) groups[kind] = group = new WarehouseGroup
                { KindId = kind, Name = item.loot.name, Shape = item.loot.shape, SaleLocked = IsSaleLocked(kind) };
            group.Quantity += item.quantity; group.MinimumPrice = Math.Min(group.MinimumPrice, item.unitPrice);
            group.MaximumPrice = Math.Max(group.MaximumPrice, item.unitPrice);
        }
        return new(groups.Values);
    }

    public static bool TryReceiveRun(GridInventory bag, out string error)
    {
        error = null;
        if (bag == null || !GameSession.HasActiveGame || GameSession.IsLoading) { error = "회수품을 보관할 수 없습니다."; return false; }
        var staged = CopyWarehouse(); int stagedId = nextWarehouseId;
        foreach (var item in bag.Items)
        {
            var loot = item.Definition;
            var stack = staged.Find(row => row.loot.kindId == loot.KindId && row.unitPrice == item.SaleValue
                && row.loot.value == loot.Value && row.loot.name == loot.Name && row.loot.shape == loot.Shape);
            if (stack != null)
            {
                if (stack.quantity == int.MaxValue) { error = "해당 회수품의 기록 가능한 수량을 초과했습니다."; return false; }
                stack.quantity++;
            }
            else
            {
                if (stagedId == int.MaxValue) { error = "회수품 기록을 추가할 수 없습니다."; return false; }
                staged.Add(new() { id = stagedId++, loot = LootSaveData.Capture(loot), quantity = 1,
                    unitPrice = item.SaleValue, saleLocked = IsSaleLocked(loot.KindId) });
            }
        }
        LastRecoveredCount = bag.Items.Count; LastRecoveredValue = bag.TotalValue;
        LastRunGold = LastContractBonus = 0;
        warehouse = staged; nextWarehouseId = stagedId;
        WarehouseChanged();
        bag.Clear();
        GameSession.RequestAutosave(); return true;
    }

    public static bool TrySellOne(int stackId, out string error)
    {
        error = null;
        if (!InTown) { error = "마을 거래소에서 판매해 주세요."; return false; }
        var stack = warehouse.Find(item => item.id == stackId);
        if (stack == null) { error = "판매할 전리품이 없습니다."; return false; }
        if (IsSaleLocked(stack.loot.kindId)) { error = "판매 잠금을 먼저 해제해 주세요."; return false; }
        if ((long)Gold + stack.unitPrice > int.MaxValue) { error = "골드를 더 받을 수 없습니다."; return false; }
        Gold += stack.unitPrice; stack.quantity--;
        if (stack.quantity == 0) warehouse.Remove(stack);
        WarehouseChanged();
        GameSession.RequestAutosave(); return true;
    }

    private static void Consume(List<WarehouseStackData> staged, string kindId, int quantity)
    {
        staged.Sort((a, b) => a.unitPrice != b.unitPrice ? a.unitPrice.CompareTo(b.unitPrice) : a.id.CompareTo(b.id));
        foreach (var stack in staged)
        {
            if (stack.loot.kindId != kindId) continue;
            int count = Math.Min(quantity, stack.quantity); stack.quantity -= count; quantity -= count;
            if (quantity == 0) break;
        }
        staged.RemoveAll(item => item.quantity == 0);
    }
    public static long MaterialCostPreview(string kindId, int quantity)
    {
        long cost = 0;
        foreach (var stack in GetStacks(kindId))
        {
            int count = Math.Min(quantity, stack.quantity); cost += (long)count * stack.unitPrice; quantity -= count;
            if (quantity == 0) break;
        }
        return cost;
    }
    public static BagUpgradeDefinition NextBagUpgrade => BagLevel < TownEconomyConfig.Active.bagUpgrades.Count
        ? TownEconomyConfig.Active.bagUpgrades[BagLevel] : null;
    public static bool CanUpgradeBag(out string error)
    {
        error = null;
        if (!InTown) { error = "마을 창고에서 가방을 확장해 주세요."; return false; }
        var next = NextBagUpgrade;
        if (next == null) { error = "가방이 최대 단계입니다."; return false; }
        if (!next.IsValid()) { error = "가방 확장 설정이 올바르지 않습니다."; return false; }
        Vector2Int currentSize = BagSize;
        if (next.width < currentSize.x || next.height < currentSize.y || (next.width == currentSize.x && next.height == currentSize.y))
        { error = "가방 확장은 기존 크기보다 커야 합니다."; return false; }
        if (Gold < next.goldCost) { error = "골드가 부족합니다."; return false; }
        foreach (var material in next.materials)
            if (MaterialCount(material.kindId) < material.quantity) { error = material.displayName + " 재료가 부족합니다."; return false; }
        return true;
    }
    public static bool TryUpgradeBag(out string error)
    {
        if (!CanUpgradeBag(out error)) return false;
        var next = NextBagUpgrade; var staged = CopyWarehouse();
        foreach (var material in next.materials) Consume(staged, material.kindId, material.quantity);
        warehouse = staged; Gold -= next.goldCost; BagLevel++;
        WarehouseChanged();
        GameSession.RequestAutosave(); return true;
    }
    public static bool TrySubmitContract(out string error)
    {
        error = null;
        if (!InTown || !HasAcceptedContract) { error = "진행 중인 회수 의뢰가 없습니다."; return false; }
        if (MaterialCount(ContractKindId) < 1) { error = "창고에 의뢰 제출품이 없습니다."; return false; }
        if ((long)Gold + ContractBonus > int.MaxValue) { error = "골드를 더 받을 수 없습니다."; return false; }
        var staged = CopyWarehouse(); Consume(staged, ContractKindId, 1); warehouse = staged;
        Gold += ContractBonus; LastContractBonus = LastRunGold = ContractBonus; HasAcceptedContract = false;
        WarehouseChanged();
        GameSession.RequestAutosave(); return true;
    }
}
