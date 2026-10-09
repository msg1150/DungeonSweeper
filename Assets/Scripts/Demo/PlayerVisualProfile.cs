using UnityEngine;

[CreateAssetMenu(fileName = "PlayerVisualProfile", menuName = "Dungeon Sweeper/Player Visual Profile")]
public sealed class PlayerVisualProfile : ScriptableObject
{
    private static PlayerVisualProfile active;
    public string walkSheetResource = "Sprites/player-walk-cycle";
    public string dashSheetResource = "Sprites/player-dash-cycle";
    [Min(1)] public int columns = 2;
    [Min(1)] public int rows = 2;
    [Range(0f, .1f), Tooltip("각 칸 왼쪽의 안전 여백. 옆 프레임의 작은 조각이 표시되는 것을 막습니다.")]
    public float frameLeftInset = .06f;
    [Min(1f)] public float walkFramesPerSecond = 9f;
    [Min(1f)] public float dashFramesPerSecond = 20f;
    [Range(0f, .2f), Tooltip("이동 대시가 끝난 뒤 착지 자세를 보여주는 시간. 실제 대시 시간/거리는 바꾸지 않습니다.")]
    public float dashRecoverySeconds = .08f;
    [Range(0f, .08f), Tooltip("대시 자세 전환 시 이전 자세가 짧게 사라지는 시간.")]
    public float poseBlendSeconds = .045f;
    [Min(.1f)] public float visualScale = 1.35f;
    public static PlayerVisualProfile Active { get { if (active == null) active = Resources.Load<PlayerVisualProfile>("PlayerVisualProfile"); if (active == null) active = CreateInstance<PlayerVisualProfile>(); return active; } }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetActive() => active = null;
}
