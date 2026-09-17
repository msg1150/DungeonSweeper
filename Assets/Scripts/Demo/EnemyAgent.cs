using UnityEngine;

/// <summary>순찰 → 탐지 → 추적만 수행하는 비전투 위협.</summary>
public class EnemyAgent : MonoBehaviour
{
    [SerializeField] private float patrolSpeed = 1.05f;
    [SerializeField] private float chaseSpeed = 2.15f;
    [SerializeField] private float detectionRange = 3.1f;

    private Transform player;
    private Vector2 pointA;
    private Vector2 pointB;
    private Vector2 patrolTarget;
    private float catchCooldown;

    public void Initialize(Transform target, Vector2 a, Vector2 b)
    {
        player = target;
        pointA = a;
        pointB = b;
        patrolTarget = b;
    }

    private void Update()
    {
        if (player == null || DungeonRunController.Instance == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        bool isChasing = distance < detectionRange;
        Vector2 destination = isChasing ? player.position : patrolTarget;
        float speed = isChasing ? chaseSpeed : patrolSpeed;
        transform.position = Vector2.MoveTowards(transform.position, destination, speed * Time.deltaTime);

        if (!isChasing && Vector2.Distance(transform.position, patrolTarget) < .08f)
            patrolTarget = Vector2.Distance(patrolTarget, pointA) < .1f ? pointB : pointA;

        if (catchCooldown > 0f) catchCooldown -= Time.deltaTime;
        if (distance < .58f && catchCooldown <= 0f)
        {
            catchCooldown = 2f;
            DungeonRunController.Instance.NotifyPlayerCaught();
        }
    }
}
