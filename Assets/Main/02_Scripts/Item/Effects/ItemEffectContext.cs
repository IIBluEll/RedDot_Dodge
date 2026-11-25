using UnityEngine;

public enum ITEM_EFFECT_ANCHOR
{
    WORLD,
    PLAYER
}

public sealed class ItemEffectContext
{
    private Vector2 _lastOwnerPosition;
    public IItemEffectTarget Target { get; }
    public Transform OwnerTrans { get; }
    public Vector2 ActivationPosition { get; }
    public float StartedAtSeconds { get; }
    public Vector2 PreviousOwnerPosition { get; private set; }

    public ItemEffectContext(IItemEffectTarget target, Transform ownerTrans, Vector2 activationPosition, float startedAtSeconds)
    {
        Target = target;
        OwnerTrans = ownerTrans;
        ActivationPosition = activationPosition;
        StartedAtSeconds = startedAtSeconds;
        _lastOwnerPosition = ownerTrans != null ? (Vector2)ownerTrans.position : activationPosition;
        PreviousOwnerPosition = _lastOwnerPosition;
    }

    public void TickOwnerPosition()
    {
        PreviousOwnerPosition = _lastOwnerPosition;
        if (OwnerTrans != null)
            _lastOwnerPosition = OwnerTrans.position;
    }
}

public readonly struct PlayerHit
{
    public Vector2 PlayerPosition { get; }
    public Vector2 EnemyPosition { get; }
    public float SurvivalSeconds { get; }

    public PlayerHit(Vector2 playerPosition, Vector2 enemyPosition, float survivalSeconds)
    {
        PlayerPosition = playerPosition;
        EnemyPosition = enemyPosition;
        SurvivalSeconds = survivalSeconds;
    }
}

public interface IPlayerHitInterceptor
{
    bool TryBlockHit(PlayerHit hit);
}
