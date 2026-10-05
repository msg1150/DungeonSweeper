using UnityEngine;

public sealed class GameAudio : MonoBehaviour
{
    private AudioSource music, effects;
    private AudioListener fallbackListener;
    private GameSettingsData appliedSettings;

    private void Awake()
    {
        music = gameObject.AddComponent<AudioSource>();
        music.playOnAwake = false;
        music.loop = true;
        effects = gameObject.AddComponent<AudioSource>();
        effects.playOnAwake = false;
        fallbackListener = gameObject.AddComponent<AudioListener>();
        GameSettings.Changed += ApplyVolumes;
        ApplyVolumes();
    }
    private void ApplyVolumes()
    {
        if (GameSettings.Current == null) return;
        music.volume = GameSettings.Current.musicVolume;
        effects.volume = GameSettings.Current.effectsVolume;
        appliedSettings = GameSettings.Current;
        music.outputAudioMixerGroup = MainMenuPresentationConfig.Active.musicMixerGroup;
        effects.outputAudioMixerGroup = MainMenuPresentationConfig.Active.effectsMixerGroup;
    }
    public void OnSceneChanged(string scene)
    {
        ApplyVolumes();
        fallbackListener.enabled = false;
        bool hasListener = false;
        foreach (AudioListener listener in FindObjectsByType<AudioListener>())
            if (listener != fallbackListener && listener.enabled) { hasListener = true; break; }
        fallbackListener.enabled = !hasListener;
        MainMenuPresentationConfig config = MainMenuPresentationConfig.Active;
        GameFlowConfig flow = GameFlowConfig.Active;
        AudioClip clip = scene == flow.mainMenuSceneName ? config.menuMusic
            : scene == flow.townSceneName ? config.townMusic : config.dungeonMusic;
        if (music.clip == clip) return;
        music.Stop();
        music.clip = clip;
        if (clip != null) music.Play();
    }
    public void PlayButton() { if (MainMenuPresentationConfig.Active.buttonSound != null) effects.PlayOneShot(MainMenuPresentationConfig.Active.buttonSound); }
    private void Update() { if (!ReferenceEquals(appliedSettings, GameSettings.Current)) ApplyVolumes(); }
    public void PlayEffect(AudioClip clip) { if (clip != null) effects.PlayOneShot(clip); }
    private void OnDestroy() => GameSettings.Changed -= ApplyVolumes;
}
