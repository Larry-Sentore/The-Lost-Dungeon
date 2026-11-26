using System;
using System.IO;
using UnityEngine;

[Serializable]
public class SaveData
{
    public int HighestUnlockedLevel = 1;
    public int CompletedLevel = 0;
    public float PlayerHealth = 100f;
    public string[] Inventory = Array.Empty<string>();
}

public static class JsonProgressStore
{
    private static string SavePath => Path.Combine(Application.persistentDataPath, "lost-dungeon-save.json");

    public static void Save(SaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to save progress: {exception.Message}");
        }
    }

    public static SaveData Load()
    {
        try
        {
            if (!File.Exists(SavePath))
            {
                return null;
            }
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data == null)
            {
                return null;
            }
            data.HighestUnlockedLevel = Mathf.Clamp(data.HighestUnlockedLevel, 1, 5);
            data.CompletedLevel = Mathf.Clamp(data.CompletedLevel, 0, 5);
            data.PlayerHealth = Mathf.Clamp(data.PlayerHealth, 0f, 100f);
            data.Inventory ??= Array.Empty<string>();
            return data;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to load progress: {exception.Message}");
            return null;
        }
    }

    public static void MarkLevelComplete(int level)
    {
        SaveData data = Load() ?? new SaveData();
        data.CompletedLevel = Mathf.Max(data.CompletedLevel, level);
        data.HighestUnlockedLevel = Mathf.Clamp(Mathf.Max(data.HighestUnlockedLevel, level + 1), 1, 5);
        Save(data);
        GameEvents.RaiseLevelUnlockChanged(level + 1, true);
    }
}
