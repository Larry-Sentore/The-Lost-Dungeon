using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The player: movement (with gravity), sword combat, abilities, potions, health and death.
// Input comes from PlayerInputRouter, so this class does not care which device is used.
[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour, IDamageable, IMovable, IAttackable, ISaveable
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int DeathStateHash = Animator.StringToHash("Base Layer.Death");

    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 0.55f;
    [Tooltip("Seconds after pressing attack until the sword hits (matches the slash animation).")]
    [SerializeField] private float attackHitTime = 0.4f;
    [SerializeField] private float autoAimRange = 3.5f;
    [SerializeField] private float gravity = -25f;
    [SerializeField] private float maximumHealth = 100f;
    [SerializeField] private float criticalChance = 0.15f;
    [SerializeField] private float invulnerableAfterHit = 0.5f;
    [SerializeField] private float potionHealAmount = 35f;

    private CharacterController characterController;
    private Animator animator;
    private Camera cachedCamera;
    private PlayerInputRouter input;
    private PlayerInteractor interactor;
    private readonly AbilitySet abilities = new AbilitySet();
    private readonly InventorySystem inventory = new InventorySystem();
    private float health;
    private float lastAttackTime = -10f;
    private float attackBusyUntil;
    private float verticalVelocity;
    private Vector3 externalVelocity;
    private float invulnerableUntil;
    private Renderer[] meshRenderers;
    private MaterialPropertyBlock flashBlock;
    private float flashUntil;
    private bool flashing;

    public float Health => health;
    public float MaxHealth => maximumHealth;
    public bool IsDead { get; private set; }
    public AbilitySet Abilities => abilities;
    public InventorySystem Inventory => inventory;

    // Sets up the CharacterController (fixing its centre so the player stands on the floor),
    // the Animator, the two abilities and the inventory.
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
        }
        // The scene's controller was centred on the feet, which sank half the capsule into the floor.
        if (characterController.center.y < characterController.height * 0.25f)
        {
            characterController.center = new Vector3(0f, characterController.height * 0.5f + characterController.skinWidth, 0f);
        }
        characterController.stepOffset = Mathf.Min(0.35f, characterController.height * 0.3f);
        characterController.slopeLimit = 50f;
        characterController.minMoveDistance = 0f;

        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.applyRootMotion = false;
        }

        abilities.Add(new SwordSlashAbility(2.6f));
        abilities.Add(new ShieldBashAbility(2.8f));
        input = new PlayerInputRouter();
        interactor = GetComponent<PlayerInteractor>();
        if (interactor == null)
        {
            interactor = gameObject.AddComponent<PlayerInteractor>();
        }
        health = maximumHealth;
        meshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        flashBlock = new MaterialPropertyBlock();
        SaveSystem.Register(this);
    }

    // Flashes the player red for a moment after a hit (the model has no "hit" animation clip).
    private void LateUpdate()
    {
        bool shouldFlash = Time.time < flashUntil;
        if (shouldFlash == flashing)
        {
            return;
        }
        flashing = shouldFlash;
        Color tint = new Color(1f, 0.35f, 0.35f);
        flashBlock.SetColor("_BaseColor", tint);
        flashBlock.SetColor("_Color", tint);
        foreach (Renderer r in meshRenderers)
        {
            if (r != null)
            {
                r.SetPropertyBlock(shouldFlash ? flashBlock : null);
            }
        }
    }

    // Stops saving this player's data.
    private void OnDestroy()
    {
        SaveSystem.Unregister(this);
    }

    // Tells the UI the starting health.
    private void Start()
    {
        GameEvents.RaiseHealthChanged(health, maximumHealth);
    }

    // Resets the player for a new level: full health, ready abilities, and placed on the ground at the spawn point.
    public void PrepareForLevel(Vector3 spawnPosition)
    {
        StopAllCoroutines();
        IsDead = false;
        health = maximumHealth;
        lastAttackTime = -10f;
        attackBusyUntil = 0f;
        verticalVelocity = 0f;
        externalVelocity = Vector3.zero;
        invulnerableUntil = 0f;

        characterController.enabled = false;
        transform.position = FindGround(spawnPosition);
        characterController.enabled = true;

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }
        abilities.ResetAll();
        GameEvents.RaiseHealthChanged(health, maximumHealth);
    }

    // Finds the floor under a point so the player never spawns floating or inside the ground.
    private Vector3 FindGround(Vector3 point)
    {
        RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 2f, Vector3.down, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 result = point;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<EnemyController>() != null)
            {
                continue;
            }
            if (hit.distance < best)
            {
                best = hit.distance;
                result = hit.point;
            }
        }
        return result;
    }

    // Every frame: read input, move, attack, use abilities, interact and drink potions.
    private void Update()
    {
        if (IsDead || Time.deltaTime <= 0f)
        {
            return;
        }

        PlayerInputFrame frame = input.Read();
        ApplyMovement(frame.Move);

        if (frame.Attack)
        {
            TryAttack();
        }
        if (frame.AbilityOne)
        {
            FaceNearestEnemy();
            abilities.TryActivate(0, gameObject);
        }
        if (frame.AbilityTwo)
        {
            FaceNearestEnemy();
            abilities.TryActivate(1, gameObject);
        }
        if (frame.Interact)
        {
            interactor.TryInteract();
        }
        if (frame.UsePotion)
        {
            UsePotion();
        }
        abilities.UpdateReadyEvents();
    }

    // Moves in a direction relative to the camera (IMovable). x = right, z = forward.
    public void Move(Vector3 direction)
    {
        ApplyMovement(new Vector2(direction.x, direction.z));
    }

    // Moves the player relative to the camera, adds gravity and any knockback, and turns to face the way they walk.
    private void ApplyMovement(Vector2 moveInput)
    {
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }
        Quaternion cameraYaw = cachedCamera != null
            ? Quaternion.Euler(0f, cachedCamera.transform.eulerAngles.y, 0f)
            : Quaternion.identity;
        Vector3 worldDirection = cameraYaw * new Vector3(moveInput.x, 0f, moveInput.y);
        bool attacking = Time.time < attackBusyUntil;
        float speedFactor = attacking ? 0.35f : 1f;

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        verticalVelocity += gravity * Time.deltaTime;
        externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, 14f * Time.deltaTime);

        Vector3 velocity = worldDirection * (moveSpeed * speedFactor) + externalVelocity + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);

        if (!attacking && worldDirection.sqrMagnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 12f * Time.deltaTime);
        }
        SetAnimatorSpeed(Mathf.Clamp01(worldDirection.magnitude) * speedFactor);
    }

    // Starts a sword swing if the cooldown allows. Turns toward the nearest enemy first (auto-aim).
    private void TryAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown || Time.time < attackBusyUntil)
        {
            return;
        }
        lastAttackTime = Time.time;
        attackBusyUntil = Time.time + attackHitTime + 0.2f;
        FaceNearestEnemy();
        PlayAnimationTrigger("Attack");
        GameEvents.RaisePlayerAttacked();
        PerformAreaAttack(attackRange, 1, 0.9f, 120f, false, attackHitTime);
    }

    // Snaps to face the closest living enemy in range, using the nearest-enemy search.
    private void FaceNearestEnemy()
    {
        EnemyController target = GameplayAlgorithms.FindNearestEnemy(transform.position, EnemyController.Active, autoAimRange);
        if (target == null)
        {
            return;
        }
        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    // Hits a target directly (IAttackable).
    public void Attack(IDamageable target)
    {
        target?.TakeDamage(new DamageInfo(1, transform.position, 0.9f, gameObject));
    }

    // Damages enemies in a circle (or a cone in front, when arc is under 360) after a delay. Used by attacks and abilities.
    public void PerformAreaAttack(float radius, int damage, float knockback, float arc, bool breaksGuard, float delay)
    {
        StartCoroutine(AreaAttackAfterDelay(radius, damage, knockback, arc, breaksGuard, delay));
    }

    // Waits for the animation to reach the hit, then applies the area attack.
    private IEnumerator AreaAttackAfterDelay(float radius, int damage, float knockback, float arc, bool breaksGuard, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        if (!IsDead)
        {
            ApplyAreaAttack(radius, damage, knockback, arc, breaksGuard);
        }
    }

    // Finds every enemy inside the area and hits each one once. Each hit can be a critical.
    private void ApplyAreaAttack(float radius, int damage, float knockback, float arc, bool breaksGuard)
    {
        Vector3 origin = transform.position;
        Vector3 center = origin + Vector3.up * 0.9f;
        Collider[] hits = Physics.OverlapSphere(center, radius + 1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        HashSet<EnemyController> damaged = new HashSet<EnemyController>();
        foreach (Collider hit in hits)
        {
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null || enemy.IsDead || damaged.Contains(enemy))
            {
                continue;
            }
            Vector3 closest = hit.ClosestPoint(center);
            Vector3 offset = closest - origin;
            offset.y = 0f;
            if (offset.magnitude > radius)
            {
                continue;
            }
            Vector3 toEnemy = enemy.transform.position - origin;
            toEnemy.y = 0f;
            if (arc < 360f && toEnemy.sqrMagnitude > 0.04f && Vector3.Angle(transform.forward, toEnemy) > arc * 0.5f)
            {
                continue;
            }
            damaged.Add(enemy);
            bool critical = DamageCalculator.RollCritical(criticalChance, UnityEngine.Random.value);
            enemy.TakeDamage(new DamageInfo(damage, origin, knockback, gameObject, breaksGuard, critical));
        }
    }

    // Drinks a potion from the inventory if the player is hurt.
    public void UsePotion()
    {
        if (health >= maximumHealth)
        {
            GameEvents.RaiseNotification("Health is already full");
            return;
        }
        if (!inventory.Remove(ItemCatalog.Potion, 1))
        {
            GameEvents.RaiseNotification("No potions left");
            GameEvents.RaiseUiSound("error");
            return;
        }
        SaveSystem.Data.Stats.PotionsUsed++;
        Heal(potionHealAmount);
    }

    // Restores health (never above the maximum).
    public void Heal(float amount)
    {
        if (IsDead)
        {
            return;
        }
        health = DamageCalculator.ApplyHeal(health, maximumHealth, amount);
        GameEvents.RaiseHealthChanged(health, maximumHealth);
        GameEvents.RaisePlayerHealed();
    }

    // Takes a hit: loses health, gets pushed back, is briefly invulnerable, and dies at zero.
    public void TakeDamage(DamageInfo damage)
    {
        if (IsDead || Time.time < invulnerableUntil)
        {
            return;
        }
        int amount = DamageCalculator.Calculate(damage.Amount, 1f, 0, false);
        if (amount <= 0)
        {
            return;
        }

        health = Mathf.Max(0f, health - amount);
        invulnerableUntil = Time.time + invulnerableAfterHit;
        externalVelocity = DamageCalculator.KnockbackVelocity(damage.SourcePosition, transform.position, damage.Knockback, 0.25f);
        flashUntil = Time.time + 0.15f;
        GameEvents.RaiseHealthChanged(health, maximumHealth);
        GameEvents.RaisePlayerDamaged(amount);

        if (health <= 0f)
        {
            Die();
        }
    }

    // The player dies: stop moving, play the death animation, record the death, then end the level.
    private void Die()
    {
        IsDead = true;
        externalVelocity = Vector3.zero;
        SetAnimatorSpeed(0f);
        PlayAnimationTrigger("Dead");
        characterController.enabled = false;
        SaveSystem.Data.Stats.Deaths++;
        SaveSystem.Save();
        GameEvents.RaisePlayerDied();
        StartCoroutine(GameOverAfterDeathAnimation());
    }

    // Waits for the death animation to finish (or 3 seconds at most), then shows game over.
    private IEnumerator GameOverAfterDeathAnimation()
    {
        float elapsed = 0f;
        while (elapsed < 3f)
        {
            if (animator != null && elapsed > 0.3f)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.fullPathHash == DeathStateHash && !animator.IsInTransition(0) && state.normalizedTime >= 0.95f)
                {
                    break;
                }
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        GameEvents.RaiseGameOver();
    }

    // Fires an Animator trigger if the controller has it.
    public void PlayAnimationTrigger(string triggerName)
    {
        if (HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(triggerName);
        }
    }

    // Sets the walk/run blend value on the Animator.
    private void SetAnimatorSpeed(float value)
    {
        if (HasAnimatorParameter("Speed", AnimatorControllerParameterType.Float))
        {
            animator.SetFloat(SpeedHash, value, 0.1f, Time.deltaTime);
        }
    }

    // Checks if the Animator has a parameter with this name and type.
    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == type)
            {
                return true;
            }
        }
        return false;
    }

    // Writes the inventory into the save data.
    public void CaptureState(SaveData data)
    {
        inventory.CaptureState(data);
    }

    // Loads the inventory from the save data.
    public void RestoreState(SaveData data)
    {
        inventory.RestoreState(data);
    }
}
