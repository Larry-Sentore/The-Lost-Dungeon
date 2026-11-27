using UnityEngine;

public static class GameSystemsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        PlatformBehaviours.Configure();
        if (Object.FindAnyObjectByType<LevelManager>() != null)
        {
            return;
        }

        GameObject systems = new GameObject("Game Systems");
        systems.AddComponent<LevelManager>();
        systems.AddComponent<UiGameController>();
        systems.AddComponent<GameManager>();
        systems.AddComponent<LeaderboardService>();

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            if (player.GetComponent<PlayerController>() == null)
            {
                player.AddComponent<PlayerController>();
            }
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.GetComponent<CameraController>() == null)
        {
            mainCamera.gameObject.AddComponent<CameraController>();
        }

        ConfigureExistingEnemies();
    }

    private static void ConfigureExistingEnemies()
    {
        foreach (GameObject root in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
        {
            if (root.name.Contains("Skeleton"))
            {
                LevelManager.AddEnemyComponents(root, EnemyKind.Skeleton);
            }
            else if (root.name.Contains("CannonMachine"))
            {
                LevelManager.AddEnemyComponents(root, EnemyKind.CannonRobot);
            }
            else if (root.name.Contains("Giant"))
            {
                LevelManager.AddEnemyComponents(root, EnemyKind.RockGiant);
            }
        }
    }
}
