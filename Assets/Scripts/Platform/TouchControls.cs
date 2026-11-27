using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// On-screen controls for phones and tablets: a floating joystick on the left half of the screen
// and buttons on the right. Built in code so the scene does not need extra objects.
public class TouchControls : MonoBehaviour
{
    private static Vector2 moveVector;
    private static bool attackPressed;
    private static bool abilityOnePressed;
    private static bool abilityTwoPressed;
    private static bool interactPressed;
    private static bool potionPressed;
    private static bool pausePressed;

    private GameObject root;

    // Reads the controls for this frame. Button presses only count once.
    public static PlayerInputFrame ReadFrame()
    {
        PlayerInputFrame frame = new PlayerInputFrame
        {
            Move = moveVector,
            Attack = attackPressed,
            AbilityOne = abilityOnePressed,
            AbilityTwo = abilityTwoPressed,
            Interact = interactPressed,
            UsePotion = potionPressed
        };
        attackPressed = abilityOnePressed = abilityTwoPressed = interactPressed = potionPressed = false;
        return frame;
    }

    // Returns true once after the on-screen pause button was pressed.
    public static bool ConsumePausePressed()
    {
        bool pressed = pausePressed;
        pausePressed = false;
        return pressed;
    }

    // Builds the controls under the canvas. Returns the component.
    public static TouchControls Create(Canvas canvas)
    {
        GameObject go = new GameObject("Touch Controls", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        TouchControls controls = go.AddComponent<TouchControls>();
        controls.Build((RectTransform)go.transform);
        return controls;
    }

    // Creates the joystick zone and all the buttons.
    private void Build(RectTransform parent)
    {
        root = parent.gameObject;
        Stretch(parent);

        // Left half: floating joystick.
        RectTransform zone = NewRect("Joystick Zone", parent);
        zone.anchorMin = new Vector2(0f, 0f);
        zone.anchorMax = new Vector2(0.45f, 0.75f);
        zone.offsetMin = zone.offsetMax = Vector2.zero;
        Image zoneImage = zone.gameObject.AddComponent<Image>();
        zoneImage.color = new Color(1f, 1f, 1f, 0.001f);
        VirtualJoystick stick = zone.gameObject.AddComponent<VirtualJoystick>();
        stick.Build(v => moveVector = v);

        // Right side buttons, anchored to the bottom-right corner.
        AddButton(parent, "ATTACK", new Vector2(-170f, 170f), 190f, new Color(0.85f, 0.25f, 0.2f, 0.65f), () => attackPressed = true);
        AddButton(parent, "SLASH", new Vector2(-370f, 120f), 130f, new Color(0.95f, 0.6f, 0.15f, 0.6f), () => abilityOnePressed = true);
        AddButton(parent, "BASH", new Vector2(-170f, 370f), 130f, new Color(0.3f, 0.55f, 0.95f, 0.6f), () => abilityTwoPressed = true);
        AddButton(parent, "USE", new Vector2(-370f, 300f), 110f, new Color(0.3f, 0.8f, 0.4f, 0.6f), () => interactPressed = true);
        AddButton(parent, "POTION", new Vector2(-520f, 120f), 110f, new Color(0.8f, 0.2f, 0.4f, 0.6f), () => potionPressed = true);

        // Pause in the top-right corner.
        RectTransform pause = AddButton(parent, "II", new Vector2(-90f, -90f), 100f, new Color(0f, 0f, 0f, 0.5f), () => pausePressed = true);
        pause.anchorMin = pause.anchorMax = new Vector2(1f, 1f);

        GameEvents.GameStateChanged += OnGameStateChanged;
        root.SetActive(false);
    }

    // Stops listening.
    private void OnDestroy()
    {
        GameEvents.GameStateChanged -= OnGameStateChanged;
        moveVector = Vector2.zero;
    }

    // Only shows the controls while a level is being played.
    private void OnGameStateChanged(GameStateId previous, GameStateId next)
    {
        if (root != null)
        {
            root.SetActive(next == GameStateId.Playing);
        }
        if (next != GameStateId.Playing)
        {
            moveVector = Vector2.zero;
        }
    }

    // Creates one round-ish button with a label at an offset from the bottom-right corner.
    private static RectTransform AddButton(RectTransform parent, string label, Vector2 offset, float size, Color color, System.Action onPress)
    {
        RectTransform rect = NewRect(label + " Button", parent);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = offset;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        TouchButton button = rect.gameObject.AddComponent<TouchButton>();
        button.Pressed = onPress;

        RectTransform textRect = NewRect("Label", rect);
        Stretch(textRect);
        TextMeshProUGUI text = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = size * 0.22f;
        text.color = Color.white;
        text.raycastTarget = false;
        return rect;
    }

    // Makes a new UI object under a parent.
    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Makes a rect fill its parent.
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
