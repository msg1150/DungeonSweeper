using System;
using UnityEngine;

[Serializable]
public sealed class GameSettingsData
{
    public float masterVolume = 1f, musicVolume = .8f, effectsVolume = 1f;
    public int width, height, qualityLevel, frameRate = 60;
    public bool fullscreen = true, vSync = true;
}

/// <summary>Device preferences are separate from game save slots.</summary>
public static class GameSettings
{
    private const string Key = "DungeonSweeper.Settings.v1";
    private static GameSettingsData current;
    private static GameSettingsData defaults;
    public static GameSettingsData Current
    {
        get { if (current == null) Initialize(); return current; }
        private set => current = value;
    }
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Current = null; defaults = null; Changed = null; }

    public static GameSettingsData Defaults()
    {
        if (defaults == null) defaults = new GameSettingsData
        {
            width = Mathf.Max(640, Screen.width), height = Mathf.Max(360, Screen.height),
            qualityLevel = QualitySettings.GetQualityLevel(), fullscreen = Screen.fullScreen
        };
        return Clone(defaults);
    }

    public static void Initialize()
    {
        Current = Defaults();
        if (!PlayerPrefs.HasKey(Key)) { Apply(Current, false); return; }
        try
        {
            GameSettingsData saved = JsonUtility.FromJson<GameSettingsData>(PlayerPrefs.GetString(Key));
            Apply(saved ?? Current, false);
        }
        catch (ArgumentException) { Debug.LogWarning("Options save could not be read. Using defaults."); Apply(Current, false); }
    }

    public static GameSettingsData Copy() => Clone(Current);

    private static GameSettingsData Clone(GameSettingsData data) => new()
    {
        masterVolume = data.masterVolume, musicVolume = data.musicVolume, effectsVolume = data.effectsVolume,
        width = data.width, height = data.height, qualityLevel = data.qualityLevel,
        frameRate = data.frameRate, fullscreen = data.fullscreen, vSync = data.vSync
    };

    public static void Apply(GameSettingsData settings, bool persist = true)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        // Keep a committed copy: modifying the options draft must not change live settings.
        settings = Clone(settings);
        settings.masterVolume = ClampVolume(settings.masterVolume, 1f);
        settings.musicVolume = ClampVolume(settings.musicVolume, .8f);
        settings.effectsVolume = ClampVolume(settings.effectsVolume, 1f);
        int maxWidth = Screen.currentResolution.width > 0 ? Screen.currentResolution.width : 3840;
        int maxHeight = Screen.currentResolution.height > 0 ? Screen.currentResolution.height : 2160;
        foreach (Resolution resolution in Screen.resolutions)
        {
            maxWidth = Mathf.Max(maxWidth, resolution.width);
            maxHeight = Mathf.Max(maxHeight, resolution.height);
        }
        settings.width = Mathf.Clamp(settings.width, 640, Mathf.Max(640, maxWidth));
        settings.height = Mathf.Clamp(settings.height, 360, Mathf.Max(360, maxHeight));
        settings.qualityLevel = Mathf.Clamp(settings.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        if (settings.frameRate != -1 && settings.frameRate != 30 && settings.frameRate != 60 && settings.frameRate != 120) settings.frameRate = 60;
        Current = settings;
        QualitySettings.SetQualityLevel(settings.qualityLevel, true);
        QualitySettings.vSyncCount = settings.vSync ? 1 : 0;
        Application.targetFrameRate = settings.frameRate;
        Screen.SetResolution(settings.width, settings.height,
            settings.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        ApplyAudio();
        Changed?.Invoke();
        if (persist) { PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current)); PlayerPrefs.Save(); }
    }

    private static void ApplyAudio()
    {
        AudioListener.volume = Current.masterVolume;
    }

    private static float ClampVolume(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value)
        ? fallback : Mathf.Clamp01(value);
}
