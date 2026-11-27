using System;
using System.Collections;
using UnityEngine;

// The treasure chest that appears when every enemy in a level is defeated.
// Opening it gives the level's rewards and finishes the level.
public class TreasureChest : Interactable
{
    private Transform lid;
    private Light glow;
    private bool opened;
    private int gems;
    private int gold;
    private Action onOpened;

    // Builds a chest out of simple shapes at a position and returns it.
    public static TreasureChest Spawn(Vector3 position, Quaternion rotation, int gemReward, int goldReward, Action opened)
    {
        GameObject root = new GameObject("Treasure Chest");
        root.transform.SetPositionAndRotation(position, rotation);

        Color wood = new Color(0.45f, 0.26f, 0.12f);
        Color metal = new Color(1f, 0.78f, 0.25f);
        CreatePart(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.6f, 0.7f), wood);
        CreatePart(PrimitiveType.Cube, "Band", root.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.14f, 0.12f, 0.74f), metal);

        // The lid hinges from the back edge.
        GameObject hinge = new GameObject("Lid Hinge");
        hinge.transform.SetParent(root.transform, false);
        hinge.transform.localPosition = new Vector3(0f, 0.6f, -0.35f);
        CreatePart(PrimitiveType.Cube, "Lid", hinge.transform, new Vector3(0f, 0.12f, 0.35f), new Vector3(1.12f, 0.24f, 0.72f), wood);
        CreatePart(PrimitiveType.Cube, "Lock", hinge.transform, new Vector3(0f, 0.05f, 0.72f), new Vector3(0.18f, 0.22f, 0.06f), metal);

        BoxCollider solid = root.AddComponent<BoxCollider>();
        solid.center = new Vector3(0f, 0.42f, 0f);
        solid.size = new Vector3(1.1f, 0.84f, 0.7f);

        GameObject lightObject = new GameObject("Glow");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = metal;
        light.range = 4f;
        light.intensity = 2f;

        TreasureChest chest = root.AddComponent<TreasureChest>();
        chest.lid = hinge.transform;
        chest.glow = light;
        chest.gems = gemReward;
        chest.gold = goldReward;
        chest.onOpened = opened;
        chest.SetPrompt("Open the treasure chest", 2.4f);
        return chest;
    }

    // Makes one coloured shape of the chest (without its own collider).
    private static void CreatePart(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Renderer r = part.GetComponent<Renderer>();
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        r.SetPropertyBlock(block);
    }

    // Makes the chest's light pulse so it is easy to spot.
    private void Update()
    {
        if (glow != null && !opened)
        {
            glow.intensity = 1.6f + Mathf.Sin(Time.time * 3f) * 0.6f;
        }
    }

    // A chest can be opened once.
    public override bool CanInteract(GameObject interactor) => !opened;

    // Opens the lid, gives the rewards to the player and tells the level it is done.
    public override void Interact(GameObject interactor)
    {
        if (opened)
        {
            return;
        }
        opened = true;
        PlayerController player = interactor != null ? interactor.GetComponent<PlayerController>() : null;
        if (player != null)
        {
            player.Inventory.Add(ItemCatalog.Gem, gems);
            player.Inventory.Add(ItemCatalog.Gold, gold);
            player.Inventory.Add(ItemCatalog.Potion, 1);
        }
        SaveSystem.Data.Stats.ChestsOpened++;
        GameEvents.RaiseChestOpened();
        GameEvents.RaiseNotification($"Found {gems} soul gems, {gold} gold and a potion!");
        StartCoroutine(OpenRoutine());
    }

    // Swings the lid open, then calls back after a short pause so the player sees the reward.
    private IEnumerator OpenRoutine()
    {
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            lid.localRotation = Quaternion.Euler(-110f * k, 0f, 0f);
            yield return null;
        }
        if (glow != null)
        {
            glow.intensity = 4f;
        }
        yield return new WaitForSeconds(1.0f);
        onOpened?.Invoke();
    }
}
