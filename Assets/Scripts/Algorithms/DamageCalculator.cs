using UnityEngine;

// All damage maths lives here so every attacker and defender uses the same rules.
public static class DamageCalculator
{
    public const float CriticalMultiplier = 2f;

    // Works out final damage: base damage times a multiplier, doubled on a critical hit,
    // then defence is taken away. The result is never below zero.
    public static int Calculate(int baseDamage, float multiplier, int defence, bool critical)
    {
        float raw = Mathf.Max(0, baseDamage) * Mathf.Max(0f, multiplier) * (critical ? CriticalMultiplier : 1f);
        return Mathf.Max(0, Mathf.RoundToInt(raw) - Mathf.Max(0, defence));
    }

    // Decides if a hit is critical. roll is a random number from 0 to 1.
    public static bool RollCritical(float chance, float roll01)
    {
        return roll01 < Mathf.Clamp01(chance);
    }

    // Returns true if the attacker is in front of the defender, inside its guard angle (in degrees).
    // Used by the Rock Giant guardian: hits from the front are blocked, hits from behind get through.
    public static bool IsBlockedByGuard(Vector3 defenderPosition, Vector3 defenderForward, Vector3 attackerPosition, float guardAngleDegrees)
    {
        Vector3 toAttacker = attackerPosition - defenderPosition;
        toAttacker.y = 0f;
        defenderForward.y = 0f;
        if (toAttacker.sqrMagnitude < 0.0001f || defenderForward.sqrMagnitude < 0.0001f)
        {
            return false;
        }
        return Vector3.Angle(defenderForward, toAttacker) <= guardAngleDegrees * 0.5f;
    }

    // Works out the starting speed for a knockback that slows to a stop over "duration" seconds
    // and covers exactly "distance" metres. Speed falls in a straight line, so distance = speed * duration / 2.
    public static Vector3 KnockbackVelocity(Vector3 from, Vector3 to, float distance, float duration)
    {
        Vector3 direction = to - from;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f || distance <= 0f || duration <= 0f)
        {
            return Vector3.zero;
        }
        return direction.normalized * (2f * distance / duration);
    }

    // Returns how much health a heal restores without going over the maximum.
    public static float ApplyHeal(float current, float maximum, float amount)
    {
        return Mathf.Min(maximum, current + Mathf.Max(0f, amount));
    }
}
