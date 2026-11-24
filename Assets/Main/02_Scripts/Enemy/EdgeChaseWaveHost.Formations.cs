using System.Collections.Generic;
using UnityEngine;

public sealed partial class EdgeChaseWaveHost
{
    private static readonly Color OPENING_COLOR = new Color32(104, 228, 255, 255);
    private static readonly Color DASH_WARNING_COLOR = new Color32(255, 166, 122, 255);
    private static readonly Color TAIL_LEADER_COLOR = new Color32(255, 210, 155, 255);
    private const float TAIL_CATCH_UP_MULTIPLIER = 1.25f;

    private readonly List<SpriteRenderer> FORMATION_MARKERS = new();

    private static bool IsPathDash(ENEMY_MOTION motion) => motion == ENEMY_MOTION.REBOUND || motion == ENEMY_MOTION.PATH_DASH;

    private static bool IsTailMotion(ENEMY_MOTION motion) => motion == ENEMY_MOTION.TAIL_LEADER || motion == ENEMY_MOTION.TAIL_FOLLOWER;

    private static bool IsFormationMotion(ENEMY_MOTION motion)
    {
        return motion == ENEMY_MOTION.LINEAR || motion == ENEMY_MOTION.ORBIT || IsPathDash(motion) || IsTailMotion(motion);
    }

    private static float GetEnemySpeed(WaveRequest request, ENEMY_MOTION motion)
    {
        float chaseSpeed = request.Stage.ChaseSpeed;
        return request.Pattern switch
        {
            ReboundDashPatternData rebound => rebound.DashSpeed,
            ArrowDashPatternData arrow => arrow.DashSpeed,
            InterceptorPatternData interceptor => interceptor.DashSpeed,
            FanDashPatternData fan => fan.DashSpeed,
            StaggeredCrossPatternData cross => cross.DashSpeed,
            TailChasePatternData tail => chaseSpeed * tail.SpeedMultiplier,
            WallPatternData wall when motion == ENEMY_MOTION.LINEAR => chaseSpeed * wall.SpeedMultiplier,
            _ => chaseSpeed
        };
    }

    private static EnemyDashPath GetDashPath(WavePlan plan, EnemyPlacement placement)
    {
        if (placement.DashPath is EnemyDashPath path)
        {
            return path;
        }
        ReboundPath rebound = plan.Rebound.Value;
        Vector2 offset = placement.SpawnPosition - rebound.Start;
        return new EnemyDashPath(placement.SpawnPosition, rebound.Turn + offset, rebound.PreviewEnd + offset);
    }

    private static void InitializeFormationEnemy(ref ActiveEnemy enemy, EnemyPlacement placement, int index)
    {
        if (IsPathDash(enemy.Motion))
        {
            enemy.DashPath = GetDashPath(enemy.Wave.Plan, placement);
        }
        else if (enemy.Motion == ENEMY_MOTION.ORBIT)
        {
            enemy.OrbitOffset = placement.SpawnPosition - enemy.Wave.Plan.Anchor;
        }
        else if (enemy.Motion == ENEMY_MOTION.TAIL_FOLLOWER)
        {
            float spacing = ((TailChasePatternData)enemy.Wave.Request.Pattern).Spacing;
            enemy.PathDistance = index * spacing;
            enemy.TailGap = (enemy.Wave.Plan.EnemyCount - 1 - index) * spacing;
        }
    }

    private static bool IsOutsideFormationPath(Vector2 escape, float radius, WavePlan plan)
    {
        if (plan.Orbit is EnemyOrbitPath orbit)
        {
            float distance = orbit.Radius + radius;
            return (escape - orbit.Center).sqrMagnitude > distance * distance;
        }

        if (!plan.Rebound.HasValue && !plan.Placements[0].DashPath.HasValue)
        {
            return true;
        }
        float radiusSquared = radius * radius;
        foreach (EnemyPlacement placement in plan.Placements)
        {
            if (GetDashPath(plan, placement).Touches(escape, radiusSquared))
            {
                return false;
            }
        }
        return true;
    }

    private static Vector2 PredictEnemyPosition(ActiveEnemy enemy, Vector2 position, Vector2 playerPosition, float seconds)
    {
        if (IsPathDash(enemy.Motion))
        {
            return enemy.DashPath.GetPosition(enemy.PathDistance + enemy.Speed * seconds);
        }
        if (enemy.Motion == ENEMY_MOTION.ORBIT)
        {
            EnemyOrbitPath orbit = enemy.Wave.Plan.Orbit.Value;
            float elapsed = enemy.OrbitElapsedSeconds + seconds;
            Vector2 orbitPosition = orbit.GetPosition(enemy.OrbitOffset, elapsed);
            return Vector2.MoveTowards(orbitPosition, playerPosition, enemy.Speed * Mathf.Max(0f, elapsed - orbit.DurationSeconds));
        }
        if (enemy.Motion == ENEMY_MOTION.LINEAR)
        {
            return position + enemy.Direction * (enemy.Speed * seconds);
        }
        Vector2 target = enemy.Motion == ENEMY_MOTION.INTERCEPTOR ? enemy.Target : playerPosition;
        return Vector2.MoveTowards(position, target, enemy.Speed * seconds);
    }

    private static void TickPathDash(ref ActiveEnemy enemy, float deltaTime)
    {
        float travel = enemy.Speed * deltaTime;
        float nextDistance = enemy.PathDistance + travel;
        float turnDistance = enemy.DashPath.TurnDistance;
        if (enemy.PathDistance < turnDistance && nextDistance > turnDistance)
        {
            enemy.PathTurnFraction = (turnDistance - enemy.PathDistance) / travel;
        }
        enemy.PathDistance = nextDistance;
        enemy.Renderer.transform.position = enemy.DashPath.GetPosition(nextDistance);
    }

    private void TickOrbit(ref ActiveEnemy enemy, float deltaTime)
    {
        EnemyOrbitPath orbit = enemy.Wave.Plan.Orbit.Value;
        enemy.OrbitElapsedSeconds += deltaTime;
        enemy.Renderer.transform.position = orbit.GetPosition(enemy.OrbitOffset, enemy.OrbitElapsedSeconds);
        if (enemy.OrbitElapsedSeconds >= orbit.DurationSeconds)
        {
            enemy.Motion = ENEMY_MOTION.CHASE;
            ReleaseOrbitIfFinished(enemy.Wave, enemy.Id);
        }
    }

    private void ReleaseOrbitIfFinished(WaveState wave, int enemyId)
    {
        if (!ACTIVE_ENEMIES.Exists(other => other.Id != enemyId && other.Wave == wave && other.Motion == ENEMY_MOTION.ORBIT))
        {
            wave.ReleaseFormation();
        }
    }

    private static void TickTail(ref ActiveEnemy enemy, Vector2 playerPosition, float deltaTime)
    {
        EnemyTailTrace trace = enemy.Wave.TailTrace;
        if (enemy.Motion == ENEMY_MOTION.TAIL_LEADER)
        {
            Vector2 position = Vector2.MoveTowards(enemy.PreviousPosition, playerPosition, enemy.Speed * deltaTime);
            enemy.Renderer.transform.position = position;
            trace.Record(position);
            return;
        }

        float targetDistance = Mathf.Max(enemy.PathDistance, trace.Length - enemy.TailGap);
        enemy.PathDistance = Mathf.MoveTowards(enemy.PathDistance, targetDistance, enemy.Speed * TAIL_CATCH_UP_MULTIPLIER * deltaTime);
        enemy.Renderer.transform.position = trace.GetPosition(enemy.PathDistance);
    }

    private void ReleaseTailFormation(WaveState wave)
    {
        for (int i = 0; i < ACTIVE_ENEMIES.Count; i++)
        {
            ActiveEnemy enemy = ACTIVE_ENEMIES[i];
            if (enemy.Wave != wave || !IsTailMotion(enemy.Motion))
            {
                continue;
            }
            enemy.Motion = ENEMY_MOTION.CHASE;
            enemy.Speed = wave.Request.Stage.ChaseSpeed;
            ACTIVE_ENEMIES[i] = enemy;
        }
        wave.ReleaseFormation();
    }

    private static Color GetWarningColor(EnemyPlacement placement)
    {
        return placement.Motion switch
        {
            ENEMY_MOTION.REBOUND => REBOUND_COLOR,
            ENEMY_MOTION.INTERCEPTOR => INTERCEPT_COLOR,
            ENEMY_MOTION.TAIL_LEADER => TAIL_LEADER_COLOR,
            ENEMY_MOTION.PATH_DASH => DASH_WARNING_COLOR,
            _ => Color.white
        };
    }

    private void ShowFormationWarning(WavePlan plan)
    {
        if (plan.Rebound is ReboundPath rebound)
        {
            ShowFormationMarker(rebound.Turn, REBOUND_COLOR, Mathf.Min(0.42f, _gameBalance.EnemyRadius * 2f));
        }
        if (plan.Orbit is EnemyOrbitPath orbit)
        {
            Vector2 opening = plan.InitialDirection * orbit.Radius;
            Vector2 target = orbit.GetPosition(opening, orbit.DurationSeconds);
            ShowFormationMarker(target, OPENING_COLOR);
        }

        foreach (EnemyPlacement placement in plan.Placements)
        {
            if (placement.MarkDashTurn && placement.DashPath is EnemyDashPath path)
            {
                ShowFormationMarker(path.Turn, DASH_WARNING_COLOR);
            }
        }
    }

    private void ShowFormationMarker(Vector2 position, Color color, float width = 0.28f)
    {
        SpriteRenderer marker = RentRenderer();
        FORMATION_MARKERS.Add(marker);
        ShowSprite(marker, _warningSprite, position, width);
        marker.color = color;
    }

    private void ClearFormationWarning()
    {
        foreach (SpriteRenderer marker in FORMATION_MARKERS)
        {
            ReturnRenderer(marker);
        }
        FORMATION_MARKERS.Clear();
    }
}
