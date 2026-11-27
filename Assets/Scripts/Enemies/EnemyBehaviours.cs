using System;
using System.Collections.Generic;
using UnityEngine;

// Creates the right behaviour for each enemy type. To add a new enemy type: write a behaviour class,
// add a profile in EnemyProfiles, and register it here. Nothing else needs to change.
public static class EnemyBehaviourRegistry
{
    private static readonly Dictionary<EnemyKind, Func<IEnemyBehaviour>> factories = new Dictionary<EnemyKind, Func<IEnemyBehaviour>>
    {
        { EnemyKind.Skeleton, () => new MeleeEnemyBehaviour() },
        { EnemyKind.CannonRobot, () => new CannonEnemyBehaviour() },
        { EnemyKind.RockGiant, () => new GuardianEnemyBehaviour() },
        { EnemyKind.GiantCannonBoss, () => new BossEnemyBehaviour() }
    };

    // Adds or replaces the behaviour for an enemy type.
    public static void Register(EnemyKind kind, Func<IEnemyBehaviour> factory)
    {
        factories[kind] = factory;
    }

    // Makes a new behaviour for an enemy type (melee is the fallback).
    public static IEnemyBehaviour Create(EnemyKind kind)
    {
        return factories.TryGetValue(kind, out Func<IEnemyBehaviour> factory) ? factory() : new MeleeEnemyBehaviour();
    }
}

// Shared code for every behaviour: owns the state machine and the stagger/death states.
public abstract class EnemyBehaviourBase : IEnemyBehaviour
{
    protected readonly StateMachine<EnemyState, EnemyController> machine = new StateMachine<EnemyState, EnemyController>();

    public EnemyState State => machine.CurrentId;

    // The state to go back to after a stagger.
    protected abstract EnemyState ResumeState { get; }

    // How long a normal hit staggers this enemy (0 = never).
    protected virtual float StaggerTime => 0.35f;

    // Each behaviour adds its own states here.
    protected abstract void BuildStates(EnemyController enemy);

    // Builds the states and starts in Idle.
    public void Initialise(EnemyController enemy)
    {
        BuildStates(enemy);
        machine.Add(new StaggerState(ResumeState));
        machine.Add(new DeadState());
        machine.Start(EnemyState.Idle, enemy);
    }

    // Runs the state machine for one frame.
    public void Tick(EnemyController enemy)
    {
        machine.Tick(enemy);
    }

    // By default damage is not changed.
    public virtual int ModifyIncomingDamage(EnemyController enemy, DamageInfo damage, int amount) => amount;

    // A hit with knockback staggers the enemy (interrupting its attack).
    public virtual void OnDamaged(EnemyController enemy, DamageInfo damage)
    {
        if (StaggerTime > 0f && damage.Knockback > 0f)
        {
            enemy.Stagger(StaggerTime);
            machine.ChangeTo(EnemyState.Stagger, enemy);
        }
    }

    // Switches to the dead state.
    public void OnDeath(EnemyController enemy)
    {
        machine.ChangeTo(EnemyState.Dead, enemy);
    }
}

// SKELETON (Warrior): rushes the player and slashes up close. Easy to interrupt.
public class MeleeEnemyBehaviour : EnemyBehaviourBase
{
    protected override EnemyState ResumeState => EnemyState.Chase;

    // Idle -> Chase -> Attack -> Chase ...
    protected override void BuildStates(EnemyController enemy)
    {
        machine.Add(new IdleState(EnemyState.Chase));
        machine.Add(new ChaseState(EnemyState.Attack, enemy.AttackRange * 0.85f));
        machine.Add(new MeleeAttackState(0.55f, 1.1f, 110f, 1f, 0.6f, new Color(1f, 0.4f, 0.3f)));
    }
}

// ROCK GIANT (Guardian): defensive. Raises its guard and blocks anything hitting it from the front,
// then slams the ground around it. Hit it from behind, during its slam recovery, or with Shield Bash.
public class GuardianEnemyBehaviour : EnemyBehaviourBase
{
    protected override EnemyState ResumeState => EnemyState.Guard;
    protected override float StaggerTime => 0.25f;

    // Idle -> Chase -> Guard -> Attack (slam) -> Guard ...
    protected override void BuildStates(EnemyController enemy)
    {
        machine.Add(new IdleState(EnemyState.Chase));
        machine.Add(new ChaseState(EnemyState.Guard, enemy.AttackRange * 1.6f));
        machine.Add(new GuardState(1.6f));
        // A wide slam with heavy knockback, followed by a long recovery where the giant is open.
        machine.Add(new MeleeAttackState(1.5f, 1.15f, 360f, 1f, 2.6f, new Color(1f, 0.6f, 0.15f), EnemyState.Guard));
    }

    // Blocks hits from the front while guarding, unless the hit breaks guards (Shield Bash).
    public override int ModifyIncomingDamage(EnemyController enemy, DamageInfo damage, int amount)
    {
        if (enemy.IsGuarding && !damage.BreaksGuard &&
            DamageCalculator.IsBlockedByGuard(enemy.transform.position, enemy.transform.forward, damage.SourcePosition, 140f))
        {
            return 0;
        }
        return amount;
    }

    // Shield Bash breaks the guard and leaves the giant staggered for a long time.
    public override void OnDamaged(EnemyController enemy, DamageInfo damage)
    {
        if (damage.BreaksGuard)
        {
            enemy.Stagger(1.4f);
            machine.ChangeTo(EnemyState.Stagger, enemy);
            return;
        }
        if (State != EnemyState.Attack)
        {
            base.OnDamaged(enemy, damage);
        }
    }
}

// CANNON ROBOT (Archer): cannot move. Turns to track the player and fires slow projectiles you can dodge.
public class CannonEnemyBehaviour : EnemyBehaviourBase
{
    protected override EnemyState ResumeState => EnemyState.Attack;
    protected override float StaggerTime => 0.2f;

    // Idle -> Attack (track and fire).
    protected override void BuildStates(EnemyController enemy)
    {
        machine.Add(new IdleState(EnemyState.Attack));
        machine.Add(new RangedAttackState(_ => 5f, Fire));
    }

    // Fires one projectile at the player.
    protected virtual void Fire(EnemyController enemy)
    {
        enemy.FireProjectile(enemy.AimDirectionFrom(enemy.Visuals.MuzzlePosition), 1f, 0.22f, new Color(0.2f, 0.75f, 1f));
    }
}

// GIANT CANNON BOSS: two phases. Phase 1 fires single shots with a three-way burst every third shot.
// Below half health it enters phase 2: turns faster, fires sooner, and every shot is a five-way fan.
public class BossEnemyBehaviour : CannonEnemyBehaviour
{
    private int phase = 1;
    private int shotsFired;

    protected override float StaggerTime => 0f;

    // Idle -> Attack, with a faster turn in phase 2.
    protected override void BuildStates(EnemyController enemy)
    {
        machine.Add(new IdleState(EnemyState.Attack));
        machine.Add(new RangedAttackState(_ => phase == 1 ? 2.5f : 4.5f, Fire));
    }

    // Fires the pattern for the current phase.
    protected override void Fire(EnemyController enemy)
    {
        shotsFired++;
        Vector3 aim = enemy.AimDirectionFrom(enemy.Visuals.MuzzlePosition);
        Color color = phase == 1 ? new Color(1f, 0.45f, 0.1f) : new Color(1f, 0.15f, 0.35f);
        int count = phase == 2 ? 5 : (shotsFired % 3 == 0 ? 3 : 1);
        float spread = phase == 2 ? 14f : 18f;
        for (int i = 0; i < count; i++)
        {
            float angle = (i - (count - 1) * 0.5f) * spread;
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * aim;
            enemy.FireProjectile(direction, 1f, 0.4f, color);
        }
    }

    // Enters phase 2 when health drops to half.
    public override void OnDamaged(EnemyController enemy, DamageInfo damage)
    {
        if (phase == 1 && enemy.Health01 <= 0.5f)
        {
            phase = 2;
            enemy.CooldownMultiplier = 0.6f;
            enemy.Visuals.Telegraph(new Color(1f, 0.1f, 0.2f), 1.5f);
            GameEvents.RaiseBossPhaseChanged(2);
            GameEvents.RaiseNotification("The Cannon King is enraged!");
        }
    }
}
