using System;
using System.Collections.Generic;
using UnityEngine;

// The game's own algorithms. They are kept separate from MonoBehaviours so they can be unit tested.
public static class GameplayAlgorithms
{
    // SEARCHING: Linear nearest-neighbour search.
    // Goes through every item once and keeps the closest one that passes the filter.
    // Uses squared distance so no square root is needed. Returns null if nothing is in range.
    public static T FindNearest<T>(Vector3 origin, IEnumerable<T> items, Func<T, Vector3> positionOf,
        Func<T, bool> isValid = null, float maxDistance = float.PositiveInfinity) where T : class
    {
        if (items == null)
        {
            return null;
        }

        T nearest = null;
        float bestSqr = float.IsPositiveInfinity(maxDistance) ? float.PositiveInfinity : maxDistance * maxDistance;
        foreach (T item in items)
        {
            if (item == null || (isValid != null && !isValid(item)))
            {
                continue;
            }
            float sqr = (positionOf(item) - origin).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                nearest = item;
            }
        }
        return nearest;
    }

    // Finds the closest living enemy (used for the player's auto-aim).
    public static EnemyController FindNearestEnemy(Vector3 origin, IEnumerable<EnemyController> enemies, float maxDistance = float.PositiveInfinity)
    {
        return FindNearest(origin, enemies, e => e.transform.position, e => !e.IsDead, maxDistance);
    }

    // SORTING: Merge sort. Splits the list in half, sorts each half, then merges them back together.
    // It is stable (equal items keep their order) and always O(n log n). Returns a new sorted list.
    public static List<T> MergeSort<T>(IReadOnlyList<T> items, Comparison<T> compare)
    {
        List<T> result = new List<T>(items ?? Array.Empty<T>());
        if (result.Count < 2)
        {
            return result;
        }
        T[] buffer = new T[result.Count];
        MergeSortRange(result, buffer, 0, result.Count - 1, compare);
        return result;
    }

    // Sorts one part of the list (from left to right) using the merge sort steps.
    private static void MergeSortRange<T>(List<T> list, T[] buffer, int left, int right, Comparison<T> compare)
    {
        if (left >= right)
        {
            return;
        }
        int middle = (left + right) / 2;
        MergeSortRange(list, buffer, left, middle, compare);
        MergeSortRange(list, buffer, middle + 1, right, compare);

        int i = left;
        int j = middle + 1;
        int k = left;
        while (i <= middle && j <= right)
        {
            // "<= 0" keeps equal items in their original order (stable sort).
            buffer[k++] = compare(list[i], list[j]) <= 0 ? list[i++] : list[j++];
        }
        while (i <= middle)
        {
            buffer[k++] = list[i++];
        }
        while (j <= right)
        {
            buffer[k++] = list[j++];
        }
        for (k = left; k <= right; k++)
        {
            list[k] = buffer[k];
        }
    }

    // Sorts damage values from highest to lowest and returns the highest one.
    public static int SortDamageRanking(int[] damageValues)
    {
        if (damageValues == null || damageValues.Length == 0)
        {
            return 0;
        }
        List<int> sorted = MergeSort(damageValues, (a, b) => b.CompareTo(a));
        sorted.CopyTo(damageValues);
        return sorted[0];
    }

    // ITEM SELECTION: Weighted random pick (roulette wheel).
    // Adds up all weights, then walks the list until the running total passes roll * total.
    // roll is a number from 0 to 1 (passed in so tests can control it). Returns -1 if all weights are 0.
    public static int WeightedRandomIndex(IReadOnlyList<float> weights, float roll01)
    {
        if (weights == null || weights.Count == 0)
        {
            return -1;
        }
        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            total += Mathf.Max(0f, weights[i]);
        }
        if (total <= 0f)
        {
            return -1;
        }

        float target = Mathf.Clamp01(roll01) * total;
        float running = 0f;
        for (int i = 0; i < weights.Count; i++)
        {
            float weight = Mathf.Max(0f, weights[i]);
            running += weight;
            if (weight > 0f && target < running)
            {
                return i;
            }
        }
        // roll01 == 1 lands here: return the last item that has weight.
        for (int i = weights.Count - 1; i >= 0; i--)
        {
            if (weights[i] > 0f)
            {
                return i;
            }
        }
        return -1;
    }

    // NAVIGATION: Feeler-based steering (obstacle avoidance).
    // If the straight path is blocked, tries turning by each angle in order (e.g. 30, -30, 60, -60...)
    // and returns the first direction that is clear. Returns Vector3.zero if every direction is blocked.
    public static Vector3 ChooseSteeringDirection(Vector3 desired, Func<Vector3, bool> isBlocked, IReadOnlyList<float> turnAngles)
    {
        desired.y = 0f;
        if (desired.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }
        desired.Normalize();
        if (!isBlocked(desired))
        {
            return desired;
        }
        if (turnAngles != null)
        {
            for (int i = 0; i < turnAngles.Count; i++)
            {
                Vector3 candidate = Quaternion.AngleAxis(turnAngles[i], Vector3.up) * desired;
                if (!isBlocked(candidate))
                {
                    return candidate;
                }
            }
        }
        return Vector3.zero;
    }
}
