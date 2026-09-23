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
    private float nextRoamDecision;
    private Vector2 investigationTarget;
    private float investigationUntil;
    private bool wasChasing;

    public void Initialize(Transform target, Vector2 a, Vector2 b, MonsterDefinition data, MonsterVisualAnimator animator)
    {
        player = target;
        playerHealth = target.GetComponent<PlayerHealth>();
        definition = data;
        visual = animator;
        roamTarget = b;
        nextRoamDecision = Random.Range(1.5f, 3.5f);
    }

    /// <summary>해체 실패 같은 소리를 들으면 마지막 소리 위치를 조사한다.</summary>
    public void HearNoise(Vector2 noisePosition, float radius)
    {
        if (Vector2.Distance(transform.position, noisePosition) > radius) return;
        investigationTarget = noisePosition;
        investigationUntil = Time.time + DungeonTuning.Active.investigationSeconds;
    }

    private void Update()
    {
        if (player == null || definition == null || DungeonRunController.Instance == null || Time.timeScale == 0f) return;

        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
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
        bool isChasing = distance < DungeonTuning.Active.detectionRange && HasClearSight(player.position);
        bool isInvestigating = !isChasing && Time.time < investigationUntil;
        if (!isChasing && !isInvestigating && (wasChasing || Time.time >= nextRoamDecision || Vector2.Distance(transform.position, roamTarget) < .18f))
        {
            roamTarget = DungeonLayoutFactory.RandomRoamPosition(transform.position);
            nextRoamDecision = Time.time + Random.Range(2.5f, 5.5f);
        }

        Vector2 target = isChasing ? player.position : isInvestigating ? investigationTarget : roamTarget;
        Vector2 destination = DungeonLayoutFactory.GetNextPathPoint(transform.position, target);
        float speed = isChasing ? DungeonTuning.Active.chaseSpeed : DungeonTuning.Active.patrolSpeed;
        Vector2 before = transform.position;
        transform.position = Vector2.MoveTowards(transform.position, destination, speed * Time.deltaTime);
        Vector2 velocity = (Vector2)transform.position - before;
        visual.Tick(false, 0f, velocity.sqrMagnitude > .00001f, velocity);
        wasChasing = isChasing;

        if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;
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

}
