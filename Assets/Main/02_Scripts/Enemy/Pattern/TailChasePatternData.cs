using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Tail Chase")]
public sealed class TailChasePatternData : AEnemyPatternData
{
    [SerializeField, Min(2)] private int _enemyCount = 5;
    [SerializeField, Min(0.01f)] private float _spacing = 0.6f;
    [SerializeField, Min(0.01f)] private float _speedMultiplier = 1.3f;
    [SerializeField, Min(0.01f)] private float _formationDurationSeconds = 7f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.TAIL_CHASE;
    public int EnemyCount => _enemyCount;
    public float Spacing => _spacing;
    public float SpeedMultiplier => _speedMultiplier;
    public float FormationDurationSeconds => _formationDurationSeconds;
}
