using System.Collections.Generic;
using UnityEngine;

// 배치만 계산합니다. 예고와 적 오브젝트는 호스트가 일괄 등록합니다.
public static class EnemyWaveLayouts
{
    public static WavePlan Build(WaveRequest request, GameBalance balance, Vector2 limits, Vector2 playerPosition)
    {
        var placements = new List<EnemyPlacement>();
        Vector2 anchor = Vector2.zero;
        Vector2 direction = Vector2.right;

        if (request.Pattern is WallPatternData wall)
        {
            int side = Random.value < 0.5f ? -1 : 1;
            direction = Vector2.left * side;
            anchor = new Vector2(limits.x * side, 0f);
            float delay = 0f;
            if (wall is ChaseWallPatternData combined)
            {
                AddChasers(placements, request.ChaseCount, balance, limits, -side, true);
                delay = request.Stage.WarningDurationSeconds + combined.ChaseLeadSeconds;
            }
            float halfGap = wall.GapWidth * 0.5f + balance.EnemyRadius;
            if (halfGap >= limits.y || wall.MaxEnemySpacing <= 0f
                || Mathf.CeilToInt((limits.y * 2f - halfGap * 2f) / wall.MaxEnemySpacing) + placements.Count + 4 > balance.EnemyCapacity)
                return null;
            float gapCenter = Random.Range(-limits.y + halfGap, limits.y - halfGap);
            AddWallSegment(placements, anchor.x, -limits.y, gapCenter - halfGap, wall.MaxEnemySpacing, delay);
            AddWallSegment(placements, anchor.x, gapCenter + halfGap, limits.y, wall.MaxEnemySpacing, delay);
        }
        else if (request.Pattern is EncirclePatternData circle)
        {
            anchor = playerPosition;
            direction = playerPosition.sqrMagnitude > 0.01f ? -playerPosition.normalized : Random.insideUnitCircle.normalized;
            Vector2 exit = anchor + direction * (circle.Radius + balance.PlayerRadius);
            if (!Inside(exit, balance.ArenaSize * 0.5f - Vector2.one * balance.PlayerRadius))
                return null;
            float exitAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            int count = circle.EnemyCount;
            if (count < 2 || count > balance.EnemyCapacity || circle.MaxEnemySpacing > (balance.PlayerRadius + balance.EnemyRadius) * 2f)
                return null;
            for (int i = 0; i < count; i++)
            {
                float angle = (exitAngle + circle.OpeningAngleDegrees * 0.5f
                    + (360f - circle.OpeningAngleDegrees) * i / (count - 1)) * Mathf.Deg2Rad;
                Vector2 position = anchor + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * circle.Radius;
                placements.Add(new EnemyPlacement(position, position));
            }
        }
        else if (request.Pattern is ArrowDashPatternData arrow)
        {
            float radius = 0f;
            foreach (ArrowSlot slot in arrow.Slots)
                radius = Mathf.Max(radius, Mathf.Max(slot.ScatteredOffset.magnitude, slot.FormationOffset.magnitude));
            Vector2 centerLimits = limits - Vector2.one * radius;
            if (centerLimits.x <= 0f || centerLimits.y <= 0f)
                return null;
            float side = playerPosition.x >= 0f ? -1f : 1f;
            anchor = new Vector2(centerLimits.x * side, Random.Range(-centerLimits.y, centerLimits.y));
            direction = Vector2.left * side;
            foreach (ArrowSlot slot in arrow.Slots)
            {
                Vector2 position = anchor + Rotate(slot.ScatteredOffset, direction);
                placements.Add(new EnemyPlacement(position, position, ENEMY_MOTION.LINEAR));
            }
        }
        else if (request.Pattern is InterceptorPatternData)
        {
            Vector2 position = InterceptorStart(playerPosition, limits);
            placements.Add(new EnemyPlacement(position, position, ENEMY_MOTION.INTERCEPTOR));
        }
        else if (request.Pattern is EdgeChasePatternData)
        {
            AddChasers(placements, request.ChaseCount, balance, limits, Random.value < 0.5f ? -1 : 1, Random.value < 0.5f);
        }

        if (placements.Count == 0 || placements.Count > balance.EnemyCapacity)
            return null;
        foreach (EnemyPlacement placement in placements)
            if (!Inside(placement.SpawnPosition, limits))
                return null;
        return new WavePlan(placements.ToArray(), anchor, direction);
    }

    private static void AddChasers(List<EnemyPlacement> placements, int count, GameBalance balance, Vector2 limits, int side, bool vertical)
    {
        if (count <= 0)
            return;
        var pattern = (EdgeChasePatternData)balance.GetPattern(WAVE_PATTERN.EDGE_CHASE);
        Vector2 halfSize = balance.ArenaSize * 0.5f;
        float distance = vertical ? halfSize.x : halfSize.y;
        float padding = vertical ? halfSize.x - limits.x : halfSize.y - limits.y;
        float span = (vertical ? limits.y : limits.x) * 2f * Mathf.Clamp01(pattern.EdgeCoverage);
        float minimum = Mathf.Min(distance, Mathf.Max(padding, pattern.MinimumSpawnInset));
        float maximum = Mathf.Min(distance, Mathf.Max(minimum, pattern.MaximumSpawnInset));
        for (int i = 0; i < count; i++)
        {
            float offset = -span * 0.5f + (i + Random.Range(0.2f, 0.8f)) * span / count;
            float across = side * (distance - Random.Range(minimum, maximum));
            Vector2 position = vertical ? new Vector2(across, offset) : new Vector2(offset, across);
            placements.Add(new EnemyPlacement(position, position));
        }
    }

    private static void AddWallSegment(List<EnemyPlacement> placements, float x, float from, float to, float spacing, float delay)
    {
        if (to - from <= 0.0001f)
        {
            Vector2 position = new Vector2(x, from);
            placements.Add(new EnemyPlacement(position, position, ENEMY_MOTION.LINEAR, delay));
            return;
        }
        int intervals = Mathf.Max(1, Mathf.CeilToInt((to - from) / spacing));
        for (int i = 0; i <= intervals; i++)
        {
            Vector2 position = new Vector2(x, Mathf.Lerp(from, to, (float)i / intervals));
            placements.Add(new EnemyPlacement(position, position, ENEMY_MOTION.LINEAR, delay));
        }
    }

    public static Vector2 Rotate(Vector2 offset, Vector2 direction)
    {
        return direction * offset.x + new Vector2(-direction.y, direction.x) * offset.y;
    }

    public static bool Inside(Vector2 position, Vector2 limits)
    {
        return Mathf.Abs(position.x) <= limits.x && Mathf.Abs(position.y) <= limits.y;
    }

    public static Vector2 InterceptorStart(Vector2 target, Vector2 limits)
    {
        target = new Vector2(Mathf.Clamp(target.x, -limits.x, limits.x), Mathf.Clamp(target.y, -limits.y, limits.y));
        if (limits.x - Mathf.Abs(target.x) < limits.y - Mathf.Abs(target.y))
            target.x = (target.x >= 0f ? 1f : -1f) * limits.x;
        else
            target.y = (target.y >= 0f ? 1f : -1f) * limits.y;
        return target;
    }
}
