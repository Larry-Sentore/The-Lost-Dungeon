using UnityEngine;
using UnityEngine.SceneManagement;

// Creates the systems the game needs without having to place them in the scene by hand.
// Session-wide services (content, audio, bestiary, pools) live on one object that survives scene reloads.
// Scene systems (level, UI, game manager) are made sure of every time a scene loads.
public static class GameSystemsBootstrap
{
    private static GameObject services;

    // Runs automatically after the first scene loads. Applies platform settings and creates every system.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        PlatformBehaviours.Configure();
        CreatePersistentServices();
        EnsureSceneSystems();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Makes sure the scene systems exist again after a scene reload.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSceneSystems();
    }

    // Creates the services that live for the whole session (only once).
    private static void CreatePersistentServices()
    {
        if (services != null)
        {
            return;
        }
        services = new GameObject("Persistent Services");
        Object.DontDestroyOnLoad(services);
        services.AddComponent<ContentManager>();
        services.AddComponent<AudioManager>();
        services.AddComponent<BestiaryService>();
        ProjectileSpawner.EnsureExists(services.transform);
        HitEffects.EnsureExists(services.transform);
        PickupSpawner.EnsureExists(services.transform);
    }

    // Makes sure the scene has its game systems, the player has a controller and the camera follows them.
    private static void EnsureSceneSystems()
    {
        LevelManager levelManager = Object.FindAnyObjectByType<LevelManager>();
        GameObject systems = levelManager != null ? levelManager.gameObject : new GameObject("Game Systems");
        Ensure<LevelManager>(systems);
        Ensure<GameManager>(systems);
        Ensure<LeaderboardService>(systems);
        Ensure<AssetBundleLevelLoader>(systems);
        Ensure<UiGameController>(systems);

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            Ensure<PlayerController>(player);
        }
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            Ensure<CameraController>(mainCamera.gameObject);
        }
    }

    // Adds a component if the object does not have one yet.
    private static T Ensure<T>(GameObject target) where T : Component
    {
        T existing = target.GetComponent<T>();
        return existing != null ? existing : target.AddComponent<T>();
    }
}
