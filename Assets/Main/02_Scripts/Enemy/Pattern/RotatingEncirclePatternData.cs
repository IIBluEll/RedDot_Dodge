using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Rotating Encircle")]
public sealed class RotatingEncirclePatternData : AEnemyPatternData
{
    [SerializeField, Min(0.01f)] private float _radius = 2.5f;
    [SerializeField, Range(90f, 180f)] private float _openingAngleDegrees = 120f;
    [SerializeField, Min(0.01f)] private float _maxEnemySpacing = 0.65f;
    [SerializeField, Min(0.01f)] private float _minimumRadius = 1.3f;
    [SerializeField, Min(0.01f)] private float _rotationDurationSeconds = 2.2f;
    [SerializeField, Range(1f, 90f)] private float _rotationDegrees = 34f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.ROTATING_ENCIRCLE;
    public float Radius => _radius;
    public float OpeningAngleDegrees => _openingAngleDegrees;
    public float MaxEnemySpacing => _maxEnemySpacing;
    public float MinimumRadius => _minimumRadius;
    public float RotationDurationSeconds => _rotationDurationSeconds;
    public float RotationDegrees => _rotationDegrees;
}
