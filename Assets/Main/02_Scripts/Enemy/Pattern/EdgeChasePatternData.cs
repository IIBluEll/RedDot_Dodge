using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Edge Chase")]
public class EdgeChasePatternData : AEnemyPatternData
{
    //해당 변에서 추적 적을 흩어 놓을 범위
    [SerializeField, Range(0.1f, 1f)] private float _edgeCoverage = 0.8f;

    // 테두리에서 경기장 안쪽으로 떨어진 거리. 기존 에셋의 거리 값도 안쪽 여백으로 사용한다.
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("_minOutSideDistance"), Min(0f)] private float _minimumSpawnInset = 0.35f;
    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("_maxOutSideDistance"), Min(0f)] private float _maximumSpawnInset = 0.75f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.EDGE_CHASE;

    public float EdgeCoverage => _edgeCoverage;
    public float MinimumSpawnInset => _minimumSpawnInset;
    public float MaximumSpawnInset => _maximumSpawnInset;
}
