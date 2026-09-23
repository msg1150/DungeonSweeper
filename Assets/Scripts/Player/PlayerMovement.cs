using System;
using UnityEngine;

public interface IPlayerMotionState { Vector2 Velocity { get; } bool IsDashing { get; } }

[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerMovement : MonoBehaviour, IPlayerMotionState
{
    private Rigidbody2D body;
    private IPlayerInputSource input;
    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.down;
    private Vector2 dashDirection;
    private float dashTimeRemaining;
    private float dashCooldownRemaining;
    private bool movementEnabled = true;

    public bool IsDashing { get; private set; }
    public Vector2 Velocity => body == null ? Vector2.zero : body.linearVelocity;
    public event Action DashStarted;
    public event Action DashEnded;

    private void Awake() { body = GetComponent<Rigidbody2D>(); input = new KeyboardPlayerInputSource(); }

    private void Update()
    {
        if (!movementEnabled) return;
        moveInput = input.ReadMove();
        if (moveInput != Vector2.zero) lastMoveDirection = moveInput;
        TickDash();
    }

    private void FixedUpdate()
    {
        if (!movementEnabled) { body.linearVelocity = Vector2.zero; return; }
        DungeonTuning tuning = DungeonTuning.Active;
        body.linearVelocity = IsDashing ? dashDirection * tuning.playerDashSpeed : moveInput * tuning.playerMoveSpeed;
    }

    private void TickDash()
    {
        DungeonTuning tuning = DungeonTuning.Active;
        dashCooldownRemaining = Mathf.Max(0f, dashCooldownRemaining - Time.deltaTime);
        if (IsDashing)
        {
            dashTimeRemaining -= Time.deltaTime;
            if (dashTimeRemaining <= 0f) EndDash();
            return;
        }
        if (!input.DashPressedThisFrame() || dashCooldownRemaining > 0f) return;
        dashDirection = moveInput != Vector2.zero ? moveInput : lastMoveDirection;
        IsDashing = true;
        dashTimeRemaining = tuning.playerDashDuration;
        dashCooldownRemaining = tuning.playerDashCooldown;
        DashStarted?.Invoke();
    }

    private void EndDash()
    {
        if (!IsDashing) return;
        IsDashing = false;
        DashEnded?.Invoke();
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        if (enabled) return;
        moveInput = Vector2.zero;
        body.linearVelocity = Vector2.zero;
        EndDash();
    }
}
