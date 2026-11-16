using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Encircle")]
public class EncirclePatternData : AEnemyPatternData
{
    [SerializeField, Min(2)] private int _enemyCount = 8;
    [SerializeField, Min(0.01f)] private float _radius = 2.5f;
    [SerializeField, Range(1f, 359f)] private float _openingAngleDegrees = 90f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.ENCIRCLE;

    public int EnemyCount => _enemyCount;
    public float Radius => _radius;
    public float OpeningAngleDegrees => _openingAngleDegrees;
}
