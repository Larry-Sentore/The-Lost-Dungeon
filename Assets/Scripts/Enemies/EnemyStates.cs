using System;
using UnityEngine;

// Base class for enemy states so each state only overrides what it needs.
public abstract class EnemyStateBase : IState<EnemyState, EnemyController>
{
    protected float enteredAt;

    public abstract EnemyState Id { get; }

    // Seconds spent in this state so far.
    protected float TimeInState => Time.time - enteredAt;

    // Remembers when the state started.
    public virtual void Enter(EnemyController enemy)
    {
        enteredAt = Time.time;
    }

    // Decides what to do this frame and which state comes next.
    public abstract EnemyState Tick(EnemyController enemy);

    // Runs when leaving the state.
    public virtual void Exit(EnemyController enemy) { }
}

// Standing still until the player comes close enough to notice.
public class IdleState : EnemyStateBase
{
    private readonly EnemyState engageState;

    public override EnemyState Id => EnemyState.Idle;

    // engageState is where the enemy goes when it notices the player (chase, or shoot for turrets).
    public IdleState(EnemyState engageState)
    {
        this.engageState = engageState;
    }

    // Stops the walk animation.
    public override void Enter(EnemyController enemy)
    {
        base.Enter(enemy);
        enemy.StopMoving();
    }

    // Waits until the living player is within aggro range.
    public override EnemyState Tick(EnemyController enemy)
    {
        enemy.StopMoving();
        if (enemy.PlayerAlive && enemy.DistanceToPlayer() <= enemy.Profile.AggroRange)
        {
            return engageState;
        }
        return Id;
    }
}

// Running at the player until close enough to do something else.
public class ChaseState : EnemyStateBase
{
    private readonly EnemyState closeState;
    private readonly float closeDistance;
    private readonly float speedMultiplier;

    public override EnemyState Id => EnemyState.Chase;

    // closeState is entered when the player is within closeDistance.
    public ChaseState(EnemyState closeState, float closeDistance, float speedMultiplier = 1f)
    {
        this.closeState = closeState;
        this.closeDistance = closeDistance;
        this.speedMultiplier = speedMultiplier;
    }

    // Moves toward the player. Gives up if the player dies or gets far away.
    public override EnemyState Tick(EnemyController enemy)
    {
        if (!enemy.PlayerAlive || enemy.DistanceToPlayer() > enemy.Profile.AggroRange * 1.6f)
        {
            return EnemyState.Idle;
        }
        float distance = enemy.DistanceToPlayer();
        if (distance <= closeDistance)
        {
            enemy.StopMoving();
            enemy.FacePlayer(10f);
            if (closeState != EnemyState.Attack || enemy.AttackReady)
            {
                return closeState;
            }
            return Id;
        }
        enemy.Move(enemy.DirectionToPlayer(), speedMultiplier);
        return Id;
    }
}

// A close-range attack: wind up (telegraphed), strike once, then recover.
public class MeleeAttackState : EnemyStateBase
{
    private readonly float recovery;
    private readonly float reachMultiplier;
    private readonly float arc;
    private readonly float damageMultiplier;
    private readonly float knockback;
    private readonly Color telegraphColor;
    private readonly EnemyState nextState;
    private readonly Action<EnemyController> onStrike;
    private bool struck;

    public override EnemyState Id => EnemyState.Attack;

    // Creates a melee attack. reachMultiplier scales the enemy's attack range for the hit check.
    public MeleeAttackState(float recovery, float reachMultiplier, float arc, float damageMultiplier, float knockback,
        Color telegraphColor, EnemyState nextState = EnemyState.Chase, Action<EnemyController> onStrike = null)
    {
        this.recovery = recovery;
        this.reachMultiplier = reachMultiplier;
        this.arc = arc;
        this.damageMultiplier = damageMultiplier;
        this.knockback = knockback;
        this.telegraphColor = telegraphColor;
        this.nextState = nextState;
        this.onStrike = onStrike;
    }

    // Starts the cooldown, the wind-up glow and the attack animation.
    public override void Enter(EnemyController enemy)
    {
        base.Enter(enemy);
        struck = false;
        enemy.StopMoving();
        enemy.MarkAttackStarted();
        enemy.Visuals.Telegraph(telegraphColor, enemy.Profile.AttackWindup);
        enemy.Visuals.PlayAttack(enemy.Profile.AttackWindup);
    }

    // Tracks the player during the first half of the wind-up, hits once, then waits out the recovery.
    public override EnemyState Tick(EnemyController enemy)
    {
        float windup = enemy.Profile.AttackWindup;
        if (TimeInState < windup * 0.6f)
        {
            enemy.FacePlayer(8f);
        }
        if (!struck && TimeInState >= windup)
        {
            struck = true;
            enemy.DealMeleeDamage(enemy.Profile.AttackRange * reachMultiplier, arc, damageMultiplier, knockback);
            onStrike?.Invoke(enemy);
        }
        return TimeInState >= windup + recovery ? nextState : Id;
    }
}

// A turret attack: turns toward the player and fires whenever the cooldown allows.
public class RangedAttackState : EnemyStateBase
{
    private readonly Func<EnemyController, float> turnSpeed;
    private readonly Action<EnemyController> fire;
    private bool charging;
    private float chargeStarted;

    public override EnemyState Id => EnemyState.Attack;

    // fire decides the firing pattern, so the boss and the normal cannon can share this state.
    public RangedAttackState(Func<EnemyController, float> turnSpeed, Action<EnemyController> fire)
    {
        this.turnSpeed = turnSpeed;
        this.fire = fire;
    }

    // Resets the charge-up.
    public override void Enter(EnemyController enemy)
    {
        base.Enter(enemy);
        charging = false;
    }

    // Aims, charges for the wind-up time (glowing), then fires.
    public override EnemyState Tick(EnemyController enemy)
    {
        if (!enemy.PlayerAlive || enemy.DistanceToPlayer() > enemy.Profile.AggroRange * 1.4f)
        {
            return EnemyState.Idle;
        }
        enemy.FacePlayer(turnSpeed(enemy));

        bool inRange = enemy.DistanceToPlayer() <= enemy.Profile.AttackRange;
        if (!charging && inRange && enemy.AttackReady && enemy.IsFacingPlayer(20f))
        {
            charging = true;
            chargeStarted = Time.time;
            enemy.MarkAttackStarted();
            enemy.Visuals.Telegraph(new Color(1f, 0.55f, 0.1f), enemy.Profile.AttackWindup);
        }
        if (charging && Time.time - chargeStarted >= enemy.Profile.AttackWindup)
        {
            charging = false;
            fire(enemy);
        }
        return Id;
    }
}

// Holding a shield stance: blocks hits from the front while slowly closing in (Rock Giant).
public class GuardState : EnemyStateBase
{
    private readonly float guardTime;

    public override EnemyState Id => EnemyState.Guard;

    // guardTime is how long the giant guards before it slams.
    public GuardState(float guardTime)
    {
        this.guardTime = guardTime;
    }

    // Raises the guard.
    public override void Enter(EnemyController enemy)
    {
        base.Enter(enemy);
        enemy.IsGuarding = true;
    }

    // Faces the player and edges closer; slams once guarded long enough and in range, chases if the player runs.
    public override EnemyState Tick(EnemyController enemy)
    {
        if (!enemy.PlayerAlive)
        {
            return EnemyState.Idle;
        }
        float distance = enemy.DistanceToPlayer();
        if (distance > enemy.AttackRange * 2.2f)
        {
            return EnemyState.Chase;
        }
        enemy.FacePlayer(5f);
        if (distance > enemy.AttackRange * 0.8f)
        {
            enemy.Move(enemy.DirectionToPlayer(), 0.45f);
        }
        else
        {
            enemy.StopMoving();
        }
        if (TimeInState >= guardTime && distance <= enemy.AttackRange && enemy.AttackReady)
        {
            return EnemyState.Attack;
        }
        return Id;
    }

    // Lowers the guard.
    public override void Exit(EnemyController enemy)
    {
        enemy.IsGuarding = false;
    }
}

// Knocked off balance: does nothing until the stagger time runs out.
public class StaggerState : EnemyStateBase
{
    private readonly EnemyState resumeState;

    public override EnemyState Id => EnemyState.Stagger;

    // resumeState is where the enemy goes when it recovers.
    public StaggerState(EnemyState resumeState)
    {
        this.resumeState = resumeState;
    }

    // Stops moving.
    public override void Enter(EnemyController enemy)
    {
        base.Enter(enemy);
        enemy.StopMoving();
    }

    // Waits for the stagger and knockback to finish.
    public override EnemyState Tick(EnemyController enemy)
    {
        return enemy.IsStaggered || enemy.IsBeingKnockedBack ? Id : resumeState;
    }
}

// Dead: does nothing.
public class DeadState : EnemyStateBase
{
    public override EnemyState Id => EnemyState.Dead;

    // Stays dead.
    public override EnemyState Tick(EnemyController enemy) => Id;
}
