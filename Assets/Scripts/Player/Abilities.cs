using System;
using System.Collections.Generic;
using UnityEngine;

// Shared code for every ability: name, cooldown and the "is it ready" check (Strategy pattern -
// the player just calls Activate and each ability decides what that means).
public abstract class BaseAbility : IAbility
{
    private readonly Func<float> clock;
    private float readyTime;

    public string AbilityName { get; }
    public float Cooldown { get; }
    public bool IsReady => clock() >= readyTime;
    public float CooldownRemaining => Mathf.Max(0f, readyTime - clock());
    public float CooldownRemaining01 => Cooldown <= 0f ? 0f : Mathf.Clamp01(CooldownRemaining / Cooldown);

    // Sets up the ability with its name and cooldown. clock lets tests control time (defaults to game time).
    protected BaseAbility(string name, float cooldown, Func<float> clock = null)
    {
        AbilityName = name;
        Cooldown = cooldown;
        this.clock = clock ?? (() => Time.time);
        readyTime = 0f;
    }

    // Uses the ability if it is ready: starts the cooldown, runs the effect and tells listeners.
    public bool Activate(GameObject owner)
    {
        if (!IsReady)
        {
            return false;
        }
        readyTime = clock() + Cooldown;
        Execute(owner);
        GameEvents.RaiseAbilityActivated(AbilityName);
        GameEvents.RaiseAbilityChanged(AbilityName, false);
        return true;
    }

    // Resets the cooldown so the ability is ready straight away.
    public void ResetCooldown()
    {
        readyTime = 0f;
    }

    // What the ability actually does.
    protected abstract void Execute(GameObject owner);
}

// Sword Slash: a spinning slash that hits every enemy around the player.
public class SwordSlashAbility : BaseAbility
{
    private readonly float radius;

    // Creates the Sword Slash ability with the size of its hit area.
    public SwordSlashAbility(float radius, Func<float> clock = null) : base("Sword Slash", 4f, clock)
    {
        this.radius = radius;
    }

    // Plays the slash and hits everything in a full circle shortly after.
    protected override void Execute(GameObject owner)
    {
        PlayerController player = owner != null ? owner.GetComponent<PlayerController>() : null;
        if (player == null)
        {
            return;
        }
        player.PlayAnimationTrigger("Slash");
        player.PerformAreaAttack(radius, 1, 1.2f, 360f, false, 0.35f);
    }
}

// Shield Bash: a heavy shove in front of the player. Breaks a guardian's guard and sends enemies flying.
public class ShieldBashAbility : BaseAbility
{
    private readonly float radius;

    // Creates the Shield Bash ability with the size of its push area.
    public ShieldBashAbility(float radius, Func<float> clock = null) : base("Shield Bash", 6f, clock)
    {
        this.radius = radius;
    }

    // Plays the bash and hits enemies in a cone in front with big knockback.
    protected override void Execute(GameObject owner)
    {
        PlayerController player = owner != null ? owner.GetComponent<PlayerController>() : null;
        if (player == null)
        {
            return;
        }
        player.PlayAnimationTrigger("Shield");
        player.PerformAreaAttack(radius, 1, 3f, 120f, true, 0.25f);
    }
}

// The player's list of abilities, in button order. New abilities are added without changing the player.
public class AbilitySet
{
    private readonly List<IAbility> abilities = new List<IAbility>();
    private readonly List<bool> wasReady = new List<bool>();

    public int Count => abilities.Count;
    public IAbility this[int index] => abilities[index];

    // Adds an ability to the next free button.
    public void Add(IAbility ability)
    {
        abilities.Add(ability);
        wasReady.Add(true);
    }

    // Uses the ability in a slot. Returns false if there is none or it is cooling down.
    public bool TryActivate(int index, GameObject owner)
    {
        return index >= 0 && index < abilities.Count && abilities[index].Activate(owner);
    }

    // Raises "ready again" events for abilities whose cooldown just finished.
    public void UpdateReadyEvents()
    {
        for (int i = 0; i < abilities.Count; i++)
        {
            bool ready = abilities[i].IsReady;
            if (ready && !wasReady[i])
            {
                GameEvents.RaiseAbilityChanged(abilities[i].AbilityName, true);
            }
            wasReady[i] = ready;
        }
    }

    // Makes every ability ready (used at the start of a level).
    public void ResetAll()
    {
        foreach (IAbility ability in abilities)
        {
            (ability as BaseAbility)?.ResetCooldown();
            GameEvents.RaiseAbilityChanged(ability.AbilityName, true);
        }
        for (int i = 0; i < wasReady.Count; i++)
        {
            wasReady[i] = true;
        }
    }
}
