using System.Collections.Generic;
using UnityEngine;

// The local leaderboard: the best scores, ranked with the game's merge sort and saved in the JSON save file.
public class LeaderboardService : MonoBehaviour
{
    public const int MaxEntries = 10;

    // Adds a score and returns its rank (1 = best). Returns 0 if it did not make the top list.
    public int Record(string playerName, int score, int level)
    {
        List<LeaderboardRecord> records = SaveSystem.Data.Leaderboard;
        LeaderboardRecord record = new LeaderboardRecord { PlayerName = playerName, Score = score, Level = level };
        records.Add(record);
        List<LeaderboardRecord> ranked = Rank(records);
        if (ranked.Count > MaxEntries)
        {
            ranked.RemoveRange(MaxEntries, ranked.Count - MaxEntries);
        }
        SaveSystem.Data.Leaderboard = ranked;
        int rank = ranked.IndexOf(record) + 1;
        return rank;
    }

    // Returns the records from best to worst. Ties keep their original order (merge sort is stable).
    public static List<LeaderboardRecord> Rank(IReadOnlyList<LeaderboardRecord> records)
    {
        return GameplayAlgorithms.MergeSort(records, (a, b) => b.Score.CompareTo(a.Score));
    }

    // Builds a short text version of the top scores for the UI.
    public static string FormatTop(int count)
    {
        List<LeaderboardRecord> records = SaveSystem.Data.Leaderboard;
        if (records.Count == 0)
        {
            return "No scores yet";
        }
        System.Text.StringBuilder text = new System.Text.StringBuilder();
        for (int i = 0; i < Mathf.Min(count, records.Count); i++)
        {
            LeaderboardRecord r = records[i];
            text.AppendLine($"{i + 1}. {r.PlayerName}  {r.Score}  (L{r.Level})");
        }
        return text.ToString();
    }
}
