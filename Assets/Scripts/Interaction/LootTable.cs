using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Decides what an enemy drops, using the weighted random pick algorithm.
public static class LootTable
{
    // Returns the item id to drop (or null for nothing). roll is a random number from 0 to 1.
    public static string Roll(EnemyProfile profile, float roll01)
    {
        float[] weights = { profile.LootGemWeight, profile.LootPotionWeight, profile.LootNothingWeight };
        int index = GameplayAlgorithms.WeightedRandomIndex(weights, roll01);
        switch (index)
        {
            case 0:
                return ItemCatalog.Gem;
            case 1:
                return ItemCatalog.Potion;
            default:
                return null;
        }
    }
}
