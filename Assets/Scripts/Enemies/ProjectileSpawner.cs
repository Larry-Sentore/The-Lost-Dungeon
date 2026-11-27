using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Owns the projectile pool. Lives for the whole session and recalls every projectile when a scene loads.
public class ProjectileSpawner : MonoBehaviour
{
    private static ProjectileSpawner instance;
    private ObjectPool<Projectile> pool;

    // Creates the spawner if it does not exist yet.
    public static void EnsureExists(Transform parent)
    {
        if (instance != null)
        {
            return;
        }
        GameObject go = new GameObject("Projectile Pool");
        go.transform.SetParent(parent, false);
        instance = go.AddComponent<ProjectileSpawner>();
    }

    // Builds the projectile template and prewarms the pool.
    private void Awake()
    {
        GameObject template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        template.name = "Projectile";
        // DestroyImmediate: a normal Destroy waits until the end of the frame, so the prewarmed copies would keep the collider.
        DestroyImmediate(template.GetComponent<Collider>());
        template.AddComponent<Projectile>();
        template.SetActive(false);
        template.transform.SetParent(transform, false);
        pool = new ObjectPool<Projectile>(template.GetComponent<Projectile>(), 12, transform);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Stops listening for scene loads.
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            instance = null;
        }
    }

    // Puts every flying projectile back in the pool when a scene (re)loads.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        pool?.ReleaseAll();
    }

    // Takes a projectile from the pool and fires it.
    public static void Fire(Vector3 position, Vector3 direction, float speed, int damage, float scale, Color color, GameObject owner)
    {
        if (instance == null)
        {
            EnsureExists(null);
        }
        Projectile projectile = instance.pool.Get(position, Quaternion.LookRotation(direction));
        projectile.Launch(direction, speed, damage, scale, color, owner, p => instance.pool.Release(p));
    }
}
