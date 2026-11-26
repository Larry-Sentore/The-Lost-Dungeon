using UnityEngine;

public interface IDamageable
{
    void TakeDamage(int amount);
}

public interface IAttackable
{
    void Attack(IDamageable target);
}

public interface IMovable
{
    void Move(Vector3 direction);
}

public interface IInteractable
{
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
}

public interface ICollectable
{
    void Collect(GameObject collector);
}

public interface IAbility
{
    string AbilityName { get; }
    float Cooldown { get; }
    bool IsReady { get; }
    void Activate(GameObject owner);
}

public interface ISaveable
{
    object CaptureState();
    void RestoreState(object state);
}

public interface IEnemyBehaviour
{
    void Tick(EnemyController enemy, Transform player);
    EnemyState State { get; }
}
