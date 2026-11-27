using System.Collections.Generic;

// Counts how many users each key has. Content is only unloaded when its count drops to zero,
// so shared assets stay loaded while anything still uses them.
public class ReferenceCounter<TKey>
{
    private readonly Dictionary<TKey, int> counts = new Dictionary<TKey, int>();

    // Adds a user. Returns true if this is the first user (so the content needs loading).
    public bool Acquire(TKey key)
    {
        counts.TryGetValue(key, out int count);
        counts[key] = count + 1;
        return count == 0;
    }

    // Removes a user. Returns true if that was the last user (so the content can be unloaded).
    // Releasing something that was never acquired does nothing and returns false.
    public bool Release(TKey key)
    {
        if (!counts.TryGetValue(key, out int count) || count <= 0)
        {
            return false;
        }
        count--;
        if (count == 0)
        {
            counts.Remove(key);
            return true;
        }
        counts[key] = count;
        return false;
    }

    // How many users a key has right now.
    public int Count(TKey key) => counts.TryGetValue(key, out int count) ? count : 0;

    // Forgets every count.
    public void Clear() => counts.Clear();
}
