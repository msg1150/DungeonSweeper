using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Main menu, in-game pause/save UI, options, and normal-exit autosaving.</summary>
public sealed class GameShell : MonoBehaviour
{
    private enum Page { Playing, Main, Pause, Load, Save, Options }
    private static readonly string[] pauseLabels = { "계속하기", "저장하기", "불러오기", "옵션", "메인 화면", "게임종료" };
    private static GameShell instance;
    public static bool IsMenuOpen => instance != null && instance.page != Page.Playing;
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
        if (scene.name == GameFlowConfig.Active.townSceneName || scene.name == GameFlowConfig.Active.dungeonSceneName
            || scene.name == GameFlowConfig.Active.dungeonTestSceneName) GameSession.BeginDirectPlay();
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

    private void EnsureStyles()
    {
        if (titleStyle != null) return;
        Font font = EnsureFont();
        Color white = GameUiTheme.Ink;
        titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 52, fontStyle = FontStyle.Bold, wordWrap = true };
        titleStyle.normal.textColor = white;
        subtitleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 21, wordWrap = true };
        subtitleStyle.normal.textColor = GameUiTheme.MutedInk;
        labelStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 19, alignment = TextAnchor.MiddleLeft, wordWrap = true };
        labelStyle.normal.textColor = white;
        smallStyle = new GUIStyle(labelStyle) { fontSize = 15 };
        smallStyle.normal.textColor = GameUiTheme.MutedInk;
        GUISkin theme = GameUiTheme.GetSkin(GUI.skin);
        buttonStyle = new GUIStyle(theme.button)
        {
            font = font, fontSize = 21, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(18, 18, 9, 9)
        };
        slotStyle = new GUIStyle(buttonStyle) { alignment = TextAnchor.MiddleLeft, fontSize = 18, wordWrap = true };
        panelStyle = new GUIStyle(theme.box);
    }

    public static Font UiFont => instance != null ? instance.EnsureFont() : null;

    private Font EnsureFont()
    {
        Font font = MainMenuPresentationConfig.Active.uiFont;
        if (font == null)
        {
            if (fallbackFont == null)
                fallbackFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "Arial" }, 20);
            font = fallbackFont;
        }
        return font;
    }

    private bool Button(Rect rect, string label)
    {
        bool clicked = GUI.Button(rect, label, buttonStyle);
        if (clicked) audio.PlayButton();
        return clicked;
    }

    private void OnGUI()
    {
        EnsureStyles();
        Matrix4x4 originalMatrix = GUI.matrix;
        Color originalColor = GUI.color;
        int originalDepth = GUI.depth;
        using var gui = new GameGuiScope(false);
        GUI.depth = -1000;
        GUI.color = Color.white;
        bool menuScene = SceneManager.GetActiveScene().name == GameFlowConfig.Active.mainMenuSceneName;
        if (menuScene)
        {
            GUI.color = GameUiTheme.Paper;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (presentation != null && presentation.Background != null)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), presentation.Background, ScaleMode.ScaleAndCrop);
            GUI.color = new Color(0, 0, 0, presentation != null ? presentation.Config.backgroundDarkness : 0f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        else if (page != Page.Playing)
        {
            GUI.color = GameUiTheme.Scrim;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280f * scale) * .5f, (Screen.height - 720f * scale) * .5f, 0),
            Quaternion.identity, new Vector3(scale, scale, 1));
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && !IsFocusInputBlocked && overwriteSlot == -2;
        switch (page)
        {
            case Page.Main: DrawMain(); break;
            case Page.Pause: DrawPause(); break;
            case Page.Load:
            case Page.Save: DrawSlots(); break;
            case Page.Options: DrawOptions(); break;
            case Page.Playing:
                if (GameSession.HasActiveGame && Button(new Rect(1120, 648, 140, 42), "메뉴 [Esc]")) OpenPause();
                break;
        }
        GUI.enabled = wasEnabled && !IsFocusInputBlocked;
        if (overwriteSlot != -2) DrawOverwriteConfirmation();
        if (!string.IsNullOrEmpty(notice) && Time.unscaledTime < noticeUntil)
        {
            GUI.Box(new Rect(200, 665, 880, 42), GUIContent.none, panelStyle);
            GUI.Label(new Rect(218, 670, 844, 30), notice, smallStyle);
        }
        GUI.matrix = originalMatrix;
        GUI.color = originalColor;
        GUI.depth = originalDepth;
        GUI.enabled = wasEnabled;
    }

    private void DrawMain()
    {
        // 배경만 반투명하게 한다. 글자까지 GUI.color로 흐리게 만들지 않는다.
        Color previousBackground = GUI.backgroundColor;
        var config = MainMenuPresentationConfig.Active;
        GUI.backgroundColor = new Color(1, 1, 1, Mathf.Clamp01(config.menuPanelOpacity));
        GUI.Box(new Rect(138, 110, 400, 512), GUIContent.none, panelStyle);
        GUI.backgroundColor = new Color(1, 1, 1, Mathf.Clamp01(config.menuButtonOpacity));
        if (presentation != null && presentation.Config.logo != null)
            GUI.DrawTexture(new Rect(160, 130, 570, 150), presentation.Config.logo, ScaleMode.ScaleToFit);
        else GUI.Label(new Rect(160, 130, 660, 140), "DUNGEON\nSWEEPER", titleStyle);
        GUI.Label(new Rect(168, 275, 500, 60), "들키기 전에 회수하고,\n무사히 돌아오세요.", subtitleStyle);
        if (Button(new Rect(168, 345, 340, 54), "새 게임"))
            if (!GameSession.StartNewGame(out string error)) ShowNotice(error);
        if (Button(new Rect(168, 411, 340, 54), "이어하기")) OpenSlots(false);
        if (Button(new Rect(168, 477, 340, 54), "옵션")) OpenOptions();
        if (Button(new Rect(168, 543, 340, 54), "게임종료")) RequestQuit();
        GUI.backgroundColor = previousBackground;
    }

    private void DrawPause()
    {
        GUI.Box(new Rect(380, 85, 520, 550), GUIContent.none, panelStyle);
        GUI.Label(new Rect(440, 108, 400, 55), "잠시 쉬어가기", subtitleStyle);
        for (int i = 0; i < pauseLabels.Length; i++)
        {
            if (!Button(new Rect(440, 180 + i * 65, 400, 51), pauseLabels[i])) continue;
            switch (i)
            {
                case 0: Resume(); break;
                case 1: OpenSlots(true); break;
                case 2: OpenSlots(false); break;
                case 3: OpenOptions(); break;
                case 4: ReturnToMain(); break;
                case 5: RequestQuit(); break;
            }
            break;
        }
    }

    private void DrawSlots()
    {
        GUI.Box(new Rect(130, 60, 1020, 580), GUIContent.none, panelStyle);
        GUI.Label(new Rect(170, 85, 870, 45), page == Page.Save ? "저장할 슬롯 선택" : "이어할 저장 선택", subtitleStyle);
        slots ??= GameSaveService.GetSlots();
        slotScroll = GUI.BeginScrollView(new Rect(170, 145, 940, 400), slotScroll, new Rect(0, 0, 914, slots.Count * 76));
        for (int i = 0; i < slots.Count; i++)
        {
            SaveSlotInfo slot = slots[i];
            string detail = slot.CanLoad ? Describe(slot.Data) : slot.Exists ? slot.Error : "비어 있음";
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && (page == Page.Save ? slot.Index >= 0 : slot.CanLoad);
            if (GUI.Button(new Rect(0, i * 76, 910, 66), $"{slot.Name}\n{detail}", slotStyle))
            {
                audio.PlayButton();
                if (page == Page.Load)
                {
                    if (!GameSession.LoadSlot(slot.Index, out string error)) ShowNotice(error);
                }
                else if (slot.Exists) overwriteSlot = slot.Index;
                else SaveManual(slot.Index);
                GUI.enabled = oldEnabled;
                break;
            }
            GUI.enabled = oldEnabled;
        }
        GUI.EndScrollView();
        if (Button(new Rect(170, 568, 180, 45), "뒤로")) page = returnPage;
        GUI.Label(new Rect(390, 573, 690, 35), page == Page.Save ? "자동 저장은 게임이 최신 상태로 갱신합니다." : "저장된 항목을 선택하면 해당 상태로 시작합니다.", smallStyle);
    }

    private static string Describe(GameSaveData data)
    {
        string time = DateTime.TryParse(data.savedAtUtc, out DateTime saved) ? saved.ToLocalTime().ToString("yyyy.MM.dd HH:mm") : "";
        TimeSpan play = TimeSpan.FromSeconds(Math.Min(data.playSeconds, TimeSpan.MaxValue.TotalSeconds / 2));
        return $"{time}   ·   {(data.area == SaveArea.Dungeon ? "던전" : "마을")}   ·   {data.town.gold}G   ·   플레이 {(long)play.TotalHours:D2}:{play.Minutes:D2}";
    }

    private void SaveManual(int slot)
    {
        if (GameSession.Save(slot, out string error)) { ShowNotice($"저장 슬롯 {slot + 1}에 저장했습니다."); slots = GameSaveService.GetSlots(); }
        else ShowNotice(error);
        overwriteSlot = -2;
    }

    private void DrawOverwriteConfirmation()
    {
        GUI.Box(new Rect(370, 235, 540, 210), GUIContent.none, panelStyle);
        GUI.Label(new Rect(405, 265, 470, 60), $"저장 슬롯 {overwriteSlot + 1}을 현재 상태로 덮어쓸까요?", labelStyle);
        if (Button(new Rect(405, 357, 220, 48), "저장")) SaveManual(overwriteSlot);
        if (Button(new Rect(655, 357, 220, 48), "취소")) overwriteSlot = -2;
    }

    private void DrawOptions()
    {
        GUI.Box(new Rect(190, 50, 900, 590), GUIContent.none, panelStyle);
        GUI.Label(new Rect(230, 72, 760, 40), "옵션", subtitleStyle);
        GUI.Label(new Rect(230, 125, 350, 30), "사운드", subtitleStyle);
        draft.masterVolume = VolumeRow(165, "전체 음량", draft.masterVolume);
        draft.musicVolume = VolumeRow(208, "음악", draft.musicVolume);
        draft.effectsVolume = VolumeRow(251, "효과음", draft.effectsVolume);
        GUI.Label(new Rect(230, 300, 350, 30), "화면과 그래픽", subtitleStyle);
        GUI.Label(new Rect(230, 346, 240, 35), "화면 모드", labelStyle);
        if (Button(new Rect(490, 346, 530, 35), draft.fullscreen ? "전체화면" : "창 모드")) draft.fullscreen = !draft.fullscreen;
        GUI.Label(new Rect(230, 388, 240, 35), "해상도", labelStyle);
        if (Button(new Rect(490, 388, 530, 35), $"{draft.width} × {draft.height}"))
        {
            int index = resolutions.IndexOf(new Vector2Int(draft.width, draft.height));
            Vector2Int next = resolutions[(index + 1) % resolutions.Count];
            draft.width = next.x; draft.height = next.y;
        }
        GUI.Label(new Rect(230, 430, 240, 35), "그래픽 품질", labelStyle);
        string[] quality = QualitySettings.names;
        if (Button(new Rect(490, 430, 530, 35), quality.Length > 0 ? quality[Mathf.Clamp(draft.qualityLevel, 0, quality.Length - 1)] : "기본"))
            draft.qualityLevel = (draft.qualityLevel + 1) % Mathf.Max(1, quality.Length);
        GUI.Label(new Rect(230, 472, 240, 35), "수직 동기화", labelStyle);
        if (Button(new Rect(490, 472, 250, 35), draft.vSync ? "켜짐" : "꺼짐")) draft.vSync = !draft.vSync;
        if (Button(new Rect(770, 472, 250, 35), draft.frameRate == -1 ? "프레임 제한 없음" : $"{draft.frameRate} FPS"))
            draft.frameRate = draft.frameRate == 30 ? 60 : draft.frameRate == 60 ? 120 : draft.frameRate == 120 ? -1 : 30;
        GUI.Label(new Rect(230, 518, 790, 26), "수직 동기화를 켜면 모니터 주사율을 따릅니다.", smallStyle);
        if (Button(new Rect(230, 570, 230, 45), "적용")) { GameSettings.Apply(draft); draft = GameSettings.Copy(); ShowNotice("옵션을 적용했습니다."); }
        if (Button(new Rect(495, 570, 230, 45), "기본값")) draft = GameSettings.Defaults();
        if (Button(new Rect(760, 570, 260, 45), "뒤로")) page = returnPage;
    }

    private float VolumeRow(float y, string label, float volume)
    {
        GUI.Label(new Rect(230, y, 250, 35), label, labelStyle);
        volume = GUI.HorizontalSlider(new Rect(490, y + 12, 450, 20), volume, 0f, 1f);
        GUI.Label(new Rect(960, y, 70, 35), $"{Mathf.RoundToInt(volume * 100)}%", labelStyle);
        return volume;
    }
}
