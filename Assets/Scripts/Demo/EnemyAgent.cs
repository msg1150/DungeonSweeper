using System.Collections.Generic;
using UnityEngine;

/// <summary>순찰·추격·마지막 목격 지점 추적·소음 조사와 공격을 담당한다.</summary>
public class EnemyAgent : MonoBehaviour, IDungeonPoolResettable
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
    private float noiseAlertRemaining;
    private bool wasChasing;
    private bool wasInvestigating;
    private Vector2 lastSeenPlayerPosition;
    private bool hasLastSeenPlayerPosition;
    private readonly List<Vector2> path = new();
    private readonly List<RaycastHit2D> sightHits = new(16);
    private int pathIndex, pathMode = -1;
    private Vector2Int pathTargetCell;
    public string PrefabId { get; set; }
    public bool IsAttacking => attackTimer > 0f;
    public float AttackProgress => definition == null ? 0f : Mathf.Clamp01(1f - attackTimer / Mathf.Max(.01f, definition.attackAnimationSeconds));
    public Vector2 AttackDirection => player != null ? (Vector2)(player.position - transform.position) : Vector2.down;
    public string DisplayName => definition?.displayName ?? name;
    public float BaseDetectionRange => definition == null ? 0f : definition.hasMovementStats ? definition.detectionRange : DungeonTuning.Active.detectionRange;
    public float HearingRange => definition == null ? 0f : definition.hasMovementStats ? definition.hearingRange : DungeonTuning.Active.hearingRange;
    public float NoiseAlertRemaining => noiseAlertRemaining;
    // 판정과 디버그 표시는 같은 값을 사용한다. 소음 청취 반경과는 별개인, 벽에 차단되는 시야 거리다.
    public float CurrentDetectionRange => BaseDetectionRange * (definition != null && noiseAlertRemaining > 0f ? definition.NoiseDetectionMultiplier : 1f);
    private float TravelDistance => definition.hasMovementStats ? definition.patrolTravelDistance : DungeonTuning.Active.patrolTravelDistance;

    public void HearNoise(Vector2 noisePosition)
    {
        if (definition != null) HearNoise(noisePosition, HearingRange);
    }

    public void Initialize(Transform target, Vector2 a, Vector2 b, MonsterDefinition data, MonsterVisualAnimator animator)
    {
        if (target == null || data == null) throw new System.ArgumentException("An enemy requires a player and stats.");
        ResetForPool();
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

    /// <summary>이전 런의 공격·조사·경로·플레이어 참조를 다음 대여에 넘기지 않는다.</summary>
    public void ResetForPool()
    {
        player = null; playerHealth = null; definition = null; visual = null; PrefabId = null;
        roamTarget = investigationTarget = previousPatrolOrigin = lastSeenPlayerPosition = Vector2.zero;
        attackCooldown = attackTimer = patrolPauseRemaining = investigationRemaining = noiseAlertRemaining = 0f;
        damageApplied = hasPreviousPatrolOrigin = wasChasing = wasInvestigating = hasLastSeenPlayerPosition = false;
        path.Clear(); sightHits.Clear(); pathIndex = 0; pathMode = -1; pathTargetCell = default;
    }

    /// <summary>해체 실패 같은 소리를 들으면 마지막 소리 위치를 조사한다.</summary>
    public void HearNoise(Vector2 noisePosition, float radius)
    {
        if (definition == null || !isActiveAndEnabled || !GameSaveData.Finite(noisePosition) || float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f
            || Vector2.Distance(transform.position, noisePosition) > radius) return;
        Vector2Int noiseCell = DungeonLayoutFactory.WorldToCell(noisePosition);
        investigationTarget = DungeonLayoutFactory.IsWalkablePosition(noisePosition) ? noisePosition : DungeonLayoutFactory.CellCenter(noiseCell.x, noiseCell.y);
        investigationRemaining = definition.hasMovementStats ? definition.investigationSeconds : DungeonTuning.Active.investigationSeconds;
        noiseAlertRemaining = definition.NoiseAlertSeconds;
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
        // 이동·공격 중에도 경계 시간은 흐른다. 일시정지와 비활성 층에서는 Tick 자체가 멈춘다.
        noiseAlertRemaining = Mathf.Max(0f, noiseAlertRemaining - elapsed);
        float distance = Vector2.Distance(transform.position, player.position);
        bool isChasing = distance < CurrentDetectionRange && HasClearSight(player.position);
        if (isChasing)
        {
            // 공격 준비 중에도 실제로 보인 위치만 갱신한다. 시야를 잃은 뒤의 위치는 따라 읽지 않는다.
            lastSeenPlayerPosition = player.position;
            hasLastSeenPlayerPosition = true;
        }
        if (attackTimer > 0f)
        {
            attackTimer -= elapsed;
            float progress = 1f - attackTimer / definition.attackAnimationSeconds;
            visual?.Tick(true, progress, false, player.position - transform.position);
            if (!damageApplied && progress >= .5f)
            {
                damageApplied = true;
                if (distance <= definition.attackRange + .25f && (isChasing || HasClearSight(player.position)))
                    playerHealth?.TakeDamage(definition.attackDamage);
            }
            return;
        }

        bool finishedLastSeen = !isChasing && hasLastSeenPlayerPosition
            && Vector2.Distance(transform.position, lastSeenPlayerPosition) < .18f;
        if (finishedLastSeen)
        {
            hasLastSeenPlayerPosition = false;
            pathMode = -1;
        }
        bool isFollowingLastSeen = !isChasing && hasLastSeenPlayerPosition;
        bool isInvestigating = !isChasing && !isFollowingLastSeen && investigationRemaining > 0f;
        if (isChasing) investigationRemaining = Mathf.Max(0f, investigationRemaining - elapsed);
        if (!isChasing && !isFollowingLastSeen && !isInvestigating)
        {
            if (wasChasing || wasInvestigating || finishedLastSeen)
            {
                SelectPatrolDestination();
            }
            else if (Vector2.Distance(transform.position, roamTarget) < .18f)
            {
                if (patrolPauseRemaining > 0f) patrolPauseRemaining = Mathf.Max(0f, patrolPauseRemaining - elapsed);
                if (patrolPauseRemaining <= 0f) SelectPatrolDestination();
            }
        }

        Vector2 target = isChasing ? (Vector2)player.position : isFollowingLastSeen ? lastSeenPlayerPosition
            : isInvestigating ? investigationTarget : roamTarget;
        bool usesChaseSpeed = isChasing || isFollowingLastSeen;
        float speed = definition.hasMovementStats ? (usesChaseSpeed ? definition.chaseSpeed : definition.patrolSpeed)
            : (usesChaseSpeed ? DungeonTuning.Active.chaseSpeed : DungeonTuning.Active.patrolSpeed);
        Vector2 before = transform.position;
        bool arrivedBefore = Vector2.Distance(before, target) < .18f;
        if (!arrivedBefore && !FollowPath(target, speed * elapsed, isChasing ? 2 : isFollowingLastSeen ? 3 : isInvestigating ? 1 : 0)
            && isFollowingLastSeen)
        {
            // 유효한 경로가 없는 목격 위치에 무한히 매달리지 않는다.
            hasLastSeenPlayerPosition = false;
            SelectPatrolDestination();
        }
        bool arrived = Vector2.Distance(transform.position, target) < .18f;
        if (!isChasing && !isFollowingLastSeen && !isInvestigating && !arrivedBefore && arrived)
            patrolPauseRemaining = definition.hasMovementStats ? definition.patrolArrivalPause : DungeonTuning.Active.patrolArrivalPause;
        if (isInvestigating && arrived) investigationRemaining = Mathf.Max(0f, investigationRemaining - elapsed);
        Vector2 velocity = (Vector2)transform.position - before;
        visual?.Tick(false, 0f, velocity.sqrMagnitude > .00001f, velocity);
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
        // 리스트는 개체가 보관하고 확장 시에만 할당한다. 고정 배열처럼 벽 결과가 잘리지 않는다.
        var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
        filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
        sightHits.Clear();
        Physics2D.Raycast(origin, offset / distance, filter, sightHits, distance);
        foreach (RaycastHit2D hit in sightHits)
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

    private bool FollowPath(Vector2 target, float distance, int mode)
    {
        Vector2Int cell = DungeonLayoutFactory.WorldToCell(target);
        if (pathMode != mode || pathTargetCell != cell || pathIndex >= path.Count)
        {
            if (!DungeonLayoutFactory.TryBuildPath(transform.position, target, path)) return false;
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
        // 도면 밖 좌표는 가장 가까운 바닥으로 보정된다. 그곳에서 도달 불가능한 목표를 계속 재시도하지 않는다.
        return pathIndex < path.Count || Vector2.Distance(transform.position, target) < .18f;
    }

    public EnemySaveData Capture() => new()
    {
        prefabId = PrefabId, definition = definition?.Copy(), position = transform.position, roamTarget = roamTarget,
        investigationTarget = investigationTarget, attackCooldown = attackCooldown, attackTimer = attackTimer,
        roamSeconds = patrolPauseRemaining, investigationSeconds = investigationRemaining,
        noiseAlertSeconds = noiseAlertRemaining,
        damageApplied = damageApplied, wasChasing = wasChasing, wasInvestigating = wasInvestigating,
        lastSeenPlayerPosition = lastSeenPlayerPosition, hasLastSeenPlayerPosition = hasLastSeenPlayerPosition,
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
        noiseAlertRemaining = Mathf.Clamp(state.noiseAlertSeconds, 0f, definition.NoiseAlertSeconds);
        damageApplied = state.damageApplied;
        wasChasing = state.wasChasing;
        wasInvestigating = state.wasInvestigating;
        lastSeenPlayerPosition = state.lastSeenPlayerPosition;
        hasLastSeenPlayerPosition = state.hasLastSeenPlayerPosition;
        previousPatrolOrigin = state.previousPatrolOrigin;
        hasPreviousPatrolOrigin = state.hasPreviousPatrolOrigin;
        pathMode = -1;
    }

}
