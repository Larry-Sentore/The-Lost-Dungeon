using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LevelManager : MonoBehaviour
{
    private readonly Dictionary<int, EnemyComposition> compositions = new Dictionary<int, EnemyComposition>();
    private readonly List<EnemyController> activeEnemies = new List<EnemyController>();
    private int currentLevel = 1;
    private AssetBundleLevelLoader assetLoader;

    public int CurrentLevel => currentLevel;
    public int AliveEnemyCount => activeEnemies.Count;

    private void Awake()
    {
        assetLoader = gameObject.GetComponent<AssetBundleLevelLoader>();
        if (assetLoader == null)
        {
            assetLoader = gameObject.AddComponent<AssetBundleLevelLoader>();
        }
        compositions[1] = new EnemyComposition(1, 0, 0, 0);
        compositions[2] = new EnemyComposition(2, 1, 0, 0);
        compositions[3] = new EnemyComposition(0, 0, 2, 0);
        compositions[4] = new EnemyComposition(3, 2, 2, 0);
        compositions[5] = new EnemyComposition(0, 5, 0, 1);
    }

    private void Start()
    {
        SaveData data = JsonProgressStore.Load();
        if (data != null)
        {
            ApplyLockedLevelVisuals(data.HighestUnlockedLevel);
        }
    }

    public void StartLevel(int level)
    {
        SaveData data = JsonProgressStore.Load();
        int unlocked = data?.HighestUnlockedLevel ?? 1;
        if (level < 1 || level > 5)
        {
            return;
        }
        if (level > unlocked)
        {
            return;
        }

        currentLevel = level;
        assetLoader?.LoadLevelBundle(level);
        ClearExistingEnemyObjects();
        ClearLevelEnemies();
        BuildLevel(level);
        GameEvents.RaiseObjectiveChanged($"Defeat every enemy in level {level}");
        SaveData current = data ?? new SaveData();
        current.CompletedLevel = data?.CompletedLevel ?? 0;
        current.HighestUnlockedLevel = unlocked;
        JsonProgressStore.Save(current);
    }

    private void BuildLevel(int level)
    {
        EnemyComposition composition = compositions[level];
        EnemyController skeletonTemplate = FindTemplate("Skeleton Variant", EnemyKind.Skeleton);
        EnemyController cannonTemplate = FindTemplate("CannonMachine", EnemyKind.CannonRobot);
        EnemyController rockTemplate = FindTemplate("Giant01 - Golem", EnemyKind.RockGiant);

        Spawn(skeletonTemplate, EnemyKind.Skeleton, composition.Skeletons);
        Spawn(cannonTemplate, EnemyKind.CannonRobot, composition.CannonRobots);
        Spawn(rockTemplate, EnemyKind.RockGiant, composition.RockGiants);
        Spawn(cannonTemplate, EnemyKind.GiantCannonBoss, composition.GiantCannons);
    }

    private EnemyController FindTemplate(string name, EnemyKind kind)
    {
        if (assetLoader != null)
        {
            if (TryFindBundledTemplate(name, out EnemyController bundledTemplate))
            {
                bundledTemplate.Configure(kind);
                bundledTemplate.gameObject.SetActive(true);
                return bundledTemplate;
            }
        }

        EnemyController[] existing = FindObjectsByType<EnemyController>(FindObjectsInactive.Include);
        foreach (EnemyController enemy in existing)
        {
            if (enemy.name.Contains(name, StringComparison.OrdinalIgnoreCase) &&
                enemy.Kind == kind)
            {
                enemy.gameObject.SetActive(true);
                return enemy;
            }
        }

        foreach (GameObject root in FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (root.name.Contains(name, StringComparison.OrdinalIgnoreCase))
            {
                EnemyController enemy = root.GetComponent<EnemyController>();
                if (enemy == null)
                {
                    enemy = AddEnemyComponents(root, kind);
                }
                enemy.gameObject.SetActive(true);
                return enemy;
            }
        }

        GameObject fallback = new GameObject(name);
        fallback.transform.position = new Vector3(8f, 0f, 10f);
        return AddEnemyComponents(fallback, kind);
    }

    private bool TryFindBundledTemplate(string name, out EnemyController template)
    {
        template = null;
        if (!assetLoader.TryGetPrefab(name, out GameObject prefab))
        {
            return false;
        }
        GameObject instance = Instantiate(prefab);
        template = instance.GetComponent<EnemyController>() ?? instance.AddComponent<EnemyController>();
        return template != null;
    }

    private void Spawn(EnemyController template, EnemyKind kind, int count)
    {
        if (count <= 0)
        {
            if (template != null)
            {
                template.gameObject.SetActive(false);
            }
            return;
        }

        Vector3 center = template != null
            ? template.transform.position
            : new Vector3(8f, 0f, 10f);

        for (int i = 0; i < count; i++)
        {
            EnemyController enemy = i == 0 && kind != EnemyKind.GiantCannonBoss
                ? template
                : Instantiate(template);
            if (enemy == null)
            {
                continue;
            }

            enemy.name = $"{kind} {i + 1}";
            enemy.gameObject.SetActive(true);
            enemy.Configure(kind);
            if (kind == EnemyKind.GiantCannonBoss)
            {
                enemy.transform.localScale *= 2.5f;
                enemy.name = "Giant Cannon Boss";
            }
            float angle = (360f / Mathf.Max(1, count)) * i;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (count == 1 ? 0f : 2.2f);
            enemy.transform.position = center + offset;
            enemy.Died += HandleEnemyDied;
            activeEnemies.Add(enemy);
        }
    }

    private void HandleEnemyDied(EnemyController enemy)
    {
        if (!activeEnemies.Remove(enemy))
        {
            return;
        }
        GameEvents.RaiseEnemyDefeated(activeEnemies.Count);
        if (activeEnemies.Count == 0)
        {
            JsonProgressStore.MarkLevelComplete(currentLevel);
            GameEvents.RaiseLevelCompleted(currentLevel);
            GameEvents.RaiseObjectiveChanged($"Level {currentLevel} complete");
            LeaderboardService leaderboard = FindAnyObjectByType<LeaderboardService>();
            leaderboard?.SubmitScore("Player", currentLevel * 1000);
        }
    }

    private void ClearLevelEnemies()
    {
        foreach (EnemyController enemy in activeEnemies)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
                enemy.gameObject.SetActive(false);
            }
        }
        activeEnemies.Clear();
    }

    private void ClearExistingEnemyObjects()
    {
        foreach (EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsInactive.Include))
        {
            if (enemy == null)
            {
                continue;
            }
            enemy.Died -= HandleEnemyDied;
            enemy.gameObject.SetActive(false);
        }
    }

    public static EnemyController AddEnemyComponents(GameObject target, EnemyKind kind)
    {
        EnemyController controller = target.GetComponent<EnemyController>();
        if (controller == null)
        {
            controller = target.AddComponent<EnemyController>();
        }
        controller.Configure(kind);

        if (target.GetComponent<CapsuleCollider>() == null)
        {
            CapsuleCollider collider = target.AddComponent<CapsuleCollider>();
            collider.height = kind == EnemyKind.RockGiant || kind == EnemyKind.GiantCannonBoss ? 2.8f : 1.8f;
            collider.radius = kind == EnemyKind.GiantCannonBoss ? 1.2f : 0.55f;
            collider.center = new Vector3(0f, collider.height * 0.5f, 0f);
        }

        Rigidbody rigidbody = target.GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = target.AddComponent<Rigidbody>();
        }
        rigidbody.isKinematic = true;
        rigidbody.useGravity = true;
        return controller;
    }

    private void ApplyLockedLevelVisuals(int highestUnlocked)
    {
        for (int level = 1; level <= 5; level++)
        {
            GameEvents.RaiseLevelUnlockChanged(level, level <= highestUnlocked);
        }
    }

    private readonly struct EnemyComposition
    {
        public readonly int Skeletons;
        public readonly int CannonRobots;
        public readonly int RockGiants;
        public readonly int GiantCannons;

        public EnemyComposition(int skeletons, int cannons, int giants, int giantCannons)
        {
            Skeletons = skeletons;
            CannonRobots = cannons;
            RockGiants = giants;
            GiantCannons = giantCannons;
        }
    }
}
