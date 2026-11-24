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
    private readonly float FORMATION_WARNING_BONUS_SECONDS;

    public AEnemyPatternData Pattern { get; }
    public DifficultyStage Stage { get; }
    public int ChaseCount { get; }

    public float WarningDurationSeconds
    {
        get
        {
            if (Pattern is ArrowDashPatternData arrow)
            {
                return arrow.WarningDurationSeconds + FORMATION_WARNING_BONUS_SECONDS;
            }
            if (Pattern is InterceptorPatternData interceptor)
            {
                return interceptor.WarningDurationSeconds;
            }
            // 추적+벽은 먼저 나오는 추적 적의 예고 시간을 초기 회피 검사에 사용합니다.
            return Stage.WarningDurationSeconds + (Pattern is ChaseWallPatternData ? 0f : FORMATION_WARNING_BONUS_SECONDS);
        }
    }

    public float GetWarningDuration(EnemyPlacement placement)
    {
        if (Pattern is ChaseWallPatternData && placement.Motion == ENEMY_MOTION.LINEAR)
        {
            return Stage.WarningDurationSeconds + FORMATION_WARNING_BONUS_SECONDS;
        }
        return WarningDurationSeconds;
    }

    public WaveRequest(AEnemyPatternData pattern , DifficultyStage stage , int chaseCount, float formationWarningBonusSeconds = 0f)
    {
        Pattern = pattern;
        Stage = stage;
        ChaseCount = chaseCount;
        FORMATION_WARNING_BONUS_SECONDS = pattern.IsFormation ? formationWarningBonusSeconds : 0f;
    }
}

public enum ENEMY_MOTION
{
    CHASE,
    LINEAR,
    INTERCEPTOR,
    REBOUND,
    ORBIT,
    TAIL_LEADER,
    TAIL_FOLLOWER,
    PATH_DASH
}

public readonly struct EnemyPlacement
{
    public Vector2 WarningPosition { get; }
    public Vector2 SpawnPosition { get; }
    public ENEMY_MOTION Motion { get; }
    public float WarningDelaySeconds { get; }
    public EnemyDashPath? DashPath { get; }
    public bool MarkDashTurn { get; }

    public EnemyPlacement(Vector2 warningPosition , Vector2 spawnPosition, ENEMY_MOTION motion = ENEMY_MOTION.CHASE,
        float warningDelaySeconds = 0f, EnemyDashPath? dashPath = null, bool markDashTurn = false)
    {
        WarningPosition = warningPosition;
        SpawnPosition = spawnPosition;
        Motion = motion;
        WarningDelaySeconds = warningDelaySeconds;
        DashPath = dashPath;
        MarkDashTurn = markDashTurn;
    }
}

public readonly struct ReboundPath
{
    public Vector2 Start { get; }
    public Vector2 Turn { get; }
    public Vector2 IncomingDirection { get; }
    public Vector2 OutgoingDirection { get; }
    public float TurnDistance { get; }
    public Vector2 PreviewEnd => Turn + OutgoingDirection * TurnDistance;

    public ReboundPath(Vector2 start, Vector2 turn)
    {
        Start = start;
        Turn = turn;
        Vector2 delta = turn - start;
        TurnDistance = delta.magnitude;
        IncomingDirection = delta.normalized;
        OutgoingDirection = new Vector2(-IncomingDirection.x, IncomingDirection.y);
    }

    public Vector2 GetPosition(float distance, float offsetY)
    {
        Vector2 center = distance < TurnDistance ? Start + IncomingDirection * distance
            : Turn + OutgoingDirection * (distance - TurnDistance);
        return center + Vector2.up * offsetY;
    }
}

public class WavePlan
{
    public IReadOnlyList<EnemyPlacement> Placements { get; }
    public Vector2 Anchor { get; }
    public Vector2 InitialDirection { get; }
    public ReboundPath? Rebound { get; }
    public EnemyOrbitPath? Orbit { get; }

    public int EnemyCount => Placements.Count;

    public WavePlan(EnemyPlacement[] placements , Vector2 anchor , Vector2 initialDirection, ReboundPath? rebound = null, EnemyOrbitPath? orbit = null)
    {
        if ( placements == null || placements.Length == 0 )
        {
            throw new ArgumentException("웨이브에 배치할 적이 필요합니다." , nameof(placements));
        }

        Placements = Array.AsReadOnly((EnemyPlacement[])placements.Clone());
        Anchor = anchor;
        InitialDirection = initialDirection;
        Rebound = rebound;
        Orbit = orbit;
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
    internal EnemyTailTrace TailTrace { get; private set; }

    public bool BlocksFormation => Request.Pattern.IsFormation && ( Phase == WAVE_PHASE.WARNING || Phase == WAVE_PHASE.ACTIVE_FORMATION );

    public WaveState(WaveRequest request , WavePlan plan , float warningStartedAtSeconds)
    {
        Request = request;
        Plan = plan;
        WarningStartedAtSeconds = warningStartedAtSeconds;
        if (request.Pattern is TailChasePatternData)
        {
            TailTrace = new EnemyTailTrace(plan.Placements);
        }
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

        Phase = Request.Pattern.IsFormation && kind != WAVE_PATTERN.ENCIRCLE
            ? WAVE_PHASE.ACTIVE_FORMATION : WAVE_PHASE.INDEPENDENT_ENEMIES;

        return true;
    }

    public void Finish()
    {
        TailTrace = null;
        Phase = WAVE_PHASE.FINISHED;
    }

    internal void ReleaseFormation()
    {
        TailTrace = null;
        if (Phase == WAVE_PHASE.ACTIVE_FORMATION)
        {
            Phase = WAVE_PHASE.INDEPENDENT_ENEMIES;
        }
    }
}
