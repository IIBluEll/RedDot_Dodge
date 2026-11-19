using System;
using System.Collections.Generic;

public interface IEnemyWaveHost
{
    bool HasWarning { get; }
    bool HasFormation { get; }
    int EnemyCount { get; }

    // 현재 경기장에서 가능한 배치를 계산 / 배치할 수 없으면 null을 반환
    WavePlan BuildWavePlan(WaveRequest request);

    // 예고와 활성 적을 합한 상한 안에서 웨이브 전원을 등록
    // false를 반환하는 경우 씬 상태와 사용 중인 적 수를 그대로 둠
    bool TryBeginWave(WaveState wave , int enemyCapacity);
}

public sealed class EnemyWaveDirector
{
    private readonly GameBalance BALANCE;
    private Random _random;
    private float _nextWaveAtSeconds;
    private WAVE_PATTERN? _lastPattern;
    private int _repeatCount;

    private readonly struct WaveCandidate
    {
        public WaveRequest Request { get; }
        public WavePlan Plan { get; }
        public int Weight { get; }

        public WaveCandidate(WaveRequest request , WavePlan plan , int weight)
        {
            Request = request;
            Plan = plan;
            Weight = weight;
        }
    }

    public EnemyWaveDirector(GameBalance balance , int randomSeed)
    {
        BALANCE = balance;
        BALANCE.Validate();
        Reset(randomSeed);
    }

    public void Reset(int randomSeed)
    {
        _random = new Random(randomSeed);
        _nextWaveAtSeconds = BALANCE.FirstWaveDelaySeconds;
        _lastPattern = null;
        _repeatCount = 0;
    }

    public bool TryStartWave(float survivalSeconds , IEnemyWaveHost host , out WaveState wave)
    {
        wave = null;

        if ( survivalSeconds < _nextWaveAtSeconds || host.HasWarning )
        {
            return false;
        }

        DifficultyStage stage = BALANCE.GetStage(survivalSeconds);
        _nextWaveAtSeconds = survivalSeconds + stage.WaveIntervalSeconds;

        WaveCandidate? selected = SelectCandidate(stage, host);
        return selected.HasValue && TryBegin(selected.Value , survivalSeconds , host , out wave);
    }

    private WaveCandidate? SelectCandidate(DifficultyStage stage , IEnemyWaveHost host)
    {
        var candidates = new List<WaveCandidate>(BALANCE.Patterns.Count);

        foreach ( PatternWeight entry in BALANCE.Patterns )
        {
            AEnemyPatternData pattern = entry.Pattern;
            if ( pattern.Kind == WAVE_PATTERN.INTERCEPTOR || !stage.Allows(pattern.Kind) || ( pattern.IsFormation && host.HasFormation ) )
            {
                continue;
            }

            WaveRequest request = CreateRequest(pattern, stage);
            WaveCandidate? candidate = BuildCandidate(request, entry.Weight, host);

            if ( candidate.HasValue )
            {
                candidates.Add(candidate.Value);
            }
        }

        if ( _repeatCount >= 2 && candidates.Exists(candidate => candidate.Request.Pattern.Kind != _lastPattern) )
        {
            candidates.RemoveAll(candidate => candidate.Request.Pattern.Kind == _lastPattern);
        }
        if ( candidates.Count == 0 )
        {
            return null;
        }

        long totalWeight = 0;
        foreach ( WaveCandidate candidate in candidates )
        {
            totalWeight += candidate.Weight;
        }

        double roll = _random.NextDouble() * totalWeight;

        foreach ( WaveCandidate candidate in candidates )
        {
            roll -= candidate.Weight;
            if ( roll < 0d )
            {
                return candidate;
            }
        }

        return candidates[ candidates.Count - 1 ];
    }

    private WaveCandidate? BuildCandidate(WaveRequest request , int weight , IEnemyWaveHost host)
    {
        if ( request.Pattern is ArrowDashPatternData arrow && arrow.EnemyCount == 0 )
        {
            return null;
        }

        if ( request.Pattern is WallPatternData wall
            && ( wall.GapWidth <= BALANCE.PlayerRadius * 2f || wall.MaxEnemySpacing > ( BALANCE.PlayerRadius + BALANCE.EnemyRadius ) * 2f ) )
        {
            return null;
        }

        WavePlan plan = host.BuildWavePlan(request);

        if ( plan == null || host.EnemyCount + plan.EnemyCount > BALANCE.EnemyCapacity )
        {
            return null;
        }

        return new WaveCandidate(request , plan , weight);
    }

    private WaveRequest CreateRequest(AEnemyPatternData pattern , DifficultyStage stage)
    {
        int count = 0;
        if ( pattern.Kind == WAVE_PATTERN.EDGE_CHASE || pattern.Kind == WAVE_PATTERN.CHASE_WALL )
        {
            int range = stage.MaximumChaseCount - stage.MinimumChaseCount + 1;
            count = stage.MinimumChaseCount + _random.Next(range);
        }

        return new WaveRequest(pattern , stage , count);
    }

    private bool TryBegin(WaveCandidate candidate , float survivalSeconds , IEnemyWaveHost host , out WaveState wave)
    {
        wave = new WaveState(candidate.Request , candidate.Plan , survivalSeconds);

        if ( !host.TryBeginWave(wave , BALANCE.EnemyCapacity) )
        {
            wave = null;
            return false;
        }

        WAVE_PATTERN kind = candidate.Request.Pattern.Kind;
        _repeatCount = _lastPattern == kind ? _repeatCount + 1 : 1;
        _lastPattern = kind;

        return true;
    }
}
