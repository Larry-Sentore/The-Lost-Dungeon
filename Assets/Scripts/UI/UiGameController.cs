using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Connects the scene's existing UI screens to the game. Screens are shown and hidden by reacting to
// GameStateChanged (Observer), so gameplay code never touches the UI directly.
public class UiGameController : MonoBehaviour
{
    private static int levelToStartAfterReload;
    private static bool openLevelSelectionAfterReload;

    private Canvas canvas;
    private GameObject mainMenu;
    private GameObject settings;
    private GameObject gameplayUI;
    private Transform objectiveText;
    private GameObject[] healthStages;
    private GameObject pauseMenu;
    private GameObject levelCompleteScreen;
    private GameObject gameOverScreen;
    private GameObject levelSelection;
    private Button continueButton;
    private Button nextLevelButton;
    private TextMeshProUGUI resultText;
    private HudView hud;
    private bool returnToPauseAfterSettings;
    private float currentHealth = 100f;
    private float maximumHealth = 100f;
    private int lastPlayedLevel;

    // Finds all UI screens and buttons, connects buttons to actions, listens for game events, and shows the main menu.
    private void Awake()
    {
        Canvas found = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        canvas = found != null ? found.rootCanvas : null;
        PlatformBehaviours.ConfigureCanvas(canvas);

        mainMenu = FindCanvasChild("Start");
        settings = FindCanvasChild("Settings");
        gameplayUI = FindCanvasChild("Ingame_UI");
        objectiveText = FindNamedTransform("Objective_text");
        pauseMenu = FindCanvasChild("pause");
        levelCompleteScreen = FindCanvasChild("Success");
        gameOverScreen = FindCanvasChild("Fail");
        levelSelection = FindCanvasChild("Levels");
        healthStages = new[] { FindNamedObject("100"), FindNamedObject("75"), FindNamedObject("50"), FindNamedObject("25") };

        HookButton("Start_btn", StartNewGame);
        continueButton = HookButton("Continue_btn", ContinueGame);
        HookButton("Settings_btn", OpenSettings);
        HookButton("Exit_btn", ExitGame);
        HookButton("Levels_btn", OpenLevelSelection);
        HookButton("back_btn", CloseLevelSelection, levelSelection);
        HookButton("Button", CloseSettings, settings);
        HookButton("Continue", ResumeGame, pauseMenu);
        HookButton("Settings", OpenPauseSettings, pauseMenu);
        HookButton("Main Menu", ReturnToMainMenu, pauseMenu);
        HookButton("success_continue_btn", ReloadToLevelSelection, levelCompleteScreen);
        HookButton("fail_continue_btn", ReloadToLevelSelection, gameOverScreen);
        for (int level = 1; level <= ProgressionRules.LevelCount; level++)
        {
            int levelNumber = level;
            HookButton($"Level_{levelNumber}_Unlocked", () => StartLevel(levelNumber), levelSelection);
            HookButton($"Level_{levelNumber}_Complete", () => StartLevel(levelNumber), levelSelection);
            HookButton($"Level_{levelNumber}_Locked", () => GameEvents.RaiseUiSound("error"), levelSelection);
        }

        CreateResultScreenExtras();
        WireSettings();
        AddClickSounds();
        if (canvas != null)
        {
            hud = canvas.gameObject.GetComponent<HudView>();
            if (hud == null)
            {
                hud = canvas.gameObject.AddComponent<HudView>();
            }
            hud.Build(canvas, gameplayUI);
            if (PlatformBehaviours.UseTouchControls)
            {
                TouchControls.Create(canvas);
            }
        }

        GameEvents.HealthChanged += OnHealthChanged;
        GameEvents.ObjectiveChanged += OnObjectiveChanged;
        GameEvents.LevelUnlockChanged += OnLevelUnlockChanged;
        GameEvents.GameStateChanged += OnGameStateChanged;
        GameEvents.LevelResultReady += OnLevelResultReady;
        GameEvents.LevelStarted += OnLevelStarted;

        ShowMainMenu();
    }

    // After a scene reload, starts a level or opens level select if that was requested.
    private void Start()
    {
        if (levelToStartAfterReload > 0)
        {
            int level = levelToStartAfterReload;
            levelToStartAfterReload = 0;
            StartCoroutine(RunNextFrame(() => StartLevel(level)));
        }
        else if (openLevelSelectionAfterReload)
        {
            openLevelSelectionAfterReload = false;
            StartCoroutine(RunNextFrame(OpenLevelSelection));
        }
    }

    // Escape / P / Start closes menus, or pauses and resumes the game.
    private void Update()
    {
        if (!PlayerInputRouter.PausePressed())
        {
            return;
        }
        if (settings != null && settings.activeSelf)
        {
            CloseSettings();
        }
        else if (levelSelection != null && levelSelection.activeSelf)
        {
            CloseLevelSelection();
        }
        else if (GameManager.Instance != null)
        {
            GameManager.Instance.TogglePause();
        }
    }

    // Stops listening to game events.
    private void OnDestroy()
    {
        GameEvents.HealthChanged -= OnHealthChanged;
        GameEvents.ObjectiveChanged -= OnObjectiveChanged;
        GameEvents.LevelUnlockChanged -= OnLevelUnlockChanged;
        GameEvents.GameStateChanged -= OnGameStateChanged;
        GameEvents.LevelResultReady -= OnLevelResultReady;
        GameEvents.LevelStarted -= OnLevelStarted;
    }

    // Shows the right screens for the new game state.
    private void OnGameStateChanged(GameStateId previous, GameStateId next)
    {
        switch (next)
        {
            case GameStateId.Playing:
                SetActive(mainMenu, false);
                SetActive(levelSelection, false);
                SetActive(settings, false);
                SetActive(pauseMenu, false);
                HideResultScreens();
                SetGameplayUIVisible(true);
                break;
            case GameStateId.Paused:
                SetActive(pauseMenu, true);
                break;
            case GameStateId.LevelComplete:
                SetActive(pauseMenu, false);
                SetGameplayUIVisible(false);
                SetActive(levelCompleteScreen, true);
                break;
            case GameStateId.GameOver:
                SetActive(pauseMenu, false);
                SetGameplayUIVisible(false);
                SetActive(gameOverScreen, true);
                break;
            case GameStateId.MainMenu:
                ShowMainMenu();
                break;
        }
    }

    // Shows only the main menu.
    private void ShowMainMenu()
    {
        SetActive(mainMenu, true);
        SetActive(levelSelection, false);
        SetActive(settings, false);
        SetActive(pauseMenu, false);
        HideResultScreens();
        SetGameplayUIVisible(false);
        RefreshContinueButton();
        RefreshLevelStates();
    }

    // Starts a new game: clears saved progress (settings are kept) and opens level select.
    private void StartNewGame()
    {
        SaveSystem.ResetProgress();
        GameEvents.RaiseNotification("New adventure started");
        OpenLevelSelection();
    }

    // Opens level select with the saved progress.
    private void ContinueGame()
    {
        OpenLevelSelection();
    }

    // Starts a level if it is unlocked. The screens switch when the state changes to Playing.
    private void StartLevel(int level)
    {
        if (!ProgressionRules.IsUnlocked(SaveSystem.Data, level))
        {
            GameEvents.RaiseUiSound("error");
            return;
        }
        GameManager manager = GameManager.Instance;
        if (manager == null || !manager.StartLevel(level))
        {
            GameEvents.RaiseNotification("That level could not be started.");
            return;
        }
        GameEvents.RaiseUiSound("confirm");
        returnToPauseAfterSettings = false;
    }

    // Remembers which level is being played (for Retry and Next Level).
    private void OnLevelStarted(LevelDefinition level)
    {
        lastPlayedLevel = level.Number;
    }

    // Hides the level complete and game over screens.
    private void HideResultScreens()
    {
        SetActive(levelCompleteScreen, false);
        SetActive(gameOverScreen, false);
    }

    // Opens settings from the main menu.
    private void OpenSettings()
    {
        returnToPauseAfterSettings = false;
        SetActive(settings, true);
    }

    // Opens settings from the pause menu.
    private void OpenPauseSettings()
    {
        returnToPauseAfterSettings = true;
        SetActive(pauseMenu, false);
        SetActive(settings, true);
    }

    // Closes settings, saves them, and goes back to the screen we came from.
    private void CloseSettings()
    {
        SetActive(settings, false);
        if (returnToPauseAfterSettings)
        {
            SetActive(pauseMenu, true);
        }
        returnToPauseAfterSettings = false;
        SaveSystem.Save();
    }

    // Updates the level icons and shows the level select screen.
    private void OpenLevelSelection()
    {
        RefreshLevelStates();
        SetActive(levelSelection, true);
        SetActive(mainMenu, false);
    }

    // Hides level select and shows the main menu.
    private void CloseLevelSelection()
    {
        SetActive(levelSelection, false);
        if (GameManager.Instance == null || GameManager.Instance.State == GameStateId.MainMenu)
        {
            SetActive(mainMenu, true);
            RefreshContinueButton();
        }
    }

    // Resumes from the pause menu.
    private void ResumeGame()
    {
        GameManager.Instance?.ChangeState(GameStateId.Playing);
    }

    // Quits the game (or stops play mode in the editor).
    private void ExitGame()
    {
        SaveSystem.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
        // Browsers do not let a page close itself, so go back to the menu instead.
        ReturnToMainMenu();
#else
        Application.Quit();
#endif
    }

    // Shows the health bar image that matches the player's current health.
    private void OnHealthChanged(float current, float maximum)
    {
        currentHealth = current;
        maximumHealth = maximum;
        bool visible = gameplayUI != null && gameplayUI.activeSelf;
        float percent = maximum <= 0f ? 0f : current / maximum * 100f;
        int activeStage = percent > 75f ? 0 : percent > 50f ? 1 : percent > 25f ? 2 : 3;
        for (int i = 0; i < healthStages.Length; i++)
        {
            if (healthStages[i] != null)
            {
                healthStages[i].SetActive(visible && percent > 0f && i == activeStage);
            }
        }
        hud?.SetHealth(current, maximum);
    }

    // Updates the objective text on screen.
    private void OnObjectiveChanged(string objective)
    {
        if (objectiveText == null)
        {
            return;
        }
        TMP_Text tmpText = objectiveText.GetComponent<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = objective;
        }
        Text text = objectiveText.GetComponent<Text>();
        if (text != null)
        {
            text.text = objective;
        }
    }

    // Fills in the success screen with the score, rank, time and anything unlocked.
    private void OnLevelResultReady(LevelResult result)
    {
        RefreshLevelStates();
        if (resultText != null)
        {
            int minutes = Mathf.FloorToInt(result.Seconds / 60f);
            int seconds = Mathf.FloorToInt(result.Seconds % 60f);
            string rank = result.Rank > 0 ? $"   Rank #{result.Rank}" : "";
            string unlocked = string.IsNullOrEmpty(result.Unlocked) ? "" : $"\nUnlocked: {result.Unlocked}";
            resultText.text = $"Score {result.Score}{rank}\nTime {minutes:00}:{seconds:00}   Gems +{result.GemsCollected}{unlocked}";
        }
        if (nextLevelButton != null)
        {
            bool hasNext = result.Level < ProgressionRules.LevelCount;
            nextLevelButton.gameObject.SetActive(hasNext);
        }
    }

    // Updates a level's icon when it gets unlocked.
    private void OnLevelUnlockChanged(int level, bool unlocked)
    {
        RefreshLevelState(level, SaveSystem.Data);
    }

    // Updates every level icon from the save data.
    private void RefreshLevelStates()
    {
        for (int level = 1; level <= ProgressionRules.LevelCount; level++)
        {
            RefreshLevelState(level, SaveSystem.Data);
        }
    }

    // Shows the right icon for a level: completed, available, or locked. Icons, not text, show the state.
    private void RefreshLevelState(int level, SaveData data)
    {
        LevelStatus status = ProgressionRules.GetStatus(data, level);
        SetLevelVisual(level, "Locked", status == LevelStatus.Locked);
        SetLevelVisual(level, "Unlocked", status == LevelStatus.Available);
        SetLevelVisual(level, "Complete", status == LevelStatus.Completed);
    }

    // Turns one level icon on or off.
    private void SetLevelVisual(int level, string state, bool active)
    {
        Transform visual = levelSelection != null ? FindDescendant(levelSelection.transform, $"Level_{level}_{state}") : null;
        if (visual != null)
        {
            visual.gameObject.SetActive(active);
        }
    }

    // Greys out Continue when there is no saved progress.
    private void RefreshContinueButton()
    {
        if (continueButton != null)
        {
            continueButton.interactable = SaveSystem.HasProgress();
        }
    }

    // Shows or hides the in-game HUD.
    private void SetGameplayUIVisible(bool visible)
    {
        SetActive(gameplayUI, visible);
        OnHealthChanged(currentHealth, maximumHealth);
    }

    // Adds a score line and Next Level / Retry buttons to the result screens, copied from the existing buttons.
    private void CreateResultScreenExtras()
    {
        if (levelCompleteScreen != null)
        {
            resultText = CreateText(levelCompleteScreen.transform, "Result Text", new Vector2(0f, 40f), new Vector2(900f, 200f), 40f);
            Transform continueButtonTransform = FindDescendant(levelCompleteScreen.transform, "success_continue_btn");
            nextLevelButton = CloneButton(continueButtonTransform, "next_level_btn", "Next Level", new Vector2(0f, -150f), () =>
            {
                levelToStartAfterReload = Mathf.Min(ProgressionRules.LevelCount, lastPlayedLevel + 1);
                ReloadScene();
            });
        }
        if (gameOverScreen != null)
        {
            Transform failButton = FindDescendant(gameOverScreen.transform, "fail_continue_btn");
            CloneButton(failButton, "retry_btn", "Retry", new Vector2(0f, -150f), () =>
            {
                levelToStartAfterReload = Mathf.Max(1, lastPlayedLevel);
                ReloadScene();
            });
        }
    }

    // Copies an existing button (keeping its look), changes its label and position, and gives it a new action.
    private static Button CloneButton(Transform source, string name, string label, Vector2 offset, UnityEngine.Events.UnityAction action)
    {
        if (source == null)
        {
            return null;
        }
        GameObject copy = Instantiate(source.gameObject, source.parent);
        copy.name = name;
        RectTransform rect = (RectTransform)copy.transform;
        rect.anchoredPosition = ((RectTransform)source).anchoredPosition + offset;
        foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true))
        {
            text.text = label;
        }
        Button button = copy.GetComponent<Button>();
        if (button != null)
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(action);
        }
        return button;
    }

    // Connects the existing Settings controls: the slider is volume, the toggle is music on/off,
    // and the text field is the player's name for the leaderboard.
    private void WireSettings()
    {
        if (settings == null)
        {
            return;
        }
        SettingsData data = SaveSystem.Data.Settings;

        Slider slider = settings.GetComponentInChildren<Slider>(true);
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(data.MasterVolume);
            slider.onValueChanged.AddListener(value =>
            {
                SaveSystem.Data.Settings.MasterVolume = value;
                GameEvents.RaiseSettingsChanged(SaveSystem.Data.Settings);
            });
            RectTransform sliderRect = (RectTransform)slider.transform;
            CreateText(settings.transform, "Volume Label", sliderRect.anchoredPosition + new Vector2(-sliderRect.sizeDelta.x * 0.5f - 130f, 0f),
                new Vector2(220f, 60f), 34f).text = "Volume";
        }

        Toggle toggle = settings.GetComponentInChildren<Toggle>(true);
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(data.MusicEnabled);
            toggle.onValueChanged.AddListener(on =>
            {
                SaveSystem.Data.Settings.MusicEnabled = on;
                GameEvents.RaiseSettingsChanged(SaveSystem.Data.Settings);
            });
            Text legacyLabel = toggle.GetComponentInChildren<Text>(true);
            if (legacyLabel != null)
            {
                legacyLabel.text = "Music";
            }
            TMP_Text tmpLabel = toggle.GetComponentInChildren<TMP_Text>(true);
            if (tmpLabel != null)
            {
                tmpLabel.text = "Music";
            }
        }

        TMP_InputField nameField = settings.GetComponentInChildren<TMP_InputField>(true);
        if (nameField != null)
        {
            nameField.characterLimit = 16;
            nameField.SetTextWithoutNotify(data.PlayerName);
            nameField.onEndEdit.AddListener(value =>
            {
                string trimmed = string.IsNullOrWhiteSpace(value) ? "Adventurer" : value.Trim();
                SaveSystem.Data.Settings.PlayerName = trimmed;
                nameField.SetTextWithoutNotify(trimmed);
                GameEvents.RaiseSettingsChanged(SaveSystem.Data.Settings);
            });
        }
    }

    // Makes every button in the UI play a click (back buttons play a softer "cancel" sound).
    private void AddClickSounds()
    {
        if (canvas == null)
        {
            return;
        }
        foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
        {
            string objectName = button.gameObject.name.ToLowerInvariant();
            bool isBack = objectName.Contains("back") || objectName == "button" || objectName.Contains("main menu");
            bool isLocked = objectName.Contains("locked") && !objectName.Contains("unlocked");
            if (isLocked)
            {
                continue;
            }
            string sound = isBack ? "cancel" : "select";
            button.onClick.AddListener(() => GameEvents.RaiseUiSound(sound));
        }
    }

    // Creates a centred TextMeshPro label under a parent.
    private static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // Finds a button by name anywhere in the scene and makes it call an action when clicked.
    private Button HookButton(string name, UnityEngine.Events.UnityAction action)
    {
        Transform target = FindNamedTransform(name);
        if (target == null)
        {
            Debug.LogWarning($"UI button '{name}' was not found in the loaded scene.");
            return null;
        }
        return HookButton(target.gameObject, action);
    }

    // Same as above, but only searches inside one parent screen.
    private Button HookButton(string name, UnityEngine.Events.UnityAction action, GameObject parent)
    {
        if (parent == null)
        {
            return null;
        }
        Transform target = FindDescendant(parent.transform, name);
        return target != null ? HookButton(target.gameObject, action) : null;
    }

    // Connects a button object to an action.
    private static Button HookButton(GameObject target, UnityEngine.Events.UnityAction action)
    {
        Button button = target.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
        return button;
    }

    // Turns an object on or off if it exists.
    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    // Finds a UI object by name.
    private GameObject FindNamedObject(string name)
    {
        Transform found = FindNamedTransform(name);
        return found != null ? found.gameObject : null;
    }

    // Finds an object by name, looking in the Canvas first.
    private Transform FindNamedTransform(string name)
    {
        if (canvas != null)
        {
            Transform match = FindDescendant(canvas.transform, name);
            if (match != null)
            {
                return match;
            }
        }
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.name == name)
            {
                return t;
            }
        }
        return null;
    }

    // Finds a direct child of the Canvas by name.
    private GameObject FindCanvasChild(string name)
    {
        if (canvas == null)
        {
            return null;
        }
        Transform child = canvas.transform.Find(name);
        return child != null ? child.gameObject : null;
    }

    // Searches a parent and all of its children for an object with this name.
    private static Transform FindDescendant(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform match = FindDescendant(parent.GetChild(i), name);
            if (match != null)
            {
                return match;
            }
        }
        return null;
    }

    // Reloads the scene to reset everything.
    private void ReloadScene()
    {
        SaveSystem.Save();
        GameManager.Instance?.LevelManager?.StopTrackingForSceneReload();
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.buildIndex >= 0)
        {
            SceneManager.LoadScene(activeScene.buildIndex);
        }
        else
        {
            SceneManager.LoadScene(activeScene.name);
        }
    }

    // Waits one frame (so every system has started), then runs an action.
    private static System.Collections.IEnumerator RunNextFrame(System.Action action)
    {
        yield return null;
        action();
    }

    // Reloads the scene and then opens level select.
    private void ReloadToLevelSelection()
    {
        levelToStartAfterReload = 0;
        openLevelSelectionAfterReload = true;
        ReloadScene();
    }

    // Reloads the scene and goes back to the main menu.
    private void ReturnToMainMenu()
    {
        levelToStartAfterReload = 0;
        openLevelSelectionAfterReload = false;
        ReloadScene();
    }
}
