using System.Collections.Generic;
using UnityEngine;

public readonly struct EnemyDashPath
{
    private readonly Vector2 INCOMING_DIRECTION;
    private readonly Vector2 OUTGOING_DIRECTION;

    public Vector2 Start { get; }
    public Vector2 Turn { get; }
    public Vector2 End { get; }
    public float TurnDistance { get; }

    public EnemyDashPath(Vector2 start, Vector2 turn, Vector2 end)
    {
        Start = start;
        Turn = turn;
        End = end;
        Vector2 incoming = turn - start;
        TurnDistance = incoming.magnitude;
        INCOMING_DIRECTION = incoming.normalized;
        OUTGOING_DIRECTION = (end - turn).normalized;
    }

    public Vector2 GetPosition(float distance)
    {
        return distance < TurnDistance ? Start + INCOMING_DIRECTION * distance
            : Turn + OUTGOING_DIRECTION * (distance - TurnDistance);
    }

    public bool Touches(Vector2 position, float radiusSquared)
    {
        return ItemGeometry.DistanceToSegmentSquared(position, Start, Turn) <= radiusSquared
            || ItemGeometry.DistanceToSegmentSquared(position, Turn, End) <= radiusSquared;
    }
}

public readonly struct EnemyOrbitPath
{
    private readonly float END_RADIUS_RATIO;

    public Vector2 Center { get; }
    public float Radius { get; }
    public float DurationSeconds { get; }
    public float RotationDegrees { get; }

    public EnemyOrbitPath(Vector2 center, float radius, float minimumRadius, float durationSeconds, float rotationDegrees)
    {
        Center = center;
        Radius = radius;
        DurationSeconds = durationSeconds;
        RotationDegrees = rotationDegrees;
        END_RADIUS_RATIO = minimumRadius / radius;
    }

    public Vector2 GetPosition(Vector2 initialOffset, float elapsedSeconds)
    {
        float progress = Mathf.Clamp01(elapsedSeconds / DurationSeconds);
        Vector2 direction = EnemyWaveLayouts.DirectionFromDegrees(RotationDegrees * progress);
        return Center + EnemyWaveLayouts.Rotate(initialOffset, direction) * Mathf.Lerp(1f, END_RADIUS_RATIO, progress);
    }
}

// The leader records at most one point per physics tick during the timed formation.
internal sealed class EnemyTailTrace
{
    private readonly List<(Vector2 Position, float Distance)> POINTS = new();

    public float Length => POINTS[POINTS.Count - 1].Distance;

    public EnemyTailTrace(IReadOnlyList<EnemyPlacement> placements)
    {
        POINTS.Add((placements[0].SpawnPosition, 0f));
        for (int i = 1; i < placements.Count; i++)
        {
            Record(placements[i].SpawnPosition);
        }
    }

    public void Record(Vector2 position)
    {
        var previous = POINTS[POINTS.Count - 1];
        float distance = Vector2.Distance(previous.Position, position);
        if (distance > 0f)
        {
            POINTS.Add((position, previous.Distance + distance));
        }
    }

    public Vector2 GetPosition(float distance)
    {
        int left = 0;
        int right = POINTS.Count - 1;
        while (right - left > 1)
        {
            int middle = (left + right) / 2;
            if (POINTS[middle].Distance < distance)
            {
                left = middle;
            }
            else
            {
                right = middle;
            }
        }
        var start = POINTS[left];
        var end = POINTS[right];
        return Vector2.Lerp(start.Position, end.Position, Mathf.InverseLerp(start.Distance, end.Distance, distance));
    }
}
