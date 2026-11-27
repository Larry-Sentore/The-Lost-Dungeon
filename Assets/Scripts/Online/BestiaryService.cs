using System;
using System.Collections.Generic;
using UnityEngine;

// The parts of the D&D 5e API monster response that the game uses.
[Serializable]
public class MonsterArmorClass
{
    public string type;
    public int value;
}

[Serializable]
public class MonsterAction
{
    public string name;
    public string desc;
}

[Serializable]
public class MonsterInfo
{
    public string index;
    public string name;
    public string size;
    public string type;
    public string alignment;
    public int hit_points;
    public float challenge_rating;
    public MonsterArmorClass[] armor_class;
    public MonsterAction[] actions;
}

// Fetches bestiary lore for each level's featured enemy from a public REST API
// (https://www.dnd5eapi.co). The monster's challenge rating also sets a score bounty.
// Results are cached in the save file, so the game still shows lore offline, and every
// failure just falls back to the cache or to built-in values.
public class BestiaryService : MonoBehaviour
{
    private const string BaseUrl = "https://www.dnd5eapi.co/api/2014/monsters/";
    private const int TimeoutSeconds = 8;

    private static readonly Dictionary<EnemyKind, string> monsterIndex = new Dictionary<EnemyKind, string>
    {
        { EnemyKind.Skeleton, "skeleton" },
        { EnemyKind.CannonRobot, "iron-golem" },
        { EnemyKind.RockGiant, "stone-golem" },
        { EnemyKind.GiantCannonBoss, "shield-guardian" }
    };

    private readonly Dictionary<EnemyKind, MonsterInfo> loaded = new Dictionary<EnemyKind, MonsterInfo>();
    private IRestClient client;

    public static BestiaryService Instance { get; private set; }

    // Sets up the singleton and listens for levels starting.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        client = new UnityRestClient();
        GameEvents.LevelStarted += OnLevelStarted;
    }

    // Stops listening.
    private void OnDestroy()
    {
        if (Instance == this)
        {
            GameEvents.LevelStarted -= OnLevelStarted;
            Instance = null;
        }
    }

    // Lets tests or other code swap the web client.
    public void SetClient(IRestClient restClient)
    {
        client = restClient ?? new UnityRestClient();
    }

    // The score bounty for a level's featured enemy: 50 points per challenge rating (10 to 600),
    // or the enemy's built-in bounty if the API has not answered.
    public int GetBounty(EnemyKind kind)
    {
        if (loaded.TryGetValue(kind, out MonsterInfo info) && info.challenge_rating > 0f)
        {
            return Mathf.Clamp(Mathf.RoundToInt(info.challenge_rating * 50f), 10, 600);
        }
        return EnemyProfiles.Get(kind).Bounty;
    }

    // When a level starts, fetch lore for its featured enemy.
    private void OnLevelStarted(LevelDefinition level)
    {
        if (level != null)
        {
            Request(level.FeaturedEnemy);
        }
    }

    // Gets the lore from the API (or the cache) and shows it.
    public void Request(EnemyKind kind)
    {
        if (!monsterIndex.TryGetValue(kind, out string index))
        {
            return;
        }
        if (loaded.TryGetValue(kind, out MonsterInfo cachedInfo))
        {
            Show(cachedInfo, false);
            return;
        }
        StartCoroutine(client.Get(BaseUrl + index, TimeoutSeconds, response => OnResponse(kind, index, response)));
    }

    // Handles the API answer: parse and cache it, or fall back to the saved copy.
    private void OnResponse(EnemyKind kind, string index, RestResponse response)
    {
        MonsterInfo info = response.Success ? Parse(response.Body) : null;
        if (info != null)
        {
            loaded[kind] = info;
            StoreInCache(index, response.Body);
            GameEvents.RaiseRequestStatusChanged("Bestiary updated", true);
            Show(info, false);
            return;
        }

        MonsterInfo cached = Parse(ReadCache(index));
        if (cached != null)
        {
            loaded[kind] = cached;
            Show(cached, true);
            GameEvents.RaiseRequestStatusChanged("Bestiary offline - showing saved entry", false);
        }
        else
        {
            GameEvents.RaiseRequestStatusChanged("Bestiary unavailable right now", false);
        }
    }

    // Turns the API's JSON into MonsterInfo. Returns null if it is not valid.
    public static MonsterInfo Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            MonsterInfo info = JsonUtility.FromJson<MonsterInfo>(json);
            return info != null && !string.IsNullOrEmpty(info.name) ? info : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Builds the one-line lore text shown in the HUD.
    public static string FormatLore(MonsterInfo info)
    {
        int armor = info.armor_class != null && info.armor_class.Length > 0 ? info.armor_class[0].value : 0;
        string text = $"Bestiary: {info.name} - {info.size} {info.type}, AC {armor}, HP {info.hit_points}, CR {info.challenge_rating:0.##}";
        if (info.actions != null && info.actions.Length > 0 && !string.IsNullOrEmpty(info.actions[0].name))
        {
            text += $". Known attack: {info.actions[0].name}";
        }
        return text;
    }

    // Shows the lore as an on-screen notification.
    private static void Show(MonsterInfo info, bool fromCache)
    {
        GameEvents.RaiseNotification(FormatLore(info) + (fromCache ? " (saved)" : ""));
    }

    // Saves the raw JSON in the save file for offline use.
    private static void StoreInCache(string index, string json)
    {
        List<BestiaryCacheEntry> cache = SaveSystem.Data.Bestiary;
        BestiaryCacheEntry entry = cache.Find(e => e.Index == index);
        if (entry == null)
        {
            cache.Add(new BestiaryCacheEntry { Index = index, Json = json });
        }
        else
        {
            entry.Json = json;
        }
        SaveSystem.Save();
    }

    // Reads the saved JSON for a monster (null if never fetched).
    private static string ReadCache(string index)
    {
        BestiaryCacheEntry entry = SaveSystem.Data.Bestiary.Find(e => e.Index == index);
        return entry?.Json;
    }
}
