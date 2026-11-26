using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class LeaderboardEntry
{
    public string PlayerName;
    public int Score;
}

public class LeaderboardService : MonoBehaviour
{
    private const string Endpoint = "https://jsonplaceholder.typicode.com/posts";

    public event Action<bool, string> RequestCompleted;

    public void SubmitScore(string playerName, int score)
    {
        StartCoroutine(SubmitRoutine(playerName, score));
    }

    private IEnumerator SubmitRoutine(string playerName, int score)
    {
        LeaderboardEntry entry = new LeaderboardEntry { PlayerName = playerName, Score = score };
        string json = JsonUtility.ToJson(entry);
        using UnityWebRequest request = new UnityWebRequest(Endpoint, "POST");
        byte[] body = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        yield return request.SendWebRequest();

#if UNITY_WEBGL
        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            RequestCompleted?.Invoke(false, "Online leaderboard unavailable. Progress remains saved locally.");
            GameEvents.RaiseRequestStatusChanged("Online leaderboard unavailable. Progress remains saved locally.", false);
            yield break;
        }
#else
        if (request.result != UnityWebRequest.Result.Success)
        {
            RequestCompleted?.Invoke(false, "Online leaderboard unavailable. Progress remains saved locally.");
            GameEvents.RaiseRequestStatusChanged("Online leaderboard unavailable. Progress remains saved locally.", false);
            yield break;
        }
#endif

        RequestCompleted?.Invoke(true, "Score submitted.");
        GameEvents.RaiseRequestStatusChanged("Score submitted.", true);
    }
}
