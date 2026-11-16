using UnityEngine;

public enum WAVE_PATTERN
{
    EDGE_CHASE = 0,
    WALL = 1,
    ENCIRCLE = 2,
    ARROW_DASH = 3
}

public abstract class AEnemyPatternData : ScriptableObject
{
    public abstract WAVE_PATTERN Kind { get; }

    public bool IsFormation => Kind != WAVE_PATTERN.EDGE_CHASE;
}
