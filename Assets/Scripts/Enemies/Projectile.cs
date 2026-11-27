using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// A cannon ball. Moves itself with a sphere sweep each frame (no Rigidbody needed),
// hurts the player, bursts on walls, and goes back to its pool instead of being destroyed.
[DisallowMultipleComponent]
public class Projectile : MonoBehaviour
{
    private const float Lifetime = 5f;
    private static readonly RaycastHit[] hits = new RaycastHit[8];

    private Vector3 velocity;
    private int damage;
    private float radius;
    private float despawnAt;
    private GameObject owner;
    private Color color;
    private Action<Projectile> release;
    private MaterialPropertyBlock block;
    private Renderer meshRenderer;

    // Sets the projectile flying.
    public void Launch(Vector3 direction, float speed, int damageAmount, float scale, Color tint, GameObject shooter, Action<Projectile> onRelease)
    {
        velocity = direction.normalized * speed;
        damage = damageAmount;
        radius = scale * 0.5f;
        owner = shooter;
        color = tint;
        release = onRelease;
        despawnAt = Time.time + Lifetime;
        transform.localScale = Vector3.one * scale;

        meshRenderer ??= GetComponent<Renderer>();
        block ??= new MaterialPropertyBlock();
        block.SetColor("_BaseColor", tint);
        block.SetColor("_Color", tint);
        block.SetColor("_EmissionColor", tint * 2f);
        if (meshRenderer != null)
        {
            meshRenderer.SetPropertyBlock(block);
        }
    }

    // Moves forward, checking what it would hit along the way.
    private void Update()
    {
        if (Time.deltaTime <= 0f)
        {
            return;
        }
        if (Time.time >= despawnAt)
        {
            Despawn(false);
            return;
        }

        Vector3 step = velocity * Time.deltaTime;
        float distance = step.magnitude;
        int count = Physics.SphereCastNonAlloc(transform.position, radius, step / Mathf.Max(0.0001f, distance), hits,
            distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float closest = float.MaxValue;
        Collider closestCollider = null;
        for (int i = 0; i < count; i++)
        {
            Collider other = hits[i].collider;
            if (other == null || hits[i].distance <= 0f && other.GetComponentInParent<PlayerController>() == null)
            {
                continue;
            }
            if (owner != null && other.transform.IsChildOf(owner.transform))
            {
                continue;
            }
            if (other.GetComponentInParent<EnemyController>() != null)
            {
                continue;
            }
            if (hits[i].distance < closest)
            {
                closest = hits[i].distance;
                closestCollider = other;
            }
        }

        if (closestCollider != null)
        {
            transform.position += step.normalized * closest;
            PlayerController player = closestCollider.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(new DamageInfo(damage, transform.position - velocity.normalized, 0.6f, owner));
            }
            Despawn(true);
            return;
        }
        transform.position += step;
    }

    // Sparks (if it hit something) and returns to the pool.
    private void Despawn(bool impact)
    {
        if (impact)
        {
            HitEffects.Burst(transform.position, color, 6);
        }
        Action<Projectile> callback = release;
        release = null;
        if (callback != null)
        {
            callback(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
