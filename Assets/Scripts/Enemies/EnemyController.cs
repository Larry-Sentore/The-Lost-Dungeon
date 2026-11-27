using System;
using System.Collections.Generic;
using UnityEngine;

// The body of an enemy: health, movement, knockback and attacks. What the enemy DECIDES to do
// comes from its IEnemyBehaviour (Strategy), which runs a state machine (State pattern).
[DisallowMultipleComponent]
public class EnemyController : MonoBehaviour, IDamageable, IAttackable, IMovable
{
    private const float KnockbackDuration = 0.28f;
    private const float Skin = 0.03f;
    private const float WalkableNormalY = 0.6f;
    private static readonly float[] SteeringAngles = { 35f, -35f, 70f, -70f, 110f, -110f };
    private static readonly List<EnemyController> active = new List<EnemyController>();

    // Every living enemy in the scene, used by searches such as the player's auto-aim.
    public static IReadOnlyList<EnemyController> Active => active;

    [SerializeField] private EnemyKind kind = EnemyKind.Skeleton;
    [Tooltip("Tick to use the numbers below instead of the defaults for this enemy type.")]
    [SerializeField] private bool overrideProfile;
    [SerializeField] private int maximumHealth = 2;
    [SerializeField] private int playerDamage = 25;
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float attackRange = 1.7f;
    [SerializeField] private float attackCooldown = 1.8f;

    private EnemyProfile profile;
    private int health;
    private float lastAttackTime = -100f;
    private CapsuleCollider movementCollider;
    private EnemyVisuals visuals;
    private IEnemyBehaviour behaviour;
    private PlayerController playerController;
    private float nextPlayerSearch;
    private Vector3 knockbackVelocity;
    private float knockbackDeceleration;
    private float staggerUntil;
    private readonly RaycastHit[] movementHits = new RaycastHit[16];

    public EnemyKind Kind => kind;
    public EnemyProfile Profile => profile ??= EnemyProfiles.Get(kind);
    public EnemyState State => behaviour != null ? behaviour.State : EnemyState.Idle;
    public bool IsDead { get; private set; }
    public int Health => health;
    public int MaximumHealth => Profile.MaxHealth;
    public float Health01 => MaximumHealth > 0 ? (float)health / MaximumHealth : 0f;
    public float MoveSpeed => Profile.MoveSpeed;
    public float AttackRange => Profile.AttackRange;
    public bool IsGuarding { get; set; }
    public bool IsStaggered => Time.time < staggerUntil;
    public bool IsBeingKnockedBack => knockbackVelocity.sqrMagnitude > 0.01f;
    public float CooldownMultiplier { get; set; } = 1f;
    public bool AttackReady => Time.time - lastAttackTime >= Profile.AttackCooldown * CooldownMultiplier;
    public EnemyVisuals Visuals => visuals;
    public PlayerController PlayerTarget => playerController;
    public bool PlayerAlive => playerController != null && !playerController.IsDead && playerController.isActiveAndEnabled;

    public event Action<EnemyController> Died;
    public event Action<EnemyController> Destroyed;
    public event Action<EnemyController, DamageInfo> Damaged;

    // Grabs components, fixes the collider so it sits on the floor, and sets up the enemy.
    private void Awake()
    {
        movementCollider = GetComponent<CapsuleCollider>();
        EnsurePhysicsComponents();
        FitColliderToModel();
        visuals = GetComponent<EnemyVisuals>();
        if (visuals == null)
        {
            visuals = gameObject.AddComponent<EnemyVisuals>();
        }
        Configure(kind);
    }

    // Adds this enemy to the list of living enemies.
    private void OnEnable()
    {
        if (!IsDead && !active.Contains(this))
        {
            active.Add(this);
        }
    }

    // Removes this enemy from the list of living enemies.
    private void OnDisable()
    {
        active.Remove(this);
    }

    // Sets this enemy's numbers from its type (or the Inspector values), picks its behaviour and resets it.
    public void Configure(EnemyKind enemyKind)
    {
        kind = enemyKind;
        profile = EnemyProfiles.Get(kind);
        if (overrideProfile)
        {
            profile.MaxHealth = Mathf.Max(1, maximumHealth);
            profile.Damage = Mathf.Max(0, playerDamage);
            profile.MoveSpeed = Mathf.Max(0f, moveSpeed);
            profile.AttackRange = Mathf.Max(0.5f, attackRange);
            profile.AttackCooldown = Mathf.Max(0.1f, attackCooldown);
        }

        health = profile.MaxHealth;
        IsDead = false;
        IsGuarding = false;
        CooldownMultiplier = 1f;
        lastAttackTime = -100f;
        knockbackVelocity = Vector3.zero;
        staggerUntil = 0f;
        if (movementCollider != null)
        {
            movementCollider.enabled = true;
        }

        behaviour = EnemyBehaviourRegistry.Create(kind);
        behaviour.Initialise(this);
    }

    // Every frame: find the player, slide from any knockback, then let the behaviour decide what to do.
    private void Update()
    {
        if (Time.deltaTime <= 0f || IsDead)
        {
            return;
        }
        FindPlayer();
        ApplyKnockbackStep();
        behaviour?.Tick(this);
    }

    // Looks for the player (at most once a second while we have none).
    private void FindPlayer()
    {
        if (playerController != null || Time.time < nextPlayerSearch)
        {
            return;
        }
        nextPlayerSearch = Time.time + 1f;
        playerController = FindAnyObjectByType<PlayerController>();
    }

    // Flat (ignoring height) distance to the player. Infinity if there is no player.
    public float DistanceToPlayer()
    {
        if (playerController == null)
        {
            return float.PositiveInfinity;
        }
        Vector3 offset = playerController.transform.position - transform.position;
        offset.y = 0f;
        return offset.magnitude;
    }

    // Flat direction from this enemy to the player.
    public Vector3 DirectionToPlayer()
    {
        if (playerController == null)
        {
            return transform.forward;
        }
        Vector3 offset = playerController.transform.position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude > 0.0001f ? offset.normalized : transform.forward;
    }

    // Returns true if the player is inside the given angle in front of the enemy.
    public bool IsFacingPlayer(float maxAngle)
    {
        return Vector3.Angle(transform.forward, DirectionToPlayer()) <= maxAngle;
    }

    // Moves at normal speed (IMovable).
    public void Move(Vector3 direction)
    {
        Move(direction, 1f);
    }

    // Moves toward a direction, steering around walls, and updates the walk/run animation.
    public void Move(Vector3 direction, float speedMultiplier)
    {
        direction.y = 0f;
        float step = Profile.MoveSpeed * Mathf.Max(0f, speedMultiplier) * Time.deltaTime;
        if (direction.sqrMagnitude < 0.0001f || step <= 0f)
        {
            visuals.SetLocomotion(0f);
            return;
        }

        float lookAhead = Mathf.Max(0.5f, step * 6f);
        Vector3 chosen = GameplayAlgorithms.ChooseSteeringDirection(direction, d => IsPathBlocked(d, lookAhead), SteeringAngles);
        if (chosen == Vector3.zero)
        {
            Face(direction, 6f);
            visuals.SetLocomotion(0f);
            return;
        }

        Vector3 moved = MoveSafely(chosen * step);
        Face(chosen, 8f);
        float speed01 = moved.magnitude / Mathf.Max(0.0001f, Profile.MoveSpeed * Time.deltaTime);
        visuals.SetLocomotion(Mathf.Clamp01(speed01));
    }

    // Stops the walk animation.
    public void StopMoving()
    {
        visuals.SetLocomotion(0f);
    }

    // Turns smoothly to face a flat direction.
    public void Face(Vector3 direction, float turnSpeed)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Mathf.Clamp01(turnSpeed * Time.deltaTime));
    }

    // Turns smoothly toward the player.
    public void FacePlayer(float turnSpeed)
    {
        Face(DirectionToPlayer(), turnSpeed);
    }

    // Builds the enemy's capsule in world space. The bottom is lifted a little so sweeps
    // do not scrape along the floor.
    private bool GetSweepCapsule(out Vector3 top, out Vector3 bottom, out float radius)
    {
        top = bottom = transform.position;
        radius = 0f;
        if (movementCollider == null)
        {
            return false;
        }
        Vector3 scale = transform.lossyScale;
        radius = movementCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * 0.95f;
        float height = Mathf.Max(movementCollider.height * Mathf.Abs(scale.y), radius * 2f);
        Vector3 center = transform.TransformPoint(movementCollider.center);
        float half = Mathf.Max(0f, height * 0.5f - radius);
        top = center + Vector3.up * half;
        bottom = center - Vector3.up * half;
        bottom += Vector3.up * Mathf.Min(0.2f, half * 2f);
        return true;
    }

    // Returns the closest blocking hit along a direction, or -1 if nothing blocks.
    // The floor, things we already overlap, triggers and ourselves never count as blocking.
    private float SweepDistance(Vector3 direction, float distance, bool ignorePlayer)
    {
        if (!GetSweepCapsule(out Vector3 top, out Vector3 bottom, out float radius))
        {
            return -1f;
        }
        int hitCount = Physics.CapsuleCastNonAlloc(top, bottom, radius, direction, movementHits, distance + Skin,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        float closest = -1f;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = movementHits[i];
            Collider other = hit.collider;
            if (other == null || other.transform == transform || other.transform.IsChildOf(transform))
            {
                continue;
            }
            // distance 0 means we started inside it (usually the floor) - Unity reports those as hits.
            if (hit.distance <= 0f || hit.normal.y > WalkableNormalY)
            {
                continue;
            }
            if (ignorePlayer && other.GetComponentInParent<PlayerController>() != null)
            {
                continue;
            }
            if (closest < 0f || hit.distance < closest)
            {
                closest = hit.distance;
            }
        }
        return closest;
    }

    // Returns true if a wall or obstacle is in the way within the given distance (the player does not count).
    private bool IsPathBlocked(Vector3 direction, float distance)
    {
        return SweepDistance(direction, distance, true) >= 0f;
    }

    // Moves the enemy, stopping just before walls and the player. Returns how far it really moved.
    private Vector3 MoveSafely(Vector3 displacement)
    {
        displacement.y = 0f;
        float distance = displacement.magnitude;
        if (distance <= 0.0001f)
        {
            return Vector3.zero;
        }
        Vector3 direction = displacement / distance;
        float blockedAt = SweepDistance(direction, distance, false);
        float allowed = blockedAt < 0f ? distance : Mathf.Clamp(blockedAt - Skin, 0f, distance);
        Vector3 moved = direction * allowed;
        transform.position += moved;
        return moved;
    }

    // Starts a knockback slide away from where the hit came from. Heavier enemies slide less.
    public void ApplyKnockback(Vector3 sourcePosition, float distance)
    {
        distance *= 1f - Mathf.Clamp01(Profile.KnockbackResistance);
        if (distance < 0.02f || IsDead)
        {
            return;
        }
        Vector3 velocity = DamageCalculator.KnockbackVelocity(sourcePosition, transform.position, distance, KnockbackDuration);
        if (velocity == Vector3.zero)
        {
            velocity = -transform.forward * (2f * distance / KnockbackDuration);
        }
        knockbackVelocity = velocity;
        knockbackDeceleration = velocity.magnitude / KnockbackDuration;
    }

    // Moves the enemy a little along its knockback each frame and slows it down. Stops early at walls.
    private void ApplyKnockbackStep()
    {
        if (knockbackVelocity.sqrMagnitude < 0.0001f)
        {
            knockbackVelocity = Vector3.zero;
            return;
        }
        Vector3 wanted = knockbackVelocity * Time.deltaTime;
        Vector3 moved = MoveSafely(wanted);
        if (moved.sqrMagnitude < wanted.sqrMagnitude * 0.25f)
        {
            knockbackVelocity = Vector3.zero;
            return;
        }
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, knockbackDeceleration * Time.deltaTime);
    }

    // Stops the enemy from acting for a short time.
    public void Stagger(float duration)
    {
        staggerUntil = Mathf.Max(staggerUntil, Time.time + duration);
    }

    // Records that an attack started (for the cooldown) and tells listeners such as the audio system.
    public void MarkAttackStarted()
    {
        lastAttackTime = Time.time;
        GameEvents.RaiseEnemyAttacked(kind);
    }

    // Hits a target directly with this enemy's damage (IAttackable).
    public void Attack(IDamageable target)
    {
        if (target == null || IsDead)
        {
            return;
        }
        target.TakeDamage(new DamageInfo(Profile.Damage, transform.position, 0.8f, gameObject));
    }

    // Damages the player if they are within reach and inside the attack arc. Returns true on a hit.
    public bool DealMeleeDamage(float reach, float arcDegrees, float damageMultiplier, float knockback)
    {
        if (!PlayerAlive)
        {
            return false;
        }
        Vector3 offset = playerController.transform.position - transform.position;
        offset.y = 0f;
        const float playerRadius = 0.4f;
        if (offset.magnitude > reach + playerRadius)
        {
            return false;
        }
        if (arcDegrees < 360f && offset.sqrMagnitude > 0.01f && Vector3.Angle(transform.forward, offset) > arcDegrees * 0.5f)
        {
            return false;
        }
        int damage = DamageCalculator.Calculate(Profile.Damage, damageMultiplier, 0, false);
        playerController.TakeDamage(new DamageInfo(damage, transform.position, knockback, gameObject));
        return true;
    }

    // Returns the direction from a point to the player's chest.
    public Vector3 AimDirectionFrom(Vector3 origin)
    {
        if (playerController == null)
        {
            return transform.forward;
        }
        Vector3 target = playerController.transform.position + Vector3.up * 1.0f;
        Vector3 direction = target - origin;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
    }

    // Shoots a pooled projectile in a direction and plays the firing effect.
    public void FireProjectile(Vector3 direction, float damageMultiplier, float scale, Color color)
    {
        Vector3 origin = visuals.MuzzlePosition;
        int damage = DamageCalculator.Calculate(Profile.Damage, damageMultiplier, 0, false);
        ProjectileSpawner.Fire(origin, direction, Profile.ProjectileSpeed, damage, scale, color, gameObject);
        visuals.PlayFire();
    }

    // Takes a hit: blocks, damage, flash, knockback, and death when health reaches zero.
    public void TakeDamage(DamageInfo damage)
    {
        if (IsDead)
        {
            return;
        }

        int amount = DamageCalculator.Calculate(damage.Amount, 1f, Profile.Defence, damage.IsCritical);
        if (behaviour != null)
        {
            amount = behaviour.ModifyIncomingDamage(this, damage, amount);
        }

        if (amount <= 0)
        {
            visuals.PlayBlock();
            GameEvents.RaiseEnemyBlocked(kind, transform.position + Vector3.up);
            ApplyKnockback(damage.SourcePosition, damage.Knockback * 0.3f);
            return;
        }

        health = Mathf.Max(0, health - amount);
        visuals.PlayHit();
        ApplyKnockback(damage.SourcePosition, damage.Knockback);
        GameEvents.RaiseEnemyHit(kind, transform.position + Vector3.up);
        Damaged?.Invoke(this, damage);

        if (health <= 0)
        {
            Die();
            return;
        }
        behaviour?.OnDamaged(this, damage);
    }

    // Kills the enemy: stops it, turns off its collider, plays the death animation and removes it later.
    private void Die()
    {
        IsDead = true;
        IsGuarding = false;
        knockbackVelocity = Vector3.zero;
        behaviour?.OnDeath(this);
        if (movementCollider != null)
        {
            movementCollider.enabled = false;
        }
        visuals.PlayDeath();
        active.Remove(this);
        Died?.Invoke(this);
        GameEvents.RaiseEnemyKilled(kind, transform.position);
        Destroy(gameObject, 3.2f);
    }

    // Tells listeners (like the LevelManager) that this enemy is gone.
    private void OnDestroy()
    {
        active.Remove(this);
        Destroyed?.Invoke(this);
    }

    // Makes sure the enemy has a kinematic Rigidbody so moving it by script is cheap and safe.
    private void EnsurePhysicsComponents()
    {
        if (movementCollider == null)
        {
            movementCollider = gameObject.AddComponent<CapsuleCollider>();
            movementCollider.radius = 0.5f;
            movementCollider.height = 1f;
            movementCollider.center = Vector3.zero;
        }
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.None;
    }

    // If the capsule collider pokes under the floor (it was centred on the feet), resize it
    // to wrap the model's meshes so it stands on the ground. This is what stopped enemies moving.
    private void FitColliderToModel()
    {
        if (movementCollider == null)
        {
            return;
        }
        float bottom = movementCollider.center.y - movementCollider.height * 0.5f;
        if (bottom >= -0.05f * movementCollider.height)
        {
            return;
        }

        bool found = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        foreach (Renderer meshRenderer in GetComponentsInChildren<Renderer>(true))
        {
            if (!(meshRenderer is MeshRenderer) && !(meshRenderer is SkinnedMeshRenderer))
            {
                continue;
            }
            if (!found)
            {
                bounds = meshRenderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(meshRenderer.bounds);
            }
        }

        Vector3 scale = transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
        if (!found || bounds.size.y < 0.1f)
        {
            movementCollider.center = new Vector3(0f, movementCollider.height * 0.5f, 0f);
            return;
        }

        float minY = Mathf.Max(0f, (bounds.min.y - transform.position.y) / sy);
        float maxY = (bounds.max.y - transform.position.y) / sy;
        float height = Mathf.Max(0.5f, maxY - minY);
        float footprint = Mathf.Min(bounds.size.x, bounds.size.z) / sx;
        float radius = Mathf.Clamp(footprint * 0.35f, 0.25f / sx, height * 0.5f);

        // Stay centred on the pivot: some models have long parts (like the cannon's laser) that would pull the centre off.
        movementCollider.direction = 1;
        movementCollider.height = height;
        movementCollider.radius = radius;
        movementCollider.center = new Vector3(0f, minY + height * 0.5f, 0f);
    }

    // Makes sure an object is a working enemy of the given type (used for enemies placed without the component).
    public static EnemyController Setup(GameObject target, EnemyKind enemyKind)
    {
        EnemyController controller = target.GetComponent<EnemyController>();
        if (controller == null)
        {
            controller = target.AddComponent<EnemyController>();
        }
        controller.Configure(enemyKind);
        return controller;
    }
}
