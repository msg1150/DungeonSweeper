using System.Collections.Generic;
using UnityEngine;

/// <summary>배회/추적/공격 상태와 타격 시점만 담당한다.</summary>
public class EnemyAgent : MonoBehaviour
{
    private Transform player;
    private Vector2 roamTarget;
    private float attackCooldown;
    private float attackTimer;
    private bool damageApplied;
    private MonsterDefinition definition;
    private MonsterVisualAnimator visual;
    private PlayerHealth playerHealth;
    private float patrolPauseRemaining;
    private Vector2 previousPatrolOrigin;
    private bool hasPreviousPatrolOrigin;
    private Vector2 investigationTarget;
    private float investigationRemaining;
    private bool wasChasing;
    private bool wasInvestigating;
    private readonly List<Vector2> path = new();
    private int pathIndex, pathMode = -1;
    private Vector2Int pathTargetCell;
    public string PrefabId { get; set; }
    private float DetectionRange => definition.hasMovementStats ? definition.detectionRange : DungeonTuning.Active.detectionRange;
    private float TravelDistance => definition.hasMovementStats ? definition.patrolTravelDistance : DungeonTuning.Active.patrolTravelDistance;

    public void HearNoise(Vector2 noisePosition) => HearNoise(noisePosition,
        definition.hasMovementStats ? definition.hearingRange : DungeonTuning.Active.hearingRange);

    public void Initialize(Transform target, Vector2 a, Vector2 b, MonsterDefinition data, MonsterVisualAnimator animator)
    {
        player = target;
        playerHealth = target.GetComponent<PlayerHealth>();
        definition = data;
        visual = animator;
        roamTarget = b;
        previousPatrolOrigin = a;
        hasPreviousPatrolOrigin = true;
        patrolPauseRemaining = 0f;
        pathMode = -1;
    }

    /// <summary>해체 실패 같은 소리를 들으면 마지막 소리 위치를 조사한다.</summary>
    public void HearNoise(Vector2 noisePosition, float radius)
    {
        if (!GameSaveData.Finite(noisePosition) || float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f
            || Vector2.Distance(transform.position, noisePosition) > radius) return;
        Vector2Int noiseCell = DungeonLayoutFactory.WorldToCell(noisePosition);
        investigationTarget = DungeonLayoutFactory.IsWalkablePosition(noisePosition) ? noisePosition : DungeonLayoutFactory.CellCenter(noiseCell.x, noiseCell.y);
        investigationRemaining = definition.hasMovementStats ? definition.investigationSeconds : DungeonTuning.Active.investigationSeconds;
        pathMode = -1;
    }

    private void Update()
    {
        if (player == null || definition == null || DungeonRunController.Instance == null
            || !DungeonRunController.Instance.IsRunActive || GameShell.IsGameplayInputBlocked || Time.timeScale == 0f) return;
        Tick(Time.deltaTime);
    }

    private void Tick(float elapsed)
    {
        if (attackTimer > 0f)
        {
            attackTimer -= elapsed;
            float progress = 1f - attackTimer / definition.attackAnimationSeconds;
            visual.Tick(true, progress, false, player.position - transform.position);
            if (!damageApplied && progress >= .5f)
            {
                damageApplied = true;
                if (Vector2.Distance(transform.position, player.position) <= definition.attackRange + .25f && HasClearSight(player.position))
                    playerHealth.TakeDamage(definition.attackDamage);
            }
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);
        bool isChasing = distance < DetectionRange && HasClearSight(player.position);
        bool isInvestigating = !isChasing && investigationRemaining > 0f;
        if (isChasing) investigationRemaining = Mathf.Max(0f, investigationRemaining - elapsed);
        if (!isChasing && !isInvestigating)
        {
            if (wasChasing || wasInvestigating)
            {
                SelectPatrolDestination();
            }
            else if (Vector2.Distance(transform.position, roamTarget) < .18f)
            {
                if (patrolPauseRemaining > 0f) patrolPauseRemaining = Mathf.Max(0f, patrolPauseRemaining - elapsed);
                if (patrolPauseRemaining <= 0f) SelectPatrolDestination();
            }
        }

        Vector2 target = isChasing ? player.position : isInvestigating ? investigationTarget : roamTarget;
        float speed = definition.hasMovementStats ? (isChasing ? definition.chaseSpeed : definition.patrolSpeed)
            : (isChasing ? DungeonTuning.Active.chaseSpeed : DungeonTuning.Active.patrolSpeed);
        Vector2 before = transform.position;
        bool arrivedBefore = Vector2.Distance(before, target) < .18f;
        if (!arrivedBefore) FollowPath(target, speed * elapsed, isChasing ? 2 : isInvestigating ? 1 : 0);
        bool arrived = Vector2.Distance(transform.position, target) < .18f;
        if (!isChasing && !isInvestigating && !arrivedBefore && arrived)
            patrolPauseRemaining = definition.hasMovementStats ? definition.patrolArrivalPause : DungeonTuning.Active.patrolArrivalPause;
        if (isInvestigating && arrived) investigationRemaining = Mathf.Max(0f, investigationRemaining - elapsed);
        Vector2 velocity = (Vector2)transform.position - before;
        visual.Tick(false, 0f, velocity.sqrMagnitude > .00001f, velocity);
        wasChasing = isChasing;
        wasInvestigating = isInvestigating;

        if (attackCooldown > 0f) attackCooldown -= elapsed;
        if (isChasing && distance <= definition.attackRange && attackCooldown <= 0f)
        {
            attackCooldown = definition.attackCooldown;
            attackTimer = definition.attackAnimationSeconds;
            damageApplied = false;
        }
    }

    private bool HasClearSight(Vector2 targetPosition)
    {
        Vector2 origin = transform.position;
        Vector2 offset = targetPosition - origin;
        float distance = offset.magnitude;
        if (distance <= .01f) return true;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, offset / distance, distance);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null) continue;
            if (hit.collider.GetComponentInParent<EnemyAgent>() != null) continue;
            if (hit.collider.GetComponentInParent<PlayerMovement>() != null) continue;
            return false; // 활성화된 레이아웃 벽이 먼저 닿으면 시야가 막힌다.
        }
        return true;
    }

    private void SelectPatrolDestination()
    {
        Vector2 origin = transform.position;
        roamTarget = DungeonLayoutFactory.RandomRoamPosition(origin, hasPreviousPatrolOrigin ? previousPatrolOrigin : null, TravelDistance);
        previousPatrolOrigin = origin;
        hasPreviousPatrolOrigin = true;
        patrolPauseRemaining = 0f;
        pathMode = -1;
    }

    private void FollowPath(Vector2 target, float distance, int mode)
    {
        Vector2Int cell = DungeonLayoutFactory.WorldToCell(target);
        if (pathMode != mode || pathTargetCell != cell || pathIndex >= path.Count)
        {
            if (!DungeonLayoutFactory.TryBuildPath(transform.position, target, path)) return;
            pathTargetCell = cell; pathMode = mode; pathIndex = 0;
        }
        else if (DungeonLayoutFactory.IsWalkablePosition(target)) path[path.Count - 1] = target;
        while (distance > 0f && pathIndex < path.Count)
        {
            Vector2 before = transform.position;
            Vector2 next = Vector2.MoveTowards(before, path[pathIndex], distance);
            distance -= Vector2.Distance(before, next);
            transform.position = next;
            if (Vector2.Distance(next, path[pathIndex]) > .001f) break;
            pathIndex++;
        }
    }

    public EnemySaveData Capture() => new()
    {
        prefabId = PrefabId, definition = definition, position = transform.position, roamTarget = roamTarget,
        investigationTarget = investigationTarget, attackCooldown = attackCooldown, attackTimer = attackTimer,
        roamSeconds = patrolPauseRemaining, investigationSeconds = investigationRemaining,
        damageApplied = damageApplied, wasChasing = wasChasing, wasInvestigating = wasInvestigating,
        patrolStateVersion = 1, previousPatrolOrigin = previousPatrolOrigin, hasPreviousPatrolOrigin = hasPreviousPatrolOrigin
    };

    public void Restore(EnemySaveData state)
    {
        transform.position = state.position;
        roamTarget = state.roamTarget;
        investigationTarget = state.investigationTarget;
        attackCooldown = Mathf.Max(0f, state.attackCooldown);
        attackTimer = Mathf.Max(0f, state.attackTimer);
        patrolPauseRemaining = state.patrolStateVersion == 1 ? Mathf.Max(0f, state.roamSeconds) : 0f;
        investigationRemaining = Mathf.Max(0f, state.investigationSeconds);
        damageApplied = state.damageApplied;
        wasChasing = state.wasChasing;
        wasInvestigating = state.wasInvestigating;
        previousPatrolOrigin = state.previousPatrolOrigin;
        hasPreviousPatrolOrigin = state.hasPreviousPatrolOrigin;
        pathMode = -1;
    }

}
