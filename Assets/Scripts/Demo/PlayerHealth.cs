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
    }

    public bool TakeDamage(int amount)
    {
        if (Time.time < invulnerableUntil || Current <= 0) return false;
        Current = Mathf.Max(0, Current - Mathf.Max(0, amount));
        invulnerableUntil = Time.time + DungeonTuning.Active.playerHitInvulnerability;
        if (Current == 0) DungeonRunController.Instance.HandlePlayerDeath();
        return true;
    }
}
