using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Edge Chase")]
public class EdgeChasePatternData : AEnemyPatternData
{
    //해당 변에서 추적 적을 흩어 놓을 범위
    [SerializeField, Range(0.1f, 1f)] private float _edgeCoverage = 0.8f;

    [SerializeField, Min(0f)] private float _minOutSideDistance = 0.2f;
    [SerializeField, Min(0f)] private float _maxOutSideDistance = 0.75f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.EDGE_CHASE;

    public float EdgeCoverage => _edgeCoverage;
    public float MinOutSideDistance => _minOutSideDistance;
    public float MaxOutSideDistance => _maxOutSideDistance;
}
