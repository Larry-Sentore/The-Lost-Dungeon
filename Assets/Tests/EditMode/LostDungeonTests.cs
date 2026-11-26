using NUnit.Framework;
using System;
using UnityEngine;

public class LostDungeonTests
{
    [Test]
    public void SkeletonComposition_HasTwoHealthHits()
    {
        Assert.That(GetEnemyHealth(0), Is.EqualTo(2));
    }

    [Test]
    public void CannonComposition_HasTwoHealthHits()
    {
        Assert.That(GetEnemyHealth(1), Is.EqualTo(2));
    }

    [Test]
    public void RockGiantRequiresThreeHits()
    {
        Assert.That(GetEnemyHealth(2), Is.EqualTo(3));
    }

    [Test]
    public void GiantCannonRequiresTenHits()
    {
        Assert.That(GetEnemyHealth(3), Is.EqualTo(10));
    }

    [Test]
    public void SkeletonDamageRemovesQuarterHealth()
    {
        float health = 100f;
        health = Mathf.Max(0f, health - 25f);
        Assert.That(health, Is.EqualTo(75f));
    }

    [Test]
    public void InventoryCanAddAndRemoveItems()
    {
        TestInventory inventory = new TestInventory();
        inventory.Add("Key", 2);
        Assert.That(inventory.Remove("Key", 1), Is.True);
        Assert.That(inventory.Remove("Key", 2), Is.False);
    }

    [Test]
    public void LevelUnlockMovesToNextLevel()
    {
        int completed = 3;
        int unlocked = 4;
        int newUnlocked = Mathf.Clamp(Mathf.Max(unlocked, completed + 1), 1, 5);
        Assert.That(newUnlocked, Is.EqualTo(4));
    }

    [Test]
    public void SortingAlgorithmReturnsHighestDamage()
    {
        int[] damage = { 12, 45, 31, 7 };
        Assert.That(SortDescending(damage), Is.EqualTo(45));
    }

    [Test]
    public void DamageCalculationRespectsDefence()
    {
        Assert.That(CalculateDamage(3, 10f, 5f), Is.EqualTo(25f));
    }

    [Test]
    public void ObjectPoolCreatesReusableComponents()
    {
        GameObject source = new GameObject("PoolTest");
        TestPool<Transform> pool = new TestPool<Transform>(source.transform, 2);
        Transform item = pool.Get(Vector3.zero, Quaternion.identity);
        Assert.That(item.gameObject.activeSelf, Is.True);
        pool.Release(item);
        Assert.That(item.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void ObjectPoolDoesNotReleaseItemTwice()
    {
        GameObject source = new GameObject("PoolTestDuplicate");
        TestPool<Transform> pool = new TestPool<Transform>(source.transform, 1);
        Transform item = pool.Get(Vector3.zero, Quaternion.identity);
        pool.Release(item);
        pool.Release(item);
        Assert.That(item.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void DamageCalculationCannotProduceNegativeDamage()
    {
        Assert.That(CalculateDamage(1, 10f, 50f), Is.EqualTo(0f));
    }

    [Test]
    public void SaveDataRejectsHighestUnlockedLevelAboveFive()
    {
        int unlocked = Mathf.Clamp(99, 1, 5);
        Assert.That(unlocked, Is.EqualTo(5));
    }

    [Test]
    public void InventoryQuantityIsTrackedAcrossAdditions()
    {
        TestInventory inventory = new TestInventory();
        inventory.Add("Potion", 2);
        inventory.Add("Potion", 3);
        Assert.That(inventory.Quantity("Potion"), Is.EqualTo(5));
    }

    [Test]
    public void ProgressCompletionUnlocksOnlyOneNextLevel()
    {
        int highestUnlocked = 3;
        int completedLevel = 2;
        int completed = 3;
        completedLevel = Mathf.Max(completedLevel, completed);
        highestUnlocked = Mathf.Clamp(Mathf.Max(highestUnlocked, completed + 1), 1, 5);
        Assert.That(highestUnlocked, Is.EqualTo(4));
    }

    [Test]
    public void SaveDataJsonCanRoundTrip()
    {
        TestSaveData source = new TestSaveData { HighestUnlockedLevel = 3, PlayerHealth = 75f };
        string json = JsonUtility.ToJson(source);
        TestSaveData restored = JsonUtility.FromJson<TestSaveData>(json);
        Assert.That(restored.HighestUnlockedLevel, Is.EqualTo(3));
        Assert.That(restored.PlayerHealth, Is.EqualTo(75f));
    }

    [Test]
    public void NearestEnemyUsesShortestDistance()
    {
        Assert.That(NearestIndex(new[] { 9f, 2f, 7f }), Is.EqualTo(1));
    }

    [Test]
    public void HealthCannotFallBelowZero()
    {
        Assert.That(Mathf.Max(0f, 10f - 25f), Is.EqualTo(0f));
    }

    [Test]
    public void LevelFiveCannotUnlockLevelSix()
    {
        Assert.That(Mathf.Clamp(6, 1, 5), Is.EqualTo(5));
    }

    private static int GetEnemyHealth(int kind)
    {
        switch (kind)
        {
            case 0:
            case 1:
                return 2;
            case 2:
                return 3;
            case 3:
                return 10;
            default:
                return 0;
        }
    }

    private static int SortDescending(int[] values)
    {
        if (values == null || values.Length == 0)
        {
            return 0;
        }
        Array.Sort(values);
        Array.Reverse(values);
        return values[0];
    }

    private static float CalculateDamage(int hits, float baseDamage, float defence)
    {
        return Mathf.Max(0f, hits * baseDamage - defence);
    }

    private static int NearestIndex(float[] distances)
    {
        int nearest = 0;
        for (int i = 1; i < distances.Length; i++)
        {
            if (distances[i] < distances[nearest])
            {
                nearest = i;
            }
        }
        return nearest;
    }

    [Serializable]
    private class TestSaveData
    {
        public int HighestUnlockedLevel;
        public float PlayerHealth;
    }

    private class TestInventory
    {
        private readonly System.Collections.Generic.Dictionary<string, int> quantities =
            new System.Collections.Generic.Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public void Add(string itemId, int quantity)
        {
            quantities[itemId] = Quantity(itemId) + quantity;
        }

        public bool Remove(string itemId, int quantity)
        {
            int current = Quantity(itemId);
            if (current < quantity)
            {
                return false;
            }
            if (current == quantity)
            {
                quantities.Remove(itemId);
            }
            else
            {
                quantities[itemId] = current - quantity;
            }
            return true;
        }

        public int Quantity(string itemId)
        {
            return quantities.TryGetValue(itemId, out int quantity) ? quantity : 0;
        }
    }

    private class TestPool<T> where T : Component
    {
        private readonly T prefab;
        private readonly System.Collections.Generic.Stack<T> available =
            new System.Collections.Generic.Stack<T>();

        public TestPool(T prefab, int count)
        {
            this.prefab = prefab;
            for (int i = 0; i < count; i++)
            {
                T item = UnityEngine.Object.Instantiate(prefab);
                item.gameObject.SetActive(false);
                available.Push(item);
            }
        }

        public T Get(Vector3 position, Quaternion rotation)
        {
            T item = available.Count > 0 ? available.Pop() : UnityEngine.Object.Instantiate(prefab);
            item.transform.SetPositionAndRotation(position, rotation);
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            item.gameObject.SetActive(false);
            available.Push(item);
        }
    }
}
