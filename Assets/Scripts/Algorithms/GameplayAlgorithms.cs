using System.Collections.Generic;
using UnityEngine;

public static class GameplayAlgorithms
{
    public static EnemyController FindNearestEnemy(Vector3 origin, IEnumerable<EnemyController> enemies)
    {
        EnemyController nearest = null;
        float nearestDistance = float.MaxValue;
        if (enemies == null)
        {
            return null;
        }

        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
            {
                continue;
            }
            float distance = (enemy.transform.position - origin).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = enemy;
            }
        }
        return nearest;
    }

    public static int SortDamageRanking(int[] damageValues)
    {
        if (damageValues == null || damageValues.Length == 0)
        {
            return 0;
        }

        for (int i = 0; i < damageValues.Length - 1; i++)
        {
            for (int j = i + 1; j < damageValues.Length; j++)
            {
                if (damageValues[j] > damageValues[i])
                {
                    (damageValues[i], damageValues[j]) = (damageValues[j], damageValues[i]);
                }
            }
        }
        return damageValues[0];
    }

    public static float CalculateDamage(int hits, float baseDamage, float defence)
    {
        return Mathf.Max(0f, hits * baseDamage - defence);
    }
}
