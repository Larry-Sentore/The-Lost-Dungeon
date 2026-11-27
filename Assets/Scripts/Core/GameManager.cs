using UnityEngine;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public LevelManager LevelManager { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LevelManager = GetComponent<LevelManager>();
        DontDestroyOnLoad(gameObject);
    }

    public void StartLevel(int level)
    {
        LevelManager?.StartLevel(level);
    }
}
