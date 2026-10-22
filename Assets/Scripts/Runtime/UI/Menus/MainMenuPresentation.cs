using UnityEngine;
using UnityEngine.Video;

/// <summary>Optional image/video background; the menu also works with no media assets.</summary>
public sealed class MainMenuPresentation : MonoBehaviour
{
    [SerializeField] private MainMenuPresentationConfig presentation;
    private VideoPlayer video;
    private RenderTexture videoTexture;
    private bool videoFailed;
    public MainMenuPresentationConfig Config => presentation != null ? presentation : MainMenuPresentationConfig.Active;
    public Texture Background => !videoFailed && video != null && video.isPrepared ? videoTexture : Config.backgroundImage;

    private void Awake()
    {
        if (Config.backgroundVideo == null) return;
        videoTexture = new RenderTexture(1920, 1080, 0);
        video = gameObject.AddComponent<VideoPlayer>();
        video.playOnAwake = false;
        video.isLooping = true;
        video.audioOutputMode = VideoAudioOutputMode.None;
        video.renderMode = VideoRenderMode.RenderTexture;
        video.targetTexture = videoTexture;
        video.clip = Config.backgroundVideo;
        video.prepareCompleted += PlayPrepared;
        video.errorReceived += OnVideoError;
        video.Prepare();
    }
    private void PlayPrepared(VideoPlayer source) => source.Play();
    private void OnVideoError(VideoPlayer source, string message)
    {
        videoFailed = true;
        source.Stop();
        Debug.LogWarning($"Menu video: {message}. Using the background image.");
    }
    private void OnDestroy()
    {
        if (video != null) { video.prepareCompleted -= PlayPrepared; video.errorReceived -= OnVideoError; video.Stop(); }
        if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
    }
}
