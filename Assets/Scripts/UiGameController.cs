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
        settings = FindNamedObject("Settings");
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
        if (mainMenu != null)
        {
            mainMenu.SetActive(false);
        }
        levelManager?.StartLevel(data.HighestUnlockedLevel);
    }

    private void OpenSettings()
    {
        if (settings != null)
        {
            settings.SetActive(true);
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
        for (int i = 0; i < healthStages.Length; i++)
        {
            if (healthStages[i] == null)
            {
                continue;
            }
            int threshold = new[] { 100, 75, 50, 25 }[i];
            healthStages[i].SetActive(percent >= threshold);
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
