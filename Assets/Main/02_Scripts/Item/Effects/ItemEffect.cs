using UnityEngine;

public interface IItemEffectTarget
{
    bool IsItemPositionClear(Vector2 position, float enemyDistance);
    int KillEnemiesInRadius(Vector2 position, float radius);
    int KillEnemiesAlongPath(Vector2 start, Vector2 end, float radius);
    void FreezeEnemiesInRadius(Vector2 position, float radius, float frozenUntilSeconds);
}

public interface IItemContactTarget
{
    bool TryFindDangerContact(Vector2 start, Vector2 end, float radius, out Vector2 impactPosition);
}

public interface IRadialItemEffect
{
    float Radius { get; }
}

public abstract class AItemEffect
{
    protected readonly ItemEffectContext CONTEXT;
    private bool _began;

    public int Priority { get; }
    public ITEM_EFFECT_ANCHOR Anchor { get; }
    public float DurationSeconds { get; }
    public float ElapsedSeconds { get; private set; }
    public bool IsFinished { get; private set; }
    public virtual bool CanBeReplaced => true;
    public float Progress => Mathf.Clamp01(ElapsedSeconds / DurationSeconds);
    public virtual Vector2 Position => Anchor == ITEM_EFFECT_ANCHOR.PLAYER ? (Vector2)CONTEXT.OwnerTrans.position : CONTEXT.ActivationPosition;
    public virtual Vector2 PreviousPosition => Anchor == ITEM_EFFECT_ANCHOR.PLAYER ? CONTEXT.PreviousOwnerPosition : CONTEXT.ActivationPosition;

    protected AItemEffect(ItemEffectContext context, float durationSeconds, ITEM_EFFECT_ANCHOR anchor, int priority)
    {
        if (context == null || context.Target == null || durationSeconds <= 0f)
            throw new System.ArgumentException("효과 대상과 지속시간이 필요합니다.");
        if (anchor == ITEM_EFFECT_ANCHOR.PLAYER && context.OwnerTrans == null)
            throw new System.ArgumentException("플레이어 부착 효과에는 Owner Transform이 필요합니다.");

        CONTEXT = context;
        DurationSeconds = durationSeconds;
        Anchor = anchor;
        Priority = priority;
    }

    public void Begin()
    {
        if (_began || IsFinished)
            return;

        _began = true;
        OnBegan();
    }

    public bool Tick(float survivalSeconds)
    {
        if (IsFinished)
            return false;

        ElapsedSeconds = Mathf.Max(0f, survivalSeconds - CONTEXT.StartedAtSeconds);
        if (ElapsedSeconds >= DurationSeconds || (Anchor == ITEM_EFFECT_ANCHOR.PLAYER && CONTEXT.OwnerTrans == null))
        {
            Finish();
            return false;
        }

        CONTEXT.TickOwnerPosition();
        OnTicked();
        return !IsFinished;
    }

    public void Finish()
    {
        if (IsFinished)
            return;

        IsFinished = true;
        if (_began)
            OnFinished();
    }

    protected virtual void OnBegan() { }
    protected virtual void OnTicked() { }
    protected virtual void OnFinished() { }
    protected int KillEnemiesInRange(float radius)
    {
        return CONTEXT.Target.KillEnemiesAlongPath(PreviousPosition, Position, radius);
    }
    public abstract int Apply();
}

public sealed class BombEffect : AItemEffect, IRadialItemEffect
{
    private readonly float RADIUS;
    public float Radius => RADIUS;

    public BombEffect(ItemData data, ItemEffectContext context)
        : base(context, data.DurationSeconds, ITEM_EFFECT_ANCHOR.WORLD, data.EffectPriority)
    {
        RADIUS = data.Radius;
    }

    public override int Apply()
    {
        return KillEnemiesInRange(RADIUS);
    }
}

public sealed class FreezeEffect : AItemEffect, IRadialItemEffect
{
    private bool _applied;
    private readonly float RADIUS;
    private readonly float FROZEN_UNTIL_SECONDS;
    public float Radius => RADIUS;

    public FreezeEffect(ItemData data, ItemEffectContext context)
        : base(context, data.VisualDurationSeconds, ITEM_EFFECT_ANCHOR.WORLD, data.EffectPriority)
    {
        RADIUS = data.Radius;
        FROZEN_UNTIL_SECONDS = context.StartedAtSeconds + data.DurationSeconds;
    }

    public override int Apply()
    {
        if (!_applied)
        {
            CONTEXT.Target.FreezeEnemiesInRadius(Position, RADIUS, FROZEN_UNTIL_SECONDS);
            _applied = true;
        }

        return 0;
    }
}
