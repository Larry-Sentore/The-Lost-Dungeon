using System.IO;

// One AssetBundle and the asset files that go into it.
public class ContentBundleDefinition
{
    public string Name;
    public string[] AssetPaths;

    // Creates a bundle definition.
    public ContentBundleDefinition(string name, params string[] assetPaths)
    {
        Name = name;
        AssetPaths = assetPaths;
    }
}

// The list of managed content. The editor build script and the runtime loader both read this,
// so adding a bundle or an asset only means editing this one list.
public static class ContentCatalog
{
    public const string RootFolder = "Content";
    public const string SfxBundle = "audio-sfx";
    public const string MenuMusicBundle = "music-menu";
    public const string DungeonMusicBundle = "music-dungeon";
    public const string BossMusicBundle = "music-boss";
    public const string MenuMusicAsset = "menu_theme";
    public const string DungeonMusicAsset = "dungeon_theme";
    public const string BossMusicAsset = "boss_theme";

    private const string Sfx = "Assets/PixelSFXFree/";
    private const string Music = "Assets/Audio/Music/";

    public static readonly ContentBundleDefinition[] Bundles =
    {
        new ContentBundleDefinition(SfxBundle,
            Sfx + "coins/coin_bright.wav", Sfx + "coins/coin_double.wav", Sfx + "coins/gem.wav",
            Sfx + "coins/gem_big.wav", Sfx + "coins/heart.wav", Sfx + "coins/key.wav",
            Sfx + "explosions/boom_big.wav", Sfx + "explosions/explosion_01.wav",
            Sfx + "hits/death.wav", Sfx + "hits/enemy_hit.wav", Sfx + "hits/hurt.wav",
            Sfx + "jingles/game_over.wav", Sfx + "jingles/level_complete.wav",
            Sfx + "magic/teleport.wav",
            Sfx + "movement/bounce.wav", Sfx + "movement/dash.wav", Sfx + "movement/jump_classic.wav",
            Sfx + "movement/jump_double.wav", Sfx + "movement/jump_high.wav", Sfx + "movement/land.wav",
            Sfx + "powerups/extra_life.wav", Sfx + "powerups/heal.wav", Sfx + "powerups/powerup_arp.wav",
            Sfx + "shooting/charge.wav", Sfx + "shooting/laser_01.wav", Sfx + "shooting/shot_classic.wav",
            Sfx + "ui/cancel.wav", Sfx + "ui/confirm.wav", Sfx + "ui/error.wav", Sfx + "ui/select.wav"),
        new ContentBundleDefinition(MenuMusicBundle, Music + MenuMusicAsset + ".wav"),
        new ContentBundleDefinition(DungeonMusicBundle, Music + DungeonMusicAsset + ".wav"),
        new ContentBundleDefinition(BossMusicBundle, Music + BossMusicAsset + ".wav")
    };

    // The folder name used for the current build target. Bundles are built separately per platform
    // because a bundle built for one platform cannot be loaded on another.
    public static string PlatformFolder
    {
        get
        {
#if UNITY_ANDROID
            return "Android";
#elif UNITY_IOS
            return "iOS";
#elif UNITY_WEBGL
            return "WebGL";
#else
            return "StandaloneWindows";
#endif
        }
    }

    // Finds a bundle definition by name (null if there is none).
    public static ContentBundleDefinition Find(string bundleName)
    {
        foreach (ContentBundleDefinition bundle in Bundles)
        {
            if (bundle.Name == bundleName)
            {
                return bundle;
            }
        }
        return null;
    }

    // The key an asset is looked up by: its file name without the extension.
    public static string AssetKey(string assetPath) => Path.GetFileNameWithoutExtension(assetPath);
}
