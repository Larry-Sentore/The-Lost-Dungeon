using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the game's AssetBundles from ContentCatalog into StreamingAssets/Content/<Platform>.
// Run it from the "Lost Dungeon" menu, or just build the player: ContentBuildPreprocessor builds
// the bundles for the target platform automatically before every build.
public static class ContentBuilder
{
    // Builds the bundles for the platform currently selected in Build Settings.
    [MenuItem("Lost Dungeon/Content/Build Bundles (Active Platform)")]
    public static void BuildActivePlatform()
    {
        Build(EditorUserBuildSettings.activeBuildTarget);
    }

    // Builds the bundles for Windows, WebGL and Android in one go.
    [MenuItem("Lost Dungeon/Content/Build Bundles (Windows + WebGL + Android)")]
    public static void BuildAllPlatforms()
    {
        Build(BuildTarget.StandaloneWindows64);
        Build(BuildTarget.WebGL);
        Build(BuildTarget.Android);
    }

    // Deletes every built bundle (the editor then falls back to loading straight from the project).
    [MenuItem("Lost Dungeon/Content/Delete Built Bundles")]
    public static void Clean()
    {
        string root = Path.Combine(Application.streamingAssetsPath, ContentCatalog.RootFolder);
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
            File.Delete(root + ".meta");
        }
        AssetDatabase.Refresh();
    }

    // Builds every bundle in the ContentCatalog for one platform. Returns false only if the build failed
    // (platforms the game does not target are skipped and count as success).
    public static bool Build(BuildTarget target)
    {
        string folder = PlatformFolder(target);
        if (folder == null)
        {
            Debug.LogWarning($"[Content] No bundle folder is defined for {target}; skipping.");
            return true;
        }
        string output = Path.Combine(Application.streamingAssetsPath, ContentCatalog.RootFolder, folder);
        Directory.CreateDirectory(output);

        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
        foreach (ContentBundleDefinition bundle in ContentCatalog.Bundles)
        {
            List<string> existing = new List<string>();
            foreach (string path in bundle.AssetPaths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    existing.Add(path);
                }
                else
                {
                    Debug.LogWarning($"[Content] '{path}' in bundle '{bundle.Name}' was not found and is skipped.");
                }
            }
            builds.Add(new AssetBundleBuild { assetBundleName = bundle.Name, assetNames = existing.ToArray() });
        }

        // LZ4 (chunk-based) compression loads quickly and works on every target, including WebGL.
        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(output, builds.ToArray(),
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, target);
        AssetDatabase.Refresh();
        if (manifest == null)
        {
            Debug.LogError($"[Content] AssetBundle build for {target} failed.");
            return false;
        }
        Debug.Log($"[Content] Built {builds.Count} bundles for {target} into {output}");
        return true;
    }

    // The folder name for a build target. Must match ContentCatalog.PlatformFolder at runtime.
    public static string PlatformFolder(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows:
            case BuildTarget.StandaloneWindows64:
                return "StandaloneWindows";
            case BuildTarget.WebGL:
                return "WebGL";
            case BuildTarget.Android:
                return "Android";
            case BuildTarget.iOS:
                return "iOS";
            default:
                return null;
        }
    }
}

// Builds the content for the target platform right before Unity builds the player,
// so every Windows, WebGL and Android build always ships with matching bundles.
public class ContentBuildPreprocessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    // Builds the bundles and stops the player build if that fails.
    public void OnPreprocessBuild(BuildReport report)
    {
        if (!ContentBuilder.Build(report.summary.platform))
        {
            throw new BuildFailedException("AssetBundle content build failed - see the Console for details.");
        }
    }
}
