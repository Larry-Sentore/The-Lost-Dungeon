using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;

// What a caller gets back after asking for a bundle.
public class ContentHandle
{
    private readonly Dictionary<string, Object> assets;

    public string BundleName { get; }
    public bool Success { get; }
    public string Error { get; }

    // Creates a handle for a loaded (or failed) bundle.
    public ContentHandle(string bundleName, bool success, string error, Dictionary<string, Object> assets)
    {
        BundleName = bundleName;
        Success = success;
        Error = error;
        this.assets = assets ?? new Dictionary<string, Object>();
    }

    // Gets one asset by name (null if it is missing or the load failed).
    public T Get<T>(string assetName) where T : Object
    {
        return assetName != null && assets.TryGetValue(assetName, out Object asset) ? asset as T : null;
    }

    // Gets every asset of a type from the bundle.
    public IEnumerable<T> GetAll<T>() where T : Object
    {
        foreach (Object asset in assets.Values)
        {
            if (asset is T typed)
            {
                yield return typed;
            }
        }
    }
}

// Loads and unloads AssetBundles at runtime (Singleton). Callers Acquire a bundle when they need it
// and Release it when they are done; the bundle is unloaded only when nobody uses it any more.
// Loading reports progress through GameEvents so the UI can show a loading bar, and failures
// never stop the game - callers just get a handle with Success = false.
public class ContentManager : MonoBehaviour
{
    private class BundleEntry
    {
        public AssetBundle Bundle;
        public readonly Dictionary<string, Object> Assets = new Dictionary<string, Object>();
        public bool Loading;
        public bool Loaded;
        public string Error;
        public readonly List<Action<ContentHandle>> Waiting = new List<Action<ContentHandle>>();
    }

    private const int TimeoutSeconds = 20;
    private readonly ReferenceCounter<string> references = new ReferenceCounter<string>();
    private readonly Dictionary<string, BundleEntry> entries = new Dictionary<string, BundleEntry>();

    public static ContentManager Instance { get; private set; }

    // Sets up the singleton.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    // How many users a bundle has (for debugging and tests).
    public int ReferenceCount(string bundleName) => references.Count(bundleName);

    // Says "I need this bundle". Loads it if needed and calls onReady when it is ready (or has failed).
    public void Acquire(string bundleName, Action<ContentHandle> onReady)
    {
        bool first = references.Acquire(bundleName);
        if (!entries.TryGetValue(bundleName, out BundleEntry entry))
        {
            entry = new BundleEntry();
            entries[bundleName] = entry;
        }

        if (entry.Loaded)
        {
            onReady?.Invoke(MakeHandle(bundleName, entry));
            return;
        }
        if (onReady != null)
        {
            entry.Waiting.Add(onReady);
        }
        if (!entry.Loading && (first || entry.Error != null))
        {
            entry.Error = null;
            StartCoroutine(LoadRoutine(bundleName, entry));
        }
    }

    // Says "I am done with this bundle". Unloads it when it was the last user.
    public void Release(string bundleName)
    {
        if (!references.Release(bundleName) || !entries.TryGetValue(bundleName, out BundleEntry entry))
        {
            return;
        }
        if (entry.Loading)
        {
            // Still downloading: it will be unloaded as soon as it finishes.
            return;
        }
        Unload(bundleName, entry);
    }

    // Unloads a bundle and every asset loaded from it.
    private void Unload(string bundleName, BundleEntry entry)
    {
        if (entry.Bundle != null)
        {
            entry.Bundle.Unload(true);
        }
        entries.Remove(bundleName);
        Debug.Log($"[Content] Unloaded '{bundleName}'.");
    }

    // Downloads the bundle, loads all of its assets, and tells everyone who was waiting.
    private IEnumerator LoadRoutine(string bundleName, BundleEntry entry)
    {
        entry.Loading = true;
        GameEvents.RaiseContentLoadProgress(bundleName, 0f);

#if UNITY_EDITOR
        // In the editor, if the bundles have not been built yet, load straight from the project
        // so the game still runs ("simulation mode"). Builds always use the real bundles.
        if (!BundleFileExists(bundleName))
        {
            yield return null;
            LoadFromProject(bundleName, entry);
            Finish(bundleName, entry);
            yield break;
        }
#endif

        string url = GetBundleUrl(bundleName);
        using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(url))
        {
            request.timeout = TimeoutSeconds;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                GameEvents.RaiseContentLoadProgress(bundleName, request.downloadProgress * 0.8f);
                yield return null;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                entry.Error = $"{request.error} ({url})";
            }
            else
            {
                entry.Bundle = DownloadHandlerAssetBundle.GetContent(request);
                if (entry.Bundle == null)
                {
                    entry.Error = "The file was not a valid AssetBundle for this platform.";
                }
            }
        }

        if (entry.Bundle != null)
        {
            AssetBundleRequest assetsRequest = entry.Bundle.LoadAllAssetsAsync();
            while (!assetsRequest.isDone)
            {
                GameEvents.RaiseContentLoadProgress(bundleName, 0.8f + assetsRequest.progress * 0.2f);
                yield return null;
            }
            foreach (Object asset in assetsRequest.allAssets)
            {
                if (asset != null)
                {
                    entry.Assets[asset.name] = asset;
                }
            }
        }
        Finish(bundleName, entry);
    }

    // Marks the load as done, reports the result and hands the handle to every waiting caller.
    private void Finish(string bundleName, BundleEntry entry)
    {
        entry.Loading = false;
        entry.Loaded = entry.Error == null;
        if (entry.Error != null)
        {
            Debug.LogWarning($"[Content] Could not load '{bundleName}': {entry.Error}");
        }
        else
        {
            Debug.Log($"[Content] Loaded '{bundleName}' ({entry.Assets.Count} assets).");
        }
        GameEvents.RaiseContentLoadProgress(bundleName, 1f);
        GameEvents.RaiseContentLoadFinished(bundleName, entry.Loaded);

        ContentHandle handle = MakeHandle(bundleName, entry);
        Action<ContentHandle>[] waiting = entry.Waiting.ToArray();
        entry.Waiting.Clear();
        foreach (Action<ContentHandle> callback in waiting)
        {
            try
            {
                callback(handle);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        // Everyone let go while it was loading: unload it now. A failed load is forgotten so it can be retried.
        if (references.Count(bundleName) == 0 || !entry.Loaded)
        {
            if (!entry.Loaded)
            {
                while (references.Count(bundleName) > 0)
                {
                    references.Release(bundleName);
                }
            }
            Unload(bundleName, entry);
        }
    }

    // Wraps an entry in a handle for callers.
    private static ContentHandle MakeHandle(string bundleName, BundleEntry entry)
    {
        return new ContentHandle(bundleName, entry.Loaded, entry.Error, entry.Assets);
    }

    // Builds the address of a bundle file. Each platform needs a different form of address.
    public static string GetBundleUrl(string bundleName)
    {
        string path = Application.streamingAssetsPath + "/" + ContentCatalog.RootFolder + "/" + ContentCatalog.PlatformFolder + "/" + bundleName;
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: StreamingAssets is already an http(s) URL next to the page.
        return path;
#elif UNITY_ANDROID && !UNITY_EDITOR
        // Android: StreamingAssets is inside the APK and already a jar:file:// URL.
        return path;
#else
        // Windows, iOS and the editor: a local file path that needs a file:// URL.
        return new Uri(path).AbsoluteUri;
#endif
    }

#if UNITY_EDITOR
    // Returns true if the built bundle file exists on disk (editor only).
    private static bool BundleFileExists(string bundleName)
    {
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, ContentCatalog.RootFolder, ContentCatalog.PlatformFolder, bundleName);
        return System.IO.File.Exists(path);
    }

    // Loads a bundle's assets straight from the project (editor simulation mode).
    private static void LoadFromProject(string bundleName, BundleEntry entry)
    {
        ContentBundleDefinition definition = ContentCatalog.Find(bundleName);
        if (definition == null)
        {
            entry.Error = "No such bundle in the ContentCatalog.";
            return;
        }
        foreach (string path in definition.AssetPaths)
        {
            Object asset = UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
            {
                entry.Assets[asset.name] = asset;
            }
        }
        if (entry.Assets.Count == 0)
        {
            entry.Error = "None of the bundle's assets were found in the project.";
        }
    }
#endif
}
