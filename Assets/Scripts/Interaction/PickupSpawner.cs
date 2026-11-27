using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Owns the pickup pool and drops loot when enemies die, using each enemy's loot weights.
public class PickupSpawner : MonoBehaviour
{
    private static PickupSpawner instance;
    private ObjectPool<Pickup> pool;

    // Creates the spawner if it does not exist yet.
    public static void EnsureExists(Transform parent)
    {
        if (instance != null)
        {
            return;
        }
        GameObject go = new GameObject("Pickup Pool");
        go.transform.SetParent(parent, false);
        instance = go.AddComponent<PickupSpawner>();
    }

    // Builds the pickup template, prewarms the pool and listens for enemy deaths.
    private void Awake()
    {
        GameObject template = GameObject.CreatePrimitive(PrimitiveType.Cube);
        template.name = "Pickup";
        // DestroyImmediate: a normal Destroy waits until the end of the frame, so the prewarmed copies would keep the collider.
        DestroyImmediate(template.GetComponent<Collider>());
        template.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        template.transform.rotation = Quaternion.Euler(45f, 0f, 45f);
        template.AddComponent<Pickup>();
        template.SetActive(false);
        template.transform.SetParent(transform, false);
        pool = new ObjectPool<Pickup>(template.GetComponent<Pickup>(), 8, transform);
        GameEvents.EnemyKilled += OnEnemyKilled;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Stops listening.
    private void OnDestroy()
    {
        GameEvents.EnemyKilled -= OnEnemyKilled;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            instance = null;
        }
    }

    // Recalls every pickup when a scene loads.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => pool?.ReleaseAll();

    // Rolls the enemy's loot table and drops the result where it died.
    private void OnEnemyKilled(EnemyKind kind, Vector3 position)
    {
        EnemyProfile profile = EnemyProfiles.Get(kind);
        string item = LootTable.Roll(profile, UnityEngine.Random.value);
        if (item == null)
        {
            return;
        }
        int amount = kind == EnemyKind.GiantCannonBoss ? 5 : 1;
        Drop(item, amount, position);
    }

    // Drops an item at a position.
    public static void Drop(string itemId, int amount, Vector3 position)
    {
        if (instance == null)
        {
            return;
        }
        PlayerController player = FindAnyObjectByType<PlayerController>();
        Pickup pickup = instance.pool.Get(position, Quaternion.identity);
        pickup.Setup(itemId, amount, position, player != null ? player.transform : null, p => instance.pool.Release(p));
    }
}
