using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Encircle")]
public class EncirclePatternData : AEnemyPatternData
{
    [SerializeField, Min(0.01f)] private float _maxEnemySpacing = 0.68f;
    [SerializeField, Min(0.01f)] private float _radius = 2.5f;
    [SerializeField, Range(1f, 359f)] private float _openingAngleDegrees = 90f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.ENCIRCLE;

    public int EnemyCount => Mathf.CeilToInt(_radius * (360f - _openingAngleDegrees) * Mathf.Deg2Rad / _maxEnemySpacing) + 1;
    public float MaxEnemySpacing => _maxEnemySpacing;
    public float Radius => _radius;
    public float OpeningAngleDegrees => _openingAngleDegrees;
}
