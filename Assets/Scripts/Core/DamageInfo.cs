using UnityEngine;

// Everything about one hit: how much damage, where it came from and how hard it pushes.
public struct DamageInfo
{
    public int Amount;
    public Vector3 SourcePosition;
    public float Knockback;
    public GameObject Attacker;
    public bool BreaksGuard;
    public bool IsCritical;

    // Creates a hit description.
    public DamageInfo(int amount, Vector3 sourcePosition, float knockback = 0f, GameObject attacker = null, bool breaksGuard = false, bool isCritical = false)
    {
        Amount = amount;
        SourcePosition = sourcePosition;
        Knockback = knockback;
        Attacker = attacker;
        BreaksGuard = breaksGuard;
        IsCritical = isCritical;
    }
}
