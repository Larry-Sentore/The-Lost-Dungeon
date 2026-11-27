using System;
using System.Collections.Generic;
using UnityEngine;

// Runs one level at a time: turns on that level's enemy group, tracks the enemies, spawns the
// treasure chest when they are all defeated, and records the result when the chest is opened.
[DisallowMultipleComponent]
public class LevelManager : MonoBehaviour
{
    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private readonly GameObject[] enemyGroups = new GameObject[ProgressionRules.LevelCount];
    private LevelDefinition currentDefinition;
    private int currentLevel;
    private bool levelFinished;
    private float levelStartTime;
    private int damageTaken;
    private int gemsAtStart;
    private PlayerController player;
    private TreasureChest chest;

    public int CurrentLevel => currentLevel;
    public LevelDefinition CurrentDefinition => currentDefinition;
    public int AliveEnemyCount => activeEnemies.Count;
    public float ElapsedTime => currentLevel > 0 ? Time.time - levelStartTime : 0f;
    public IReadOnlyList<EnemyController> ActiveEnemies => activeEnemies;

    // Finds the enemy group for each level and hides them all until a level starts.
    private void Awake()
    {
        for (int level = 1; level <= enemyGroups.Length; level++)
        {
            enemyGroups[level - 1] = FindSceneObject($"Level_{level}_Enemies");
            if (enemyGroups[level - 1] == null)
            {
                Debug.LogError($"Level enemy group 'Level_{level}_Enemies' was not found.");
            }
            else
            {
                enemyGroups[level - 1].SetActive(false);
            }
        }
        GameEvents.PlayerDamaged += OnPlayerDamaged;
    }

    // Tells the UI which levels are locked or unlocked.
    private void Start()
    {
        for (int level = 1; level <= ProgressionRules.LevelCount; level++)
        {
            GameEvents.RaiseLevelUnlockChanged(level, ProgressionRules.IsUnlocked(SaveSystem.Data, level));
        }
    }

    // Stops listening to enemies and events.
    private void OnDestroy()
    {
        GameEvents.PlayerDamaged -= OnPlayerDamaged;
        UnsubscribeFromActiveEnemies();
    }

    // Stops tracking enemies when the game closes, so their destruction does not count as a win.
    private void OnApplicationQuit()
    {
        StopTrackingForSceneReload();
    }

    // Stops tracking the current level so reloading the scene does not count as finishing it.
    public void StopTrackingForSceneReload()
    {
        levelFinished = true;
        currentLevel = 0;
        UnsubscribeFromActiveEnemies();
        activeEnemies.Clear();
    }

    // Starts a level if it is unlocked: moves the player to the spawn point, turns on that level's
    // enemies and tracks them. Returns false (with a log message) if it could not start.
    public bool StartLevel(int level)
    {
        LevelDefinition definition = LevelCatalog.Get(level);
        if (definition == null || !ProgressionRules.IsUnlocked(SaveSystem.Data, level))
        {
            return false;
        }
        GameObject group = enemyGroups[level - 1];
        if (group == null)
        {
            Debug.LogError($"Cannot start level {level}: its scene enemy group is missing.");
            return false;
        }
        Transform spawnPoint = FindSpawnPoint(level);
        player = FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (spawnPoint == null || player == null)
        {
            Debug.LogError($"Cannot start level {level}: missing SpawnPoint_Level_{level} or the player.");
            return false;
        }

        ClearActiveLevel();
        group.SetActive(true);
        List<EnemyController> enemies = PrepareGroupEnemies(group);
        if (enemies.Count == 0)
        {
            Debug.LogError($"Cannot start level {level}: '{group.name}' contains no enemies.");
            group.SetActive(false);
            return false;
        }

        currentLevel = level;
        currentDefinition = definition;
        levelFinished = false;
        levelStartTime = Time.time;
        damageTaken = 0;
        gemsAtStart = player.Inventory.Count(ItemCatalog.Gem);
        player.PrepareForLevel(spawnPoint.position);

        foreach (EnemyController enemy in enemies)
        {
            enemy.Configure(enemy.Kind);
            enemy.Died += HandleEnemyDied;
            activeEnemies.Add(enemy);
        }

        GameEvents.RaiseLevelStarted(definition);
        GameEvents.RaiseEnemyDefeated(activeEnemies.Count);
        UpdateObjective();
        return true;
    }

    // Finds the spawn point object for a level.
    private static Transform FindSpawnPoint(int level)
    {
        GameObject spawnPoint = FindSceneObject($"SpawnPoint_Level_{level}");
        return spawnPoint != null ? spawnPoint.transform : null;
    }

    // Finds any object in the scene by name, including hidden ones.
    private static GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject sceneObject in FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (sceneObject.name == objectName && sceneObject.scene.IsValid())
            {
                return sceneObject;
            }
        }
        return null;
    }

    // Gets every enemy in a level group. Enemies placed with an EnemyController keep the type set in the
    // Inspector; objects without one get a type guessed from their name.
    private static List<EnemyController> PrepareGroupEnemies(GameObject group)
    {
        List<EnemyController> enemies = new List<EnemyController>();
        for (int i = 0; i < group.transform.childCount; i++)
        {
            Transform enemyRoot = group.transform.GetChild(i);
            EnemyController[] controllers = enemyRoot.GetComponentsInChildren<EnemyController>(true);
            if (controllers.Length == 0)
            {
                controllers = new[] { EnemyController.Setup(enemyRoot.gameObject, GetKindFromName(enemyRoot.name)) };
            }
            foreach (EnemyController controller in controllers)
            {
                if (controller != null && !controller.IsDead && !enemies.Contains(controller))
                {
                    enemies.Add(controller);
                }
            }
        }
        return enemies;
    }

    // Guesses the enemy type from the object's name (only used when no EnemyController was set up).
    public static EnemyKind GetKindFromName(string objectName)
    {
        if (objectName.IndexOf("Boss", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return EnemyKind.GiantCannonBoss;
        }
        if (objectName.IndexOf("Golem", StringComparison.OrdinalIgnoreCase) >= 0 ||
            objectName.IndexOf("Giant", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return EnemyKind.RockGiant;
        }
        if (objectName.IndexOf("Cannon", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return EnemyKind.CannonRobot;
        }
        return EnemyKind.Skeleton;
    }

    // Counts damage taken this level (it lowers the score).
    private void OnPlayerDamaged(int amount)
    {
        if (currentLevel > 0 && !levelFinished)
        {
            damageTaken += amount;
        }
    }

    // Called when an enemy dies. When the last one falls, a treasure chest appears where it died.
    private void HandleEnemyDied(EnemyController enemy)
    {
        enemy.Died -= HandleEnemyDied;
        if (currentLevel <= 0 || levelFinished || !activeEnemies.Remove(enemy))
        {
            return;
        }
        SaveSystem.Data.Stats.EnemiesDefeated++;
        GameEvents.RaiseEnemyDefeated(activeEnemies.Count);
        if (activeEnemies.Count > 0)
        {
            UpdateObjective();
            return;
        }

        Vector3 position = enemy.transform.position;
        Vector3 facePlayer = player != null ? player.transform.position - position : Vector3.forward;
        facePlayer.y = 0f;
        Quaternion rotation = facePlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facePlayer) : Quaternion.identity;
        chest = TreasureChest.Spawn(position, rotation, currentDefinition.ChestGems, currentDefinition.ChestGold, CompleteLevel);
        GameEvents.RaiseObjectiveChanged("All enemies defeated! Open the treasure chest");
        GameEvents.RaiseNotification("A treasure chest has appeared!");
    }

    // Updates the objective text with how many enemies are left.
    private void UpdateObjective()
    {
        int left = activeEnemies.Count;
        GameEvents.RaiseObjectiveChanged($"Defeat the enemies ({left} left), then claim the treasure");
    }

    // Finishes the level: works out the score, saves progress and rewards, and tells everyone.
    private void CompleteLevel()
    {
        if (levelFinished || currentLevel <= 0)
        {
            return;
        }
        levelFinished = true;
        SaveData data = SaveSystem.Data;
        float seconds = Time.time - levelStartTime;
        int gems = Mathf.Max(0, player.Inventory.Count(ItemCatalog.Gem) - gemsAtStart);
        int bounty = BestiaryService.Instance != null
            ? BestiaryService.Instance.GetBounty(currentDefinition.FeaturedEnemy)
            : EnemyProfiles.Get(currentDefinition.FeaturedEnemy).Bounty;
        int score = ProgressionRules.CalculateScore(currentLevel, seconds, gems, damageTaken, bounty);

        int newlyUnlocked = ProgressionRules.MarkCompleted(data, currentLevel);
        data.Stats.TotalScore += score;
        float best = data.Stats.BestTimes[currentLevel - 1];
        if (best <= 0f || seconds < best)
        {
            data.Stats.BestTimes[currentLevel - 1] = seconds;
        }
        string unlocked = null;
        if (!string.IsNullOrEmpty(currentDefinition.Unlockable) && !data.Unlockables.Contains(currentDefinition.Unlockable))
        {
            data.Unlockables.Add(currentDefinition.Unlockable);
            unlocked = currentDefinition.Unlockable;
        }

        LeaderboardService leaderboard = GetComponent<LeaderboardService>();
        int rank = leaderboard != null ? leaderboard.Record(data.Settings.PlayerName, score, currentLevel) : 0;
        SaveSystem.Save();

        if (newlyUnlocked > 0)
        {
            GameEvents.RaiseLevelUnlockChanged(newlyUnlocked, true);
        }
        GameEvents.RaiseObjectiveChanged($"Level {currentLevel} complete");
        GameEvents.RaiseLevelResultReady(new LevelResult
        {
            Level = currentLevel,
            Seconds = seconds,
            Score = score,
            Rank = rank,
            GemsCollected = gems,
            Unlocked = unlocked
        });
        GameEvents.RaiseLevelCompleted(currentLevel);
    }

    // Stops tracking the current enemies, removes the chest and hides all enemy groups.
    private void ClearActiveLevel()
    {
        UnsubscribeFromActiveEnemies();
        activeEnemies.Clear();
        if (chest != null)
        {
            Destroy(chest.gameObject);
            chest = null;
        }
        foreach (GameObject group in enemyGroups)
        {
            if (group != null)
            {
                group.SetActive(false);
            }
        }
    }

    // Stops listening to every tracked enemy.
    private void UnsubscribeFromActiveEnemies()
    {
        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
            }
        }
    }
}
