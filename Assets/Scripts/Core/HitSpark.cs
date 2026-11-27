using UnityEngine;
using UnityEngine.SceneManagement;

// One small flying shard of a hit burst. Flies out, shrinks, and returns to the pool.
[DisallowMultipleComponent]
public class HitSpark : MonoBehaviour
{
    private Vector3 velocity;
    private float startScale;
    private float age;
    private float life;
    private System.Action<HitSpark> release;

    // Starts the spark moving.
    public void Launch(Vector3 startVelocity, float scale, float lifetime, Color color, MaterialPropertyBlock block, System.Action<HitSpark> onRelease)
    {
        velocity = startVelocity;
        startScale = scale;
        life = lifetime;
        age = 0f;
        release = onRelease;
        transform.localScale = Vector3.one * scale;
        Renderer r = GetComponent<Renderer>();
        if (r != null)
        {
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            r.SetPropertyBlock(block);
        }
    }

    // Moves with a little gravity and shrinks to nothing (uses unscaled time so it finishes even on pause).
    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        age += dt;
        velocity += Physics.gravity * 0.6f * dt;
        transform.position += velocity * dt;
        transform.localScale = Vector3.one * (startScale * Mathf.Clamp01(1f - age / life));
        if (age >= life)
        {
            System.Action<HitSpark> callback = release;
            release = null;
            callback?.Invoke(this);
        }
    }
}
