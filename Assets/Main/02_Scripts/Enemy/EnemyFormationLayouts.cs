using UnityEngine;

public static partial class EnemyWaveLayouts
{
    private static WavePlan BuildRotatingEncircle(RotatingEncirclePatternData pattern, GameBalance balance, Vector2 limits, Vector2 playerPosition)
    {
        float radius = pattern.Radius;
        float contactRadius = balance.PlayerRadius + balance.EnemyRadius;
        Vector2 centerLimits = limits - Vector2.one * radius;
        float halfOpeningRadians = pattern.OpeningAngleDegrees * 0.5f * Mathf.Deg2Rad;
        if (centerLimits.x < 0f || centerLimits.y < 0f || pattern.MinimumRadius <= 0f || pattern.MinimumRadius > radius
            || pattern.MinimumRadius * Mathf.Sin(halfOpeningRadians) <= contactRadius || pattern.RotationDurationSeconds <= 0f
            || pattern.MaxEnemySpacing <= 0f || pattern.MaxEnemySpacing > contactRadius * 2f)
        {
            return null;
        }

        Vector2 center = new Vector2(Mathf.Clamp(playerPosition.x, -centerLimits.x, centerLimits.x),
            Mathf.Clamp(playerPosition.y, -centerLimits.y, centerLimits.y));
        if (Vector2.Distance(center, playerPosition) + contactRadius >= radius)
        {
            return null;
        }

        float openingAngle = center.sqrMagnitude > 0.01f
            ? Mathf.Atan2(-center.y, -center.x) * Mathf.Rad2Deg : Random.Range(0f, 360f);
        float arc = 360f - pattern.OpeningAngleDegrees;
        int count = Mathf.CeilToInt(radius * arc * Mathf.Deg2Rad / pattern.MaxEnemySpacing) + 1;
        if (count < 2 || count > balance.EnemyCapacity)
        {
            return null;
        }

        float angle = openingAngle + pattern.OpeningAngleDegrees * 0.5f;
        var placements = new EnemyPlacement[count];
        for (int i = 0; i < count; i++)
        {
            Vector2 position = center + DirectionFromDegrees(angle + arc * i / (count - 1)) * radius;
            placements[i] = new EnemyPlacement(position, position, ENEMY_MOTION.ORBIT);
        }
        float rotation = pattern.RotationDegrees * (Random.value < 0.5f ? -1f : 1f);
        var orbit = new EnemyOrbitPath(center, radius, pattern.MinimumRadius, pattern.RotationDurationSeconds, rotation);
        return new WavePlan(placements, center, DirectionFromDegrees(openingAngle), orbit: orbit);
    }

    private static WavePlan BuildTailChase(TailChasePatternData pattern, GameBalance balance, Vector2 limits, Vector2 playerPosition)
    {
        int count = pattern.EnemyCount;
        float length = (count - 1) * pattern.Spacing;
        if (count < 2 || count > balance.EnemyCapacity || pattern.Spacing <= 0f || length >= limits.x
            || pattern.SpeedMultiplier <= 0f || pattern.FormationDurationSeconds <= 0f)
        {
            return null;
        }

        float side = Mathf.Abs(playerPosition.x) > 0.1f ? -Mathf.Sign(playerPosition.x) : (Random.value < 0.5f ? -1f : 1f);
        Vector2 start = new Vector2(limits.x * side, Random.Range(-limits.y * 0.5f, limits.y * 0.5f));
        Vector2 direction = Vector2.left * side;
        var placements = new EnemyPlacement[count];
        // Store the leader last so the host updates its trace before moving the followers.
        for (int i = 0; i < count; i++)
        {
            Vector2 position = start + direction * (pattern.Spacing * i);
            ENEMY_MOTION motion = i == count - 1 ? ENEMY_MOTION.TAIL_LEADER : ENEMY_MOTION.TAIL_FOLLOWER;
            placements[i] = new EnemyPlacement(position, position, motion);
        }
        return new WavePlan(placements, start, direction);
    }

    private static WavePlan BuildFanDash(FanDashPatternData pattern, GameBalance balance, Vector2 limits)
    {
        const int ENEMY_COUNT = 5;
        float halfHeight = pattern.Spacing * 2f;
        if (pattern.Spacing <= 0f || pattern.DashSpeed <= 0f || halfHeight >= limits.y || balance.EnemyCapacity < ENEMY_COUNT)
        {
            return null;
        }

        float side = Random.value < 0.5f ? -1f : 1f;
        float centerY = Random.Range(-0.3f, 0.3f) * (limits.y - halfHeight);
        Vector2 start = new Vector2(limits.x * side, centerY);
        Vector2 direction = Vector2.left * side;
        var placements = new EnemyPlacement[ENEMY_COUNT];
        for (int i = 0; i < ENEMY_COUNT; i++)
        {
            int lane = i - 2;
            Vector2 from = start + Vector2.up * (lane * pattern.Spacing);
            Vector2 turn = new Vector2(limits.x * side * 0.18f, from.y);
            Vector2 outgoing = Rotate(direction, DirectionFromDegrees(-side * System.Math.Sign(lane) * pattern.SpreadAngleDegrees));
            Vector2 end = GetPathEnd(turn, outgoing, limits);
            var path = new EnemyDashPath(from, turn, end);
            placements[i] = new EnemyPlacement(from, from, ENEMY_MOTION.PATH_DASH, dashPath: path, markDashTurn: lane == 0);
        }
        return new WavePlan(placements, start, direction);
    }

    private static WavePlan BuildStaggeredCross(StaggeredCrossPatternData pattern, GameBalance balance, Vector2 limits)
    {
        if (pattern.Spacing <= 0f || pattern.DashSpeed <= 0f || pattern.SecondGroupDelaySeconds <= 0f
            || pattern.Spacing >= Mathf.Min(limits.x, limits.y) || balance.EnemyCapacity < 6)
        {
            return null;
        }

        float horizontalSide = Random.value < 0.5f ? -1f : 1f;
        float verticalSide = Random.value < 0.5f ? -1f : 1f;
        float horizontalY = Random.Range(-0.4f, 0.4f) * (limits.y - pattern.Spacing);
        float verticalX = Random.Range(-0.4f, 0.4f) * (limits.x - pattern.Spacing);
        Vector2 horizontalStart = new Vector2(limits.x * horizontalSide, horizontalY);
        Vector2 verticalStart = new Vector2(verticalX, limits.y * verticalSide);
        var placements = new EnemyPlacement[6];
        for (int i = 0; i < 3; i++)
        {
            float offset = (i - 1) * pattern.Spacing;
            Vector2 from = horizontalStart + Vector2.up * offset;
            var horizontal = new EnemyDashPath(from, from, new Vector2(-from.x, from.y));
            placements[i] = new EnemyPlacement(from, from, ENEMY_MOTION.PATH_DASH, dashPath: horizontal);

            from = verticalStart + Vector2.right * offset;
            var vertical = new EnemyDashPath(from, from, new Vector2(from.x, -from.y));
            placements[i + 3] = new EnemyPlacement(from, from, ENEMY_MOTION.PATH_DASH, pattern.SecondGroupDelaySeconds, vertical);
        }
        return new WavePlan(placements, horizontalStart, Vector2.left * horizontalSide);
    }

    private static Vector2 GetPathEnd(Vector2 start, Vector2 direction, Vector2 limits)
    {
        float horizontalDistance = Mathf.Abs(direction.x) > 0.0001f
            ? (Mathf.Sign(direction.x) * limits.x - start.x) / direction.x : float.PositiveInfinity;
        float verticalDistance = Mathf.Abs(direction.y) > 0.0001f
            ? (Mathf.Sign(direction.y) * limits.y - start.y) / direction.y : float.PositiveInfinity;
        return start + direction * Mathf.Min(horizontalDistance, verticalDistance);
    }

    public static Vector2 DirectionFromDegrees(float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }
}
