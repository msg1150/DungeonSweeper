using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DungeonSweeper 플레이어의 기본 이동과 대시를 담당합니다.
/// 
/// 현재 프로토타입 단계에서는
/// - WASD 이동
/// - Space 대시
/// 만 처리합니다.
/// 
/// 공격 기능은 의도적으로 포함하지 않습니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    private float moveSpeed = 5f;

    [Header("Dash")]
    [SerializeField]
    private float dashSpeed = 12f;

    [SerializeField]
    private float dashDuration = 0.15f;

    [SerializeField]
    private float dashCooldown = 1f;

    private Rigidbody2D rb;

    // 현재 이동 입력 방향
    private Vector2 moveInput;

    // 마지막으로 입력했던 이동 방향
    // 정지 상태에서 대시했을 때 사용할 수 있도록 저장합니다.
    private Vector2 lastMoveDirection = Vector2.down;

    // 현재 대시 중인지
    private bool isDashing;

    // 대시가 끝날 때까지 남은 시간
    private float dashTimeRemaining;

    // 다시 대시할 수 있을 때까지 남은 시간
    private float dashCooldownRemaining;

    // 현재 대시 방향
    private Vector2 dashDirection;

    private bool movementEnabled = true;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!movementEnabled)
            return;

        ReadMovementInput();
        UpdateDash();
    }

    private void FixedUpdate()
    {
        if (!movementEnabled)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Move();
    }

    /// <summary>
    /// 키보드 이동 입력을 읽습니다.
    /// 프로토타입 단계이므로 Input Action Asset 없이
    /// Input System의 Keyboard 입력을 직접 사용합니다.
    /// </summary>
    private void ReadMovementInput()
    {
        Vector2 input = Vector2.zero;

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.wKey.isPressed)
            input.y += 1f;

        if (Keyboard.current.sKey.isPressed)
            input.y -= 1f;

        if (Keyboard.current.aKey.isPressed)
            input.x -= 1f;

        if (Keyboard.current.dKey.isPressed)
            input.x += 1f;

        // 대각선 이동 시 속도가 더 빨라지는 것을 방지합니다.
        moveInput = input.normalized;

        if (moveInput != Vector2.zero)
        {
            lastMoveDirection = moveInput;
        }
    }

    /// <summary>
    /// 일반 이동 또는 대시 이동을 적용합니다.
    /// </summary>
    private void Move()
    {
        if (isDashing)
        {
            rb.linearVelocity = dashDirection * dashSpeed;
            return;
        }

        rb.linearVelocity = moveInput * moveSpeed;
    }

    /// <summary>
    /// 대시 입력과 지속시간, 쿨다운을 관리합니다.
    /// </summary>
    private void UpdateDash()
    {
        if (dashCooldownRemaining > 0f)
        {
            dashCooldownRemaining -= Time.deltaTime;
        }

        if (isDashing)
        {
            dashTimeRemaining -= Time.deltaTime;

            if (dashTimeRemaining <= 0f)
            {
                isDashing = false;
            }

            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame &&
            dashCooldownRemaining <= 0f)
        {
            StartDash();
        }
    }

    /// <summary>
    /// 현재 이동 방향으로 대시를 시작합니다.
    /// 이동 입력이 없다면 마지막 이동 방향으로 대시합니다.
    /// </summary>
    private void StartDash()
    {
        dashDirection =
            moveInput != Vector2.zero
                ? moveInput
                : lastMoveDirection;

        isDashing = true;

        dashTimeRemaining = dashDuration;
        dashCooldownRemaining = dashCooldown;
    }

    /// <summary>
    /// 외부 시스템에서 플레이어 이동/대시 입력을
    /// 활성화하거나 비활성화할 때 사용합니다.
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;

        // 이동을 막는 순간 기존 속도도 제거합니다.
        if (!movementEnabled)
        {
            moveInput = Vector2.zero;
            rb.linearVelocity = Vector2.zero;
        }
    }
}