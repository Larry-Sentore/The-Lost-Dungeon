using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly Stack<T> available = new Stack<T>();
    private readonly HashSet<T> inUse = new HashSet<T>();

    public ObjectPool(T prefab, int prewarmCount, Transform parent = null)
    {
        this.prefab = prefab;
        this.parent = parent;
        Prewarm(prewarmCount);
    }

    private void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            T item = UnityEngine.Object.Instantiate(prefab, parent);
            item.gameObject.SetActive(false);
            available.Push(item);
        }
    }

    public T Get(Vector3 position, Quaternion rotation)
    {
        T item = available.Count > 0
            ? available.Pop()
            : UnityEngine.Object.Instantiate(prefab, parent);
        item.transform.SetPositionAndRotation(position, rotation);
        item.gameObject.SetActive(true);
        inUse.Add(item);
        return item;
    }

    public void Release(T item)
    {
        if (item == null)
        {
            return;
        }
        inUse.Remove(item);
        item.gameObject.SetActive(false);
        available.Push(item);
    }

    public void ReleaseAll(Action<T> resetAction = null)
    {
        T[] items = new T[inUse.Count];
        inUse.CopyTo(items);
        foreach (T item in items)
        {
            resetAction?.Invoke(item);
            Release(item);
        }
    }
}
