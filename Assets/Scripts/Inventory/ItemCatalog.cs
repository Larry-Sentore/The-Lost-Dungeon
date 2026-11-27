using System.Collections.Generic;
using UnityEngine;

// What an item is: its name, colour, and how it sorts.
public class ItemDefinition
{
    public string Id;
    public string DisplayName;
    public Color Color;
    public int SortOrder;
    public int ScoreValue;

    // Creates an item definition.
    public ItemDefinition(string id, string displayName, Color color, int sortOrder, int scoreValue)
    {
        Id = id;
        DisplayName = displayName;
        Color = color;
        SortOrder = sortOrder;
        ScoreValue = scoreValue;
    }
}

// Every item in the game. New items are added with Register, nothing else needs to change.
public static class ItemCatalog
{
    public const string Gem = "gem";
    public const string Potion = "potion";
    public const string Gold = "gold";

    private static readonly Dictionary<string, ItemDefinition> items = new Dictionary<string, ItemDefinition>();

    // Adds the built-in items.
    static ItemCatalog()
    {
        Register(new ItemDefinition(Potion, "Health Potion", new Color(0.95f, 0.2f, 0.25f), 0, 0));
        Register(new ItemDefinition(Gem, "Soul Gem", new Color(0.25f, 0.85f, 1f), 1, 50));
        Register(new ItemDefinition(Gold, "Gold Coins", new Color(1f, 0.8f, 0.2f), 2, 10));
    }

    // Adds or replaces an item definition.
    public static void Register(ItemDefinition definition)
    {
        items[definition.Id] = definition;
    }

    // Looks up an item. Unknown ids get a plain grey definition so the game never crashes.
    public static ItemDefinition Get(string id)
    {
        if (id != null && items.TryGetValue(id, out ItemDefinition definition))
        {
            return definition;
        }
        return new ItemDefinition(id, id, Color.gray, 99, 0);
    }
}
