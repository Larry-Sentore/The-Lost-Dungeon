using System;
using System.Collections.Generic;

public interface IInventoryItem
{
    string ItemId { get; }
    int Quantity { get; set; }
}

// One stack of items in the inventory.
public class InventoryItem : IInventoryItem
{
    public string ItemId { get; private set; }
    public int Quantity { get; set; }

    // Creates an item with a name and an amount.
    public InventoryItem(string itemId, int quantity)
    {
        ItemId = itemId;
        Quantity = quantity;
    }
}

public enum InventorySortMode
{
    ByType,
    ByQuantity,
    ByName
}

// The player's inventory. Plain C# (no MonoBehaviour), so it can be tested and saved easily.
public class InventorySystem : ISaveable
{
    private readonly Dictionary<string, IInventoryItem> items =
        new Dictionary<string, IInventoryItem>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<IInventoryItem> Items => items.Values;

    // Raised with (item id, new amount) whenever something changes.
    public event Action<string, int> Changed;

    // Adds items. If we already have some, increases the count. Tells listeners it changed.
    public void Add(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return;
        }
        if (items.TryGetValue(itemId, out IInventoryItem existing))
        {
            existing.Quantity += quantity;
        }
        else
        {
            items[itemId] = new InventoryItem(itemId, quantity);
        }
        Notify(itemId);
    }

    // Removes items if we have enough. Returns false if we do not.
    public bool Remove(string itemId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0 ||
            !items.TryGetValue(itemId, out IInventoryItem item) || item.Quantity < quantity)
        {
            return false;
        }
        item.Quantity -= quantity;
        if (item.Quantity == 0)
        {
            items.Remove(itemId);
        }
        Notify(itemId);
        return true;
    }

    // Returns how many of an item we have (0 if none).
    public int Count(string itemId)
    {
        return itemId != null && items.TryGetValue(itemId, out IInventoryItem item) ? item.Quantity : 0;
    }

    // Returns the items sorted for display, using the game's own merge sort.
    public List<IInventoryItem> GetSorted(InventorySortMode mode)
    {
        List<IInventoryItem> list = new List<IInventoryItem>(items.Values);
        Comparison<IInventoryItem> compare;
        switch (mode)
        {
            case InventorySortMode.ByQuantity:
                compare = (a, b) => b.Quantity.CompareTo(a.Quantity);
                break;
            case InventorySortMode.ByName:
                compare = (a, b) => string.Compare(ItemCatalog.Get(a.ItemId).DisplayName, ItemCatalog.Get(b.ItemId).DisplayName, StringComparison.OrdinalIgnoreCase);
                break;
            default:
                compare = (a, b) => ItemCatalog.Get(a.ItemId).SortOrder.CompareTo(ItemCatalog.Get(b.ItemId).SortOrder);
                break;
        }
        return GameplayAlgorithms.MergeSort(list, compare);
    }

    // Removes everything.
    public void Clear()
    {
        List<string> ids = new List<string>(items.Keys);
        items.Clear();
        foreach (string id in ids)
        {
            Notify(id);
        }
    }

    // Writes the inventory into the save data.
    public void CaptureState(SaveData data)
    {
        data.Items = new List<InventoryEntry>();
        foreach (IInventoryItem item in items.Values)
        {
            data.Items.Add(new InventoryEntry { ItemId = item.ItemId, Quantity = item.Quantity });
        }
    }

    // Loads the inventory from the save data.
    public void RestoreState(SaveData data)
    {
        items.Clear();
        if (data?.Items != null)
        {
            foreach (InventoryEntry entry in data.Items)
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.ItemId) && entry.Quantity > 0)
                {
                    items[entry.ItemId] = new InventoryItem(entry.ItemId, entry.Quantity);
                }
            }
        }
        foreach (IInventoryItem item in items.Values)
        {
            Notify(item.ItemId);
        }
    }

    // Tells listeners (and the global event board) that an item amount changed.
    private void Notify(string itemId)
    {
        int quantity = Count(itemId);
        Changed?.Invoke(itemId, quantity);
        GameEvents.RaiseInventoryChanged(itemId, quantity);
    }
}
