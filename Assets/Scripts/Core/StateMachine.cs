using System;
using System.Collections.Generic;

// A small reusable state machine (State pattern). Enemies use it, and it can be used for anything else.
public class StateMachine<TId, TContext>
{
    private readonly Dictionary<TId, IState<TId, TContext>> states = new Dictionary<TId, IState<TId, TContext>>();

    public IState<TId, TContext> Current { get; private set; }
    public TId CurrentId => Current != null ? Current.Id : default;

    // Raised with (old state, new state) whenever the state changes.
    public event Action<TId, TId> Changed;

    // Adds a state to the machine.
    public void Add(IState<TId, TContext> state)
    {
        states[state.Id] = state;
    }

    // Returns true if a state with this id was added.
    public bool Has(TId id) => states.ContainsKey(id);

    // Starts the machine in a state.
    public void Start(TId id, TContext context)
    {
        Current = null;
        ChangeTo(id, context);
    }

    // Runs the current state for one frame and moves to whatever state it asks for.
    public void Tick(TContext context)
    {
        if (Current == null)
        {
            return;
        }
        TId next = Current.Tick(context);
        if (!EqualityComparer<TId>.Default.Equals(next, Current.Id))
        {
            ChangeTo(next, context);
        }
    }

    // Leaves the current state and enters another one. Unknown states are ignored.
    public bool ChangeTo(TId id, TContext context)
    {
        if (!states.TryGetValue(id, out IState<TId, TContext> next))
        {
            return false;
        }
        TId previous = CurrentId;
        Current?.Exit(context);
        Current = next;
        Current.Enter(context);
        Changed?.Invoke(previous, id);
        return true;
    }
}
