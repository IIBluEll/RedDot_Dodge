using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Chase Wall")]
public sealed class ChaseWallPatternData : WallPatternData
{
    [SerializeField, Min(0f)] private float _chaseLeadSeconds = 2f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.CHASE_WALL;
    public float ChaseLeadSeconds => _chaseLeadSeconds;
}
