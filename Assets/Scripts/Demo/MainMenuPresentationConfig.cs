using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "MainMenuPresentation", menuName = "Dungeon Sweeper/Menu Presentation")]
public sealed class MainMenuPresentationConfig : ScriptableObject
{
    [Header("Main Menu Art")]
    public Texture2D backgroundImage;
    public Texture2D logo;
    public VideoClip backgroundVideo;
    public Font uiFont;
    [Range(0f, 1f)] public float backgroundDarkness = .45f;
    [Header("Music and UI Sounds")]
    public AudioClip menuMusic;
    public AudioClip townMusic;
    public AudioClip dungeonMusic;
    public AudioClip buttonSound;
    [Header("Optional Mixer Routing")]
    public AudioMixerGroup musicMixerGroup;
    public AudioMixerGroup effectsMixerGroup;
    private static MainMenuPresentationConfig active;
    public static MainMenuPresentationConfig Active
    {
        get
        {
            if (active == null) active = Resources.Load<MainMenuPresentationConfig>("MainMenuPresentation");
            if (active == null) active = CreateInstance<MainMenuPresentationConfig>();
            return active;
        }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => active = null;
}
