using System.Collections.Generic;
using UnityEngine;

// Base class for anything the player can walk up to and use. Subclasses only say what happens.
public abstract class Interactable : MonoBehaviour, IInteractable
{
    private static readonly List<Interactable> active = new List<Interactable>();

    [SerializeField] private string prompt = "Use";
    [SerializeField] private float interactRadius = 2.2f;

    // Every interactable currently in the scene (searched by the player to find the closest one).
    public static IReadOnlyList<Interactable> Active => active;

    public string Prompt => prompt;
    public float InteractRadius => interactRadius;
    public Vector3 Position => transform.position;

    // Changes the prompt text and radius (used by objects built in code).
    protected void SetPrompt(string text, float radius)
    {
        prompt = text;
        interactRadius = radius;
    }

    // Adds this object to the list of interactables.
    protected virtual void OnEnable()
    {
        if (!active.Contains(this))
        {
            active.Add(this);
        }
    }

    // Removes this object from the list of interactables.
    protected virtual void OnDisable()
    {
        active.Remove(this);
    }

    // Returns true if the object can be used right now.
    public abstract bool CanInteract(GameObject interactor);

    // Uses the object.
    public abstract void Interact(GameObject interactor);
}
