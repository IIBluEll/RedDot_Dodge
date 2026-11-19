using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Interceptor")]
public sealed class InterceptorPatternData : AEnemyPatternData
{
    [SerializeField, Min(1f)] private float _spawnIntervalSeconds = 60f;
    [SerializeField, Min(0f)] private float _lookaheadSeconds = 0.5f;
    [SerializeField, Min(0.01f)] private float _warningDurationSeconds = 0.45f;
    [SerializeField, Min(0.01f)] private float _dashSpeed = 7f;
    [SerializeField, Min(0.01f)] private float _cooldownSeconds = 1.25f;

    public override WAVE_PATTERN Kind => WAVE_PATTERN.INTERCEPTOR;
    public float SpawnIntervalSeconds => _spawnIntervalSeconds;
    public float LookaheadSeconds => _lookaheadSeconds;
    public float WarningDurationSeconds => _warningDurationSeconds;
    public float DashSpeed => _dashSpeed;
    public float CooldownSeconds => _cooldownSeconds;
}
