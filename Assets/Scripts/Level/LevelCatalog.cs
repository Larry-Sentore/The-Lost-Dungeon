using System;

// The data for one level. Adding a level means adding one entry here plus its scene group.
[Serializable]
public class LevelDefinition
{
    public int Number;
    public string Title;
    public string MusicBundle;
    public string MusicAsset;
    public EnemyKind FeaturedEnemy;
    public int ChestGems;
    public int ChestGold;
    public string Unlockable;

    // Creates a level definition.
    public LevelDefinition(int number, string title, string musicBundle, string musicAsset,
        EnemyKind featuredEnemy, int chestGems, int chestGold, string unlockable = null)
    {
        Number = number;
        Title = title;
        MusicBundle = musicBundle;
        MusicAsset = musicAsset;
        FeaturedEnemy = featuredEnemy;
        ChestGems = chestGems;
        ChestGold = chestGold;
        Unlockable = unlockable;
    }
}

// The result of finishing a level, shown on the success screen.
public struct LevelResult
{
    public int Level;
    public float Seconds;
    public int Score;
    public int Rank;
    public int GemsCollected;
    public string Unlocked;
}

// All five levels.
public static class LevelCatalog
{
    private static readonly LevelDefinition[] levels =
    {
        new LevelDefinition(1, "The Forgotten Halls", ContentCatalog.DungeonMusicBundle, ContentCatalog.DungeonMusicAsset, EnemyKind.Skeleton, 2, 10),
        new LevelDefinition(2, "The Machine Crypt", ContentCatalog.DungeonMusicBundle, ContentCatalog.DungeonMusicAsset, EnemyKind.CannonRobot, 3, 15),
        new LevelDefinition(3, "The Golem Vault", ContentCatalog.DungeonMusicBundle, ContentCatalog.DungeonMusicAsset, EnemyKind.RockGiant, 4, 20),
        new LevelDefinition(4, "The Iron Barracks", ContentCatalog.DungeonMusicBundle, ContentCatalog.DungeonMusicAsset, EnemyKind.RockGiant, 5, 30, "Title: Golem Breaker"),
        new LevelDefinition(5, "Throne of the Cannon King", ContentCatalog.BossMusicBundle, ContentCatalog.BossMusicAsset, EnemyKind.GiantCannonBoss, 10, 50, "Title: Dungeon Conqueror")
    };

    public static int Count => levels.Length;

    // Returns the definition for a level number (1 to Count), or null if there is no such level.
    public static LevelDefinition Get(int number)
    {
        return number >= 1 && number <= levels.Length ? levels[number - 1] : null;
    }
}
