using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Wall")]
public class WallPatternData : AEnemyPatternData
{
    //통로 양쪽 적 콜라이더의 안쪽 경계 사이 거리
    [SerializeField, Min(0.01f)] private float _gapWidth = 1.5f;
    [SerializeField, Min(0.01f)] private float _maxEnemySpacing = 0.68f;
    [SerializeField, Min(0.01f)] private float _speedMultiplier = 1.2f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.WALL;

    public float GapWidth => _gapWidth;
    public float MaxEnemySpacing => _maxEnemySpacing;
    public float SpeedMultiplier => _speedMultiplier;
}
