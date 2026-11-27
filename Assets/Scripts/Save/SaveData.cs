using System;
using System.Collections.Generic;

// One item stack in the saved inventory.
[Serializable]
public class InventoryEntry
{
    public string ItemId;
    public int Quantity;
}

// Player options that are remembered between sessions.
[Serializable]
public class SettingsData
{
    public float MasterVolume = 0.8f;
    public bool MusicEnabled = true;
    public string PlayerName = "Adventurer";
}

// Lifetime numbers for the player.
[Serializable]
public class PlayerStats
{
    public int EnemiesDefeated;
    public int Deaths;
    public int ChestsOpened;
    public int PotionsUsed;
    public int TotalScore;
    public float[] BestTimes = new float[ProgressionRules.LevelCount];
}

// One row on the local leaderboard.
[Serializable]
public class LeaderboardRecord
{
    public string PlayerName;
    public int Score;
    public int Level;
}

// A saved copy of a bestiary entry from the REST API, used when the player is offline.
[Serializable]
public class BestiaryCacheEntry
{
    public string Index;
    public string Json;
}

// Everything written to the JSON save file.
[Serializable]
public class SaveData
{
    public int Version = 2;
    public int HighestUnlockedLevel = 1;
    public bool[] CompletedLevels = new bool[ProgressionRules.LevelCount];
    // Old (version 1) saves stored only the highest completed level. Kept so old saves can be upgraded.
    public int CompletedLevel;
    public List<InventoryEntry> Items = new List<InventoryEntry>();
    public SettingsData Settings = new SettingsData();
    public PlayerStats Stats = new PlayerStats();
    public List<LeaderboardRecord> Leaderboard = new List<LeaderboardRecord>();
    public List<string> Unlockables = new List<string>();
    public List<BestiaryCacheEntry> Bestiary = new List<BestiaryCacheEntry>();
}
