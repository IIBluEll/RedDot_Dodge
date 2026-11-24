using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Staggered Cross")]
public sealed class StaggeredCrossPatternData : AEnemyPatternData
{
    [SerializeField, Min(0.01f)] private float _spacing = 0.52f;
    [SerializeField, Min(0.01f)] private float _dashSpeed = 5.5f;
    [SerializeField, Min(0.01f)] private float _secondGroupDelaySeconds = 0.75f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.STAGGERED_CROSS;
    public float Spacing => _spacing;
    public float DashSpeed => _dashSpeed;
    public float SecondGroupDelaySeconds => _secondGroupDelaySeconds;
}
