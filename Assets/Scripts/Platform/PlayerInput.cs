using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Everything the player asked for in one frame.
public struct PlayerInputFrame
{
    public Vector2 Move;
    public bool Attack;
    public bool AbilityOne;
    public bool AbilityTwo;
    public bool Interact;
    public bool UsePotion;

    // Combines two frames: the stronger movement wins and any pressed button counts.
    public static PlayerInputFrame Merge(PlayerInputFrame a, PlayerInputFrame b)
    {
        return new PlayerInputFrame
        {
            Move = a.Move.sqrMagnitude >= b.Move.sqrMagnitude ? a.Move : b.Move,
            Attack = a.Attack || b.Attack,
            AbilityOne = a.AbilityOne || b.AbilityOne,
            AbilityTwo = a.AbilityTwo || b.AbilityTwo,
            Interact = a.Interact || b.Interact,
            UsePotion = a.UsePotion || b.UsePotion
        };
    }
}

// One way of controlling the player (keyboard, gamepad, touch...).
public interface IPlayerInputSource
{
    // Reads this device's input for the current frame.
    PlayerInputFrame Read();
}

// Keyboard and mouse controls.
public class KeyboardMouseInputSource : IPlayerInputSource
{
    // WASD/arrows to move, left click or Space to attack, 1/2 or right click for abilities, E/F to interact, H for a potion.
    public PlayerInputFrame Read()
    {
        PlayerInputFrame frame = new PlayerInputFrame();
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null)
        {
            Vector2 move = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
            frame.Move = Vector2.ClampMagnitude(move, 1f);
            frame.Attack = keyboard.spaceKey.wasPressedThisFrame || keyboard.jKey.wasPressedThisFrame;
            frame.AbilityOne = keyboard.digit1Key.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame;
            frame.AbilityTwo = keyboard.digit2Key.wasPressedThisFrame;
            frame.Interact = keyboard.eKey.wasPressedThisFrame || keyboard.fKey.wasPressedThisFrame;
            frame.UsePotion = keyboard.hKey.wasPressedThisFrame;
        }
        if (mouse != null)
        {
            frame.Attack |= mouse.leftButton.wasPressedThisFrame;
            frame.AbilityTwo |= mouse.rightButton.wasPressedThisFrame;
        }
        return frame;
    }
}

// Gamepad controls.
public class GamepadInputSource : IPlayerInputSource
{
    // Left stick to move, X/West to attack, LB/RB for abilities, A/South to interact, Y/North for a potion.
    public PlayerInputFrame Read()
    {
        Gamepad pad = Gamepad.current;
        if (pad == null)
        {
            return default;
        }
        Vector2 stick = pad.leftStick.ReadValue();
        return new PlayerInputFrame
        {
            Move = stick.sqrMagnitude > 0.02f ? Vector2.ClampMagnitude(stick, 1f) : Vector2.zero,
            Attack = pad.buttonWest.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame,
            AbilityOne = pad.leftShoulder.wasPressedThisFrame,
            AbilityTwo = pad.rightShoulder.wasPressedThisFrame,
            Interact = pad.buttonSouth.wasPressedThisFrame,
            UsePotion = pad.buttonNorth.wasPressedThisFrame
        };
    }
}

// On-screen touch controls (virtual joystick and buttons).
public class TouchInputSource : IPlayerInputSource
{
    // Reads the on-screen controls.
    public PlayerInputFrame Read() => TouchControls.ReadFrame();
}

// Picks the right input sources for the platform and merges them each frame.
public class PlayerInputRouter
{
    private readonly List<IPlayerInputSource> sources = new List<IPlayerInputSource>();

    // Builds the list of input sources. Desktop and web get keyboard/mouse and gamepad;
    // mobile gets touch first. WebGL on a phone or tablet also gets touch.
    public PlayerInputRouter()
    {
#if UNITY_ANDROID || UNITY_IOS
        sources.Add(new TouchInputSource());
        sources.Add(new GamepadInputSource());
#elif UNITY_WEBGL
        sources.Add(new KeyboardMouseInputSource());
        sources.Add(new GamepadInputSource());
        if (PlatformBehaviours.UseTouchControls)
        {
            sources.Add(new TouchInputSource());
        }
#else
        sources.Add(new KeyboardMouseInputSource());
        sources.Add(new GamepadInputSource());
#endif
    }

    // Adds another input source (for example a test or replay source).
    public void AddSource(IPlayerInputSource source)
    {
        sources.Add(source);
    }

    // Reads and merges every source for this frame.
    public PlayerInputFrame Read()
    {
        PlayerInputFrame frame = default;
        foreach (IPlayerInputSource source in sources)
        {
            frame = PlayerInputFrame.Merge(frame, source.Read());
        }
        return frame;
    }

    // True on the frame the player pressed pause (Escape or P, or Start on a gamepad).
    public static bool PausePressed()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        // Escape also leaves fullscreen in web browsers, so P works as a pause key there too.
        bool key = keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || keyboard.pKey.wasPressedThisFrame);
        bool button = pad != null && pad.startButton.wasPressedThisFrame;
        return key || button || TouchControls.ConsumePausePressed();
    }
}
