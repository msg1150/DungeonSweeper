using UnityEngine;

[CreateAssetMenu(fileName = "PlayerVisualProfile", menuName = "Dungeon Sweeper/Player Visual Profile")]
public sealed class PlayerVisualProfile : ScriptableObject
{
    private static PlayerVisualProfile active;
    public string walkSheetResource = "Sprites/player-walk-cycle";
    public string dashSheetResource = "Sprites/player-dash-cycle";
    [Min(1)] public int columns = 2;
    [Min(1)] public int rows = 2;
    [Min(1f)] public float walkFramesPerSecond = 9f;
    [Min(1f)] public float dashFramesPerSecond = 20f;
    [Min(.1f)] public float visualScale = 1.35f;
    public static PlayerVisualProfile Active { get { if (active == null) active = Resources.Load<PlayerVisualProfile>("PlayerVisualProfile"); if (active == null) active = CreateInstance<PlayerVisualProfile>(); return active; } }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetActive() => active = null;
}
