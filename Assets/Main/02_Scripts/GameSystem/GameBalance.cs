using System;
using System.Collections.Generic;
using UnityEngine;
using static WAVE_PATTERN;

[Serializable]
public class PatternWeight
{
    [SerializeField] private AEnemyPatternData _pattern;

    [SerializeField, Min(1)] private int _weight = 1;

    public AEnemyPatternData Pattern => _pattern;
    public int Weight => _weight;
}

[Serializable]
public class DifficultyStage
{
    [SerializeField, Min(0f)] private float _startSeconds;
    [SerializeField, Min(0.01f)] private float _waveIntervalSeconds;
    [SerializeField, Min(1)] private int _minimumChaseCount;
    [SerializeField, Min(1)] private int _maximumChaseCount;
    [SerializeField, Min(0.01f)] private float _chaseSpeed;
    [SerializeField, Min(0.01f)] private float _warningDurationSeconds;
    [SerializeField] private WAVE_PATTERN[] _allowedPatterns;

    public float StartSeconds => _startSeconds;
    public float WaveIntervalSeconds => _waveIntervalSeconds;
    public int MinimumChaseCount => _minimumChaseCount;
    public int MaximumChaseCount => _maximumChaseCount;
    public float ChaseSpeed => _chaseSpeed;
    public float WarningDurationSeconds => _warningDurationSeconds;
    public IReadOnlyList<WAVE_PATTERN> AllowedPatterns => _allowedPatterns;

    public DifficultyStage(float startSeconds , float waveIntervalSeconds , int minimumChaseCount , int maximumChaseCount , float chaseSpeed , float warningDurationSeconds , params WAVE_PATTERN[] allowedPatterns)
    {
        _startSeconds = startSeconds;
        _waveIntervalSeconds = waveIntervalSeconds;
        _minimumChaseCount = minimumChaseCount;
        _maximumChaseCount = maximumChaseCount;
        _chaseSpeed = chaseSpeed;
        _warningDurationSeconds = warningDurationSeconds;
        _allowedPatterns = allowedPatterns;
    }

    public bool Allows(WAVE_PATTERN kind)
    {
        return Array.IndexOf(_allowedPatterns , kind) >= 0;
    }
}

[CreateAssetMenu(menuName = "RedDot Dodge/Game Balance")]
public sealed class GameBalance : ScriptableObject
{
    [SerializeField] private Vector2 _arenaSize = new Vector2(16f, 9f);
    [SerializeField, Min(0.01f)] private float _playerRadius = 0.25f;
    [SerializeField, Min(0.01f)] private float _enemyRadius = 0.12f;
    [SerializeField, Min(0.01f)] private float _enemyLifetimeSeconds = 45f;
    [SerializeField, Min(1)] private int _enemyCapacity = 96;
    [SerializeField, Min(0f)] private float _firstWaveDelaySeconds = 2f;

    [SerializeField] private PatternWeight[] _patterns = Array.Empty<PatternWeight>();

    [SerializeField]
    private DifficultyStage[] _stages =
    {
        new DifficultyStage(0f, 6f, 3, 4, 1.2f, 1.5f, EDGE_CHASE),
        new DifficultyStage(15f, 6f, 3, 4, 1.2f, 1.5f, EDGE_CHASE, WALL),
        new DifficultyStage(30f, 5f, 4, 6, 1.45f, 1.4f, EDGE_CHASE, WALL, ENCIRCLE),
        new DifficultyStage(60f, 5f, 6, 8, 1.45f, 1.4f, EDGE_CHASE, WALL, ENCIRCLE, ARROW_DASH),
        new DifficultyStage(120f, 5f, 8, 10, 1.45f, 1.4f, EDGE_CHASE, WALL, ENCIRCLE, ARROW_DASH),
        new DifficultyStage(180f, 5f, 8, 10, 1.45f, 1.4f, EDGE_CHASE, WALL, ENCIRCLE, ARROW_DASH)
    };

    public Vector2 ArenaSize => _arenaSize;
    public float PlayerRadius => _playerRadius;
    public float EnemyRadius => _enemyRadius;
    public float EnemyLifetimeSeconds => _enemyLifetimeSeconds;
    public int EnemyCapacity => _enemyCapacity;
    public float FirstWaveDelaySeconds => _firstWaveDelaySeconds;
    public IReadOnlyList<PatternWeight> Patterns => _patterns;

    public DifficultyStage GetStage(float survivalSeconds)
    {
        for ( int i = _stages.Length - 1; i >= 0; i-- )
        {
            if ( survivalSeconds >= _stages[ i ].StartSeconds )
            {
                return _stages[ i ];
            }
        }

        return _stages[ 0 ];
    }

    public AEnemyPatternData GetPattern(WAVE_PATTERN kind)
    {
        foreach ( PatternWeight entry in _patterns )
        {
            if ( entry.Pattern.Kind == kind )
            {
                return entry.Pattern;
            }
        }

        throw new InvalidOperationException($"패턴 에셋을 찾을 수 없습니다. {kind}");
    }

    public void Validate()
    {
        if ( _arenaSize.x <= 0f || _arenaSize.y <= 0f )
        {
            throw new InvalidOperationException("경기장 크기를 확인하세요.");
        }

        var kinds = new HashSet<WAVE_PATTERN>();

        foreach ( PatternWeight entry in _patterns )
        {
            if ( entry == null || entry.Pattern == null || !kinds.Add(entry.Pattern.Kind) )
            {
                throw new InvalidOperationException("패턴 에셋의 누락과 중복을 확인하세요.");
            }
        }

        if ( _stages.Length == 0 || _stages[ 0 ] == null || _stages[ 0 ].StartSeconds != 0f )
        {
            throw new InvalidOperationException("0초부터 시작하는 난이도 단계가 필요합니다.");
        }

        float previousStartSeconds = -1f;

        foreach ( DifficultyStage stage in _stages )
        {
            if ( stage == null || stage.StartSeconds <= previousStartSeconds || stage.MaximumChaseCount < stage.MinimumChaseCount
                || stage.MaximumChaseCount > _enemyCapacity )
            {
                throw new InvalidOperationException("난이도 단계의 시작 시각과 추적 적 수 범위를 확인하세요.");
            }

            if ( stage.AllowedPatterns == null || !stage.Allows(EDGE_CHASE) )
            {
                throw new InvalidOperationException("모든 난이도 단계에 외곽 추적을 포함하세요.");
            }

            foreach ( WAVE_PATTERN kind in stage.AllowedPatterns )
            {
                if ( !kinds.Contains(kind) )
                {
                    throw new InvalidOperationException($"난이도 단계에 연결되지 않은 패턴이 있습니다. {kind}");
                }
            }

            previousStartSeconds = stage.StartSeconds;
        }
    }
}