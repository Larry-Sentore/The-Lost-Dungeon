using UnityEngine;

// The top-level game controller (Singleton). Owns the game state machine (State pattern) and turns
// gameplay events into state changes, so the UI only has to react to GameStateChanged.
[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public LevelManager LevelManager { get; private set; }
    public GameStateMachine StateMachine { get; private set; }
    public GameStateId State => StateMachine.CurrentId;

    // Makes sure only one GameManager exists, creates the state machine and listens for win/lose events.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Only remove this component: it shares its GameObject with other systems.
            Destroy(this);
            return;
        }
        Instance = this;
        LevelManager = GetComponent<LevelManager>();
        if (LevelManager == null)
        {
            LevelManager = FindAnyObjectByType<LevelManager>();
        }

        StateMachine = new GameStateMachine(scale => Time.timeScale = scale);
        StateMachine.Changed += GameEvents.RaiseGameStateChanged;
        GameEvents.LevelCompleted += OnLevelCompleted;
        GameEvents.GameOver += OnGameOver;
    }

    // Clears the shared instance and stops listening.
    private void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }
        StateMachine.Changed -= GameEvents.RaiseGameStateChanged;
        GameEvents.LevelCompleted -= OnLevelCompleted;
        GameEvents.GameOver -= OnGameOver;
        Instance = null;
    }

    // Saves when the app is closed or sent to the background (important on mobile, where apps are killed).
    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveSystem.Save();
        }
    }

    // Saves when the app quits.
    private void OnApplicationQuit()
    {
        SaveSystem.Save();
    }

    // Asks for a state change. Returns false if the change is not allowed from the current state.
    public bool ChangeState(GameStateId next) => StateMachine.TryChange(next);

    // Starts a level and switches to Playing. Returns false if the level could not start.
    public bool StartLevel(int level)
    {
        if (LevelManager == null || !LevelManager.StartLevel(level))
        {
            return false;
        }
        if (State != GameStateId.Playing)
        {
            ChangeState(GameStateId.Playing);
        }
        return true;
    }

    // Pauses or resumes, depending on the current state.
    public void TogglePause()
    {
        if (State == GameStateId.Playing)
        {
            SaveSystem.Save();
            ChangeState(GameStateId.Paused);
        }
        else if (State == GameStateId.Paused)
        {
            ChangeState(GameStateId.Playing);
        }
    }

    // Moves to the level complete state.
    private void OnLevelCompleted(int level) => ChangeState(GameStateId.LevelComplete);

    // Moves to the game over state.
    private void OnGameOver() => ChangeState(GameStateId.GameOver);
}
