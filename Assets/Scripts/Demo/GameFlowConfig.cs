using UnityEngine;

[CreateAssetMenu(fileName = "GameFlowConfig", menuName = "Dungeon Sweeper/Game Flow Config")]
public sealed class GameFlowConfig : ScriptableObject
{
    private static GameFlowConfig active;
    [Header("Scenes")]
    public string townSceneName = "Town";
    public string dungeonSceneName = "Dungeon";
    public string dungeonTestSceneName = "Dungeon_Test";
    [Header("Interaction Ranges")]
    [Min(.1f)] public float corpseRange = 1.25f;
    [Min(.1f)] public float exitRange = 1.15f;
    [Min(.1f)] public float townGateRange = 1.5f;
    [Min(.1f)] public float townGuildRange = 2.1f;
    [Min(.1f)] public float townShopRange = 1.7f;
    public static GameFlowConfig Active { get { if (active == null) active = Resources.Load<GameFlowConfig>("GameFlowConfig"); if (active == null) active = CreateInstance<GameFlowConfig>(); return active; } }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetActive() => active = null;
}
