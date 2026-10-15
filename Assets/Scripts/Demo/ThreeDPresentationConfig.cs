using UnityEngine;

/// <summary>2.5D 표시와 보관 중인 Blender 실험 모델의 설정. 게임 좌표·저장 형식은 유지한다.</summary>
[CreateAssetMenu(menuName = "Dungeon Sweeper/3D Presentation")]
public sealed class ThreeDPresentationConfig : ScriptableObject
{
    public bool use3D = true;
    [Tooltip("기존 일러스트 배우 + 입체 배경. 끄면 보관 중인 Blender 모델을 사용합니다.")]
    public bool useSpriteActors = true;
    [Range(45, 75)] public float spriteCameraElevation = 58;
    [Range(4, 10)] public float dungeonViewSize = 5.8f;
    [Range(5, 10)] public float townViewSize = 6.5f;
    public GameObject cat, goblin, slime, orc, town, portal;
    public int forwardRendererIndex = 1;
    [Range(35, 65)] public float cameraFieldOfView = 48;
    private static ThreeDPresentationConfig active;
    public static ThreeDPresentationConfig Active
    {
        get { if (active == null) active = Resources.Load<ThreeDPresentationConfig>("ThreeDPresentation"); return active; }
    }
    public static bool Enabled => Active != null && Active.use3D && (Active.useSpriteActors || (Active.cat != null && Active.town != null));
    public static bool SpritesEnabled => Enabled && Active.useSpriteActors;
    public GameObject Model(string species) => species == "goblin" ? goblin : species == "slime" ? slime : species == "orc" ? orc : cat;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void Reset() => active = null;
}
