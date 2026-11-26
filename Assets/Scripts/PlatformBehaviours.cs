using UnityEngine;

public static class PlatformBehaviours
{
    public static void Configure()
    {
#if UNITY_ANDROID || UNITY_IOS
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 30;
        Physics.simulationMode = SimulationMode.FixedUpdate;
#elif UNITY_WEBGL
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
#elif UNITY_STANDALONE_WIN
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
#endif
    }
}
