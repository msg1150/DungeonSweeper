using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CorpsePrefab : MonoBehaviour
{
    public string prefabId = "corpse.custom", displayName = "시체";
    public bool useSpriteSheet = true;
    [Tooltip("Defeated 애니메이션을 가진 실제 3D 모델 프리팹.")]
    public GameObject model3D;
    public string spriteSheetResource = "Sprites/Monsters/corpse-sheet";
    [Min(1)] public int sheetColumns = 3, sheetRows = 1;
    [Min(0)] public int sheetIndex;
    [Min(.01f)] public float pixelsPerUnit = 180f;
    [Min(1)] public int requiredSuccesses = 3, maxFailures = 3;
    [Min(.01f)] public float pointerSpeed = .95f;
    [Range(.01f, 1f)] public float windowSize = .22f;
    public List<MonsterLootEntry> loot = new();

    public bool IsValid()
    {
        if (!LootKinds.ValidId(prefabId) || string.IsNullOrWhiteSpace(displayName) || GetComponentInChildren<SpriteRenderer>(true) == null
            || requiredSuccesses < 1 || maxFailures < 1 || !MonsterDefinition.Positive(pointerSpeed)
            || !MonsterDefinition.Positive(windowSize) || windowSize > 1f || loot == null) return false;
        if (useSpriteSheet && (sheetColumns < 1 || sheetColumns > 32 || sheetRows < 1 || sheetRows > 32
            || sheetIndex < 0 || sheetIndex >= sheetColumns * sheetRows || !MonsterDefinition.Positive(pixelsPerUnit)
            || Resources.Load<Texture2D>(spriteSheetResource) == null)) return false;
        if (!useSpriteSheet && GetComponentInChildren<SpriteRenderer>(true).sprite == null) return false;
        foreach (var entry in loot) if (entry == null || !entry.IsValid()) return false;
        return true;
    }
    public DismantleDifficulty Difficulty => new(requiredSuccesses, maxFailures, pointerSpeed, windowSize);
    public LootDefinition[] RollLoot()
    {
        var result = new List<LootDefinition>();
        foreach (var entry in loot) { var item = entry.Roll(); if (item != null) result.Add(item); }
        return result.ToArray();
    }
    public LootDefinition[] ResolveLoot(CorpseSaveData saved, CorpseLootPlan plan = null)
    {
        // 저장된 확정 전리품은 현재 테이블과 무관하게 복원한다. 불러오기에서 난수를 소비하지 않는다.
        if (saved == null)
        {
            if (plan != null && plan.prefabId != prefabId) throw new System.ArgumentException("Loot plan belongs to another corpse prefab.");
            return plan != null ? plan.Roll() : RollLoot();
        }
        var items = new List<LootDefinition>();
        foreach (var item in saved.loot) items.Add(item.Restore());
        return items.ToArray();
    }

    public CorpseRunData Spawn(Vector2 position, CorpseSaveData saved = null, CorpseLootPlan lootPlan = null)
    {
        CorpsePrefab actor = DungeonActorPool.Rent(this, position);
        string label = saved?.name ?? displayName;
        actor.name = label;
        if (useSpriteSheet)
        {
            var frames = CasualArtLibrary.LoadSheet(spriteSheetResource, sheetColumns, sheetRows, pixelsPerUnit, true);
            actor.GetComponentInChildren<SpriteRenderer>(true).sprite = frames.Length > sheetIndex ? frames[sheetIndex] : CasualArtLibrary.WhiteSprite;
        }
        var difficulty = saved == null ? Difficulty : new DismantleDifficulty(saved.requiredSuccesses, saved.maxFailures, saved.pointerSpeed, saved.windowSize);
        LootDefinition[] items = ResolveLoot(saved, lootPlan);
        actor.gameObject.SetActive(true);
        if (ThreeDWorldView.Current != null) ThreeDActorView.Attach(actor.gameObject, sheetIndex == 1 ? "slime" : sheetIndex == 2 ? "orc" : "goblin", true, model3D);
        // 시각 오브젝트만 재사용한다. 해체 세션과 확정 전리품은 매번 새 런 데이터에 보관한다.
        return new CorpseRunData(actor.gameObject, label, difficulty, items) { PrefabId = prefabId, MonsterIndex = saved?.monsterIndex ?? sheetIndex };
    }
}
