using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// An on-screen button that reacts the moment a finger touches it.
public class TouchButton : MonoBehaviour, IPointerDownHandler
{
    public System.Action Pressed;

    // Fires the press and plays a click.
    public void OnPointerDown(PointerEventData eventData)
    {
        Pressed?.Invoke();
    }
}
