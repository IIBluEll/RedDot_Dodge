using UnityEngine;

public sealed class ShieldEffect : AItemEffect
{
    private readonly ShieldEffectDefinition DEFINITION;
    private readonly IItemContactTarget CONTACT_TARGET;
    private Vector2 _burstPosition;
    private float _burstStartedAtSeconds;

    public bool HasBurst { get; private set; }
    public float ShieldRadius => DEFINITION.ShieldRadius;
    public float BurstRadius => DEFINITION.BurstRadius;
    public float BurstProgress => HasBurst ? Mathf.Clamp01((ElapsedSeconds - _burstStartedAtSeconds) / DEFINITION.BurstDurationSeconds) : 0f;
    public override bool CanBeReplaced => !HasBurst;
    public override Vector2 Position => HasBurst ? _burstPosition : base.Position;
    public override Vector2 PreviousPosition => HasBurst ? _burstPosition : base.PreviousPosition;

    public ShieldEffect(ShieldEffectDefinition definition, ItemEffectContext context)
        : base(context, float.PositiveInfinity, ITEM_EFFECT_ANCHOR.PLAYER, definition.Priority)
    {
        DEFINITION = definition;
        CONTACT_TARGET = context.Target as IItemContactTarget
            ?? throw new System.ArgumentException("보호막 효과에는 적 접촉 조회 기능이 필요합니다.");
    }

    protected override void OnTicked()
    {
        if (HasBurst && ElapsedSeconds - _burstStartedAtSeconds >= DEFINITION.BurstDurationSeconds)
            Finish();
    }

    public override int Apply()
    {
        if (!HasBurst)
        {
            if (!CONTACT_TARGET.TryFindDangerContact(PreviousPosition, Position, DEFINITION.ShieldRadius, out Vector2 impactPosition))
                return 0;

            HasBurst = true;
            _burstPosition = impactPosition;
            _burstStartedAtSeconds = ElapsedSeconds;
        }

        return CONTEXT.Target.KillEnemiesInRadius(_burstPosition, DEFINITION.BurstRadius);
    }
}
