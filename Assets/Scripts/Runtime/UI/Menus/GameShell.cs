using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Main menu, in-game pause/save UI, options, and normal-exit autosaving.</summary>
public sealed partial class GameShell : MonoBehaviour
{
    private enum Page { Playing, Main, Pause, Load, Save, Options }
    private static readonly string[] pauseLabels = { "계속하기", "저장하기", "불러오기", "옵션", "메인 화면", "게임종료" };
    private static GameShell instance;
    public static bool IsMenuOpen => instance != null && instance.page != Page.Playing;
    // Losing Game view focus blocks input, but must not hide the frame being inspected.
    public static bool IsGameplayHudVisible => !IsMenuOpen && !GameSession.IsLoading && !TownCommercePanel.IsOpen;
    public static bool IsFocusInputBlocked => instance != null && (!instance.hasFocus || instance.blockInputFrame == Time.frameCount);
    public static bool IsGameplayInputBlocked
    {
        get
        {
            if (instance == null) instance = FindAnyObjectByType<GameShell>();
            return instance != null && (!instance.hasFocus || instance.page != Page.Playing || GameSession.IsLoading
                || instance.blockInputFrame == Time.frameCount || TownCommercePanel.IsOpen);
        }
    }
    private int blockInputFrame = -1, consumedEscapeFrame = -1;
    public static void ConsumeGameplayEscape() { if (instance != null) instance.consumedEscapeFrame = Time.frameCount; }
    private Page page, returnPage;
    private float previousTimeScale = 1f;
    private bool hasFocus = true, focusPaused;
    private float focusTimeScale = 1f;
    private bool quitSaved;
    private Scene handledScene;
    private int overwriteSlot = -2;
    private List<SaveSlotInfo> slots;
    private Vector2 slotScroll;
    private GameSettingsData draft;
    private readonly List<Vector2Int> resolutions = new();
    private MainMenuPresentation presentation;
    private GameAudio audio;
    private string notice;
    private float noticeUntil;
    private float nextAutosaveAttempt;
    private GUIStyle titleStyle, subtitleStyle, buttonStyle, slotStyle, labelStyle, smallStyle, panelStyle;
    private Font fallbackFont;
    private MenuUiRenderer menuUi;
    private MenuUiArt styledMenuArt;
    private GUIStyle artButtonStyle, artSlotStyle, mainButtonStyle;
    private MenuUiArt MenuArt
    {
        get
        {
            var art = MainMenuPresentationConfig.Active.menuUi;
            return art != null && art.IsReady ? art : null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance() => instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance != null) return;
        GameSettings.Initialize();
        new GameObject("Game Shell").AddComponent<GameShell>();
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        audio = gameObject.AddComponent<GameAudio>();
        GameSaveService.MigrateLegacySave();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.wantsToQuit += OnWantsToQuit;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive && !scene.Equals(SceneManager.GetActiveScene())) return;
        if (handledScene.Equals(scene)) return;
        handledScene = scene;
        focusPaused = false;
        StopAllCoroutines();
        page = scene.name == GameFlowConfig.Active.mainMenuSceneName ? Page.Main : Page.Playing;
        overwriteSlot = -2;
        presentation = FindAnyObjectByType<MainMenuPresentation>();
        if (page == Page.Main && presentation == null)
            presentation = new GameObject("Main Menu Presentation").AddComponent<MainMenuPresentation>();
        bool gameplayScene = scene.name == GameFlowConfig.Active.townSceneName || scene.name == GameFlowConfig.Active.dungeonSceneName;
#if UNITY_EDITOR
        // 테스트 씬 이름을 출시 설정에 저장하지 않는다. 에디터의 탐험 샌드박스도 같은 컨트롤러를 사용한다.
        gameplayScene |= DungeonRunController.Instance != null;
#endif
        if (gameplayScene) GameSession.BeginDirectPlay();
        audio.OnSceneChanged(scene.name);
        StartCoroutine(FinishLoading());
    }

    private IEnumerator FinishLoading()
    {
        yield return null;
        GameSession.FinishSceneLoad();
        PauseForFocusLoss();
    }

    private void Update()
    {
        // Also handle active-scene changes initiated by editor tools or other systems.
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.isLoaded && !handledScene.Equals(activeScene)) OnSceneLoaded(activeScene, LoadSceneMode.Single);
        PauseForFocusLoss();
        GameSession.Tick(Time.unscaledDeltaTime);
        if (!hasFocus || blockInputFrame == Time.frameCount) return;
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (consumedEscapeFrame == Time.frameCount) return;
        if (page == Page.Playing && TownCommercePanel.IsOpen) return;
        if (overwriteSlot != -2) { overwriteSlot = -2; return; }
        if (page == Page.Playing && GameSession.HasActiveGame)
        {
            // Escape remains the dismantling cancel key while that activity is active.
            DungeonRunController run = DungeonRunController.Instance;
            if (run != null && (run.ActiveSession != null || run.IsLootPlacementOpen)) return;
            OpenPause();
        }
        else if (page == Page.Pause) Resume();
        else if (page == Page.Load || page == Page.Save || page == Page.Options) page = returnPage;
    }

    private void LateUpdate()
    {
        if (!GameSession.AutosavePending || GameSession.IsLoading || Time.unscaledTime < nextAutosaveAttempt) return;
        if (!GameSession.Save(GameSaveService.AutosaveSlot, out string error))
        {
            nextAutosaveAttempt = Time.unscaledTime + 5f;
            ShowNotice(error + " 5초 후 다시 시도합니다.");
        }
        else nextAutosaveAttempt = 0f;
    }

    private void OnApplicationFocus(bool focused)
    {
        bool wasFocused = hasFocus;
        hasFocus = focused;
        if (!focused) { PauseForFocusLoss(); return; }
        if (!wasFocused) blockInputFrame = Time.frameCount;
        if (!focusPaused) return;
        focusPaused = false;
        // Resume only the pause owned by focus loss; existing menus and loot screens keep their pause.
        if (page == Page.Playing && GameSession.HasActiveGame && !GameSession.IsLoading
            && !TownCommercePanel.IsOpen && DungeonRunController.Instance?.IsLootPlacementOpen != true && Time.timeScale == 0f)
            Time.timeScale = focusTimeScale;
    }

    private void PauseForFocusLoss()
    {
        if (hasFocus || focusPaused || page != Page.Playing || !GameSession.HasActiveGame
            || GameSession.IsLoading || Time.timeScale <= 0f) return;
        focusTimeScale = Time.timeScale;
        focusPaused = true;
        Time.timeScale = 0f;
    }

    private void OpenPause()
    {
        if (page != Page.Playing) return;
        previousTimeScale = focusPaused ? focusTimeScale : Time.timeScale;
        focusPaused = false;
        Time.timeScale = 0f;
        page = Page.Pause;
    }

    private void Resume()
    {
        page = Page.Playing;
        blockInputFrame = Time.frameCount;
        Time.timeScale = previousTimeScale;
        PauseForFocusLoss();
    }

    private void OpenSlots(bool save)
    {
        returnPage = page;
        page = save ? Page.Save : Page.Load;
        slots = GameSaveService.GetSlots();
        slotScroll = Vector2.zero;
        overwriteSlot = -2;
    }

    private void OpenOptions()
    {
        returnPage = page;
        page = Page.Options;
        draft = GameSettings.Copy();
        resolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            Vector2Int size = new(resolution.width, resolution.height);
            if (size.x >= 640 && size.y >= 360 && !resolutions.Contains(size)) resolutions.Add(size);
        }
        Vector2Int current = new(draft.width, draft.height);
        if (!resolutions.Contains(current)) resolutions.Add(current);
        resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
    }

    private bool SaveBeforeLeaving()
    {
        if (!GameSession.HasActiveGame) return true;
        if (GameSession.Save(GameSaveService.AutosaveSlot, out string error)) return true;
        ShowNotice(error);
        if (page == Page.Playing && !GameSession.IsLoading) OpenPause();
        return false;
    }

    private bool OnWantsToQuit()
    {
        if (quitSaved) return true;
        quitSaved = SaveBeforeLeaving();
        return quitSaved;
    }

    private void RequestQuit()
    {
        if (!SaveBeforeLeaving()) return;
        quitSaved = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ReturnToMain()
    {
        string menu = GameFlowConfig.Active.mainMenuSceneName;
        if (!Application.CanStreamedLevelBeLoaded(menu)) { ShowNotice("메인 화면 씬이 빌드 목록에 없습니다."); return; }
        if (!SaveBeforeLeaving()) return;
        GameSession.EndGame();
        Time.timeScale = 1f;
        SceneManager.LoadScene(menu);
    }

    private void OnApplicationQuit()
    {
        if (!quitSaved && GameSession.HasActiveGame) GameSession.Save(GameSaveService.AutosaveSlot, out _);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.wantsToQuit -= OnWantsToQuit;
        if (instance == this) { instance = null; Time.timeScale = 1f; }
        if (fallbackFont != null) Destroy(fallbackFont);
    }

    private void ShowNotice(string text) { notice = text; noticeUntil = Time.unscaledTime + 7f; }

}
