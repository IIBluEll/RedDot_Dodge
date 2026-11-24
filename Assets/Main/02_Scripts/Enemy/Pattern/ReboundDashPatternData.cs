using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Rebound Dash")]
public sealed class ReboundDashPatternData : AEnemyPatternData
{
    [SerializeField, Min(1)] private int _enemyCount = 3;
    [SerializeField, Min(0.01f)] private float _enemySpacing = 0.48f;
    [SerializeField, Min(0.01f)] private float _dashSpeed = 7.5f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.REBOUND_DASH;
    public int EnemyCount => _enemyCount;
    public float EnemySpacing => _enemySpacing;
    public float DashSpeed => _dashSpeed;
}
