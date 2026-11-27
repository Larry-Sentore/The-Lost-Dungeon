using UnityEngine;

public enum LevelStatus
{
    Locked,
    Available,
    Completed
}

// The rules for unlocking levels and scoring. Pure code with no Unity objects, so it is easy to test.
public static class ProgressionRules
{
    public const int LevelCount = 5;

    // Returns true if the level exists and has been unlocked.
    public static bool IsUnlocked(SaveData data, int level)
    {
        return data != null && level >= 1 && level <= LevelCount && level <= data.HighestUnlockedLevel;
    }

    // Returns true if the level has been finished at least once.
    public static bool IsCompleted(SaveData data, int level)
    {
        return data != null && level >= 1 && level <= LevelCount && data.CompletedLevels[level - 1];
    }

    // Works out if a level is locked, available or completed (used by the level select screen).
    public static LevelStatus GetStatus(SaveData data, int level)
    {
        if (IsCompleted(data, level))
        {
            return LevelStatus.Completed;
        }
        return IsUnlocked(data, level) ? LevelStatus.Available : LevelStatus.Locked;
    }

    // Marks a level as finished and unlocks the next one. Returns the level that was newly unlocked, or 0.
    public static int MarkCompleted(SaveData data, int level)
    {
        if (data == null || level < 1 || level > LevelCount)
        {
            return 0;
        }
        Normalize(data);
        data.CompletedLevels[level - 1] = true;
        int before = data.HighestUnlockedLevel;
        data.HighestUnlockedLevel = Mathf.Clamp(Mathf.Max(data.HighestUnlockedLevel, level + 1), 1, LevelCount);
        return data.HighestUnlockedLevel > before ? data.HighestUnlockedLevel : 0;
    }

    // Fixes any missing or out-of-range values, for example after loading an old or edited save file.
    public static void Normalize(SaveData data)
    {
        if (data == null)
        {
            return;
        }
        data.HighestUnlockedLevel = Mathf.Clamp(data.HighestUnlockedLevel, 1, LevelCount);
        if (data.CompletedLevels == null || data.CompletedLevels.Length != LevelCount)
        {
            bool[] fixedLevels = new bool[LevelCount];
            if (data.CompletedLevels != null)
            {
                for (int i = 0; i < Mathf.Min(LevelCount, data.CompletedLevels.Length); i++)
                {
                    fixedLevels[i] = data.CompletedLevels[i];
                }
            }
            data.CompletedLevels = fixedLevels;
        }
        if (data.CompletedLevel > 0)
        {
            // Upgrade an old save: every level up to the old "completed" number counts as completed.
            for (int i = 0; i < Mathf.Min(LevelCount, data.CompletedLevel); i++)
            {
                data.CompletedLevels[i] = true;
            }
            data.CompletedLevel = 0;
        }
        data.Items ??= new System.Collections.Generic.List<InventoryEntry>();
        data.Settings ??= new SettingsData();
        data.Settings.MasterVolume = Mathf.Clamp01(data.Settings.MasterVolume);
        if (string.IsNullOrWhiteSpace(data.Settings.PlayerName))
        {
            data.Settings.PlayerName = "Adventurer";
        }
        data.Stats ??= new PlayerStats();
        if (data.Stats.BestTimes == null || data.Stats.BestTimes.Length != LevelCount)
        {
            data.Stats.BestTimes = new float[LevelCount];
        }
        data.Leaderboard ??= new System.Collections.Generic.List<LeaderboardRecord>();
        data.Unlockables ??= new System.Collections.Generic.List<string>();
        data.Bestiary ??= new System.Collections.Generic.List<BestiaryCacheEntry>();
    }

    // Works out the score for a finished level.
    // Points for the level number, a time bonus for finishing quickly, points per gem, a bounty,
    // and a penalty for damage taken. The score never drops below 100.
    public static int CalculateScore(int level, float seconds, int gemsCollected, int damageTaken, int bounty)
    {
        int levelPoints = level * 1000;
        int timeBonus = Mathf.Max(0, Mathf.RoundToInt((300f - seconds) * 3f));
        int gemPoints = gemsCollected * 50;
        int penalty = damageTaken * 2;
        return Mathf.Max(100, levelPoints + timeBonus + gemPoints + bounty - penalty);
    }
}
