using System;
using System.Collections.Generic;

public enum GameStateId
{
    MainMenu,
    Playing,
    Paused,
    LevelComplete,
    GameOver
}

// Base class for a game state. Each state says which states it can move to (State pattern).
public abstract class GameState : IGameState
{
    private readonly HashSet<GameStateId> allowedNext;

    public GameStateId Id { get; }
    public float TimeScale { get; }

    // Creates a state with its time scale and the states it is allowed to move to.
    protected GameState(GameStateId id, float timeScale, params GameStateId[] allowed)
    {
        Id = id;
        TimeScale = timeScale;
        allowedNext = new HashSet<GameStateId>(allowed);
    }

    // Runs when the game enters this state. Override to add behaviour.
    public virtual void Enter() { }

    // Runs when the game leaves this state. Override to add behaviour.
    public virtual void Exit() { }

    // Returns true if moving to the next state is allowed.
    public bool CanTransitionTo(GameStateId next) => allowedNext.Contains(next);
}

// Menus are showing. Gameplay is frozen.
public class MainMenuState : GameState
{
    // Can only go into a level from the menu.
    public MainMenuState() : base(GameStateId.MainMenu, 0f, GameStateId.Playing) { }
}

// The player is in a level. Gameplay runs.
public class PlayingState : GameState
{
    // From playing you can pause, win, lose or quit to the menu.
    public PlayingState() : base(GameStateId.Playing, 1f,
        GameStateId.Paused, GameStateId.LevelComplete, GameStateId.GameOver, GameStateId.MainMenu) { }
}

// The pause menu is open. Gameplay is frozen.
public class PausedState : GameState
{
    // From pause you can resume or quit to the menu.
    public PausedState() : base(GameStateId.Paused, 0f, GameStateId.Playing, GameStateId.MainMenu) { }
}

// The level was finished. Gameplay is frozen behind the success screen.
public class LevelCompleteState : GameState
{
    // After winning you can go back to the menu or start another level.
    public LevelCompleteState() : base(GameStateId.LevelComplete, 0f, GameStateId.MainMenu, GameStateId.Playing) { }
}

// The player died. Gameplay is frozen behind the fail screen.
public class GameOverState : GameState
{
    // After losing you can go back to the menu or retry.
    public GameOverState() : base(GameStateId.GameOver, 0f, GameStateId.MainMenu, GameStateId.Playing) { }
}

// Keeps track of which game state we are in and only allows valid changes.
public class GameStateMachine
{
    private readonly Dictionary<GameStateId, IGameState> states = new Dictionary<GameStateId, IGameState>();
    private readonly Action<float> applyTimeScale;

    public IGameState Current { get; private set; }
    public GameStateId CurrentId => Current.Id;

    public event Action<GameStateId, GameStateId> Changed;

    // Creates the machine with all game states. applyTimeScale is how time gets frozen (Time.timeScale in the game).
    public GameStateMachine(Action<float> applyTimeScale, GameStateId initial = GameStateId.MainMenu)
    {
        this.applyTimeScale = applyTimeScale;
        Register(new MainMenuState());
        Register(new PlayingState());
        Register(new PausedState());
        Register(new LevelCompleteState());
        Register(new GameOverState());
        Current = states[initial];
        Current.Enter();
        this.applyTimeScale?.Invoke(Current.TimeScale);
    }

    // Adds (or replaces) a state. New states can be added without changing this class.
    public void Register(IGameState state)
    {
        states[state.Id] = state;
    }

    // Moves to another state if that is allowed. Returns false if it is not allowed.
    public bool TryChange(GameStateId next)
    {
        if (Current.Id == next || !Current.CanTransitionTo(next) || !states.TryGetValue(next, out IGameState target))
        {
            return false;
        }

        GameStateId previous = Current.Id;
        Current.Exit();
        Current = target;
        Current.Enter();
        applyTimeScale?.Invoke(Current.TimeScale);
        Changed?.Invoke(previous, next);
        return true;
    }
}
