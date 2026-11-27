using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Platform-specific settings, picked at compile time with conditional compilation.
public static class PlatformBehaviours
{
    // Changes frame rate, quality and screen settings for the platform we are built for.
    public static void Configure()
    {
#if UNITY_ANDROID || UNITY_IOS
        // Phones: cap at 60 to save battery and heat, shorter shadows, keep the screen awake, landscape only.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        QualitySettings.shadowDistance = 20f;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.orientation = ScreenOrientation.AutoRotation;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
#elif UNITY_WEBGL
        // Browsers drive the frame loop themselves; -1 lets the browser pick the refresh rate.
        Application.targetFrameRate = -1;
        QualitySettings.shadowDistance = 30f;
#elif UNITY_STANDALONE_WIN
        // Windows PC: vsync on, full draw distance for shadows.
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
        QualitySettings.shadowDistance = 50f;
#endif
    }

    // True when the on-screen touch controls should be shown.
    public static bool UseTouchControls
    {
        get
        {
#if UNITY_ANDROID || UNITY_IOS
            return true;
#elif UNITY_WEBGL
            return Application.isMobilePlatform || Touchscreen.current != null;
#else
            return false;
#endif
        }
    }

    // The button name shown in prompts such as "[E] Open chest".
    public static string InteractButtonLabel
    {
        get
        {
#if UNITY_ANDROID || UNITY_IOS
            return "Tap USE";
#else
            return Gamepad.current != null && Keyboard.current == null ? "A" : "E";
#endif
        }
    }

    // Makes the UI scale with the screen. The UI was designed at 1920x1080; phones lean toward
    // matching height so buttons stay a usable size in landscape.
    public static void ConfigureCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
#if UNITY_ANDROID || UNITY_IOS
        scaler.matchWidthOrHeight = 0.75f;
#else
        scaler.matchWidthOrHeight = 0.5f;
#endif
    }
}
