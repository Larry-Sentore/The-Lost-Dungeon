using System;
using System.Collections;
using UnityEngine;

public class AssetBundleLevelLoader : MonoBehaviour
{
    private AssetBundle loadedBundle;
    private readonly System.Collections.Generic.Dictionary<string, UnityEngine.Object> loadedAssets =
        new System.Collections.Generic.Dictionary<string, UnityEngine.Object>(StringComparer.OrdinalIgnoreCase);

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
        loadedAssets.Clear();

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
        AssetBundleRequest assetsRequest = loadedBundle.LoadAllAssetsAsync();
        yield return assetsRequest;
        foreach (UnityEngine.Object asset in assetsRequest.allAssets)
        {
            if (asset != null)
            {
                loadedAssets[asset.name] = asset;
            }
        }
        LoadingFinished?.Invoke($"Level {level} AssetBundle loaded.", true);
    }

    public bool TryGetPrefab(string assetName, out GameObject prefab)
    {
        if (loadedAssets.TryGetValue(assetName, out UnityEngine.Object asset))
        {
            prefab = asset as GameObject;
            return prefab != null;
        }
        prefab = null;
        return false;
    }

    public void Unload()
    {
        if (loadedBundle != null)
        {
            loadedBundle.Unload(true);
            loadedBundle = null;
        }
        loadedAssets.Clear();
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
