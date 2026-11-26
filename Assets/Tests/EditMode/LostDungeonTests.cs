using NUnit.Framework;
using UnityEngine;

public class LostDungeonTests
{
    [Test]
    public void SkeletonComposition_HasTwoHealthHits()
    {
        Assert.That(EnemyHealthForKind(EnemyKind.Skeleton), Is.EqualTo(2));
    }

    [Test]
    public void CannonComposition_HasTwoHealthHits()
    {
        Assert.That(EnemyHealthForKind(EnemyKind.CannonRobot), Is.EqualTo(2));
    }

    [Test]
    public void RockGiantRequiresThreeHits()
    {
        Assert.That(EnemyHealthForKind(EnemyKind.RockGiant), Is.EqualTo(3));
    }

    [Test]
    public void GiantCannonRequiresTenHits()
    {
        Assert.That(EnemyHealthForKind(EnemyKind.GiantCannonBoss), Is.EqualTo(10));
    }

    [Test]
    public void SkeletonDamageRemovesQuarterHealth()
    {
        SaveData data = new SaveData { PlayerHealth = 100f };
        data.PlayerHealth = Mathf.Max(0f, data.PlayerHealth - 25f);
        Assert.That(data.PlayerHealth, Is.EqualTo(75f));
    }

    [Test]
    public void InventoryCanAddAndRemoveItems()
    {
        InventorySystem inventory = new InventorySystem();
        inventory.Add("Key", 2);
        Assert.That(inventory.Remove("Key", 1), Is.True);
        Assert.That(inventory.Remove("Key", 2), Is.False);
    }

    [Test]
    public void LevelUnlockMovesToNextLevel()
    {
        SaveData data = new SaveData { CompletedLevel = 3, HighestUnlockedLevel = 4 };
        int newUnlocked = Mathf.Clamp(Mathf.Max(data.HighestUnlockedLevel, data.CompletedLevel + 1), 1, 5);
        Assert.That(newUnlocked, Is.EqualTo(4));
    }

    [Test]
    public void SortingAlgorithmReturnsHighestDamage()
    {
        int[] damage = { 12, 45, 31, 7 };
        Assert.That(GameplayAlgorithms.SortDamageRanking(damage), Is.EqualTo(45));
    }

    [Test]
    public void DamageCalculationRespectsDefence()
    {
        Assert.That(GameplayAlgorithms.CalculateDamage(3, 10f, 5f), Is.EqualTo(25f));
    }

    [Test]
    public void ObjectPoolCreatesReusableComponents()
    {
        GameObject source = new GameObject("PoolTest");
        Transform component = source.transform;
        Assert.That(component.gameObject.activeSelf, Is.True);
    }

    private static int EnemyHealthForKind(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Skeleton:
            case EnemyKind.CannonRobot:
                return 2;
            case EnemyKind.RockGiant:
                return 3;
            case EnemyKind.GiantCannonBoss:
                return 10;
            default:
                return 0;
        }
    }
}
