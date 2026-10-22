using System.Collections.Generic;
using UnityEngine;

/// <summary>Spread recovery targets across the map and keep initial enemies away from corpses.</summary>
public sealed class DungeonPopulationPlan
{
    public readonly List<Vector2> Corpses = new(), Enemies = new(), PatrolTargets = new();
    public Vector2 Gate;

    public static DungeonPopulationPlan Create(int count)
        => Create(count, count, null);

    public static DungeonPopulationPlan Create(int corpseCount, IReadOnlyList<MonsterPrefab> monsters)
        => Create(corpseCount, monsters.Count, monsters);

    private static DungeonPopulationPlan Create(int corpseCount, int monsterCount, IReadOnlyList<MonsterPrefab> monsters)
    {
        var plan = new DungeonPopulationPlan();
        for (int i = 0; i < corpseCount; i++)
            plan.Corpses.Add(DungeonLayoutFactory.PickSpawnPosition(i == 0 ? 3f : 9f, i == 0 ? 7f : float.PositiveInfinity, plan.Corpses, 4.5f));
        var occupied = new List<Vector2>(plan.Corpses);
        for (int i = 0; i < monsterCount; i++)
        {
            float clearance = Mathf.Max(4.5f, (monsters == null ? DungeonTuning.Active.detectionRange : monsters[i].DetectionRange) + 1f);
            Vector2 spawn = DungeonLayoutFactory.PickSpawnPosition(6f, float.PositiveInfinity, occupied, clearance);
            plan.Enemies.Add(spawn); occupied.Add(spawn);
            plan.PatrolTargets.Add(DungeonLayoutFactory.RandomRoamPosition(spawn, null, monsters == null ? null : monsters[i].PatrolDistance));
        }
        plan.Gate = DungeonLayoutFactory.PickSpawnPosition(16f, float.PositiveInfinity, occupied, 3f);
        return plan;
    }
}
