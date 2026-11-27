using UnityEngine;

// Anything that can be hurt (the player, every enemy type).
public interface IDamageable
{
    // True once this object has run out of health.
    bool IsDead { get; }

    // Applies one hit. DamageInfo says how much, where it came from and how hard it pushes.
    void TakeDamage(DamageInfo damage);
}

// Anything that can attack something else.
public interface IAttackable
{
    // Attacks a target that can be hurt.
    void Attack(IDamageable target);
}

// Anything that can move around the level.
public interface IMovable
{
    // Moves in a direction (the length of the vector does not matter).
    void Move(Vector3 direction);
}

// Anything the player can use by walking up and pressing the interact button (chests, doors, switches).
public interface IInteractable
{
    // The text shown to the player, for example "Open chest".
    string Prompt { get; }

    // Where the object is, so the player can find the closest one.
    Vector3 Position { get; }

    // Returns true if this object can be used right now.
    bool CanInteract(GameObject interactor);

    // Uses the object.
    void Interact(GameObject interactor);
}

// Anything the player can pick up (gems, potions, gold).
public interface ICollectable
{
    // The item id this pickup adds to the inventory.
    string ItemId { get; }

    // Picks the item up.
    void Collect(GameObject collector);
}

// A player ability. Each ability is its own class, so they can be swapped (Strategy pattern).
public interface IAbility
{
    string AbilityName { get; }
    float Cooldown { get; }
    bool IsReady { get; }

    // 0 means ready, 1 means the cooldown just started.
    float CooldownRemaining01 { get; }

    // Tries to use the ability. Returns false if it is still cooling down.
    bool Activate(GameObject owner);
}

// Anything that stores its own data in the save file.
public interface ISaveable
{
    // Writes this object's data into the save data.
    void CaptureState(SaveData data);

    // Reads this object's data back out of the save data.
    void RestoreState(SaveData data);
}

// Where the save file text is kept. Different platforms use different storage.
public interface ISaveStorage
{
    // Reads the saved text. Returns false if nothing has been saved yet.
    bool TryRead(out string json);

    // Writes the saved text.
    void Write(string json);

    // Removes the save.
    void Delete();
}

// How an enemy type thinks and acts. Each enemy type has its own class (Strategy pattern).
public interface IEnemyBehaviour
{
    // The state the enemy is currently in.
    EnemyState State { get; }

    // Sets up the behaviour's states for this enemy.
    void Initialise(EnemyController enemy);

    // Runs the behaviour for one frame.
    void Tick(EnemyController enemy);

    // Lets a behaviour change incoming damage, for example a guardian blocking hits.
    int ModifyIncomingDamage(EnemyController enemy, DamageInfo damage, int amount);

    // Called after the enemy has been hit.
    void OnDamaged(EnemyController enemy, DamageInfo damage);

    // Called once when the enemy dies.
    void OnDeath(EnemyController enemy);
}

// One state in a state machine (State pattern). TId names the state, TContext is who owns it.
public interface IState<TId, TContext>
{
    TId Id { get; }

    // Runs once when the machine enters this state.
    void Enter(TContext context);

    // Runs every frame. Returns the state to be in next (return Id to stay).
    TId Tick(TContext context);

    // Runs once when the machine leaves this state.
    void Exit(TContext context);
}

// One screen-level game state, such as Playing or Paused (State pattern).
public interface IGameState
{
    GameStateId Id { get; }

    // How fast game time runs in this state (0 freezes gameplay).
    float TimeScale { get; }

    // Runs once when the game enters this state.
    void Enter();

    // Runs once when the game leaves this state.
    void Exit();

    // Returns true if the game is allowed to go from this state to the next one.
    bool CanTransitionTo(GameStateId next);
}

// Sends web requests. Hidden behind an interface so the game does not depend on one HTTP library.
public interface IRestClient
{
    // Sends a GET request and calls done with the result.
    System.Collections.IEnumerator Get(string url, int timeoutSeconds, System.Action<RestResponse> done);
}
