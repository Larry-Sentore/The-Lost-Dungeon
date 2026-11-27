using System.Collections.Generic;
using UnityEngine;

// Finds the closest usable object near the player, shows its prompt, and uses it on the interact button.
public class PlayerInteractor : MonoBehaviour
{
    private Interactable current;
    private string shownPrompt;

    // Every frame: search for the nearest interactable in range and update the on-screen prompt.
    private void Update()
    {
        Interactable nearest = GameplayAlgorithms.FindNearest(transform.position, Interactable.Active,
            i => i.Position,
            i => i.isActiveAndEnabled && i.CanInteract(gameObject) &&
                 (i.Position - transform.position).sqrMagnitude <= i.InteractRadius * i.InteractRadius);
        current = nearest;

        string prompt = current != null ? $"[{PlatformBehaviours.InteractButtonLabel}] {current.Prompt}" : null;
        if (prompt != shownPrompt)
        {
            shownPrompt = prompt;
            GameEvents.RaiseInteractionPromptChanged(prompt);
        }
    }

    // Uses the closest interactable, if there is one.
    public void TryInteract()
    {
        if (current != null && current.CanInteract(gameObject))
        {
            current.Interact(gameObject);
        }
    }

    // Hides the prompt when the player is removed.
    private void OnDisable()
    {
        if (shownPrompt != null)
        {
            shownPrompt = null;
            GameEvents.RaiseInteractionPromptChanged(null);
        }
    }
}
