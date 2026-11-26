using System;
using System.Collections.Generic;

public interface IInventoryItem
{
    string ItemId { get; }
    int Quantity { get; set; }
}

public class InventoryItem : IInventoryItem
{
    public string ItemId { get; private set; }
    public int Quantity { get; set; }

    public InventoryItem(string itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}

public class InventorySystem : ICollectable
{
    private readonly Dictionary<string, IInventoryItem> items =
        new Dictionary<string, IInventoryItem>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IInventoryItem> Items => items.Values;

    public void Add(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return;
        }
        if (items.TryGetValue(itemId, out IInventoryItem existing))
        {
            existing.Quantity += quantity;
            GameEvents.RaiseInventoryChanged(itemId, existing.Quantity);
        }
        else
        {
            items[itemId] = new InventoryItem(itemId, quantity);
            GameEvents.RaiseInventoryChanged(itemId, quantity);
        }
    }

    public bool Remove(string itemId, int quantity)
    {
        if (!items.TryGetValue(itemId, out IInventoryItem item) || item.Quantity < quantity)
        {
            return false;
        }
        item.Quantity -= quantity;
        if (item.Quantity == 0)
        {
            items.Remove(itemId);
        }
        GameEvents.RaiseInventoryChanged(itemId, item.Quantity);
        return true;
    }

    public void Collect(UnityEngine.GameObject collector)
    {
        Add("Dungeon Collectible", 1);
    }
}
