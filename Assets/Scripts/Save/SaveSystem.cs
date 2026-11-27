using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Saves to a JSON file on disk (Windows, Android, iOS).
public class FileSaveStorage : ISaveStorage
{
    private readonly string path;

    // Creates file storage at the given path.
    public FileSaveStorage(string path)
    {
        this.path = path;
    }

    // Reads the save file, if there is one.
    public bool TryRead(out string json)
    {
        json = null;
        if (!File.Exists(path))
        {
            return false;
        }
        json = File.ReadAllText(path);
        return true;
    }

    // Writes to a temp file first and then swaps it in, so a crash mid-save cannot corrupt the save.
    public void Write(string json)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, json);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        File.Move(temp, path);
    }

    // Deletes the save file.
    public void Delete()
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

// Saves the same JSON inside PlayerPrefs. On WebGL, PlayerPrefs is stored in the browser's IndexedDB,
// which is more reliable there than writing files.
public class PlayerPrefsSaveStorage : ISaveStorage
{
    private readonly string key;

    // Creates PlayerPrefs storage under a key.
    public PlayerPrefsSaveStorage(string key)
    {
        this.key = key;
    }

    // Reads the saved JSON, if there is any.
    public bool TryRead(out string json)
    {
        json = PlayerPrefs.GetString(key, null);
        return !string.IsNullOrEmpty(json);
    }

    // Writes the JSON and flushes it to storage straight away.
    public void Write(string json)
    {
        PlayerPrefs.SetString(key, json);
        PlayerPrefs.Save();
    }

    // Removes the saved JSON.
    public void Delete()
    {
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }
}

// Keeps the save in memory only. Used by unit tests.
public class MemorySaveStorage : ISaveStorage
{
    public string Json;

    // Returns the stored text, if any.
    public bool TryRead(out string json)
    {
        json = Json;
        return !string.IsNullOrEmpty(json);
    }

    // Stores the text.
    public void Write(string json) => Json = json;

    // Clears the stored text.
    public void Delete() => Json = null;
}

// The save system. Holds the current save data, turns it into JSON and back,
// and lets any ISaveable object add its own data before saving.
public static class SaveSystem
{
    private const string SaveFileName = "lost-dungeon-save.json";
    private static ISaveStorage storage;
    private static SaveData cached;
    private static readonly List<ISaveable> saveables = new List<ISaveable>();

    // The storage in use. Chosen per platform the first time it is needed.
    public static ISaveStorage Storage
    {
        get => storage ??= CreateDefaultStorage();
        set
        {
            storage = value;
            cached = null;
        }
    }

    // The current save data. Loaded from storage the first time it is used.
    public static SaveData Data => cached ??= Load();

    // Picks the right storage for the platform we are built for.
    private static ISaveStorage CreateDefaultStorage()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return new PlayerPrefsSaveStorage("lost-dungeon-save");
#else
        return new FileSaveStorage(Path.Combine(Application.persistentDataPath, SaveFileName));
#endif
    }

    // Reads the save from storage. Returns fresh data if there is no save or it cannot be read.
    public static SaveData Load()
    {
        try
        {
            if (Storage.TryRead(out string json))
            {
                return Deserialize(json);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not read the save, starting fresh: {exception.Message}");
        }
        SaveData fresh = new SaveData();
        ProgressionRules.Normalize(fresh);
        return fresh;
    }

    // Lets every registered ISaveable write its data, then writes everything to storage.
    public static void Save()
    {
        foreach (ISaveable saveable in saveables.ToArray())
        {
            saveable?.CaptureState(Data);
        }
        try
        {
            Storage.Write(Serialize(Data));
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to save progress: {exception.Message}");
        }
    }

    // Turns save data into JSON text.
    public static string Serialize(SaveData data) => JsonUtility.ToJson(data, true);

    // Turns JSON text back into save data. Broken text gives fresh data instead of crashing.
    public static SaveData Deserialize(string json)
    {
        SaveData data = null;
        try
        {
            data = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Save file was not valid JSON: {exception.Message}");
        }
        data ??= new SaveData();
        ProgressionRules.Normalize(data);
        return data;
    }

    // Adds an object whose data should be saved, and gives it the saved data straight away.
    public static void Register(ISaveable saveable)
    {
        if (saveable != null && !saveables.Contains(saveable))
        {
            saveables.Add(saveable);
            saveable.RestoreState(Data);
        }
    }

    // Stops saving an object's data.
    public static void Unregister(ISaveable saveable)
    {
        saveables.Remove(saveable);
    }

    // Starts a new game: clears progress, inventory and stats but keeps the player's settings and leaderboard.
    public static void ResetProgress()
    {
        SaveData fresh = new SaveData
        {
            Settings = Data.Settings,
            Leaderboard = Data.Leaderboard,
            Bestiary = Data.Bestiary
        };
        ProgressionRules.Normalize(fresh);
        cached = fresh;
        foreach (ISaveable saveable in saveables.ToArray())
        {
            saveable?.RestoreState(cached);
        }
        Save();
    }

    // Returns true if there is saved progress to continue from.
    public static bool HasProgress()
    {
        SaveData data = Data;
        return data.HighestUnlockedLevel > 1 || data.Items.Count > 0 || Array.Exists(data.CompletedLevels, c => c);
    }
}
