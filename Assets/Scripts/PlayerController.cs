using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerController : MonoBehaviour, IDamageable, IMovable, IAttackable, ISaveable
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 0.55f;

    private CharacterController characterController;
    private Animator animator;
    private float lastAttackTime = -10f;
    private float health = 100f;
    private readonly Dictionary<string, IAbility> abilities = new Dictionary<string, IAbility>();

    public float Health => health;
    public float MaxHealth { get; private set; } = 100f;
    public bool IsDead { get; private set; }

    public event Action AttackStarted;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.32f;
            characterController.center = new Vector3(0f, 0.9f, 0f);
        }

        animator = GetComponentInChildren<Animator>();
        abilities["Sword Slash"] = new SwordSlashAbility(1.8f);
        abilities["Shield Bash"] = new ShieldBashAbility(3f);
    }

    private void Start()
    {
        SaveData data = JsonProgressStore.Load();
        if (data != null)
        {
            health = Mathf.Clamp(data.PlayerHealth, 1f, MaxHealth);
        }
        else
        {
            health = MaxHealth;
        }

        GameEvents.RaiseHealthChanged(health, MaxHealth);
    }

    private void Update()
    {
        if (IsDead)
        {
            return;
        }

        Vector3 input = ReadMovementInput();
        Move(input);

        if (ReadAttackInput())
        {
            PerformSwordAttack();
        }

        if (ReadAbilityOneInput())
        {
            abilities["Sword Slash"].Activate(gameObject);
        }

        if (ReadAbilityTwoInput())
        {
            abilities["Shield Bash"].Activate(gameObject);
        }
    }

    private Vector3 ReadMovementInput()
    {
#if UNITY_ANDROID || UNITY_IOS
        Vector2 touch = MobileInput.ReadMovement();
        return new Vector3(touch.x, 0f, touch.y);
#else
        return new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")).normalized;
#endif
    }

    private bool ReadAttackInput()
    {
#if UNITY_ANDROID || UNITY_IOS
        return MobileInput.AttackPressed;
#else
        return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Q);
#endif
    }

    private bool ReadAbilityOneInput()
    {
#if UNITY_ANDROID || UNITY_IOS
        return MobileInput.AbilityOnePressed;
#else
        return Input.GetKeyDown(KeyCode.Alpha1);
#endif
    }

    private bool ReadAbilityTwoInput()
    {
#if UNITY_ANDROID || UNITY_IOS
        return MobileInput.AbilityTwoPressed;
#else
        return Input.GetKeyDown(KeyCode.Alpha2);
#endif
    }

    public void Move(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
        {
            animator?.SetFloat("Speed", 0f);
            return;
        }

        Quaternion cameraYaw = Camera.main != null
            ? Quaternion.Euler(0f, Camera.main.transform.eulerAngles.y, 0f)
            : Quaternion.identity;
        Vector3 worldDirection = cameraYaw * direction;
        characterController.Move(worldDirection * moveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(worldDirection), 12f * Time.deltaTime);
        animator?.SetFloat("Speed", moveSpeed);
    }

    private void PerformSwordAttack()
    {
        if (Time.time - lastAttackTime < attackCooldown)
        {
            return;
        }

        lastAttackTime = Time.time;
        AttackStarted?.Invoke();
        animator?.SetTrigger("Attack");
        Collider[] hits = Physics.OverlapSphere(transform.position + transform.forward * (attackRange * 0.5f), attackRange);
        foreach (Collider hit in hits)
        {
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                Attack(enemy);
                break;
            }
        }
    }

    public void Attack(IDamageable target)
    {
        target.TakeDamage(1);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead)
        {
            return;
        }

        health = Mathf.Max(0f, health - amount);
        animator?.SetTrigger("Hit");
        GameEvents.RaiseHealthChanged(health, MaxHealth);
        SaveData saveData = JsonProgressStore.Load() ?? new SaveData();
        saveData.PlayerHealth = health;
        JsonProgressStore.Save(saveData);

        if (health <= 0f)
        {
            IsDead = true;
            animator?.SetTrigger("Dead");
            characterController.enabled = false;
            GameEvents.RaiseGameOver();
        }
    }

    public object CaptureState()
    {
        return new PlayerSaveState { Health = health };
    }

    public void RestoreState(object state)
    {
        if (state is PlayerSaveState playerState)
        {
            health = Mathf.Clamp(playerState.Health, 0f, MaxHealth);
            IsDead = health <= 0f;
            GameEvents.RaiseHealthChanged(health, MaxHealth);
        }
    }

    [Serializable]
    private class PlayerSaveState
    {
        public float Health;
    }
}
