using UnityEngine;
using UnityEngine.UI;

public class UiGameController : MonoBehaviour
{
    private GameObject mainMenu;
    private GameObject settings;
    private GameObject healthBar;
    private Transform objectiveText;
    private GameObject[] healthStages;
    private LevelManager levelManager;

    private void Awake()
    {
        levelManager = FindAnyObjectByType<LevelManager>();
        mainMenu = FindNamedObject("Main Menu");
        settings = FindNamedObject("Settings", preferInactive: true);
        healthBar = FindNamedObject("healthbar");
        objectiveText = FindNamedTransform("Objective_text");
        healthStages = new[]
        {
            FindNamedObject("100"),
            FindNamedObject("75"),
            FindNamedObject("50"),
            FindNamedObject("25")
        };

        HookButton("Start_btn", StartNewGame);
        HookButton("Continue", ContinueGame);
        HookButton("Continue_btn", ContinueGame);
        HookButton("Settings_btn", OpenSettings);
        HookButton("Exit_btn", ExitGame);
        HookButton("back_btn", CloseSettings);
        for (int level = 1; level <= 5; level++)
        {
            int levelNumber = level;
            HookButton($"Level_{levelNumber}_Unlocked", () => StartLevel(levelNumber));
        }

        GameEvents.HealthChanged += OnHealthChanged;
        GameEvents.ObjectiveChanged += OnObjectiveChanged;
        GameEvents.LevelCompleted += OnLevelCompleted;
        GameEvents.GameOver += OnGameOver;
        GameEvents.LevelUnlockChanged += OnLevelUnlockChanged;
    }

    private void OnDestroy()
    {
        GameEvents.HealthChanged -= OnHealthChanged;
        GameEvents.ObjectiveChanged -= OnObjectiveChanged;
        GameEvents.LevelCompleted -= OnLevelCompleted;
        GameEvents.GameOver -= OnGameOver;
        GameEvents.LevelUnlockChanged -= OnLevelUnlockChanged;
    }

    private void StartNewGame()
    {
        SaveData data = JsonProgressStore.Load() ?? new SaveData();
        data.PlayerHealth = 100f;
        JsonProgressStore.Save(data);
        if (mainMenu != null)
        {
            mainMenu.SetActive(false);
        }
        levelManager?.StartLevel(1);
    }

    private void ContinueGame()
    {
        SaveData data = JsonProgressStore.Load() ?? new SaveData();
        data.PlayerHealth = 100f;
        JsonProgressStore.Save(data);
        if (mainMenu != null)
        {
            mainMenu.SetActive(false);
        }
        levelManager?.StartLevel(data.HighestUnlockedLevel);
    }

    private void StartLevel(int level)
    {
        SaveData data = JsonProgressStore.Load() ?? new SaveData();
        if (level > data.HighestUnlockedLevel)
        {
            return;
        }
        data.PlayerHealth = 100f;
        JsonProgressStore.Save(data);
        if (mainMenu != null)
        {
            mainMenu.SetActive(false);
        }
        levelManager?.StartLevel(level);
    }

    private void OpenSettings()
    {
        if (settings != null)
        {
            settings.SetActive(true);
        }
    }

    private void CloseSettings()
    {
        if (settings != null)
        {
            settings.SetActive(false);
        }
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnHealthChanged(float current, float maximum)
    {
        if (healthStages == null)
        {
            return;
        }

        float percent = maximum <= 0f ? 0f : current / maximum * 100f;
        int activeStage = percent > 75f ? 0 : percent > 50f ? 1 : percent > 25f ? 2 : 3;
        for (int i = 0; i < healthStages.Length; i++)
        {
            if (healthStages[i] == null)
            {
                continue;
            }
            healthStages[i].SetActive(percent > 0f && i == activeStage);
        }
    }

    private void OnObjectiveChanged(string objective)
    {
        if (objectiveText == null)
        {
            return;
        }

        Text text = objectiveText.GetComponent<Text>();
        if (text != null)
        {
            text.text = objective;
        }

        TMPro.TMP_Text tmpText = objectiveText.GetComponent<TMPro.TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = objective;
        }
    }

    private void OnLevelCompleted(int level)
    {
        OnObjectiveChanged($"Level {level} complete");
    }

    private void OnGameOver()
    {
        OnObjectiveChanged("Game over");
        SaveData data = JsonProgressStore.Load() ?? new SaveData();
        data.PlayerHealth = 0f;
        JsonProgressStore.Save(data);
    }

    private void OnLevelUnlockChanged(int level, bool unlocked)
    {
        GameObject unlockedVisual = FindNamedObject($"Level_{level}_Unlocked");
        GameObject lockedVisual = FindNamedObject($"Level_{level}_Locked");
        if (unlockedVisual != null)
        {
            unlockedVisual.SetActive(unlocked);
        }
        if (lockedVisual != null)
        {
            lockedVisual.SetActive(!unlocked);
        }
    }

    private void HookButton(string name, UnityEngine.Events.UnityAction action)
    {
        GameObject target = FindNamedObject(name);
        if (target == null)
        {
            return;
        }
        Button button = target.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private static GameObject FindNamedObject(string name)
    {
        return FindNamedTransform(name)?.gameObject;
    }

    private static GameObject FindNamedObject(string name, bool preferInactive)
    {
        Transform selected = null;
        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name != name)
            {
                continue;
            }
            if (!preferInactive)
            {
                return transform.gameObject;
            }
            if (!transform.gameObject.activeSelf)
            {
                return transform.gameObject;
            }
            selected = transform;
        }
        return selected?.gameObject;
    }

    private static Transform FindNamedTransform(string name)
    {
        foreach (Transform transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (transform.name == name)
            {
                return transform;
            }
        }
        return null;
    }
}
