using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action<float, float> HealthChanged;
    public static event Action<int> EnemyDefeated;
    public static event Action<int> LevelCompleted;
    public static event Action GameOver;
    public static event Action<int, bool> LevelUnlockChanged;
    public static event Action<string> ObjectiveChanged;

    public static void RaiseHealthChanged(float current, float maximum) => HealthChanged?.Invoke(current, maximum);
    public static void RaiseEnemyDefeated(int enemyCount) => EnemyDefeated?.Invoke(enemyCount);
    public static void RaiseLevelCompleted(int level) => LevelCompleted?.Invoke(level);
    public static void RaiseGameOver() => GameOver?.Invoke();
    public static void RaiseLevelUnlockChanged(int level, bool unlocked) => LevelUnlockChanged?.Invoke(level, unlocked);
    public static void RaiseObjectiveChanged(string objective) => ObjectiveChanged?.Invoke(objective);
}
