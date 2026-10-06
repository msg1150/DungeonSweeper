using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAgent))]
public sealed class MonsterPrefab : MonoBehaviour, IDungeonPoolResettable
{
    public string prefabId = "monster.custom";
    public bool useSpriteSheetAnimation = true;
    public MonsterDefinition stats = new() { hasMovementStats = true };
    public float PatrolDistance => stats.hasMovementStats ? stats.patrolTravelDistance : DungeonTuning.Active.patrolTravelDistance;
    public float DetectionRange => stats.hasMovementStats ? stats.detectionRange : DungeonTuning.Active.detectionRange;

    public bool IsValid() => LootKinds.ValidId(prefabId) && stats != null && stats.hasMovementStats && stats.IsValid()
        && GetComponentInChildren<SpriteRenderer>(true) != null && GetComponent<EnemyAgent>() != null
        && (useSpriteSheetAnimation ? Resources.Load<Texture2D>(stats.spriteSheetResource) != null
            : GetComponentInChildren<SpriteRenderer>(true).sprite != null);

    public EnemyAgent Spawn(Transform player, Vector2 origin, Vector2 target, MonsterDefinition savedStats = null)
    {
        MonsterPrefab actor = DungeonActorPool.Rent(this, origin);
        actor.prefabId = prefabId;
        actor.useSpriteSheetAnimation = useSpriteSheetAnimation;
        actor.name = "배회 " + (savedStats ?? stats).displayName;
        // 데이터 에셋이나 저장 스냅샷을 런타임 개체가 변경하지 않도록 독립적인 복사본을 사용한다.
        actor.stats = (savedStats ?? stats).Copy();
        var renderer = actor.GetComponentInChildren<SpriteRenderer>(true);
        var visual = actor.GetComponent<MonsterVisualAnimator>();
        if (visual == null) visual = actor.gameObject.AddComponent<MonsterVisualAnimator>();
        if (actor.useSpriteSheetAnimation) visual.Initialize(actor.stats.spriteSheetResource, renderer);
        var agent = actor.GetComponent<EnemyAgent>();
        agent.Initialize(player, origin, target, actor.stats, visual);
        agent.PrefabId = prefabId;
        actor.gameObject.SetActive(true);
        return agent;
    }
    public void ResetForPool() => stats = null;
}
