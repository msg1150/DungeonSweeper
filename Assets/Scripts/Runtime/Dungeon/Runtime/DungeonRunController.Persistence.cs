using System.Collections.Generic;
using UnityEngine;

/// <summary>탐험 스냅샷의 캡처와 배우·가방 복원만 담당한다.</summary>
public sealed partial class DungeonRunController
{
    public DungeonSaveData CaptureSave()
    {
        DungeonSaveData data = new()
        {
            dungeonId = activeDungeonId, layoutIndex = DungeonLayoutFactory.LayoutIndex, health = playerHealth.Current,
            bagWidth = inventory.Columns, bagHeight = inventory.Rows,
            playerPosition = IsPlayerHidden ? hidingPosition : (Vector2)player.position, specialGate = specialGate,
            playerHidden = IsPlayerHidden, hidingCell = hidingCell,
            activeCorpseIndex = activeCorpse == null ? -1 : corpses.IndexOf(activeCorpse),
            lootPlacementOpen = lootPlacementOpen, inventory = inventory.Capture(), totalValue = inventory.TotalValue,
            discoveredCells = new List<Vector2Int>(discoveredCells), motion = movement.Capture(),
            invulnerabilitySeconds = playerHealth.RemainingInvulnerability
        };
        foreach (CorpseRunData corpse in corpses)
        {
            CorpseSaveData state = new()
            {
                prefabId = corpse.PrefabId, name = corpse.Name, monsterIndex = corpse.MonsterIndex, position = corpse.Visual.transform.position,
                requiredSuccesses = corpse.Difficulty.RequiredSuccesses, maxFailures = corpse.Difficulty.MaxFailures,
                pointerSpeed = corpse.Difficulty.PointerSpeed, windowSize = corpse.Difficulty.SuccessWindowSize,
                processed = corpse.IsProcessed, session = corpse.CaptureSession()
            };
            foreach (LootDefinition loot in corpse.Loot) state.loot.Add(LootSaveData.Capture(loot));
            data.corpses.Add(state);
        }
        foreach (EnemyAgent enemy in enemies) if (enemy != null) data.enemies.Add(enemy.Capture());
        foreach (LootDefinition loot in pendingLoot) data.pendingLoot.Add(LootSaveData.Capture(loot));
        if (expedition != null)
        {
            // 먼저 독립 사본을 만든다. 반환할 현재 층 목록을 실제 탐험에 연결하면
            // 저장 호출자의 편집이 내부 층 상태까지 바꾸게 된다.
            data.expedition = expedition.Copy();
            var current = data.expedition.Current;
            current.visited = true; current.corpses = data.corpses; current.enemies = data.enemies;
            current.discoveredCells = data.discoveredCells; current.specialGate = specialGate;
            data.mapSnapshot = current.map;
            data.hasMapSnapshot = true; data.expeditionVersion = 1;
        }
        return data;
    }

    private void RestoreWorld(DungeonSaveData data)
    {
        DungeonWorldFactory factory = new();
        foreach (CorpseSaveData state in data.corpses)
        {
            CorpseRunData corpse = !string.IsNullOrEmpty(state.prefabId)
                ? DungeonCatalog.Active.FindCorpse(state.prefabId).Spawn(state.position, state)
                : factory.CreateCorpse(state.name, state.position,
                new DismantleDifficulty(state.requiredSuccesses, state.maxFailures, state.pointerSpeed, state.windowSize),
                state.monsterIndex, RestoreLegacyLoot(state));
            corpse.RestoreSession(state.session);
            if (state.processed) corpse.MarkProcessed();
            corpses.Add(corpse);
        }
        foreach (EnemySaveData state in data.enemies)
        {
            EnemyAgent enemy = !string.IsNullOrEmpty(state.prefabId)
                ? DungeonCatalog.Active.FindMonster(state.prefabId).Spawn(player, state.position, state.roamTarget, state.definition)
                : factory.CreateEnemy(state.definition, player, state.position, state.roamTarget);
            enemy.Restore(state);
            enemies.Add(enemy);
        }
        specialGate = data.specialGate;
        CreatePortals(factory);
        RestorePlayerAndBag(data);
    }

    private void RestorePlayerAndBag(DungeonSaveData data)
    {
        inventory.Restore(data.inventory, data.totalValue);
        foreach (LootSaveData loot in data.pendingLoot) pendingLoot.Add(loot.Restore());
        player.position = data.playerPosition;
        if (playerBody != null) { playerBody.position = data.playerPosition; playerBody.linearVelocity = Vector2.zero; }
        playerHealth.Restore(data.health, data.invulnerabilitySeconds);
        movement.Restore(data.motion);
        if (data.discoveredCells != null) foreach (Vector2Int cell in data.discoveredCells) discoveredCells.Add(cell);
        if (data.activeCorpseIndex >= 0)
        {
            activeCorpse = corpses[data.activeCorpseIndex];
            dismantleSession = activeCorpse.Session;
            movement.SetMovementEnabled(false);
        }
        if (data.lootPlacementOpen) OpenLootPlacement();
        if (data.playerHidden) { hidingCell = data.hidingCell; SetPlayerHidden(true); }
    }

    private static LootDefinition[] RestoreLegacyLoot(CorpseSaveData state)
    {
        var loot = new LootDefinition[state.loot.Count];
        for (int i = 0; i < loot.Length; i++) loot[i] = state.loot[i].Restore();
        return loot;
    }
}
