using System;
using System.Collections.Generic;
using UnityEngine;

// Keeps a stack of ready-made objects so we can reuse them instead of creating and destroying
// them all the time (Object Pooling pattern). Used for projectiles, hit sparks and loot pickups.
public class ObjectPool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly Stack<T> available = new Stack<T>();
    private readonly HashSet<T> inUse = new HashSet<T>();
    private readonly Action<T> onGet;
    private readonly Action<T> onRelease;

    public int CountActive => inUse.Count;
    public int CountInactive => available.Count;
    public int CountAll => inUse.Count + available.Count;

    // Creates a pool and makes some objects ahead of time.
    public ObjectPool(T prefab, int prewarmCount, Transform parent = null, Action<T> onGet = null, Action<T> onRelease = null)
    {
        this.prefab = prefab;
        this.parent = parent;
        this.onGet = onGet;
        this.onRelease = onRelease;
        Prewarm(prewarmCount);
    }

    // Creates objects now and hides them so they are ready to use later.
    public void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            T item = CreateItem();
            item.gameObject.SetActive(false);
            available.Push(item);
        }
    }

    // Takes an object from the pool (or makes a new one if the pool is empty), places it, and turns it on.
    public T Get(Vector3 position, Quaternion rotation)
    {
        T item = null;
        while (available.Count > 0 && item == null)
        {
            item = available.Pop();
        }
        if (item == null)
        {
            item = CreateItem();
        }

        item.transform.SetPositionAndRotation(position, rotation);
        item.gameObject.SetActive(true);
        inUse.Add(item);
        onGet?.Invoke(item);
        return item;
    }

    // Turns the object off and puts it back in the pool. Objects that are not in use are ignored.
    public bool Release(T item)
    {
        if (item == null || !inUse.Remove(item))
        {
            return false;
        }
        onRelease?.Invoke(item);
        item.gameObject.SetActive(false);
        available.Push(item);
        return true;
    }

    // Puts every object that is in use back in the pool.
    public void ReleaseAll()
    {
        T[] items = new T[inUse.Count];
        inUse.CopyTo(items);
        foreach (T item in items)
        {
            if (item == null)
            {
                inUse.Remove(item);
                continue;
            }
            Release(item);
        }
    }

    // Makes a new copy of the prefab.
    private T CreateItem()
    {
        return UnityEngine.Object.Instantiate(prefab, parent);
    }
}
