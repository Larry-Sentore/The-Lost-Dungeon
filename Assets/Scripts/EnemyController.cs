using System;
using UnityEngine;

public enum EnemyKind
{
    Skeleton,
    CannonRobot,
    RockGiant,
    GiantCannonBoss
}

public enum EnemyState
{
    Idle,
    Chase,
    Attack,
    Dead
}

[DisallowMultipleComponent]
public class EnemyController : MonoBehaviour, IDamageable, IAttackable, IMovable
{
    [SerializeField] private EnemyKind kind = EnemyKind.Skeleton;
    [SerializeField] private int maximumHealth = 2;
    [SerializeField] private int playerDamage = 25;
    [SerializeField] private float moveSpeed = 1.2f;
    [SerializeField] private float attackRange = 1.7f;
    [SerializeField] private float attackCooldown = 1.8f;

    private int health;
    private float lastAttackTime = -10f;
    private Animator animator;
    private Transform player;
    private IEnemyBehaviour behaviour;
    private EnemyState state = EnemyState.Idle;

    public EnemyKind Kind => kind;
    public EnemyState State => state;
    public bool IsDead => state == EnemyState.Dead;
    public int MaximumHealth => maximumHealth;
    public float MoveSpeed => moveSpeed;
    public float AttackRange => attackRange;

    public event Action<EnemyController> Died;

    public void Configure(EnemyKind enemyKind)
    {
        kind = enemyKind;
        switch (kind)
        {
            case EnemyKind.Skeleton:
                maximumHealth = 2;
                playerDamage = 25;
                moveSpeed = 1.6f;
                attackRange = 1.8f;
                attackCooldown = 1.4f;
                break;
            case EnemyKind.CannonRobot:
                maximumHealth = 2;
                playerDamage = 25;
                moveSpeed = 0.65f;
                attackRange = 7f;
                attackCooldown = 2.2f;
                break;
            case EnemyKind.RockGiant:
                maximumHealth = 3;
                playerDamage = 25;
                moveSpeed = 0.9f;
                attackRange = 2.1f;
                attackCooldown = 1.8f;
                break;
            case EnemyKind.GiantCannonBoss:
                maximumHealth = 10;
                playerDamage = 50;
                moveSpeed = 0.45f;
                attackRange = 9f;
                attackCooldown = 3f;
                break;
        }
        behaviour = EnemyBehaviourFactory.Create(kind);
        health = maximumHealth;
    }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (kind == EnemyKind.Skeleton || kind == EnemyKind.CannonRobot ||
            kind == EnemyKind.RockGiant || kind == EnemyKind.GiantCannonBoss)
        {
            Configure(kind);
        }
    }

    private void Start()
    {
        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void Update()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject == null)
            {
                return;
            }
            player = playerObject.transform;
        }

        behaviour?.Tick(this, player);
    }

    public void SetState(EnemyState nextState)
    {
        state = nextState;
        animator?.SetInteger("State", (int)nextState);
    }

    public void Move(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f)
        {
            animator?.SetFloat("Speed", 0f);
            return;
        }

        transform.position += direction.normalized * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction.normalized),
            8f * Time.deltaTime);
        animator?.SetFloat("Speed", moveSpeed);
    }

    public void Attack(IDamageable target)
    {
        if (Time.time - lastAttackTime < attackCooldown || IsDead)
        {
            return;
        }

        lastAttackTime = Time.time;
        SetState(EnemyState.Attack);
        animator?.SetTrigger("Attack");
        target.TakeDamage(playerDamage);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead)
        {
            return;
        }

        health = Mathf.Max(0, health - amount);
        animator?.SetTrigger("Hit");
        if (health <= 0)
        {
            SetState(EnemyState.Dead);
            animator?.SetTrigger("Dead");
            Died?.Invoke(this);
            enabled = false;
            Destroy(gameObject, 2f);
        }
    }
}

public static class EnemyBehaviourFactory
{
    public static IEnemyBehaviour Create(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Skeleton:
                return new MeleeEnemyBehaviour();
            case EnemyKind.CannonRobot:
                return new CannonEnemyBehaviour(false);
            case EnemyKind.RockGiant:
                return new RockGiantBehaviour();
            case EnemyKind.GiantCannonBoss:
                return new CannonEnemyBehaviour(true);
            default:
                return new MeleeEnemyBehaviour();
        }
    }
}

public abstract class BaseEnemyBehaviour : IEnemyBehaviour
{
    public EnemyState State { get; protected set; } = EnemyState.Idle;

    protected bool TryAttack(EnemyController enemy, Transform player)
    {
        float distance = Vector3.Distance(enemy.transform.position, player.position);
        if (distance > enemy.AttackRange)
        {
            return false;
        }

        PlayerController target = player.GetComponent<PlayerController>();
        if (target != null)
        {
            enemy.Attack(target);
        }
        enemy.SetState(EnemyState.Attack);
        return true;
    }

    protected void Chase(EnemyController enemy, Transform player)
    {
        Vector3 direction = player.position - enemy.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            enemy.SetState(EnemyState.Chase);
            enemy.Move(direction);
        }
    }

    public abstract void Tick(EnemyController enemy, Transform player);
}

public class MeleeEnemyBehaviour : BaseEnemyBehaviour
{
    public override void Tick(EnemyController enemy, Transform player)
    {
        if (enemy.IsDead)
        {
            return;
        }
        if (!TryAttack(enemy, player))
        {
            Chase(enemy, player);
        }
    }
}

public class RockGiantBehaviour : BaseEnemyBehaviour
{
    public override void Tick(EnemyController enemy, Transform player)
    {
        if (enemy.IsDead)
        {
            return;
        }
        if (!TryAttack(enemy, player))
        {
            Chase(enemy, player);
        }
    }
}

public class CannonEnemyBehaviour : BaseEnemyBehaviour
{
    private readonly bool isBoss;

    public CannonEnemyBehaviour(bool boss)
    {
        isBoss = boss;
    }

    public override void Tick(EnemyController enemy, Transform player)
    {
        if (enemy.IsDead)
        {
            return;
        }

        Vector3 direction = player.position - enemy.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            enemy.transform.rotation = Quaternion.Slerp(
                enemy.transform.rotation,
                Quaternion.LookRotation(direction),
                (isBoss ? 3f : 5f) * Time.deltaTime);
        }

        if (!TryAttack(enemy, player) && direction.magnitude > enemy.AttackRange)
        {
            enemy.SetState(EnemyState.Chase);
            enemy.Move(direction * 0.35f);
        }
        else
        {
            enemy.SetState(EnemyState.Idle);
        }
    }
}
