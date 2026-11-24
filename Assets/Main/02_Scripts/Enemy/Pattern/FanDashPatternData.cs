using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Fan Dash")]
public sealed class FanDashPatternData : AEnemyPatternData
{
    [SerializeField, Min(0.01f)] private float _spacing = 0.52f;
    [SerializeField, Min(0.01f)] private float _dashSpeed = 5.8f;
    [SerializeField, Range(5f, 60f)] private float _spreadAngleDegrees = 32f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.FAN_DASH;
    public float Spacing => _spacing;
    public float DashSpeed => _dashSpeed;
    public float SpreadAngleDegrees => _spreadAngleDegrees;
}
