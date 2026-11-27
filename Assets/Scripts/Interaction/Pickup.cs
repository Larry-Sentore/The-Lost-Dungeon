using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// A floating item dropped by enemies. Spins, bobs, and is collected when the player walks close.
[DisallowMultipleComponent]
public class Pickup : MonoBehaviour, ICollectable
{
    private const float CollectRadius = 1.3f;
    private const float MagnetRadius = 3f;

    private string itemId;
    private int quantity;
    private Vector3 basePosition;
    private float spawnTime;
    private Action<Pickup> release;
    private Transform player;

    public string ItemId => itemId;

    // Sets what this pickup gives and where it floats.
    public void Setup(string id, int amount, Vector3 position, Transform playerTransform, Action<Pickup> onRelease)
    {
        itemId = id;
        quantity = Mathf.Max(1, amount);
        basePosition = position + Vector3.up * 0.6f;
        transform.position = basePosition;
        spawnTime = Time.time;
        player = playerTransform;
        release = onRelease;

        Color color = ItemCatalog.Get(id).Color;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            r.SetPropertyBlock(block);
        }
    }

    // Spins and bobs, drifts toward a nearby player, and is collected when they are close enough.
    private void Update()
    {
        float age = Time.time - spawnTime;
        transform.Rotate(0f, 120f * Time.deltaTime, 0f, Space.World);
        Vector3 bob = Vector3.up * (Mathf.Sin(age * 3f) * 0.12f);

        if (player != null && age > 0.4f)
        {
            Vector3 toPlayer = player.position + Vector3.up * 0.8f - basePosition;
            float distance = toPlayer.magnitude;
            if (distance < CollectRadius)
            {
                PlayerController controller = player.GetComponent<PlayerController>();
                if (controller != null && !controller.IsDead)
                {
                    Collect(player.gameObject);
                    return;
                }
            }
            else if (distance < MagnetRadius)
            {
                basePosition += toPlayer.normalized * (6f * Time.deltaTime);
            }
        }
        transform.position = basePosition + bob;
    }

    // Adds the item to the collector's inventory and returns this pickup to the pool.
    public void Collect(GameObject collector)
    {
        PlayerController controller = collector != null ? collector.GetComponent<PlayerController>() : null;
        if (controller != null)
        {
            controller.Inventory.Add(itemId, quantity);
            GameEvents.RaiseItemCollected(itemId);
        }
        Action<Pickup> callback = release;
        release = null;
        if (callback != null)
        {
            callback(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
