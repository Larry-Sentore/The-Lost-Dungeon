using System;
using System.Collections.Generic;

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
    Stagger,
    Guard,
    Dead
}

// All the numbers that make an enemy type different. Kept as data so balancing never touches code.
[Serializable]
public class EnemyProfile
{
    public int MaxHealth = 2;
    public int Damage = 25;
    public float MoveSpeed = 2f;
    public float AttackRange = 1.7f;
    public float AttackCooldown = 1.6f;
    public float AggroRange = 14f;
    public float AttackWindup = 0.45f;
    public int Defence;
    public float KnockbackResistance;
    public float ProjectileSpeed = 8f;
    public float LootGemWeight = 55f;
    public float LootPotionWeight = 20f;
    public float LootNothingWeight = 25f;
    public int Bounty = 50;

    // Makes a copy so changing one enemy's numbers never changes the shared defaults.
    public EnemyProfile Clone() => (EnemyProfile)MemberwiseClone();
}

// The default numbers for each enemy type. A new enemy type only needs Register(...).
public static class EnemyProfiles
{
    private static readonly Dictionary<EnemyKind, EnemyProfile> profiles = new Dictionary<EnemyKind, EnemyProfile>();

    // Sets up the built-in enemy types.
    static EnemyProfiles()
    {
        Register(EnemyKind.Skeleton, new EnemyProfile
        {
            MaxHealth = 2, Damage = 20, MoveSpeed = 2.6f, AttackRange = 1.6f, AttackCooldown = 1.5f,
            AggroRange = 14f, AttackWindup = 0.45f, KnockbackResistance = 0f,
            LootGemWeight = 55f, LootPotionWeight = 20f, LootNothingWeight = 25f, Bounty = 50
        });
        Register(EnemyKind.CannonRobot, new EnemyProfile
        {
            MaxHealth = 2, Damage = 15, MoveSpeed = 0f, AttackRange = 10f, AttackCooldown = 2.4f,
            AggroRange = 12f, AttackWindup = 0.6f, KnockbackResistance = 0.85f, ProjectileSpeed = 8f,
            LootGemWeight = 60f, LootPotionWeight = 25f, LootNothingWeight = 15f, Bounty = 75
        });
        Register(EnemyKind.RockGiant, new EnemyProfile
        {
            MaxHealth = 3, Damage = 30, MoveSpeed = 1.9f, AttackRange = 2.4f, AttackCooldown = 2.4f,
            AggroRange = 12f, AttackWindup = 0.9f, KnockbackResistance = 0.55f,
            LootGemWeight = 50f, LootPotionWeight = 40f, LootNothingWeight = 10f, Bounty = 120
        });
        Register(EnemyKind.GiantCannonBoss, new EnemyProfile
        {
            MaxHealth = 10, Damage = 20, MoveSpeed = 0f, AttackRange = 15f, AttackCooldown = 2.6f,
            AggroRange = 18f, AttackWindup = 0.7f, KnockbackResistance = 1f, ProjectileSpeed = 10f,
            LootGemWeight = 100f, LootPotionWeight = 0f, LootNothingWeight = 0f, Bounty = 500
        });
    }

    // Adds or replaces the defaults for an enemy type.
    public static void Register(EnemyKind kind, EnemyProfile profile)
    {
        profiles[kind] = profile;
    }

    // Returns a copy of the defaults for an enemy type.
    public static EnemyProfile Get(EnemyKind kind)
    {
        return profiles.TryGetValue(kind, out EnemyProfile profile) ? profile.Clone() : new EnemyProfile();
    }
}
