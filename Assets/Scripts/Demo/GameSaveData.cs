using System;
using System.Collections.Generic;
using UnityEngine;

public enum SaveArea { Town, Dungeon }

[Serializable]
public sealed class TownProgressData
{
    public int version = 1;
    public int gold, supplyKits, lastRunGold, lastContractBonus;
    public bool hasAcceptedContract;
    public LootShape contractTarget = LootShape.Dagger;
    public int bagLevel, lastRecoveredCount, lastRecoveredValue, nextWarehouseId = 1;
    public List<WarehouseStackData> warehouse = new();
    public List<string> saleLockedKinds = new();

    public bool IsValid() => version == 1 && gold >= 0 && supplyKits >= 0 && lastRunGold >= 0
        && lastContractBonus >= 0 && lastContractBonus <= 120 && lastContractBonus <= lastRunGold
        && (contractTarget == LootShape.Dagger || contractTarget == LootShape.Core
            || contractTarget == LootShape.Hide || contractTarget == LootShape.Horn)
        && bagLevel >= 0 && bagLevel <= TownEconomyConfig.Active.bagUpgrades.Count && lastRecoveredCount >= 0
        && lastRecoveredValue >= 0 && ValidWarehouse();

    private bool ValidWarehouse()
    {
        if (saleLockedKinds != null)
        {
            var kinds = new HashSet<string>();
            foreach (string kind in saleLockedKinds) if (!LootKinds.ValidId(kind) || !kinds.Add(kind)) return false;
        }
        if (warehouse == null || warehouse.Count == 0) return nextWarehouseId >= 0;
        var ids = new HashSet<int>();
        foreach (WarehouseStackData stack in warehouse)
            if (stack == null || stack.id < 1 || stack.id >= nextWarehouseId || !ids.Add(stack.id)
                || stack.quantity <= 0 || stack.unitPrice < 0 || stack.loot == null || !stack.loot.IsValid()) return false;
        return true;
    }
}

[Serializable]
public sealed class WarehouseStackData
{
    public int id, quantity, unitPrice;
    public bool saleLocked;
    public LootSaveData loot;
    public WarehouseStackData Copy() => new() { id = id, quantity = quantity, unitPrice = unitPrice,
        saleLocked = saleLocked, loot = LootSaveData.Capture(loot.Restore()) };
}

[Serializable]
public sealed class GameSaveData
{
    public int version = 1;
    public string savedAtUtc;
    public double playSeconds;
    public SaveArea area;
    public TownProgressData town = new();
    public bool hasTownPosition;
    public Vector2 townPosition;
    public List<string> completedEvents = new();
    public DungeonSaveData dungeon;

    public bool IsValid()
    {
        if (version != 1 || town == null || !town.IsValid() || double.IsNaN(playSeconds) || double.IsInfinity(playSeconds)
            || playSeconds < 0 || !Enum.IsDefined(typeof(SaveArea), area)) return false;
        if (hasTownPosition && !Finite(townPosition)) return false;
        if (area != SaveArea.Dungeon) return true;
        if (dungeon == null || !dungeon.IsValid()) return false;
        Vector2Int size = TownEconomyConfig.Active.BagSize(town.bagLevel);
        return dungeon.ResolvedWidth == size.x && dungeon.ResolvedHeight == size.y;
    }

    public static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
        && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
}

[Serializable]
public sealed class LootSaveData
{
    public string kindId;
    public string name;
    public int value;
    public LootShape shape;
    public Vector2Int[] cells;
    public static LootSaveData Capture(LootDefinition loot)
    {
        Vector2Int[] cells = new Vector2Int[loot.OccupiedCells.Count];
        for (int i = 0; i < cells.Length; i++) cells[i] = loot.OccupiedCells[i];
        return new LootSaveData { kindId = loot.KindId, name = loot.Name, value = loot.Value, shape = loot.Shape, cells = cells };
    }
    public bool IsValid()
    {
        if (cells == null || cells.Length == 0 || cells.Length > 1024 || value < 0
            || !Enum.IsDefined(typeof(LootShape), shape) || (!string.IsNullOrEmpty(kindId) && !LootKinds.ValidId(kindId))) return false;
        HashSet<Vector2Int> unique = new();
        foreach (Vector2Int cell in cells)
            if (cell.x < 0 || cell.y < 0 || cell.x > 100 || cell.y > 100 || !unique.Add(cell)) return false;
        return true;
    }
    public LootDefinition Restore() => LootDefinition.CreateIdentified(kindId, name, value, shape, cells);
}

[Serializable]
public sealed class StoredLootSaveData
{
    public LootSaveData loot;
    public Vector2Int position;
    public bool hasSaleValue;
    public int saleValue;
}

[Serializable]
public sealed class DismantleSaveData
{
    public int successes, failures;
    public float pointer, windowStart, direction;
}

[Serializable]
public sealed class CorpseSaveData
{
    public string name;
    public int monsterIndex, requiredSuccesses, maxFailures;
    public float pointerSpeed, windowSize;
    public Vector2 position;
    public bool processed;
    public List<LootSaveData> loot = new();
    public DismantleSaveData session;
}

[Serializable]
public sealed class EnemySaveData
{
    public MonsterDefinition definition;
    public Vector2 position, roamTarget, investigationTarget;
    public float attackCooldown, attackTimer, roamSeconds, investigationSeconds;
    public bool damageApplied, wasChasing;
}

[Serializable]
public sealed class PlayerMotionSaveData
{
    public float dashSeconds, cooldownSeconds;
    public bool isDashing;
    public Vector2 dashDirection, lastDirection = Vector2.down;

    public bool IsValid() => !float.IsNaN(dashSeconds) && !float.IsInfinity(dashSeconds) && dashSeconds >= 0f
        && !float.IsNaN(cooldownSeconds) && !float.IsInfinity(cooldownSeconds) && cooldownSeconds >= 0f
        && GameSaveData.Finite(dashDirection) && GameSaveData.Finite(lastDirection)
        && lastDirection.sqrMagnitude > .01f && lastDirection.sqrMagnitude <= 1.01f
        && (!isDashing || (dashSeconds > 0f && dashDirection.sqrMagnitude > .01f && dashDirection.sqrMagnitude <= 1.01f));
}

[Serializable]
public sealed class DungeonSaveData
{
    public int bagWidth, bagHeight;
    public int ResolvedWidth => bagWidth == 0 ? GridInventory.Width : bagWidth;
    public int ResolvedHeight => bagHeight == 0 ? GridInventory.Height : bagHeight;
    public int layoutIndex, health, totalValue;
    public Vector2 playerPosition, specialGate;
    public int activeCorpseIndex = -1;
    public bool lootPlacementOpen;
    public List<CorpseSaveData> corpses = new();
    public List<EnemySaveData> enemies = new();
    public List<StoredLootSaveData> inventory = new();
    public List<LootSaveData> pendingLoot = new();
    public List<Vector2Int> discoveredCells = new();
    public PlayerMotionSaveData motion;
    public float invulnerabilitySeconds;

    public bool IsValid()
    {
        if (layoutIndex < 0 || layoutIndex > 2 || health <= 0 || totalValue < 0
            || !GameSaveData.Finite(playerPosition) || !GameSaveData.Finite(specialGate)
            || corpses == null || enemies == null || inventory == null || pendingLoot == null
            || activeCorpseIndex < -1 || activeCorpseIndex >= corpses.Count
            || !Finite(invulnerabilitySeconds) || invulnerabilitySeconds < 0f
            || (motion != null && !motion.IsValid())) return false;
        if (discoveredCells != null)
        {
            if (discoveredCells.Count > DungeonLayoutFactory.Width * DungeonLayoutFactory.Height) return false;
            HashSet<Vector2Int> unique = new();
            foreach (Vector2Int cell in discoveredCells)
                if (cell.x < 0 || cell.y < 0 || cell.x >= DungeonLayoutFactory.Width
                    || cell.y >= DungeonLayoutFactory.Height || !unique.Add(cell)) return false;
        }
        if ((bagWidth == 0) != (bagHeight == 0) || ResolvedWidth < GridInventory.Width || ResolvedHeight < GridInventory.Height
            || ResolvedWidth > GridInventory.MaximumWidth || ResolvedHeight > GridInventory.MaximumHeight) return false;
        bool[,] occupied = new bool[ResolvedWidth, ResolvedHeight];
        long priceSum = 0; bool priced = true;
        foreach (StoredLootSaveData item in inventory)
        {
            if (item?.loot == null || !item.loot.IsValid() || (item.hasSaleValue && item.saleValue < 0)) return false;
            priced &= item.hasSaleValue; priceSum += item.hasSaleValue ? item.saleValue : 0;
            foreach (Vector2Int cell in item.loot.Restore().OccupiedCells)
            {
                int x = item.position.x + cell.x, y = item.position.y + cell.y;
                if (x < 0 || y < 0 || x >= ResolvedWidth || y >= ResolvedHeight || occupied[x, y]) return false;
                occupied[x, y] = true;
            }
        }
        if (priced && totalValue != Math.Min(int.MaxValue, priceSum)) return false;
        foreach (LootSaveData loot in pendingLoot) if (loot == null || !loot.IsValid()) return false;
        foreach (CorpseSaveData corpse in corpses)
        {
            if (corpse == null || !GameSaveData.Finite(corpse.position) || corpse.loot == null
                || corpse.requiredSuccesses < 1 || corpse.maxFailures < 1
                || !Finite(corpse.pointerSpeed) || corpse.pointerSpeed <= 0
                || !Finite(corpse.windowSize) || corpse.windowSize <= 0 || corpse.windowSize > 1) return false;
            foreach (LootSaveData loot in corpse.loot) if (loot == null || !loot.IsValid()) return false;
            DismantleSaveData session = corpse.session;
            if (session != null && (session.successes < 0 || session.failures < 0
                || session.successes > corpse.requiredSuccesses || session.failures > corpse.maxFailures
                || !Finite(session.pointer) || !Finite(session.windowStart) || !Finite(session.direction))) return false;
        }
        if (activeCorpseIndex >= 0)
        {
            CorpseSaveData active = corpses[activeCorpseIndex];
            if (active.processed || active.session == null || active.session.successes >= active.requiredSuccesses
                || active.session.failures >= active.maxFailures || lootPlacementOpen) return false;
        }
        if (lootPlacementOpen != (pendingLoot.Count > 0)) return false;
        foreach (EnemySaveData enemy in enemies)
            if (enemy?.definition == null || !GameSaveData.Finite(enemy.position) || !GameSaveData.Finite(enemy.roamTarget)
                || !GameSaveData.Finite(enemy.investigationTarget) || !Finite(enemy.attackCooldown)
                || !Finite(enemy.attackTimer) || !Finite(enemy.roamSeconds) || !Finite(enemy.investigationSeconds)
                || string.IsNullOrWhiteSpace(enemy.definition.spriteSheetResource)
                || !Finite(enemy.definition.attackAnimationSeconds) || enemy.definition.attackAnimationSeconds <= 0f
                || !Finite(enemy.definition.attackRange) || enemy.definition.attackRange <= 0f
                || !Finite(enemy.definition.attackCooldown) || enemy.definition.attackCooldown < 0f) return false;
        return true;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
