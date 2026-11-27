using UnityEngine;
using UnityEngine.SceneManagement;

// Pooled hit bursts used for enemy hits, blocks and projectile impacts.
public class HitEffects : MonoBehaviour
{
    private static HitEffects instance;
    private ObjectPool<HitSpark> pool;
    private MaterialPropertyBlock block;

    // Creates the effect pool if it does not exist yet, and listens for hits.
    public static void EnsureExists(Transform parent)
    {
        if (instance != null)
        {
            return;
        }
        GameObject go = new GameObject("Hit Effect Pool");
        go.transform.SetParent(parent, false);
        instance = go.AddComponent<HitEffects>();
    }

    // Builds the spark template and prewarms the pool.
    private void Awake()
    {
        GameObject template = GameObject.CreatePrimitive(PrimitiveType.Cube);
        template.name = "Hit Spark";
        // DestroyImmediate: a normal Destroy waits until the end of the frame, so the prewarmed copies would keep the collider.
        DestroyImmediate(template.GetComponent<Collider>());
        template.AddComponent<HitSpark>();
        template.SetActive(false);
        template.transform.SetParent(transform, false);
        pool = new ObjectPool<HitSpark>(template.GetComponent<HitSpark>(), 30, transform);
        block = new MaterialPropertyBlock();
        GameEvents.EnemyHit += OnEnemyHit;
        GameEvents.EnemyBlocked += OnEnemyBlocked;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Stops listening.
    private void OnDestroy()
    {
        GameEvents.EnemyHit -= OnEnemyHit;
        GameEvents.EnemyBlocked -= OnEnemyBlocked;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this)
        {
            instance = null;
        }
    }

    // Recalls all sparks on scene load.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => pool?.ReleaseAll();

    // Orange sparks when an enemy is hurt.
    private void OnEnemyHit(EnemyKind kind, Vector3 position) => Burst(position, new Color(1f, 0.7f, 0.25f), 8);

    // Blue sparks when an enemy blocks.
    private void OnEnemyBlocked(EnemyKind kind, Vector3 position) => Burst(position, new Color(0.5f, 0.8f, 1f), 6);

    // Throws a handful of sparks out from a point.
    public static void Burst(Vector3 position, Color color, int count)
    {
        if (instance == null)
        {
            return;
        }
        for (int i = 0; i < count; i++)
        {
            Vector3 direction = (Random.insideUnitSphere + Vector3.up * 0.8f).normalized;
            HitSpark spark = instance.pool.Get(position, Random.rotation);
            spark.Launch(direction * Random.Range(2.5f, 5f), Random.Range(0.06f, 0.12f), Random.Range(0.25f, 0.45f), color, instance.block,
                s => instance.pool.Release(s));
        }
    }
}
