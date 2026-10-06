using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAgent))]
public sealed class MonsterPrefab : MonoBehaviour
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
        MonsterPrefab actor = Instantiate(this, origin, Quaternion.identity);
        actor.name = "배회 " + (savedStats ?? stats).displayName;
        actor.stats = JsonUtility.FromJson<MonsterDefinition>(JsonUtility.ToJson(savedStats ?? stats));
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
}
