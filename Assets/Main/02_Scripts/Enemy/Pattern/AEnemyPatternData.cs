using UnityEngine;

public enum WAVE_PATTERN
{
    EDGE_CHASE = 0,
    WALL = 1,
    ENCIRCLE = 2,
    ARROW_DASH = 3,
    CHASE_WALL = 4,
    INTERCEPTOR = 5,
    REBOUND_DASH = 6,
    ROTATING_ENCIRCLE = 7,
    TAIL_CHASE = 8,
    FAN_DASH = 9,
    STAGGERED_CROSS = 10
}

public abstract class AEnemyPatternData : ScriptableObject
{
    public abstract WAVE_PATTERN Kind { get; }

    public bool IsFormation => Kind == WAVE_PATTERN.WALL || Kind == WAVE_PATTERN.ENCIRCLE
        || Kind == WAVE_PATTERN.ARROW_DASH || Kind == WAVE_PATTERN.CHASE_WALL || Kind == WAVE_PATTERN.REBOUND_DASH
        || Kind == WAVE_PATTERN.ROTATING_ENCIRCLE || Kind == WAVE_PATTERN.TAIL_CHASE
        || Kind == WAVE_PATTERN.FAN_DASH || Kind == WAVE_PATTERN.STAGGERED_CROSS;
}
