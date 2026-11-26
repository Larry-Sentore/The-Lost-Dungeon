using System;
using System.Collections;
using UnityEngine;

public class AssetBundleLevelLoader : MonoBehaviour
{
    private AssetBundle loadedBundle;

    public event Action<string, bool> LoadingFinished;

    public void LoadLevelBundle(int level)
    {
        StartCoroutine(LoadRoutine(level));
    }

    private IEnumerator LoadRoutine(int level)
    {
        if (loadedBundle != null)
        {
            loadedBundle.Unload(false);
            loadedBundle = null;
        }

        string platformFolder = GetPlatformFolder();
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, platformFolder, $"level-{level}");
        AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);
        yield return request;

        if (request.assetBundle == null)
        {
            LoadingFinished?.Invoke($"Level {level} local assets unavailable; using scene assets.", false);
            yield break;
        }

        loadedBundle = request.assetBundle;
        LoadingFinished?.Invoke($"Level {level} AssetBundle loaded.", true);
    }

    public void Unload()
    {
        if (loadedBundle != null)
        {
            loadedBundle.Unload(true);
            loadedBundle = null;
        }
    }

    private static string GetPlatformFolder()
    {
#if UNITY_ANDROID
        return "Android";
#elif UNITY_IOS
        return "iOS";
#elif UNITY_WEBGL
        return "WebGL";
#elif UNITY_STANDALONE_WIN
        return "StandaloneWindows";
#else
        return "StandaloneWindows";
#endif
    }

    private void OnDestroy()
    {
        Unload();
    }
}
