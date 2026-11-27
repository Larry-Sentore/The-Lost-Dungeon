using System;
using UnityEngine;

// The game's message board (Observer pattern). Systems raise events here and other systems listen,
// so gameplay, UI, audio and saving never need direct references to each other.
public static class GameEvents
{
    // Player
    public static event Action<float, float> HealthChanged;
    public static event Action<int> PlayerDamaged;
    public static event Action PlayerAttacked;
    public static event Action PlayerHealed;
    public static event Action PlayerDied;

    // Enemies
    public static event Action<int> EnemyDefeated;
    public static event Action<EnemyKind, Vector3> EnemyKilled;
    public static event Action<EnemyKind, Vector3> EnemyHit;
    public static event Action<EnemyKind, Vector3> EnemyBlocked;
    public static event Action<EnemyKind> EnemyAttacked;
    public static event Action<int> BossPhaseChanged;

    // Levels and game flow
    public static event Action<LevelDefinition> LevelStarted;
    public static event Action<int> LevelCompleted;
    public static event Action<LevelResult> LevelResultReady;
    public static event Action GameOver;
    public static event Action<int, bool> LevelUnlockChanged;
    public static event Action<string> ObjectiveChanged;
    public static event Action<GameStateId, GameStateId> GameStateChanged;

    // Abilities, items and interaction
    public static event Action<string, bool> AbilityChanged;
    public static event Action<string> AbilityActivated;
    public static event Action<string, int> InventoryChanged;
    public static event Action<string> ItemCollected;
    public static event Action<string> InteractionPromptChanged;
    public static event Action ChestOpened;

    // Content, online services, UI
    public static event Action<string, float> ContentLoadProgress;
    public static event Action<string, bool> ContentLoadFinished;
    public static event Action<string, bool> RequestStatusChanged;
    public static event Action<string> Notification;
    public static event Action<string> UiSound;
    public static event Action<SettingsData> SettingsChanged;

    // Tells listeners the player's health changed.
    public static void RaiseHealthChanged(float current, float maximum) => HealthChanged?.Invoke(current, maximum);
    // Tells listeners the player took damage.
    public static void RaisePlayerDamaged(int amount) => PlayerDamaged?.Invoke(amount);
    // Tells listeners the player swung their sword.
    public static void RaisePlayerAttacked() => PlayerAttacked?.Invoke();
    // Tells listeners the player was healed.
    public static void RaisePlayerHealed() => PlayerHealed?.Invoke();
    // Tells listeners the player died.
    public static void RaisePlayerDied() => PlayerDied?.Invoke();

    // Tells listeners an enemy was defeated, with how many are left.
    public static void RaiseEnemyDefeated(int enemiesRemaining) => EnemyDefeated?.Invoke(enemiesRemaining);
    // Tells listeners which kind of enemy died and where.
    public static void RaiseEnemyKilled(EnemyKind kind, Vector3 position) => EnemyKilled?.Invoke(kind, position);
    // Tells listeners an enemy was hurt.
    public static void RaiseEnemyHit(EnemyKind kind, Vector3 position) => EnemyHit?.Invoke(kind, position);
    // Tells listeners an enemy blocked a hit.
    public static void RaiseEnemyBlocked(EnemyKind kind, Vector3 position) => EnemyBlocked?.Invoke(kind, position);
    // Tells listeners an enemy attacked.
    public static void RaiseEnemyAttacked(EnemyKind kind) => EnemyAttacked?.Invoke(kind);
    // Tells listeners the boss moved into a new phase.
    public static void RaiseBossPhaseChanged(int phase) => BossPhaseChanged?.Invoke(phase);

    // Tells listeners a level started.
    public static void RaiseLevelStarted(LevelDefinition level) => LevelStarted?.Invoke(level);
    // Tells listeners a level was finished.
    public static void RaiseLevelCompleted(int level) => LevelCompleted?.Invoke(level);
    // Tells listeners the score and rewards for a finished level.
    public static void RaiseLevelResultReady(LevelResult result) => LevelResultReady?.Invoke(result);
    // Tells listeners the player lost.
    public static void RaiseGameOver() => GameOver?.Invoke();
    // Tells listeners a level was locked or unlocked.
    public static void RaiseLevelUnlockChanged(int level, bool unlocked) => LevelUnlockChanged?.Invoke(level, unlocked);
    // Tells listeners the current objective text changed.
    public static void RaiseObjectiveChanged(string objective) => ObjectiveChanged?.Invoke(objective);
    // Tells listeners the game moved from one state to another (menu, playing, paused...).
    public static void RaiseGameStateChanged(GameStateId previous, GameStateId next) => GameStateChanged?.Invoke(previous, next);

    // Tells listeners an ability was used (ready = false) or is ready again (ready = true).
    public static void RaiseAbilityChanged(string abilityName, bool ready) => AbilityChanged?.Invoke(abilityName, ready);
    // Tells listeners an ability was just used.
    public static void RaiseAbilityActivated(string abilityName) => AbilityActivated?.Invoke(abilityName);
    // Tells listeners an item amount in the inventory changed.
    public static void RaiseInventoryChanged(string itemId, int quantity) => InventoryChanged?.Invoke(itemId, quantity);
    // Tells listeners an item was picked up.
    public static void RaiseItemCollected(string itemId) => ItemCollected?.Invoke(itemId);
    // Tells listeners what the interact prompt should say (null hides it).
    public static void RaiseInteractionPromptChanged(string prompt) => InteractionPromptChanged?.Invoke(prompt);
    // Tells listeners a treasure chest was opened.
    public static void RaiseChestOpened() => ChestOpened?.Invoke();

    // Tells listeners how far along a content download is (0 to 1).
    public static void RaiseContentLoadProgress(string label, float progress) => ContentLoadProgress?.Invoke(label, progress);
    // Tells listeners a content download finished, and whether it worked.
    public static void RaiseContentLoadFinished(string label, bool success) => ContentLoadFinished?.Invoke(label, success);
    // Tells listeners whether an online request worked or failed.
    public static void RaiseRequestStatusChanged(string message, bool success) => RequestStatusChanged?.Invoke(message, success);
    // Shows a short message on screen.
    public static void RaiseNotification(string message) => Notification?.Invoke(message);
    // Asks the audio system to play a UI sound.
    public static void RaiseUiSound(string soundId) => UiSound?.Invoke(soundId);
    // Tells listeners the settings changed.
    public static void RaiseSettingsChanged(SettingsData settings) => SettingsChanged?.Invoke(settings);
}
