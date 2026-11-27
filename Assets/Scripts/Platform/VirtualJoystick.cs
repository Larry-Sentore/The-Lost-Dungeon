using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A joystick that appears wherever the finger first touches, and points toward where it is dragged.
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const float Radius = 110f;
    private RectTransform baseRect;
    private RectTransform knob;
    private System.Action<Vector2> output;
    private Vector2 start;

    // Creates the joystick graphics (hidden until touched).
    public void Build(System.Action<Vector2> onChanged)
    {
        output = onChanged;
        baseRect = CreateCircle("Stick Base", transform, Radius * 2f, new Color(1f, 1f, 1f, 0.15f));
        knob = CreateCircle("Stick Knob", baseRect, Radius * 0.9f, new Color(1f, 1f, 1f, 0.45f));
        baseRect.gameObject.SetActive(false);
    }

    // Places the joystick under the finger.
    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransform zone = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, eventData.pressEventCamera, out start);
        baseRect.anchoredPosition = start;
        knob.anchoredPosition = Vector2.zero;
        baseRect.gameObject.SetActive(true);
        output(Vector2.zero);
    }

    // Moves the knob and outputs a direction (length 0 to 1).
    public void OnDrag(PointerEventData eventData)
    {
        RectTransform zone = (RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, eventData.position, eventData.pressEventCamera, out Vector2 current);
        Vector2 offset = Vector2.ClampMagnitude(current - start, Radius);
        knob.anchoredPosition = offset;
        Vector2 value = offset / Radius;
        output(value.magnitude < 0.15f ? Vector2.zero : value);
    }

    // Hides the joystick and stops moving.
    public void OnPointerUp(PointerEventData eventData)
    {
        baseRect.gameObject.SetActive(false);
        output(Vector2.zero);
    }

    // Makes a coloured square used as the stick graphic.
    private static RectTransform CreateCircle(string name, Transform parent, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }
}
