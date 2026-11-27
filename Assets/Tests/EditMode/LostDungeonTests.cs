using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Edit Mode tests for the game's real systems (they reference the LostDungeon.Runtime assembly).
public class DamageTests
{
    // Base damage x multiplier, doubled on a critical, minus defence.
    [Test]
    public void Calculate_AppliesMultiplierCriticalAndDefence()
    {
        Assert.That(DamageCalculator.Calculate(10, 1.5f, 3, false), Is.EqualTo(12));
        Assert.That(DamageCalculator.Calculate(10, 1f, 5, true), Is.EqualTo(15));
    }

    // Defence higher than the damage gives zero, never a negative number.
    [Test]
    public void Calculate_NeverProducesNegativeDamage()
    {
        Assert.That(DamageCalculator.Calculate(1, 1f, 50, false), Is.EqualTo(0));
        Assert.That(DamageCalculator.Calculate(-5, -2f, 0, true), Is.EqualTo(0));
    }

    // A guardian facing +Z blocks an attacker in front but not one behind or beside it.
    [Test]
    public void Guard_BlocksOnlyAttacksFromTheFront()
    {
        Vector3 position = Vector3.zero;
        Vector3 forward = Vector3.forward;
        Assert.That(DamageCalculator.IsBlockedByGuard(position, forward, new Vector3(0f, 0f, 3f), 140f), Is.True);
        Assert.That(DamageCalculator.IsBlockedByGuard(position, forward, new Vector3(0f, 0f, -3f), 140f), Is.False);
        Assert.That(DamageCalculator.IsBlockedByGuard(position, forward, new Vector3(3f, 0f, 0f), 140f), Is.False);
    }

    // A linearly slowing knockback that lasts T seconds must start at 2d/T to cover d metres.
    [Test]
    public void KnockbackVelocity_CoversTheRequestedDistance()
    {
        Vector3 velocity = DamageCalculator.KnockbackVelocity(Vector3.zero, new Vector3(1f, 5f, 0f), 2f, 0.5f);
        Assert.That(velocity.y, Is.EqualTo(0f));
        Assert.That(velocity.magnitude * 0.5f / 2f, Is.EqualTo(2f).Within(0.0001f));
        Assert.That(velocity.normalized.x, Is.EqualTo(1f).Within(0.0001f));
    }

    // Critical rolls below the chance hit, rolls at or above it miss.
    [Test]
    public void RollCritical_UsesTheChanceAsAThreshold()
    {
        Assert.That(DamageCalculator.RollCritical(0.15f, 0.1f), Is.True);
        Assert.That(DamageCalculator.RollCritical(0.15f, 0.15f), Is.False);
    }

    // Healing stops at the maximum.
    [Test]
    public void ApplyHeal_DoesNotGoAboveMaximum()
    {
        Assert.That(DamageCalculator.ApplyHeal(90f, 100f, 35f), Is.EqualTo(100f));
        Assert.That(DamageCalculator.ApplyHeal(20f, 100f, 35f), Is.EqualTo(55f));
    }
}

public class InventoryTests
{
    // Adding stacks, removing more than you have fails, removing the last one empties the slot.
    [Test]
    public void AddAndRemove_TracksQuantities()
    {
        InventorySystem inventory = new InventorySystem();
        inventory.Add(ItemCatalog.Potion, 2);
        inventory.Add(ItemCatalog.Potion, 3);
        Assert.That(inventory.Count(ItemCatalog.Potion), Is.EqualTo(5));
        Assert.That(inventory.Remove(ItemCatalog.Potion, 6), Is.False);
        Assert.That(inventory.Remove(ItemCatalog.Potion, 5), Is.True);
        Assert.That(inventory.Count(ItemCatalog.Potion), Is.EqualTo(0));
        Assert.That(inventory.Items.Count, Is.EqualTo(0));
    }

    // Zero, negative and empty additions are ignored.
    [Test]
    public void Add_IgnoresInvalidInput()
    {
        InventorySystem inventory = new InventorySystem();
        inventory.Add(ItemCatalog.Gem, 0);
        inventory.Add(ItemCatalog.Gem, -4);
        inventory.Add("", 3);
        Assert.That(inventory.Items.Count, Is.EqualTo(0));
    }

    // Sorting by quantity puts the biggest stack first.
    [Test]
    public void GetSorted_ByQuantity_PutsLargestStackFirst()
    {
        InventorySystem inventory = new InventorySystem();
        inventory.Add(ItemCatalog.Potion, 1);
        inventory.Add(ItemCatalog.Gold, 40);
        inventory.Add(ItemCatalog.Gem, 7);
        List<IInventoryItem> sorted = inventory.GetSorted(InventorySortMode.ByQuantity);
        Assert.That(sorted[0].ItemId, Is.EqualTo(ItemCatalog.Gold));
        Assert.That(sorted[1].ItemId, Is.EqualTo(ItemCatalog.Gem));
        Assert.That(sorted[2].ItemId, Is.EqualTo(ItemCatalog.Potion));
    }

    // The inventory survives being written to save data, turned into JSON, and read back.
    [Test]
    public void CaptureAndRestore_RoundTripsThroughSaveData()
    {
        InventorySystem original = new InventorySystem();
        original.Add(ItemCatalog.Gem, 4);
        original.Add(ItemCatalog.Potion, 2);
        SaveData data = new SaveData();
        original.CaptureState(data);

        InventorySystem restored = new InventorySystem();
        restored.RestoreState(SaveSystem.Deserialize(SaveSystem.Serialize(data)));
        Assert.That(restored.Count(ItemCatalog.Gem), Is.EqualTo(4));
        Assert.That(restored.Count(ItemCatalog.Potion), Is.EqualTo(2));
    }
}

public class ProgressionTests
{
    // A new save only has level 1 available; everything else is locked.
    [Test]
    public void NewSave_OnlyFirstLevelIsAvailable()
    {
        SaveData data = new SaveData();
        Assert.That(ProgressionRules.GetStatus(data, 1), Is.EqualTo(LevelStatus.Available));
        Assert.That(ProgressionRules.GetStatus(data, 2), Is.EqualTo(LevelStatus.Locked));
        Assert.That(ProgressionRules.IsUnlocked(data, 5), Is.False);
    }

    // Completing a level marks it completed and unlocks exactly the next one.
    [Test]
    public void MarkCompleted_UnlocksOnlyTheNextLevel()
    {
        SaveData data = new SaveData();
        int unlocked = ProgressionRules.MarkCompleted(data, 1);
        Assert.That(unlocked, Is.EqualTo(2));
        Assert.That(ProgressionRules.GetStatus(data, 1), Is.EqualTo(LevelStatus.Completed));
        Assert.That(ProgressionRules.GetStatus(data, 2), Is.EqualTo(LevelStatus.Available));
        Assert.That(ProgressionRules.GetStatus(data, 3), Is.EqualTo(LevelStatus.Locked));
    }

    // Finishing the last level cannot unlock a level 6, and replaying an old level unlocks nothing new.
    [Test]
    public void MarkCompleted_StopsAtTheLastLevel()
    {
        SaveData data = new SaveData { HighestUnlockedLevel = 5 };
        Assert.That(ProgressionRules.MarkCompleted(data, 5), Is.EqualTo(0));
        Assert.That(data.HighestUnlockedLevel, Is.EqualTo(ProgressionRules.LevelCount));
        Assert.That(ProgressionRules.MarkCompleted(data, 2), Is.EqualTo(0));
        Assert.That(ProgressionRules.IsUnlocked(data, 6), Is.False);
    }

    // Old version-1 saves (one "completed level" number) are upgraded to per-level flags.
    [Test]
    public void Normalize_UpgradesLegacySaves()
    {
        SaveData data = new SaveData { CompletedLevel = 2, HighestUnlockedLevel = 99, CompletedLevels = null };
        ProgressionRules.Normalize(data);
        Assert.That(data.HighestUnlockedLevel, Is.EqualTo(5));
        Assert.That(data.CompletedLevels[0] && data.CompletedLevels[1], Is.True);
        Assert.That(data.CompletedLevels[2], Is.False);
        Assert.That(data.CompletedLevel, Is.EqualTo(0));
    }

    // Faster, cleaner runs score more, and the score never drops below 100.
    [Test]
    public void CalculateScore_RewardsSpeedAndPunishesDamage()
    {
        int fast = ProgressionRules.CalculateScore(2, 60f, 3, 0, 100);
        int slow = ProgressionRules.CalculateScore(2, 240f, 3, 0, 100);
        int hurt = ProgressionRules.CalculateScore(2, 60f, 3, 200, 100);
        Assert.That(fast, Is.GreaterThan(slow));
        Assert.That(fast, Is.GreaterThan(hurt));
        Assert.That(ProgressionRules.CalculateScore(1, 9999f, 0, 99999, 0), Is.EqualTo(100));
    }
}

public class SaveSystemTests
{
    private ISaveStorage previous;

    // Uses in-memory storage so tests never touch the real save file.
    [SetUp]
    public void SetUp()
    {
        previous = SaveSystem.Storage;
        SaveSystem.Storage = new MemorySaveStorage();
    }

    // Puts the real storage back.
    [TearDown]
    public void TearDown()
    {
        SaveSystem.Storage = previous;
    }

    // Data written through the save system comes back the same after a reload.
    [Test]
    public void SaveAndLoad_RoundTripsProgressSettingsAndStats()
    {
        SaveData data = SaveSystem.Data;
        ProgressionRules.MarkCompleted(data, 1);
        data.Settings.MasterVolume = 0.3f;
        data.Settings.PlayerName = "Tester";
        data.Stats.EnemiesDefeated = 12;
        SaveSystem.Save();

        MemorySaveStorage storage = (MemorySaveStorage)SaveSystem.Storage;
        SaveSystem.Storage = new MemorySaveStorage { Json = storage.Json };
        SaveData loaded = SaveSystem.Data;
        Assert.That(loaded.HighestUnlockedLevel, Is.EqualTo(2));
        Assert.That(loaded.CompletedLevels[0], Is.True);
        Assert.That(loaded.Settings.MasterVolume, Is.EqualTo(0.3f));
        Assert.That(loaded.Settings.PlayerName, Is.EqualTo("Tester"));
        Assert.That(loaded.Stats.EnemiesDefeated, Is.EqualTo(12));
    }

    // A damaged save file gives a fresh save instead of crashing.
    [Test]
    public void Deserialize_CorruptJsonGivesFreshData()
    {
        SaveData data = SaveSystem.Deserialize("{ this is not json");
        Assert.That(data, Is.Not.Null);
        Assert.That(data.HighestUnlockedLevel, Is.EqualTo(1));
        Assert.That(data.CompletedLevels.Length, Is.EqualTo(ProgressionRules.LevelCount));
    }

    // New Game clears progress but keeps the player's settings.
    [Test]
    public void ResetProgress_KeepsSettings()
    {
        SaveSystem.Data.Settings.PlayerName = "Keeper";
        ProgressionRules.MarkCompleted(SaveSystem.Data, 1);
        SaveSystem.ResetProgress();
        Assert.That(SaveSystem.Data.HighestUnlockedLevel, Is.EqualTo(1));
        Assert.That(SaveSystem.Data.Settings.PlayerName, Is.EqualTo("Keeper"));
    }
}

public class StateMachineTests
{
    // The game starts frozen in the menu, runs when playing, and freezes again when paused.
    [Test]
    public void GameStateMachine_AppliesTimeScaleForEachState()
    {
        float timeScale = -1f;
        GameStateMachine machine = new GameStateMachine(s => timeScale = s);
        Assert.That(machine.CurrentId, Is.EqualTo(GameStateId.MainMenu));
        Assert.That(timeScale, Is.EqualTo(0f));
        Assert.That(machine.TryChange(GameStateId.Playing), Is.True);
        Assert.That(timeScale, Is.EqualTo(1f));
        Assert.That(machine.TryChange(GameStateId.Paused), Is.True);
        Assert.That(timeScale, Is.EqualTo(0f));
    }

    // Changes that make no sense (pausing from the menu, winning while paused) are refused.
    [Test]
    public void GameStateMachine_RejectsInvalidTransitions()
    {
        GameStateMachine machine = new GameStateMachine(null);
        int changes = 0;
        machine.Changed += (a, b) => changes++;
        Assert.That(machine.TryChange(GameStateId.Paused), Is.False);
        machine.TryChange(GameStateId.Playing);
        machine.TryChange(GameStateId.Paused);
        Assert.That(machine.TryChange(GameStateId.LevelComplete), Is.False);
        Assert.That(machine.CurrentId, Is.EqualTo(GameStateId.Paused));
        Assert.That(changes, Is.EqualTo(2));
    }

    // A small test state that records what happened to it.
    private class RecordingState : IState<EnemyState, List<string>>
    {
        private readonly EnemyState next;
        public EnemyState Id { get; }

        // Creates a state that asks to move to "next" on its first tick.
        public RecordingState(EnemyState id, EnemyState next)
        {
            Id = id;
            this.next = next;
        }

        // Records entering.
        public void Enter(List<string> log) => log.Add("enter " + Id);
        // Asks for the next state.
        public EnemyState Tick(List<string> log) => next;
        // Records leaving.
        public void Exit(List<string> log) => log.Add("exit " + Id);
    }

    // The generic state machine exits the old state before entering the new one, and ignores unknown states.
    [Test]
    public void StateMachine_ExitsBeforeEntering()
    {
        List<string> log = new List<string>();
        StateMachine<EnemyState, List<string>> machine = new StateMachine<EnemyState, List<string>>();
        machine.Add(new RecordingState(EnemyState.Idle, EnemyState.Chase));
        machine.Add(new RecordingState(EnemyState.Chase, EnemyState.Chase));
        machine.Start(EnemyState.Idle, log);
        machine.Tick(log);
        Assert.That(machine.CurrentId, Is.EqualTo(EnemyState.Chase));
        Assert.That(log, Is.EqualTo(new[] { "enter Idle", "exit Idle", "enter Chase" }));
        Assert.That(machine.ChangeTo(EnemyState.Dead, log), Is.False);
    }
}

public class AlgorithmTests
{
    // Merge sort orders correctly and keeps equal items in their original order (stable).
    [Test]
    public void MergeSort_IsCorrectAndStable()
    {
        var items = new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(3, "a"), new KeyValuePair<int, string>(1, "b"),
            new KeyValuePair<int, string>(3, "c"), new KeyValuePair<int, string>(2, "d"),
            new KeyValuePair<int, string>(1, "e")
        };
        List<KeyValuePair<int, string>> sorted = GameplayAlgorithms.MergeSort(items, (x, y) => x.Key.CompareTo(y.Key));
        Assert.That(sorted.ConvertAll(p => p.Value), Is.EqualTo(new[] { "b", "e", "d", "a", "c" }));
        Assert.That(GameplayAlgorithms.SortDamageRanking(new[] { 12, 45, 31, 7 }), Is.EqualTo(45));
    }

    // The weighted pick lands in the bucket that the roll falls into, and skips zero weights.
    [Test]
    public void WeightedRandomIndex_FollowsTheWeights()
    {
        float[] weights = { 50f, 0f, 30f, 20f };
        Assert.That(GameplayAlgorithms.WeightedRandomIndex(weights, 0f), Is.EqualTo(0));
        Assert.That(GameplayAlgorithms.WeightedRandomIndex(weights, 0.49f), Is.EqualTo(0));
        Assert.That(GameplayAlgorithms.WeightedRandomIndex(weights, 0.5f), Is.EqualTo(2));
        Assert.That(GameplayAlgorithms.WeightedRandomIndex(weights, 0.85f), Is.EqualTo(3));
        Assert.That(GameplayAlgorithms.WeightedRandomIndex(weights, 1f), Is.EqualTo(3));
        Assert.That(GameplayAlgorithms.WeightedRandomIndex(new[] { 0f, 0f }, 0.5f), Is.EqualTo(-1));
    }

    // The nearest search finds the closest valid item and respects the range limit.
    [Test]
    public void FindNearest_ReturnsClosestValidItemInRange()
    {
        var points = new List<string> { "far", "near", "dead" };
        var positions = new Dictionary<string, Vector3>
        {
            { "far", new Vector3(9f, 0f, 0f) }, { "near", new Vector3(2f, 0f, 0f) }, { "dead", new Vector3(1f, 0f, 0f) }
        };
        string found = GameplayAlgorithms.FindNearest(Vector3.zero, points, p => positions[p], p => p != "dead");
        Assert.That(found, Is.EqualTo("near"));
        Assert.That(GameplayAlgorithms.FindNearest(Vector3.zero, points, p => positions[p], p => p != "dead", 1.5f), Is.Null);
    }

    // Steering goes straight when clear, otherwise turns to the first clear angle, or stops if boxed in.
    [Test]
    public void ChooseSteeringDirection_TurnsAroundObstacles()
    {
        float[] angles = { 45f, -45f, 90f };
        Vector3 straight = GameplayAlgorithms.ChooseSteeringDirection(Vector3.forward, d => false, angles);
        Assert.That(Vector3.Angle(straight, Vector3.forward), Is.LessThan(0.01f));

        Vector3 turned = GameplayAlgorithms.ChooseSteeringDirection(Vector3.forward, d => Vector3.Angle(d, Vector3.forward) < 1f, angles);
        Assert.That(Vector3.Angle(turned, Vector3.forward), Is.EqualTo(45f).Within(0.01f));

        Assert.That(GameplayAlgorithms.ChooseSteeringDirection(Vector3.forward, d => true, angles), Is.EqualTo(Vector3.zero));
    }

    // Loot rolls map onto gem, potion or nothing using the enemy's weights.
    [Test]
    public void LootTable_UsesProfileWeights()
    {
        EnemyProfile profile = new EnemyProfile { LootGemWeight = 50f, LootPotionWeight = 25f, LootNothingWeight = 25f };
        Assert.That(LootTable.Roll(profile, 0.1f), Is.EqualTo(ItemCatalog.Gem));
        Assert.That(LootTable.Roll(profile, 0.6f), Is.EqualTo(ItemCatalog.Potion));
        Assert.That(LootTable.Roll(profile, 0.9f), Is.Null);
    }

    // The leaderboard ranks scores from highest to lowest.
    [Test]
    public void Leaderboard_RanksHighestFirst()
    {
        var records = new List<LeaderboardRecord>
        {
            new LeaderboardRecord { PlayerName = "A", Score = 900 },
            new LeaderboardRecord { PlayerName = "B", Score = 2500 },
            new LeaderboardRecord { PlayerName = "C", Score = 1200 }
        };
        List<LeaderboardRecord> ranked = LeaderboardService.Rank(records);
        Assert.That(ranked.ConvertAll(r => r.PlayerName), Is.EqualTo(new[] { "B", "C", "A" }));
    }
}

public class SystemTests
{
    private readonly List<Object> created = new List<Object>();

    // Destroys every object a test created.
    [TearDown]
    public void TearDown()
    {
        foreach (Object o in created)
        {
            if (o != null)
            {
                Object.DestroyImmediate(o);
            }
        }
        created.Clear();
    }

    // The pool reuses released objects instead of making new ones, and ignores double releases.
    [Test]
    public void ObjectPool_ReusesObjectsAndIgnoresDoubleRelease()
    {
        GameObject parent = new GameObject("Pool Parent");
        GameObject prefab = new GameObject("Pool Prefab");
        created.Add(parent);
        created.Add(prefab);
        ObjectPool<Transform> pool = new ObjectPool<Transform>(prefab.transform, 2, parent.transform);
        Assert.That(pool.CountInactive, Is.EqualTo(2));

        Transform first = pool.Get(Vector3.one, Quaternion.identity);
        Assert.That(first.gameObject.activeSelf, Is.True);
        Assert.That(first.position, Is.EqualTo(Vector3.one));
        Assert.That(pool.Release(first), Is.True);
        Assert.That(pool.Release(first), Is.False);
        Assert.That(first.gameObject.activeSelf, Is.False);

        Transform again = pool.Get(Vector3.zero, Quaternion.identity);
        Assert.That(again, Is.SameAs(first));
        Assert.That(pool.CountAll, Is.EqualTo(2));
    }

    // Shared content stays loaded until the last user releases it.
    [Test]
    public void ReferenceCounter_UnloadsOnlyAfterLastRelease()
    {
        ReferenceCounter<string> counter = new ReferenceCounter<string>();
        Assert.That(counter.Acquire("audio-sfx"), Is.True);
        Assert.That(counter.Acquire("audio-sfx"), Is.False);
        Assert.That(counter.Release("audio-sfx"), Is.False);
        Assert.That(counter.Count("audio-sfx"), Is.EqualTo(1));
        Assert.That(counter.Release("audio-sfx"), Is.True);
        Assert.That(counter.Release("audio-sfx"), Is.False);
    }

    // An ability can only be used once per cooldown.
    [Test]
    public void Ability_RespectsItsCooldown()
    {
        float now = 10f;
        SwordSlashAbility ability = new SwordSlashAbility(2f, () => now);
        Assert.That(ability.Activate(null), Is.True);
        Assert.That(ability.IsReady, Is.False);
        Assert.That(ability.Activate(null), Is.False);
        now += ability.Cooldown * 0.5f;
        Assert.That(ability.CooldownRemaining01, Is.EqualTo(0.5f).Within(0.001f));
        now += ability.Cooldown * 0.5f;
        Assert.That(ability.Activate(null), Is.True);
    }

    // Enemy profiles give each type its own numbers and hand out copies, not the shared defaults.
    [Test]
    public void EnemyProfiles_AreDistinctCopies()
    {
        Assert.That(EnemyProfiles.Get(EnemyKind.Skeleton).MaxHealth, Is.EqualTo(2));
        Assert.That(EnemyProfiles.Get(EnemyKind.RockGiant).MaxHealth, Is.EqualTo(3));
        Assert.That(EnemyProfiles.Get(EnemyKind.GiantCannonBoss).MaxHealth, Is.EqualTo(10));
        EnemyProfile copy = EnemyProfiles.Get(EnemyKind.Skeleton);
        copy.MaxHealth = 999;
        Assert.That(EnemyProfiles.Get(EnemyKind.Skeleton).MaxHealth, Is.EqualTo(2));
    }

    // The registry gives every enemy type its own behaviour class.
    [Test]
    public void EnemyBehaviourRegistry_GivesEachTypeItsOwnBehaviour()
    {
        Assert.That(EnemyBehaviourRegistry.Create(EnemyKind.Skeleton), Is.InstanceOf<MeleeEnemyBehaviour>());
        Assert.That(EnemyBehaviourRegistry.Create(EnemyKind.RockGiant), Is.InstanceOf<GuardianEnemyBehaviour>());
        Assert.That(EnemyBehaviourRegistry.Create(EnemyKind.CannonRobot), Is.InstanceOf<CannonEnemyBehaviour>());
        Assert.That(EnemyBehaviourRegistry.Create(EnemyKind.GiantCannonBoss), Is.InstanceOf<BossEnemyBehaviour>());
    }

    // The bestiary parser reads the API's JSON and rejects junk.
    [Test]
    public void Bestiary_ParsesApiJsonAndRejectsJunk()
    {
        const string json = "{\"index\":\"skeleton\",\"name\":\"Skeleton\",\"size\":\"Medium\",\"type\":\"undead\"," +
                            "\"hit_points\":13,\"challenge_rating\":0.25,\"armor_class\":[{\"type\":\"armor\",\"value\":13}]," +
                            "\"actions\":[{\"name\":\"Shortsword\",\"desc\":\"Melee\"}]}";
        MonsterInfo info = BestiaryService.Parse(json);
        Assert.That(info, Is.Not.Null);
        Assert.That(info.hit_points, Is.EqualTo(13));
        Assert.That(BestiaryService.FormatLore(info), Does.Contain("AC 13").And.Contain("Shortsword"));
        Assert.That(BestiaryService.Parse("<html>offline</html>"), Is.Null);
        Assert.That(BestiaryService.Parse(null), Is.Null);
    }

    // Every level in the catalog exists and points at a content bundle that the build will produce.
    [Test]
    public void LevelCatalog_EveryLevelHasManagedMusic()
    {
        Assert.That(LevelCatalog.Count, Is.EqualTo(ProgressionRules.LevelCount));
        for (int i = 1; i <= LevelCatalog.Count; i++)
        {
            LevelDefinition level = LevelCatalog.Get(i);
            Assert.That(level.Number, Is.EqualTo(i));
            Assert.That(ContentCatalog.Find(level.MusicBundle), Is.Not.Null, $"Level {i} music bundle");
        }
        Assert.That(LevelCatalog.Get(6), Is.Null);
    }
}
