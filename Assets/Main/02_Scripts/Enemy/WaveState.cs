using System;
using System.Collections.Generic;
using UnityEngine;

public enum WAVE_PHASE
{
    WARNING,
    ACTIVE_FORMATION,
    INDEPENDENT_ENEMIES,
    FINISHED
}

public readonly struct WaveRequest
{
    public AEnemyPatternData Pattern { get; }
    public DifficultyStage Stage { get; }
    public int ChaseCount { get; }

    public float WarningDurationSeconds => Pattern is ArrowDashPatternData arrow
        ? arrow.WarningDurationSeconds
        : Pattern is InterceptorPatternData interceptor ? interceptor.WarningDurationSeconds : Stage.WarningDurationSeconds;

    public float GetWarningDuration(EnemyPlacement placement)
    {
        return placement.Motion == ENEMY_MOTION.INTERCEPTOR
            ? ((InterceptorPatternData)Pattern).WarningDurationSeconds : WarningDurationSeconds;
    }

    public WaveRequest(AEnemyPatternData pattern , DifficultyStage stage , int chaseCount)
    {
        Pattern = pattern;
        Stage = stage;
        ChaseCount = chaseCount;
    }
}

public enum ENEMY_MOTION
{
    CHASE,
    LINEAR,
    INTERCEPTOR
}

public readonly struct EnemyPlacement
{
    public Vector2 WarningPosition { get; }
    public Vector2 SpawnPosition { get; }
    public ENEMY_MOTION Motion { get; }
    public float WarningDelaySeconds { get; }

    public EnemyPlacement(Vector2 warningPosition , Vector2 spawnPosition, ENEMY_MOTION motion = ENEMY_MOTION.CHASE, float warningDelaySeconds = 0f)
    {
        WarningPosition = warningPosition;
        SpawnPosition = spawnPosition;
        Motion = motion;
        WarningDelaySeconds = warningDelaySeconds;
    }
}

public class WavePlan
{
    public IReadOnlyList<EnemyPlacement> Placements { get; }
    public Vector2 Anchor { get; }
    public Vector2 InitialDirection { get; }

    public int EnemyCount => Placements.Count;

    public WavePlan(EnemyPlacement[] placements , Vector2 anchor , Vector2 initialDirection)
    {
        if ( placements == null || placements.Length == 0 )
        {
            throw new ArgumentException("웨이브에 배치할 적이 필요합니다." , nameof(placements));
        }

        Placements = Array.AsReadOnly((EnemyPlacement[])placements.Clone());
        Anchor = anchor;
        InitialDirection = initialDirection;
    }
}

public sealed class WaveState
{
    public WaveRequest Request { get; }
    public WavePlan Plan { get; }
    public float WarningStartedAtSeconds { get; }
    public float WarningEndsAtSeconds
    {
        get
        {
            float duration = 0f;
            foreach (EnemyPlacement placement in Plan.Placements)
                duration = Mathf.Max(duration, placement.WarningDelaySeconds + Request.GetWarningDuration(placement));
            return WarningStartedAtSeconds + duration;
        }
    }

    public WAVE_PHASE Phase { get; private set; } = WAVE_PHASE.WARNING;
    public Vector2? LockedAimDirection { get; private set; }

    public bool BlocksFormation => Request.Pattern.IsFormation && ( Phase == WAVE_PHASE.WARNING || Phase == WAVE_PHASE.ACTIVE_FORMATION );

    public WaveState(WaveRequest request , WavePlan plan , float warningStartedAtSeconds)
    {
        Request = request;
        Plan = plan;
        WarningStartedAtSeconds = warningStartedAtSeconds;
    }

    public bool TryLockArrowAim(float survivalSeconds , Vector2 playerPosition)
    {
        if ( Request.Pattern is not ArrowDashPatternData arrow || Phase != WAVE_PHASE.WARNING
            || LockedAimDirection.HasValue || survivalSeconds < WarningStartedAtSeconds + arrow.GatherDurationSeconds )
        {
            return false;
        }

        Vector2 direction = playerPosition - Plan.Anchor;
        LockedAimDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Plan.InitialDirection;
        return true;
    }

    public bool TryActivate(float survivalSeconds)
    {
        if ( Phase != WAVE_PHASE.WARNING || survivalSeconds < WarningEndsAtSeconds )
        {
            return false;
        }

        WAVE_PATTERN kind = Request.Pattern.Kind;
        
        if ( kind == WAVE_PATTERN.ARROW_DASH && !LockedAimDirection.HasValue )
        {
            return false;
        }

        Phase = kind == WAVE_PATTERN.WALL || kind == WAVE_PATTERN.ARROW_DASH || kind == WAVE_PATTERN.CHASE_WALL
            ? WAVE_PHASE.ACTIVE_FORMATION : WAVE_PHASE.INDEPENDENT_ENEMIES;

        return true;
    }

    public void Finish()
    {
        Phase = WAVE_PHASE.FINISHED;
    }
}
