using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour
{
    private float invulnerableUntil;
    public int Current { get; private set; }
    public int Maximum { get; private set; }

    public void Initialize()
    {
        Maximum = Mathf.Max(1, DungeonTuning.Active.playerMaxHealth);
        Current = Maximum;
        invulnerableUntil = 0f;
    }

    public bool TakeDamage(int amount)
    {
        DungeonRunController run = DungeonRunController.Instance;
        if (run == null || !run.IsRunActive || amount <= 0 || Time.time < invulnerableUntil || Current <= 0) return false;
        Current = Mathf.Max(0, Current - amount);
        invulnerableUntil = Time.time + DungeonTuning.Active.playerHitInvulnerability;
        if (Current == 0) run.HandlePlayerDeath();
        return true;
    }

    public float RemainingInvulnerability => Mathf.Max(0f, invulnerableUntil - Time.time);

    public void Restore(int health, float invulnerabilitySeconds = 0f)
    {
        Current = Mathf.Clamp(health, 1, Maximum);
        invulnerableUntil = Time.time + Mathf.Max(0f, invulnerabilitySeconds);
    }
}
