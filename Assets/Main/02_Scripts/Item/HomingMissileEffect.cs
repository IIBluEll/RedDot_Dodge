using System.Collections.Generic;
using UnityEngine;

public interface IHomingMissileTarget
{
    bool TryAcquireMissileTarget(Vector2 position, IReadOnlyList<int> reservedTargets, out int targetId, out Vector2 targetPosition);
    bool TryGetMissileTarget(int targetId, out Vector2 targetPosition);
    bool TryFindMissileContact(Vector2 start, Vector2 end, float radius, out Vector2 impactPosition);
}

public enum HOMING_MISSILE_PHASE
{
    FLYING,
    EXPLODING,
    FINISHED
}

public struct HomingMissileState
{
    public Vector2 Position { get; internal set; }
    public Vector2 Direction { get; internal set; }
    public int TargetId { get; internal set; }
    public HOMING_MISSILE_PHASE Phase { get; internal set; }
    public float ExplosionStartedAtSeconds { get; internal set; }
}

public sealed class HomingMissileEffect : AItemEffect
{
    private readonly HomingMissileDefinition DEFINITION;
    private readonly IHomingMissileTarget TARGET;
    private readonly HomingMissileState[] MISSILES;
    private readonly int[] RESERVED_TARGETS;
    private float _lastAppliedAtSeconds;

    public IReadOnlyList<HomingMissileState> Missiles => MISSILES;
    public float BlastRadius => DEFINITION.BlastRadius;
    public float BlastDurationSeconds => DEFINITION.BlastDurationSeconds;

    public HomingMissileEffect(HomingMissileDefinition definition, ItemEffectContext context)
        : base(context, definition.LifetimeSeconds + definition.BlastDurationSeconds + 0.1f, ITEM_EFFECT_ANCHOR.WORLD, definition.Priority)
    {
        DEFINITION = definition;
        TARGET = context.Target as IHomingMissileTarget ?? throw new System.ArgumentException("유도 미사일에는 적 추적·접촉 조회 기능이 필요합니다.");
        MISSILES = new HomingMissileState[definition.MissileCount];
        RESERVED_TARGETS = new int[definition.MissileCount];
    }

    protected override void OnBegan()
    {
        Vector2 forward = CONTEXT.OwnerTrans != null ? (Vector2)CONTEXT.OwnerTrans.up : Vector2.up;
        float initialAngle = Mathf.Atan2(forward.y, forward.x);
        for (int i = 0; i < MISSILES.Length; i++)
        {
            float angle = initialAngle + i * Mathf.PI * 2f / MISSILES.Length;
            MISSILES[i] = new HomingMissileState { Position = Position, Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) };
        }
    }

    public override int Apply()
    {
        float deltaTime = Mathf.Max(0f, ElapsedSeconds - _lastAppliedAtSeconds);
        _lastAppliedAtSeconds = ElapsedSeconds;
        int killCount = 0;
        int remainingCount = 0;

        for (int i = 0; i < MISSILES.Length; i++)
            RESERVED_TARGETS[i] = MISSILES[i].Phase == HOMING_MISSILE_PHASE.FLYING ? MISSILES[i].TargetId : 0;

        for (int i = 0; i < MISSILES.Length; i++)
        {
            HomingMissileState missile = MISSILES[i];
            if (missile.Phase == HOMING_MISSILE_PHASE.FINISHED)
                continue;

            if (missile.Phase == HOMING_MISSILE_PHASE.FLYING)
            {
                if (ElapsedSeconds >= DEFINITION.LifetimeSeconds)
                    Explode(ref missile, missile.Position);
                else
                {
                    RESERVED_TARGETS[i] = 0;
                    if (!TARGET.TryGetMissileTarget(missile.TargetId, out Vector2 targetPosition))
                    {
                        TARGET.TryAcquireMissileTarget(missile.Position, RESERVED_TARGETS, out int targetId, out targetPosition);
                        missile.TargetId = targetId;
                    }
                    RESERVED_TARGETS[i] = missile.TargetId;
                    Vector2 desired = targetPosition - missile.Position;
                    if (missile.TargetId != 0 && desired.sqrMagnitude > 0.000001f)
                    {
                        float angle = Mathf.Atan2(missile.Direction.y, missile.Direction.x) * Mathf.Rad2Deg;
                        float targetAngle = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
                        angle = Mathf.MoveTowardsAngle(angle, targetAngle, DEFINITION.TurnSpeed * deltaTime) * Mathf.Deg2Rad;
                        missile.Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    }
                    Vector2 nextPosition = missile.Position + missile.Direction * (DEFINITION.Speed * deltaTime);
                    if (TARGET.TryFindMissileContact(missile.Position, nextPosition, DEFINITION.ContactRadius, out Vector2 impactPosition))
                        Explode(ref missile, impactPosition);
                    else
                        missile.Position = nextPosition;
                }
            }

            if (missile.Phase == HOMING_MISSILE_PHASE.EXPLODING)
            {
                if (ElapsedSeconds - missile.ExplosionStartedAtSeconds >= DEFINITION.BlastDurationSeconds)
                    missile.Phase = HOMING_MISSILE_PHASE.FINISHED;
                else
                    killCount += CONTEXT.Target.KillEnemiesInRadius(missile.Position, DEFINITION.BlastRadius);
            }

            MISSILES[i] = missile;
            if (missile.Phase != HOMING_MISSILE_PHASE.FINISHED)
                remainingCount++;
        }
        if (remainingCount == 0)
            Finish();
        return killCount;
    }

    private void Explode(ref HomingMissileState missile, Vector2 position)
    {
        missile.Position = position;
        missile.TargetId = 0;
        missile.Phase = HOMING_MISSILE_PHASE.EXPLODING;
        missile.ExplosionStartedAtSeconds = ElapsedSeconds;
    }
}
