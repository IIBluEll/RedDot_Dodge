using UnityEngine;

public enum WAVE_PATTERN
{
    EDGE_CHASE = 0,
    WALL = 1,
    ENCIRCLE = 2,
    ARROW_DASH = 3,
    CHASE_WALL = 4,
    INTERCEPTOR = 5
}

public abstract class AEnemyPatternData : ScriptableObject
{
    public abstract WAVE_PATTERN Kind { get; }

    public bool IsFormation => Kind == WAVE_PATTERN.WALL || Kind == WAVE_PATTERN.ENCIRCLE
        || Kind == WAVE_PATTERN.ARROW_DASH || Kind == WAVE_PATTERN.CHASE_WALL;
}
