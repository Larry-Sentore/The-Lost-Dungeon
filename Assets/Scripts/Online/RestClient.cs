using System;
using System.Collections;
using UnityEngine.Networking;

// The result of a web request.
public struct RestResponse
{
    public bool Success;
    public long StatusCode;
    public string Body;
    public string Error;
}

// Sends GET requests with UnityWebRequest. Retries once on a network error, never throws.
public class UnityRestClient : IRestClient
{
    private readonly int retries;

    // Creates a client that retries failed requests the given number of times.
    public UnityRestClient(int retries = 1)
    {
        this.retries = Math.Max(0, retries);
    }

    // Sends a GET request and calls done with the result (success or failure).
    public IEnumerator Get(string url, int timeoutSeconds, Action<RestResponse> done)
    {
        RestResponse response = default;
        for (int attempt = 0; attempt <= retries; attempt++)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.timeout = timeoutSeconds;
                request.SetRequestHeader("Accept", "application/json");
                yield return request.SendWebRequest();

                response = new RestResponse
                {
                    Success = request.result == UnityWebRequest.Result.Success,
                    StatusCode = request.responseCode,
                    Body = request.downloadHandler != null ? request.downloadHandler.text : null,
                    Error = request.error
                };
                // Only retry problems that might go away (no connection or a server error), not "not found".
                bool retryable = request.result == UnityWebRequest.Result.ConnectionError || request.responseCode >= 500;
                if (response.Success || !retryable)
                {
                    break;
                }
            }
        }
        done?.Invoke(response);
    }
}
