using System;
using System.Collections.Generic;
using UnityEngine;

public static class LootKinds
{
    public const string GoblinHide = "goblin.hide", OrcHide = "orc.hide";
    public static string LegacyId(LootShape shape) => shape switch
    {
        LootShape.Tooth => "goblin.tooth", LootShape.Dagger => "goblin.dagger",
        LootShape.Gel => "slime.gel", LootShape.Core => "slime.core",
        LootShape.Hide => OrcHide, LootShape.Horn => "orc.horn", _ => "unknown"
    };
    public static bool ValidId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 96) return false;
        foreach (char c in id) if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '.' && c != '_' && c != '-') return false;
        return true;
    }
}

[Serializable]
public sealed class MaterialRequirement
{
    public string kindId, displayName;
    [Min(1)] public int quantity = 1;
}

[Serializable]
public sealed class BagUpgradeDefinition
{
    public string displayName;
    public int width, height;
    [Min(0)] public int goldCost;
    public List<MaterialRequirement> materials = new();
    public bool IsValid()
    {
        if (width < GridInventory.Width || height < GridInventory.Height || width > GridInventory.MaximumWidth
            || height > GridInventory.MaximumHeight || goldCost < 0 || materials == null || materials.Count == 0) return false;
        var ids = new HashSet<string>();
        foreach (var material in materials)
            if (material == null || !LootKinds.ValidId(material.kindId) || material.quantity <= 0 || !ids.Add(material.kindId)) return false;
        return true;
    }
}

[CreateAssetMenu(fileName = "TownEconomyConfig", menuName = "Dungeon Sweeper/Town Economy Config")]
public sealed class TownEconomyConfig : ScriptableObject
{
    public List<BagUpgradeDefinition> bagUpgrades = new()
    {
        new() { displayName = "확장 작업 가방", width = 6, height = 4, goldCost = 300,
            materials = new() { new() { kindId = LootKinds.GoblinHide, displayName = "고블린 가죽", quantity = 5 } } },
        new() { displayName = "대형 작업 가방", width = 6, height = 5, goldCost = 900,
            materials = new() { new() { kindId = LootKinds.GoblinHide, displayName = "고블린 가죽", quantity = 10 },
                new() { kindId = LootKinds.OrcHide, displayName = "두꺼운 가죽", quantity = 3 } } }
    };
    private static TownEconomyConfig active;
    public static TownEconomyConfig Active
    {
        get { if (active == null) active = Resources.Load<TownEconomyConfig>("TownEconomyConfig");
            if (active == null) active = CreateInstance<TownEconomyConfig>(); return active; }
    }
    public Vector2Int BagSize(int level) => level <= 0 ? new(GridInventory.Width, GridInventory.Height)
        : new(bagUpgrades[level - 1].width, bagUpgrades[level - 1].height);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => active = null;
}
