using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
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
    [SerializeField, Min(0.01f)] private float _enemyRadius = 0.15f;
    [SerializeField, Min(1)] private int _enemyCapacity = 96;
    [SerializeField, Min(0f)] private float _firstWaveDelaySeconds = 2f;

    [SerializeField] private PatternWeight[] _patterns = Array.Empty<PatternWeight>();
    [SerializeField] private InterceptorPatternData _interceptorPattern;

    [SerializeField]
    private DifficultyStage[] _stages =
    {
        new DifficultyStage(0f, 6f, 3, 4, 1.2f, 1.5f, EDGE_CHASE),
        new DifficultyStage(15f, 6f, 3, 4, 1.2f, 1.5f, EDGE_CHASE, WALL),
        new DifficultyStage(30f, 5f, 4, 6, 1.45f, 1.4f, EDGE_CHASE, WALL, ENCIRCLE, CHASE_WALL),
        new DifficultyStage(60f, 5f, 6, 8, 1.45f, 1.4f, EDGE_CHASE, WALL, ENCIRCLE, ARROW_DASH, CHASE_WALL),
        new DifficultyStage(120f, 5f, 8, 10, 1.45f, 1.4f, EDGE_CHASE, WALL, ENCIRCLE, ARROW_DASH, CHASE_WALL),
        new DifficultyStage(180f, 5f, 8, 10, 1.45f, 1.4f, EDGE_CHASE, WALL, ENCIRCLE, ARROW_DASH, CHASE_WALL)
    };

    [Header("Item Supply")]
    [SerializeField] private Vector2 _firstItemDelaySeconds = new Vector2(4f, 6f);
    [SerializeField] private Vector2 _itemSpawnIntervalSeconds = new Vector2(3f, 5f);
    [SerializeField, Min(0.01f)] private float _itemRetrySeconds = 1f;
    [SerializeField, Min(1)] private int _itemCapacity = 3;
    [SerializeField, Min(1)] private int _itemPlacementAttempts = 32;
    [SerializeField, FormerlySerializedAs("_bombPickupRadius"), Min(0.01f)] private float _itemPickupRadius = 0.7f;
    [SerializeField, FormerlySerializedAs("_bombLifetimeSeconds"), Min(0.01f)] private float _itemLifetimeSeconds = 18f;
    [SerializeField, Min(0f)] private float _itemDespawnWarningSeconds = 2f;
    [SerializeField, Min(0.01f)] private float _itemBlinkPeriodSeconds = 0.2f;
    [SerializeField, Min(0f)] private float _itemMinPlayerDistance = 1.5f;
    [SerializeField, Min(0f)] private float _itemMinEnemyDistance = 0.6f;
    [SerializeField, Min(0f)] private float _itemMinPickupDistance = 1.2f;

    [Header("Item Effects")]
    [SerializeField] private ItemData[] _items =
    {
        new ItemData(ITEM_TYPE.BOMB, 1, 2.4f, 0.35f, 0.35f),
        new ItemData(ITEM_TYPE.FREEZE, 1, 3.5f, 3.9f, 0.35f)
    };
    [SerializeField, Min(0f)] private float _freezeWarningSeconds = 0.6f;

    [Header("Score")]
    [SerializeField, Min(1)] private int _baseKillScore = 100;
    [SerializeField, Min(0.01f)] private float _comboWindowSeconds = 4f;

    public Vector2 ArenaSize => _arenaSize;
    public float PlayerRadius => _playerRadius;
    public float EnemyRadius => _enemyRadius;
    public int EnemyCapacity => _enemyCapacity;
    public float FirstWaveDelaySeconds => _firstWaveDelaySeconds;
    public IReadOnlyList<PatternWeight> Patterns => _patterns;
    public InterceptorPatternData InterceptorPattern => _interceptorPattern;

    public Vector2 FirstItemDelaySeconds => _firstItemDelaySeconds;
    public Vector2 ItemSpawnIntervalSeconds => _itemSpawnIntervalSeconds;
    public float ItemRetrySeconds => _itemRetrySeconds;
    public int ItemCapacity => _itemCapacity;
    public int ItemPlacementAttempts => _itemPlacementAttempts;
    public float ItemPickupRadius => _itemPickupRadius;
    public float ItemLifetimeSeconds => _itemLifetimeSeconds;
    public float ItemDespawnWarningSeconds => _itemDespawnWarningSeconds;
    public float ItemBlinkPeriodSeconds => _itemBlinkPeriodSeconds;
    public float ItemMinPlayerDistance => _itemMinPlayerDistance;
    public float ItemMinEnemyDistance => _itemMinEnemyDistance;
    public float ItemMinPickupDistance => _itemMinPickupDistance;
    public IReadOnlyList<ItemData> Items => _items;
    public float FreezeWarningSeconds => _freezeWarningSeconds;
    public int BaseKillScore => _baseKillScore;
    public float ComboWindowSeconds => _comboWindowSeconds;

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
        ValidateItems();
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

    public void ValidateItems()
    {
        if (_firstItemDelaySeconds.x < 0f || _firstItemDelaySeconds.y < _firstItemDelaySeconds.x
            || _itemSpawnIntervalSeconds.x <= 0f || _itemSpawnIntervalSeconds.y < _itemSpawnIntervalSeconds.x
            || _itemRetrySeconds <= 0f || _itemCapacity < 1 || _itemPlacementAttempts < 1
            || _itemPickupRadius <= 0f || _itemLifetimeSeconds <= 0f || _itemDespawnWarningSeconds < 0f
            || _itemDespawnWarningSeconds > _itemLifetimeSeconds || _itemBlinkPeriodSeconds <= 0f
            || _itemMinPlayerDistance < 0f || _itemMinEnemyDistance < 0f || _itemMinPickupDistance < 0f
            || _freezeWarningSeconds < 0f || _baseKillScore < 1 || _comboWindowSeconds <= 0f)
            throw new InvalidOperationException("아이템 공급·배치·점수 설정을 확인하세요.");

        if (_items == null || _items.Length == 0)
            throw new InvalidOperationException("아이템 설정이 필요합니다.");

        var types = new HashSet<ITEM_TYPE>();
        long totalWeight = 0;
        foreach (ItemData item in _items)
        {
            if (item == null || !types.Add(item.Type))
                throw new InvalidOperationException("아이템 설정의 누락과 종류 중복을 확인하세요.");

            item.Validate();
            if (item.EffectDefinition is ShieldEffectDefinition shield && shield.BurstRadius < shield.ShieldRadius + _enemyRadius)
                throw new InvalidOperationException("보호막 파괴 폭발은 보호막과 접촉한 적 중심까지 포함해야 합니다.");
            totalWeight += item.Weight;
        }

        if (totalWeight <= 0)
            throw new InvalidOperationException("등장 가중치가 있는 아이템이 필요합니다.");
    }
}
