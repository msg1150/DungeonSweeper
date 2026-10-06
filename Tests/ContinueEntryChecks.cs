using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>Continue a town save, then enter a new run; preserve real dungeon resume separately.</summary>
public static class ContinueEntryChecks
{
    private static void Shell(string name, params object[] args) => typeof(GameShell).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic).Invoke(UnityEngine.Object.FindAnyObjectByType<GameShell>(), args);

    public static IEnumerator Run(Action<bool, string> check, Action timeout)
    {
        for (int trial = 0; trial < 3; trial++)
        {
            Shell("OnApplicationFocus", true); Shell("ReturnToMain"); yield return null;
            check(GameSession.StartNewGame(out _), "continue trial starts in town");
            while (GameSession.IsLoading) { timeout(); yield return null; }
            Shell("OnApplicationFocus", true); Shell("Resume");
            var loot = new LootDefinition("이어하기 재료", 2, 1, 90, LootShape.Hide, LootKinds.GoblinHide);
            TownProgress.Restore(new TownProgressData
            {
                gold = 333 + trial, supplyKits = 2, bagLevel = 1, hasAcceptedContract = true, nextWarehouseId = 2,
                warehouse = new List<WarehouseStackData> { new() { id = 1, quantity = 4, unitPrice = 90, loot = LootSaveData.Capture(loot) } },
                saleLockedKinds = new List<string> { LootKinds.GoblinHide }
            });
            GameSession.CompleteEvent("continue_entry_checkpoint");
            int slot = trial == 1 ? GameSaveService.AutosaveSlot : 7;
            if (trial == 2)
            {
                var saved = GameSession.Capture();
                saved.dungeon = new DungeonSaveData { layoutIndex = 0, health = 1,
                    playerPosition = DungeonLayoutFactory.CellCenter(18, 10), specialGate = DungeonLayoutFactory.CellCenter(18, 10) };
                check(GameSaveService.TrySave(slot, saved, out _), "old town save with stale empty dungeon payload remains compatible");
            }
            else check(GameSession.Save(slot, out _), "normal manual or automatic town checkpoint saves");
            Shell("ReturnToMain"); yield return null;
            check(GameSession.LoadSlot(slot, out _), "continue selects the stored town checkpoint");
            while (GameSession.IsLoading) { timeout(); yield return null; }
            Shell("OnApplicationFocus", true); Shell("Resume");
            var town = GameSession.Capture();
            check(town.area == SaveArea.Town && town.town.gold == 333 + trial && town.town.supplyKits == 2
                && town.town.bagLevel == 1 && town.town.hasAcceptedContract && town.town.warehouse.Count == 1
                && town.town.warehouse[0].quantity == 4 && town.town.saleLockedKinds.Contains(LootKinds.GoblinHide)
                && town.completedEvents.Contains("continue_entry_checkpoint"), "continue preserves town progress independently of dungeon restoration");
            var config = DungeonCatalog.Active.dungeons[(trial + 1) % 3];
            check(GameSession.EnterDungeon(out _, config), "continued town enters a selected new dungeon");
            while (GameSession.IsLoading) { timeout(); yield return null; }
            Shell("OnApplicationFocus", true); Shell("OpenPause");
            var run = DungeonRunController.Instance;
            check(run.PlayerHealth.Current == run.PlayerHealth.Maximum && Vector2.Distance(run.Player.position, DungeonLayoutFactory.Entrance) < .2f
                && run.CaptureSave().dungeonId == config.dungeonId && DungeonLayoutFactory.LayoutIndex == config.layoutIndex
                && run.Inventory.Columns == 6 && run.Inventory.Rows == 4, "continued town starts a full-health fresh run at its entrance with the saved bag upgrade");
            DungeonPrefabChecks.CheckLive(check);
            DungeonPopulationChecks.CheckLivePopulation(check);
            check(GameSession.ReturnToTown(out _), "continued run returns to town");
            while (GameSession.IsLoading) { timeout(); yield return null; }
            Shell("OnApplicationFocus", true); Shell("Resume");
            check(GameSession.EnterDungeon(out _, config), "continued town can enter again after returning");
            while (GameSession.IsLoading) { timeout(); yield return null; }
            Shell("OpenPause");
            check(DungeonRunController.Instance.PlayerHealth.Current == DungeonRunController.Instance.PlayerHealth.Maximum
                && DungeonRunController.Instance.CaptureSave().enemies.Count == config.ExpandMonsters().Count,
                "repeated entry does not reuse consumed restore state");
        }
    }
}
