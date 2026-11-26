using UnityEngine;

public abstract class BaseAbility : IAbility
{
    public string AbilityName { get; protected set; }
    public float Cooldown { get; protected set; }
    public bool IsReady => Time.time >= readyTime;
    protected float readyTime;

    protected BaseAbility(string name, float cooldown)
    {
        AbilityName = name;
        Cooldown = cooldown;
        readyTime = 0f;
    }

    public abstract void Activate(GameObject owner);

    protected bool BeginCooldown()
    {
        if (!IsReady)
        {
            return false;
        }
        readyTime = Time.time + Cooldown;
        return true;
    }
}

public class SwordSlashAbility : BaseAbility
{
    private readonly float radius;

    public SwordSlashAbility(float radius) : base("Sword Slash", 4f)
    {
        this.radius = radius;
    }

    public override void Activate(GameObject owner)
    {
        if (!BeginCooldown())
        {
            return;
        }

        owner.GetComponent<Animator>()?.SetTrigger("Slash");
        Collider[] hits = Physics.OverlapSphere(owner.transform.position, radius);
        foreach (Collider hit in hits)
        {
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                enemy.TakeDamage(1);
            }
        }
    }
}

public class ShieldBashAbility : BaseAbility
{
    private readonly float radius;

    public ShieldBashAbility(float radius) : base("Shield Bash", 6f)
    {
        this.radius = radius;
    }

    public override void Activate(GameObject owner)
    {
        if (!BeginCooldown())
        {
            return;
        }

        owner.GetComponent<Animator>()?.SetTrigger("Shield");
        Collider[] hits = Physics.OverlapSphere(owner.transform.position, radius);
        foreach (Collider hit in hits)
        {
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                Vector3 push = (enemy.transform.position - owner.transform.position).normalized;
                enemy.transform.position += push * 1.5f;
            }
        }
    }
}
