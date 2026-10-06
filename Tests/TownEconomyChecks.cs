using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class TownEconomyChecks
{
    public static void ReceiveItems(string kind, string name, LootShape shape, int price, int count)
    {
        while (count > 0)
        {
            var bag = new GridInventory();
            for (int i = 0; i < 20 && count > 0; i++, count--)
                if (!bag.TryPlace(new LootDefinition(name, 1, 1, price, shape, kind), i % 5, i / 5)) throw new InvalidOperationException("Fixture bag placement failed.");
            if (!TownProgress.TryReceiveRun(bag, out string error)) throw new InvalidOperationException(error);
        }
    }

    public static void Run(Action<bool, string> check)
    {
        var original = TownProgress.Capture(); float multiplier = DungeonTuning.Active.lootValueMultiplier;
        try
        {
            DungeonTuning.Active.lootValueMultiplier = 1f; TownProgress.Reset();
            var bag = new GridInventory();
            bag.TryPlace(new LootDefinition("고블린 가죽", 1, 1, 40, LootShape.Hide, LootKinds.GoblinHide), 0, 0);
            check(TownProgress.TryReceiveRun(bag, out _) && TownProgress.Gold == 0 && TownProgress.WarehouseCount == 1
                && bag.Items.Count == 0, "return transfers ownership without automatically selling loot");
            TownProgress.TryReceiveRun(bag, out _);
            check(TownProgress.WarehouseCount == 1, "cleared expedition bag cannot duplicate recovered items");
            ReceiveItems(LootKinds.GoblinHide, "고블린 가죽", LootShape.Hide, 90, 2);
            ReceiveItems(LootKinds.OrcHide, "두꺼운 가죽", LootShape.Hide, 90, 1);
            check(TownProgress.GetWarehouseGroups().Count == 2 && TownProgress.MaterialCount(LootKinds.GoblinHide) == 3,
                "material IDs distinguish equal-shaped monster drops");
            var prices = TownProgress.GetStacks(LootKinds.GoblinHide);
            check(prices.Count == 2 && prices[0].unitPrice == 40 && prices[1].quantity == 2,
                "warehouse groups quantities while preserving individual price tiers");
            prices[0].quantity = 999;
            check(TownProgress.MaterialCount(LootKinds.GoblinHide) == 3, "warehouse views cannot mutate authoritative quantities");
            var cachePanel = UnityEngine.Object.FindAnyObjectByType<TownCommercePanel>();
            var groupsMethod = typeof(TownCommercePanel).GetMethod("WarehouseGroups", BindingFlags.Instance | BindingFlags.NonPublic);
            var stacksMethod = typeof(TownCommercePanel).GetMethod("WarehouseStacks", BindingFlags.Instance | BindingFlags.NonPublic);
            var cachedGroups = groupsMethod.Invoke(cachePanel, null);
            var cachedStacks = stacksMethod.Invoke(cachePanel, new object[] { LootKinds.GoblinHide });
            check(ReferenceEquals(cachedGroups, groupsMethod.Invoke(cachePanel, null))
                && ReferenceEquals(cachedStacks, stacksMethod.Invoke(cachePanel, new object[] { LootKinds.GoblinHide })),
                "unchanged warehouse UI reuses grouped and price-tier snapshots");
            TownProgress.SetSaleLock(LootKinds.GoblinHide, true);
            var lockedGroups = (List<WarehouseGroup>)groupsMethod.Invoke(cachePanel, null);
            check(!ReferenceEquals(cachedGroups, lockedGroups) && lockedGroups.Find(item => item.KindId == LootKinds.GoblinHide).SaleLocked,
                "warehouse change invalidates cached lock view");
            check(!TownProgress.TrySellOne(prices[0].id, out _) && TownProgress.Gold == 0 && TownProgress.WarehouseCount == 4,
                "sale lock prevents item and currency changes");
            TownProgress.SetSaleLock(LootKinds.GoblinHide, false);
            check(TownProgress.TrySellOne(prices[0].id, out _) && TownProgress.Gold == 40 && TownProgress.MaterialCount(LootKinds.GoblinHide) == 2,
                "selling removes exactly one chosen price-tier item");
            var soldStacks = (List<WarehouseStackData>)stacksMethod.Invoke(cachePanel, new object[] { LootKinds.GoblinHide });
            check(!ReferenceEquals(cachedStacks, soldStacks) && soldStacks.Count == 1 && soldStacks[0].quantity == 2,
                "sale refreshes cached quantities and removes depleted price tiers");
            check(!TownProgress.TrySellOne(prices[0].id, out _) && TownProgress.Gold == 40, "repeated sale of depleted stack cannot duplicate gold");
            ReceiveItems(LootKinds.GoblinHide, "고블린 가죽", LootShape.Hide, 30, 42);
            check(TownProgress.WarehouseCount == 45, "town warehouse exceeds expedition capacity without slot limits");

            TownProgress.Restore(new TownProgressData { gold = 2000 });
            ReceiveItems(LootKinds.GoblinHide, "고블린 가죽", LootShape.Hide, 40, 15);
            ReceiveItems(LootKinds.GoblinHide, "상급 고블린 가죽", LootShape.Hide, 100, 1);
            ReceiveItems(LootKinds.OrcHide, "두꺼운 가죽", LootShape.Hide, 80, 3);
            TownProgress.SetSaleLock(LootKinds.GoblinHide, true);
            var shortGold = TownProgress.Capture(); shortGold.gold = 299; TownProgress.Restore(shortGold);
            check(!TownProgress.TryUpgradeBag(out _) && TownProgress.Gold == 299 && TownProgress.WarehouseCount == 19 && TownProgress.BagLevel == 0,
                "insufficient gold leaves all upgrade materials unchanged");
            var enough = TownProgress.Capture(); enough.gold = 2000; TownProgress.Restore(enough);
            check(TownProgress.TryUpgradeBag(out _) && TownProgress.BagSize == new Vector2Int(6, 4) && TownProgress.Gold == 1700
                && TownProgress.MaterialCount(LootKinds.GoblinHide) == 11, "first bag upgrade consumes exact gold and materials");
            check(TownProgress.GetStacks(LootKinds.GoblinHide)[1].unitPrice == 100, "upgrade preserves expensive materials by consuming cheaper ones first");
            check(TownProgress.TryUpgradeBag(out _) && TownProgress.BagSize == new Vector2Int(6, 5) && TownProgress.Gold == 800
                && TownProgress.MaterialCount(LootKinds.GoblinHide) == 1 && TownProgress.MaterialCount(LootKinds.OrcHide) == 0,
                "second bag upgrade consumes both required material kinds");
            check(!TownProgress.TryUpgradeBag(out _) && TownProgress.Gold == 800, "maximum upgrade cannot charge again");
            var grown = new GridInventory(6, 5);
            check(grown.TryPlace(new LootDefinition("edge", 1, 1, 5), 5, 4) && !grown.TryPlace(new LootDefinition("outside", 1, 1, 5), 6, 4),
                "upgraded bag accepts new edge cell and rejects outside positions");
            var state = GameSession.Capture();
            GameSaveData saved = null;
            check(state.IsValid() && GameSaveService.TrySave(5, state, out _) && GameSaveService.TryLoad(5, out saved, out _)
                && saved.town.bagLevel == 2 && saved.town.warehouse[0].unitPrice == 100 && saved.town.saleLockedKinds.Contains(LootKinds.GoblinHide),
                "protected slot roundtrip preserves warehouse price locks and permanent bag level");
            TownProgress.Restore(saved.town);
            check(TownProgress.IsSaleLocked(LootKinds.GoblinHide) && TownProgress.WarehouseCount == 1, "restored warehouse retains sale protection");
            var bad = TownProgress.Capture(); bad.warehouse[0].quantity = -1;
            check(!bad.IsValid(), "negative stored quantity rejected");
            bad = TownProgress.Capture(); bad.warehouse.Add(bad.warehouse[0].Copy());
            check(!bad.IsValid(), "duplicate warehouse stack IDs rejected");
            bad = TownProgress.Capture(); bad.gold = int.MaxValue; TownProgress.Restore(bad);
            TownProgress.SetSaleLock(LootKinds.GoblinHide, false);
            check(!TownProgress.TrySellOne(TownProgress.GetStacks(LootKinds.GoblinHide)[0].id, out _)
                && TownProgress.WarehouseCount == 1, "gold overflow cannot consume sold item");

            TownProgress.Reset(); TownProgress.AcceptContract();
            check(!TownProgress.TrySubmitContract(out _) && TownProgress.HasAcceptedContract, "missing submission keeps quest active");
            ReceiveItems(TownProgress.ContractKindId, TownProgress.ContractTargetName, TownProgress.ContractTarget, 70, 1);
            check(TownProgress.Gold == 0 && TownProgress.HasAcceptedContract, "return does not automatically complete quest");
            check(TownProgress.TrySubmitContract(out _) && TownProgress.Gold == 120 && TownProgress.WarehouseCount == 0,
                "guild consumes submission once and awards contract gold");
            check(!TownProgress.TrySubmitContract(out _) && TownProgress.Gold == 120, "completed quest cannot award twice");
            ReceiveItems(LootKinds.GoblinHide, "고블린 가죽", LootShape.Hide, 40, 4);
            var lack = TownProgress.Capture(); lack.gold = 2000; TownProgress.Restore(lack);
            check(!TownProgress.TryUpgradeBag(out _) && TownProgress.Gold == 2000 && TownProgress.WarehouseCount == 4,
                "insufficient materials leave upgrade currency unchanged");
            TownProgress.FailRun();
            check(TownProgress.WarehouseCount == 4 && TownProgress.Gold == 2000, "death preserves already stored town loot and gold");

            var legacy = JsonUtility.FromJson<GameSaveData>("{\"version\":1,\"town\":{\"version\":1,\"gold\":47,\"contractTarget\":1},\"area\":0}");
            check(legacy.IsValid(), "earlier saves remain valid without warehouse fields");
            TownProgress.Restore(legacy.town);
            check(TownProgress.Gold == 47 && TownProgress.WarehouseCount == 0 && TownProgress.BagSize == new Vector2Int(5, 4),
                "legacy save restores currency with empty warehouse and base bag");
            var panel = UnityEngine.Object.FindAnyObjectByType<TownCommercePanel>();
            panel.Open(TownFacility.Warehouse);
            check(TownCommercePanel.IsOpen && Time.timeScale == 0f && GameShell.IsGameplayInputBlocked, "town facility pauses and blocks movement");
            panel.Close(); check(!TownCommercePanel.IsOpen && Time.timeScale == 1f, "closing town facility restores simulation");
        }
        finally { TownProgress.Restore(original); DungeonTuning.Active.lootValueMultiplier = multiplier; }
    }
}
