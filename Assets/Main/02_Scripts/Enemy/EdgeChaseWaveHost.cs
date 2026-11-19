using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class EdgeChaseWaveHost : MonoBehaviour, IEnemyWaveHost, IItemEffectTarget, IItemContactTarget, IHomingMissileTarget
{
    private const float READY_SECONDS = 3f;
    private const float WARNING_WIDTH = 0.8f;
    private const float ENEMY_WIDTH = 0.45f;
    private static readonly Color INTERCEPT_COLOR = new Color32(255, 186, 76, 255);

    [SerializeField] private GameBalance _gameBalance;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private Texture2D _warningTexture;
    [SerializeField] private Sprite _enemySprite;
    [SerializeField, FormerlySerializedAs("_bombItem")] private ItemSystem _itemSystem;
    [SerializeField] private AudioSource _shatterAudio;
    [SerializeField] private bool _startAutomatically = true;
    [SerializeField] private bool _allowKeyboardRestart = true;

    [Header("Enemy Animation")]
    [SerializeField, Min(0.01f)] private float _spawnBlinkStartInterval = 0.28f;
    [SerializeField, Min(0.01f)] private float _spawnBlinkEndInterval = 0.1f;
    [SerializeField, Min(0.01f)] private float _spawnRevealSeconds = 0.12f;
    [SerializeField, Min(0.01f)] private float _frozenEnemySizeMultiplier = 128f / 90f;
    [SerializeField, Range(5, 8)] private int _deathShardCount = 7;
    [SerializeField, Min(0.01f)] private float _deathDurationSeconds = 0.32f;
    [SerializeField, Min(0.01f)] private float _roundEndDeathDurationSeconds = 0.6f;
    [SerializeField, Min(0f)] private float _deathShardSpeed = 2.2f;

    private readonly List<PendingEnemy> WARNING_ENEMIES = new();
    private readonly List<ActiveEnemy> ACTIVE_ENEMIES = new();
    private readonly Stack<SpriteRenderer> FREE_RENDERERS = new();

    private struct ActiveEnemy
    {
        public int Id;
        public SpriteRenderer Renderer;
        public Vector2 PreviousPosition;
        public float Speed;
        public float FrozenUntilSeconds;
        public float ActivatedAtSeconds;
        public WaveState Wave;
        public ENEMY_MOTION Motion;
        public Vector2 Direction;
        public Vector2 Target;
        public INTERCEPT_PHASE InterceptPhase;
        public float PhaseRemainingSeconds;
        public SpriteRenderer PathRenderer;
        public SpriteRenderer TargetRenderer;
    }

    private enum INTERCEPT_PHASE { WARNING, MOVING, COOLDOWN }

    private sealed class PendingEnemy
    {
        public SpriteRenderer Renderer;
        public SpriteRenderer PathRenderer;
        public SpriteRenderer TargetRenderer;
        public Vector2 Position;
        public Vector2 Target;
        public bool AimLocked;
    }

    private EnemyWaveDirector _waveDirector;
    private RoundScore _roundScore;
    private WaveState _warningWave;
    private Sprite _warningSprite;
    private Sprite _pathSprite;
    private EnemyDeathVisuals _deathVisuals;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private Vector2 _previousPlayerPosition;
    private Vector2 _playerVelocity;
    private float _readyRemainingSeconds;
    private float _survivalSeconds;
    private float _nextInterceptorAtSeconds;
    private float _interceptorRetryAtSeconds;
    private bool _roundInProgress;
    private bool _initialized;
    private bool _canRestartRound;
    private int _nextEnemyId;

    public bool HasWarning => _warningWave != null;
    public bool HasFormation => (_warningWave != null && _warningWave.Request.Pattern.IsFormation)
        || ACTIVE_ENEMIES.Exists(enemy => enemy.Motion == ENEMY_MOTION.LINEAR);
    public int EnemyCount
    {
        get
        {
            int count = ACTIVE_ENEMIES.Count;
            foreach (PendingEnemy enemy in WARNING_ENEMIES)
                if (enemy != null)
                    count++;
            return count;
        }
    }
    public long Score => _roundScore?.Score ?? 0L;
    public int Combo => _roundScore?.Combo ?? 0;
    public int MaximumCombo => _roundScore?.MaximumCombo ?? 0;
    public int KillCount => _roundScore?.KillCount ?? 0;
    public float ComboRemainingSeconds => _roundScore?.GetRemainingSeconds(_survivalSeconds) ?? 0f;
    public float ComboWindowSeconds => _gameBalance != null ? _gameBalance.ComboWindowSeconds : 0f;
    public float SurvivalSeconds => _survivalSeconds;
    public float ReadyRemainingSeconds => _roundInProgress ? _readyRemainingSeconds : 0f;
    public event System.Action<long> ScoreChanged;
    public event System.Action<RoundResult> RoundEnded;

    private void Start()
    {
        if (_initialized || !TryInitialize())
            return;

        if (_startAutomatically)
            RestartRound();
        else
            StopRound();
    }

    private bool TryInitialize()
    {
        if (_initialized)
            return true;

        if (_gameBalance == null || _playerMovement == null || _warningTexture == null || _enemySprite == null)
        {
            Debug.LogError("EdgeChaseWaveHost의 Inspector 참조를 채워 주세요.", this);
            enabled = false;
            return false;
        }

        if (_itemSystem == null || !_itemSystem.Initialize(_gameBalance, this, Random.Range(0, int.MaxValue), _playerMovement.transform))
        {
            Debug.LogError("EdgeChaseWaveHost의 Item System 연결과 아이템 리소스를 확인해 주세요.", this);
            enabled = false;
            _playerMovement.enabled = false;
            return false;
        }

        _roundScore = new RoundScore(_gameBalance);
        _waveDirector = new EnemyWaveDirector(_gameBalance, Random.Range(0, int.MaxValue));
        Rect warningRect = new Rect(0f, 0f, _warningTexture.width, _warningTexture.height);
        _warningSprite = Sprite.Create(_warningTexture, warningRect, Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
        _pathSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f, 0, SpriteMeshType.FullRect);
        _deathVisuals = new EnemyDeathVisuals(transform);

        _spawnPosition = _playerMovement.transform.position;
        _spawnRotation = _playerMovement.transform.rotation;
        _initialized = true;
        return true;
    }

    private void Update()
    {
        if (_allowKeyboardRestart && _canRestartRound && Keyboard.current?.rKey.wasPressedThisFrame == true)
            RestartRound();

        // Let cosmetic fragments finish after a lethal hit; gameplay remains stopped.
        _deathVisuals?.Tick(_roundInProgress ? Time.deltaTime : Time.unscaledDeltaTime);
    }

    private void FixedUpdate()
    {
        if (!_roundInProgress)
            return;

        float deltaTime = Time.fixedDeltaTime;

        if (_readyRemainingSeconds > 0f)
        {
            _readyRemainingSeconds = Mathf.Max(0f, _readyRemainingSeconds - deltaTime);
            if (_readyRemainingSeconds == 0f)
                _playerMovement.enabled = true;

            return;
        }

        _survivalSeconds += deltaTime;
        Vector2 playerPosition = _playerMovement.transform.position;
        Vector2 measuredVelocity = (playerPosition - _previousPlayerPosition) / deltaTime;
        _playerVelocity = Vector2.Lerp(_playerVelocity, measuredVelocity, 1f - Mathf.Exp(-deltaTime / 0.12f));
        _itemSystem.TickLifetimes(_survivalSeconds);
        _roundScore.Tick(_survivalSeconds);
        TickActiveEnemies(playerPosition, deltaTime);
        RefreshWarningVisuals();
        ActivateWarning();

        _itemSystem.TickSupply(_survivalSeconds, playerPosition);
        _itemSystem.CollectPickups(_previousPlayerPosition, playerPosition, _gameBalance.PlayerRadius, _survivalSeconds);

        int killCount = _itemSystem.ApplyEffects();
        bool playerHit = ResolvePlayerContact(playerPosition, out int shatterCount);
        killCount += shatterCount;
        if (shatterCount > 0 && _shatterAudio != null)
            _shatterAudio.Play();
        RefreshEnemyVisuals();
        RetireExitedEnemies();
        _previousPlayerPosition = playerPosition;

        if (killCount > 0)
        {
            _roundScore.AddKills(killCount, _survivalSeconds);
            ScoreChanged?.Invoke(Score);
        }

        if (playerHit)
        {
            FinishRound();
            return;
        }

        if (!TryStartInterceptor())
            _waveDirector.TryStartWave(_survivalSeconds, this, out _);
    }

    private void FinishRound()
    {
        if (!_roundInProgress)
            return;

        _roundInProgress = false;
        _playerMovement.enabled = false;
        var result = new RoundResult(Score, MaximumCombo, KillCount, _survivalSeconds);
        DestroyRemainingEnemies();
        _itemSystem.ResetRound(Random.Range(0, int.MaxValue));
        _playerMovement.ClearTrail();
        _playerMovement.gameObject.SetActive(false);
        if (_shatterAudio != null)
            _shatterAudio.Stop();
        RoundEnded?.Invoke(result);
        if (_allowKeyboardRestart)
            Debug.Log($"플레이어 피격 ({result.SurvivalSeconds:F2}초), 점수 {result.Score}. R 키로 다시 시작합니다.", this);
    }

    private void DestroyRemainingEnemies()
    {
        // 사망 연출은 처치 점수와 통계를 갱신하지 않습니다.
        _deathVisuals?.Reset();
        for (int i = ACTIVE_ENEMIES.Count - 1; i >= 0; i--)
            KillEnemy(i, _roundEndDeathDurationSeconds);

        foreach (PendingEnemy pending in WARNING_ENEMIES)
        {
            if (pending == null)
                continue;
            if (pending.Renderer.enabled)
                _deathVisuals?.Play(pending.Position, pending.Renderer.sprite, WARNING_WIDTH, false,
                    _deathShardCount, _roundEndDeathDurationSeconds, _deathShardSpeed);
            ReturnRenderer(pending.Renderer);
            ReturnRenderer(pending.PathRenderer);
            ReturnRenderer(pending.TargetRenderer);
        }
        WARNING_ENEMIES.Clear();
        _warningWave?.Finish();
        _warningWave = null;
    }

    public void ClearDeathVisuals()
    {
        _deathVisuals?.Reset();
    }

    private bool TryStartInterceptor()
    {
        InterceptorPatternData pattern = _gameBalance.InterceptorPattern;
        if (pattern == null || HasWarning || _survivalSeconds < _nextInterceptorAtSeconds
            || _survivalSeconds < _interceptorRetryAtSeconds)
            return false;

        // 진행 중 예고가 끝난 뒤 우선 등록합니다. 상한·배치 실패 시 한 기만 대기시킵니다.
        _interceptorRetryAtSeconds = _survivalSeconds + 1f;
        var request = new WaveRequest(pattern, _gameBalance.GetStage(_survivalSeconds), 0);
        WavePlan plan = BuildWavePlan(request);
        if (plan == null || !TryBeginWave(new WaveState(request, plan, _survivalSeconds), _gameBalance.EnemyCapacity))
            return false;

        float interval = Mathf.Max(1f, pattern.SpawnIntervalSeconds);
        // 늦게 등장해도 원래 60초 단위를 유지하며 밀린 적을 몰아서 생성하지 않습니다.
        _nextInterceptorAtSeconds = (Mathf.Floor(_survivalSeconds / interval) + 1f) * interval;
        _interceptorRetryAtSeconds = 0f;
        return true;
    }

    public WavePlan BuildWavePlan(WaveRequest request)
    {
        Vector2 limits = GetSpawnLimits();
        if (limits.x <= 0f || limits.y <= 0f)
            return null;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            WavePlan plan = EnemyWaveLayouts.Build(request, _gameBalance, limits, _playerMovement.transform.position);
            if (plan != null && EnemyCount + plan.EnemyCount <= _gameBalance.EnemyCapacity && HasEscapeRoute(request, plan))
                return plan;
        }
        return null;
    }

    private bool HasEscapeRoute(WaveRequest request, WavePlan plan)
    {
        Vector2 start = _playerMovement.transform.position;
        float radius = _gameBalance.PlayerRadius + _gameBalance.EnemyRadius + 0.05f;
        Vector2 playerLimits = _gameBalance.ArenaSize * 0.5f - Vector2.one * _gameBalance.PlayerRadius;
        // 예고 종료까지 직선 회피 후보를 검사합니다. 이후의 생존을 보장하는 경로 탐색은 아닙니다.
        for (int route = 0; route < 16; route++)
        {
            float angle = route * Mathf.PI * 2f / 16f;
            Vector2 destination = start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 3f;
            if (!EnemyWaveLayouts.Inside(destination, playerLimits))
                continue;
            bool clear = true;
            for (int step = 1; step <= 16 && clear; step++)
            {
                float time = request.WarningDurationSeconds * step / 16f;
                Vector2 position = Vector2.MoveTowards(start, destination, _playerMovement.MaximumSpeed * time);
                foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
                {
                    Vector2 predicted = enemy.Renderer.transform.position;
                    float movingTime = Mathf.Max(0f, time - Mathf.Max(0f, enemy.FrozenUntilSeconds - _survivalSeconds));
                    if (enemy.Motion == ENEMY_MOTION.LINEAR)
                        predicted += enemy.Direction * (enemy.Speed * movingTime);
                    else
                        predicted = Vector2.MoveTowards(predicted, enemy.Motion == ENEMY_MOTION.INTERCEPTOR ? enemy.Target : position, enemy.Speed * movingTime);
                    if ((position - predicted).sqrMagnitude < radius * radius)
                    {
                        clear = false;
                        break;
                    }
                }
                if (step != 16)
                    continue;
                foreach (EnemyPlacement placement in plan.Placements)
                {
                    if (placement.WarningDelaySeconds == 0f && (position - placement.SpawnPosition).sqrMagnitude < radius * radius)
                    {
                        clear = false;
                        break;
                    }
                }
            }
            if (clear)
                return true;
        }
        return false;
    }

    public bool TryBeginWave(WaveState wave, int enemyCapacity)
    {
        if (HasWarning || EnemyCount + wave.Plan.EnemyCount > enemyCapacity)
            return false;
        foreach (EnemyPlacement placement in wave.Plan.Placements)
            if (!EnemyWaveLayouts.Inside(placement.SpawnPosition, GetSpawnLimits())
                || !EnemyWaveLayouts.Inside(placement.WarningPosition, GetSpawnLimits()))
                return false;
        _warningWave = wave;
        foreach (EnemyPlacement placement in wave.Plan.Placements)
        {
            SpriteRenderer renderer = RentRenderer();
            renderer.enabled = false;
            WARNING_ENEMIES.Add(new PendingEnemy { Renderer = renderer, Position = placement.SpawnPosition });
        }
        RefreshWarningVisuals();
        return true;
    }

    private Vector2 GetSpawnLimits()
    {
        // Include the warning's maximum animated width as well as the enemy collision circle.
        float padding = Mathf.Max(_gameBalance.EnemyRadius, Mathf.Max(ENEMY_WIDTH, WARNING_WIDTH * 1.08f) * 0.5f);
        return _gameBalance.ArenaSize * 0.5f - Vector2.one * padding;
    }

    private float GetWarningDuration(EnemyPlacement placement)
    {
        return _warningWave.Request.GetWarningDuration(placement);
    }

    private void RefreshWarningVisuals()
    {
        if (!HasWarning)
            return;
        _warningWave.TryLockArrowAim(_survivalSeconds, _playerMovement.transform.position);
        float elapsed = Mathf.Max(0f, _survivalSeconds - _warningWave.WarningStartedAtSeconds);
        for (int i = 0; i < WARNING_ENEMIES.Count; i++)
        {
            PendingEnemy pending = WARNING_ENEMIES[i];
            if (pending == null)
                continue;
            EnemyPlacement placement = _warningWave.Plan.Placements[i];
            float localElapsed = elapsed - placement.WarningDelaySeconds;
            if (localElapsed < 0f)
                continue;
            if (placement.Motion == ENEMY_MOTION.INTERCEPTOR && !pending.AimLocked)
            {
                var data = (InterceptorPatternData)_warningWave.Request.Pattern;
                pending.Target = PredictInterceptTarget(data);
                pending.Position = EnemyWaveLayouts.InterceptorStart(pending.Target, GetSpawnLimits());
                pending.PathRenderer = RentRenderer();
                pending.TargetRenderer = RentRenderer();
                pending.AimLocked = true;
                ShowInterceptWarning(pending.PathRenderer, pending.TargetRenderer, pending.Position, pending.Target);
            }
            if (_warningWave.Request.Pattern is ArrowDashPatternData arrow)
            {
                ArrowSlot slot = arrow.Slots[i];
                float gather = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / arrow.GatherDurationSeconds));
                Vector2 offset = Vector2.Lerp(slot.ScatteredOffset, slot.FormationOffset, gather);
                Vector2 direction = _warningWave.Plan.InitialDirection;
                if (_warningWave.LockedAimDirection.HasValue)
                {
                    float turn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - arrow.GatherDurationSeconds) / arrow.TurnDurationSeconds));
                    float from = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    Vector2 locked = _warningWave.LockedAimDirection.Value;
                    float angle = Mathf.LerpAngle(from, Mathf.Atan2(locked.y, locked.x) * Mathf.Rad2Deg, turn) * Mathf.Deg2Rad;
                    direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
                pending.Position = _warningWave.Plan.Anchor + EnemyWaveLayouts.Rotate(offset, direction);
            }
            float progress = Mathf.Clamp01(localElapsed / Mathf.Max(0.01f, GetWarningDuration(placement)));
            float startFrequency = 1f / Mathf.Max(0.01f, _spawnBlinkStartInterval);
            float endFrequency = 1f / Mathf.Max(0.01f, _spawnBlinkEndInterval);
            float cycles = localElapsed * (startFrequency + (endFrequency - startFrequency) * progress * 0.5f);
            float pulse = (1f - Mathf.Cos(cycles * Mathf.PI * 2f)) * 0.5f;
            ShowSprite(pending.Renderer, _warningSprite, pending.Position, WARNING_WIDTH * (Mathf.Lerp(0.85f, 1f, progress) + pulse * 0.08f));
            Color color = placement.Motion == ENEMY_MOTION.INTERCEPTOR ? INTERCEPT_COLOR : Color.white;
            color.a = Mathf.Lerp(0.18f, 1f, pulse) * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(localElapsed / 0.12f));
            pending.Renderer.color = color;
        }
    }

    private void ActivateWarning()
    {
        if (!HasWarning)
            return;
        float elapsed = _survivalSeconds - _warningWave.WarningStartedAtSeconds;
        bool remaining = false;
        for (int i = 0; i < WARNING_ENEMIES.Count; i++)
        {
            PendingEnemy pending = WARNING_ENEMIES[i];
            if (pending == null)
                continue;
            EnemyPlacement placement = _warningWave.Plan.Placements[i];
            if (elapsed + 0.0001f < placement.WarningDelaySeconds + GetWarningDuration(placement))
            {
                remaining = true;
                continue;
            }
            float speed = _warningWave.Request.Stage.ChaseSpeed;
            if (placement.Motion == ENEMY_MOTION.LINEAR)
                speed = _warningWave.Request.Pattern is ArrowDashPatternData arrow ? arrow.DashSpeed : speed * ((WallPatternData)_warningWave.Request.Pattern).SpeedMultiplier;
            else if (placement.Motion == ENEMY_MOTION.INTERCEPTOR)
                speed = ((InterceptorPatternData)_warningWave.Request.Pattern).DashSpeed;
            Sprite sprite = placement.Motion == ENEMY_MOTION.INTERCEPTOR ? _pathSprite : _enemySprite;
            ShowSprite(pending.Renderer, sprite, pending.Position, ENEMY_WIDTH);
            pending.Renderer.color = placement.Motion == ENEMY_MOTION.INTERCEPTOR ? INTERCEPT_COLOR : Color.white;
            pending.Renderer.transform.rotation = placement.Motion == ENEMY_MOTION.INTERCEPTOR ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity;
            if (pending.PathRenderer != null)
                pending.PathRenderer.enabled = false;
            ACTIVE_ENEMIES.Add(new ActiveEnemy
            {
                Id = ++_nextEnemyId, Renderer = pending.Renderer, PreviousPosition = pending.Position,
                Speed = speed, ActivatedAtSeconds = _survivalSeconds, Wave = _warningWave,
                Motion = placement.Motion, Direction = _warningWave.LockedAimDirection ?? _warningWave.Plan.InitialDirection,
                Target = pending.Target, InterceptPhase = INTERCEPT_PHASE.MOVING,
                PathRenderer = pending.PathRenderer, TargetRenderer = pending.TargetRenderer
            });
            WARNING_ENEMIES[i] = null;
        }
        if (!remaining)
        {
            _warningWave.TryActivate(Mathf.Max(_survivalSeconds, _warningWave.WarningEndsAtSeconds));
            if (!ACTIVE_ENEMIES.Exists(enemy => enemy.Wave == _warningWave))
                _warningWave.Finish();
            WARNING_ENEMIES.Clear();
            _warningWave = null;
        }
    }

    private void TickActiveEnemies(Vector2 playerPosition, float deltaTime)
    {
        for (int i = ACTIVE_ENEMIES.Count - 1; i >= 0; i--)
        {
            ActiveEnemy enemy = ACTIVE_ENEMIES[i];
            enemy.PreviousPosition = enemy.Renderer.transform.position;
            if (_survivalSeconds >= enemy.FrozenUntilSeconds)
            {
                enemy.FrozenUntilSeconds = 0f;
                if (enemy.Motion == ENEMY_MOTION.INTERCEPTOR)
                    TickInterceptor(ref enemy, deltaTime);
                else if (enemy.Motion == ENEMY_MOTION.LINEAR)
                    enemy.Renderer.transform.position = enemy.PreviousPosition + enemy.Direction * (enemy.Speed * deltaTime);
                else
                    enemy.Renderer.transform.position = Vector2.MoveTowards(enemy.PreviousPosition, playerPosition, enemy.Speed * deltaTime);
            }
            else if (enemy.PathRenderer != null)
            {
                enemy.PathRenderer.enabled = false;
                enemy.TargetRenderer.enabled = false;
            }
            ACTIVE_ENEMIES[i] = enemy;
        }
    }

    private Vector2 PredictInterceptTarget(InterceptorPatternData data)
    {
        Vector2 target = (Vector2)_playerMovement.transform.position + _playerVelocity * data.LookaheadSeconds;
        Vector2 limits = _gameBalance.ArenaSize * 0.5f - Vector2.one * _gameBalance.PlayerRadius;
        return new Vector2(Mathf.Clamp(target.x, -limits.x, limits.x), Mathf.Clamp(target.y, -limits.y, limits.y));
    }

    private void ShowInterceptWarning(SpriteRenderer pathRenderer, SpriteRenderer targetRenderer, Vector2 start, Vector2 target)
    {
        Vector2 delta = target - start;
        ShowSprite(pathRenderer, _pathSprite, (start + target) * 0.5f, 1f);
        pathRenderer.transform.localScale = new Vector3(delta.magnitude, 0.06f, 1f);
        pathRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        pathRenderer.color = new Color(INTERCEPT_COLOR.r, INTERCEPT_COLOR.g, INTERCEPT_COLOR.b, 0.6f);
        ShowSprite(targetRenderer, _warningSprite, target, 0.48f);
        targetRenderer.color = INTERCEPT_COLOR;
    }

    private void TickInterceptor(ref ActiveEnemy enemy, float deltaTime)
    {
        var data = (InterceptorPatternData)enemy.Wave.Request.Pattern;
        if (enemy.InterceptPhase == INTERCEPT_PHASE.MOVING)
        {
            enemy.Renderer.transform.position = Vector2.MoveTowards(enemy.PreviousPosition, enemy.Target, enemy.Speed * deltaTime);
            enemy.TargetRenderer.enabled = true;
            if (((Vector2)enemy.Renderer.transform.position - enemy.Target).sqrMagnitude <= 0.000001f)
            {
                enemy.TargetRenderer.enabled = false;
                enemy.InterceptPhase = INTERCEPT_PHASE.COOLDOWN;
                enemy.PhaseRemainingSeconds = data.CooldownSeconds;
            }
            return;
        }
        if (enemy.InterceptPhase == INTERCEPT_PHASE.WARNING)
            ShowInterceptWarning(enemy.PathRenderer, enemy.TargetRenderer, enemy.PreviousPosition, enemy.Target);
        enemy.PhaseRemainingSeconds -= deltaTime;
        if (enemy.PhaseRemainingSeconds > 0f)
            return;
        if (enemy.InterceptPhase == INTERCEPT_PHASE.COOLDOWN)
        {
            enemy.Target = PredictInterceptTarget(data);
            enemy.InterceptPhase = INTERCEPT_PHASE.WARNING;
            enemy.PhaseRemainingSeconds = data.WarningDurationSeconds;
            ShowInterceptWarning(enemy.PathRenderer, enemy.TargetRenderer, enemy.PreviousPosition, enemy.Target);
        }
        else
        {
            enemy.PathRenderer.enabled = false;
            enemy.InterceptPhase = INTERCEPT_PHASE.MOVING;
        }
    }

    private void RetireExitedEnemies()
    {
        Vector2 limits = _gameBalance.ArenaSize * 0.5f + Vector2.one * _gameBalance.EnemyRadius;
        for (int i = ACTIVE_ENEMIES.Count - 1; i >= 0; i--)
        {
            ActiveEnemy enemy = ACTIVE_ENEMIES[i];
            if (enemy.Motion == ENEMY_MOTION.LINEAR && !EnemyWaveLayouts.Inside(enemy.Renderer.transform.position, limits))
                RemoveEnemy(i);
        }
    }

    public int KillEnemiesInRadius(Vector2 position, float radius)
    {
        return KillEnemiesAlongPath(position, position, radius);
    }

    public int KillEnemiesAlongPath(Vector2 start, Vector2 end, float radius)
    {
        int killCount = 0;

        for (int i = ACTIVE_ENEMIES.Count - 1; i >= 0; i--)
        {
            ActiveEnemy enemy = ACTIVE_ENEMIES[i];
            Vector2 enemyPosition = enemy.Renderer.transform.position;
            if (ItemGeometry.DistanceToSegmentSquared(Vector2.zero, enemy.PreviousPosition - start, enemyPosition - end) > radius * radius)
                continue;

            KillEnemy(i);
            killCount++;
        }

        return killCount;
    }

    public void FreezeEnemiesInRadius(Vector2 position, float radius, float frozenUntilSeconds)
    {
        for (int i = 0; i < ACTIVE_ENEMIES.Count; i++)
        {
            ActiveEnemy enemy = ACTIVE_ENEMIES[i];
            Vector2 enemyPosition = enemy.Renderer.transform.position;
            if ((enemyPosition - position).sqrMagnitude > radius * radius)
                continue;

            enemy.FrozenUntilSeconds = frozenUntilSeconds;
            ACTIVE_ENEMIES[i] = enemy;
        }
    }

    public bool TryFindDangerContact(Vector2 start, Vector2 end, float radius, out Vector2 impactPosition)
    {
        float earliestTime = float.PositiveInfinity;
        float contactRadius = radius + _gameBalance.EnemyRadius;
        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
        {
            if (enemy.FrozenUntilSeconds > _survivalSeconds)
                continue;

            Vector2 enemyPosition = enemy.Renderer.transform.position;
            if (ItemGeometry.TryGetContactTime(enemy.PreviousPosition - start, enemyPosition - end, contactRadius, out float time))
                earliestTime = Mathf.Min(earliestTime, time);
        }

        impactPosition = earliestTime <= 1f ? Vector2.Lerp(start, end, earliestTime) : end;
        return earliestTime <= 1f;
    }

    public bool TryAcquireMissileTarget(Vector2 position, IReadOnlyList<int> reservedTargets, out int targetId, out Vector2 targetPosition)
    {
        targetId = 0;
        targetPosition = position;
        float nearestDistance = float.PositiveInfinity;
        float nearestFreeDistance = float.PositiveInfinity;
        int nearestId = 0;
        Vector2 nearestPosition = position;
        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
        {
            Vector2 enemyPosition = enemy.Renderer.transform.position;
            float distance = (enemyPosition - position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestId = enemy.Id;
                nearestPosition = enemyPosition;
            }
            bool reserved = false;
            for (int i = 0; i < reservedTargets.Count; i++)
                reserved |= reservedTargets[i] == enemy.Id;
            if (!reserved && distance < nearestFreeDistance)
            {
                nearestFreeDistance = distance;
                targetId = enemy.Id;
                targetPosition = enemyPosition;
            }
        }
        if (targetId == 0)
        {
            targetId = nearestId;
            targetPosition = nearestPosition;
        }
        return targetId != 0;
    }

    public bool TryGetMissileTarget(int targetId, out Vector2 targetPosition)
    {
        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
        {
            if (targetId == 0 || enemy.Id != targetId)
                continue;
            targetPosition = enemy.Renderer.transform.position;
            return true;
        }
        targetPosition = Vector2.zero;
        return false;
    }

    public bool TryFindMissileContact(Vector2 start, Vector2 end, float radius, out Vector2 impactPosition)
    {
        float earliestTime = float.PositiveInfinity;
        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
        {
            Vector2 enemyPosition = enemy.Renderer.transform.position;
            if (ItemGeometry.TryGetContactTime(enemy.PreviousPosition - start, enemyPosition - end, radius + _gameBalance.EnemyRadius, out float time))
                earliestTime = Mathf.Min(earliestTime, time);
        }
        impactPosition = earliestTime <= 1f ? Vector2.Lerp(start, end, earliestTime) : end;
        return earliestTime <= 1f;
    }

    private bool ResolvePlayerContact(Vector2 playerPosition, out int shatterCount)
    {
        shatterCount = 0;
        float playerRadius = _itemSystem.GetPlayerEnemyContactRadius(_gameBalance.PlayerRadius);

        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
        {
            if (!TouchesPlayer(enemy, playerPosition, playerRadius) || enemy.FrozenUntilSeconds > _survivalSeconds)
                continue;

            var hit = new PlayerHit(playerPosition, enemy.Renderer.transform.position, _survivalSeconds);
            if (!_itemSystem.TryBlockPlayerHit(hit))
                return true;
        }

        for (int i = ACTIVE_ENEMIES.Count - 1; i >= 0; i--)
        {
            ActiveEnemy enemy = ACTIVE_ENEMIES[i];
            if (enemy.FrozenUntilSeconds <= _survivalSeconds || !TouchesPlayer(enemy, playerPosition, playerRadius))
                continue;

            KillEnemy(i);
            shatterCount++;
        }

        return false;
    }

    private bool TouchesPlayer(ActiveEnemy enemy, Vector2 playerPosition, float playerRadius)
    {
        Vector2 enemyPosition = enemy.Renderer.transform.position;
        Vector2 relativeStart = enemy.PreviousPosition - _previousPlayerPosition;
        Vector2 relativeEnd = enemyPosition - playerPosition;
        float radius = playerRadius + _gameBalance.EnemyRadius;
        return ItemGeometry.DistanceToSegmentSquared(Vector2.zero, relativeStart, relativeEnd) <= radius * radius;
    }

    private void RefreshEnemyVisuals()
    {
        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
        {
            float remainingSeconds = enemy.FrozenUntilSeconds - _survivalSeconds;
            bool frozen = remainingSeconds > 0f;
            Sprite frozenSprite = _itemSystem.FrozenEnemySprite;
            Sprite sprite = frozen && frozenSprite != null ? frozenSprite
                : enemy.Motion == ENEMY_MOTION.INTERCEPTOR ? _pathSprite : _enemySprite;
            float reveal = Mathf.Clamp01((_survivalSeconds - enemy.ActivatedAtSeconds) / Mathf.Max(0.01f, _spawnRevealSeconds));
            // 빙결 이미지는 128px 전체 프레임을 사용하므로 일반 적의 90px 본체 프레임에 맞춥니다.
            float width = ENEMY_WIDTH * (frozen && frozenSprite != null ? _frozenEnemySizeMultiplier : 1f);
            ShowSprite(enemy.Renderer, sprite, enemy.Renderer.transform.position, width * Mathf.Lerp(0.72f, 1f, reveal));
            enemy.Renderer.transform.rotation = !frozen && enemy.Motion == ENEMY_MOTION.INTERCEPTOR ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity;
            Color color = frozen ? (frozenSprite == null ? Color.cyan : Color.white)
                : enemy.Motion == ENEMY_MOTION.INTERCEPTOR ? INTERCEPT_COLOR : Color.white;
            if (!frozen)
                color *= Mathf.Lerp(1.7f, 1f, reveal);
            color.a = 1f;

            if (frozen && remainingSeconds <= _gameBalance.FreezeWarningSeconds)
                color.a = Mathf.Repeat(remainingSeconds, _gameBalance.ItemBlinkPeriodSeconds) < _gameBalance.ItemBlinkPeriodSeconds * 0.5f ? 0.3f : 1f;

            enemy.Renderer.color = color;
        }
    }

    private void KillEnemy(int index, float? deathDurationSeconds = null)
    {
        ActiveEnemy enemy = ACTIVE_ENEMIES[index];
        bool frozen = enemy.FrozenUntilSeconds > _survivalSeconds;
        Sprite deathSprite = frozen && _itemSystem.FrozenEnemySprite != null ? _itemSystem.FrozenEnemySprite
            : enemy.Motion == ENEMY_MOTION.INTERCEPTOR ? _pathSprite : _enemySprite;
        float deathWidth = ENEMY_WIDTH * (frozen && _itemSystem.FrozenEnemySprite != null ? _frozenEnemySizeMultiplier : 1f);
        _deathVisuals?.Play(enemy.Renderer.transform.position, deathSprite, deathWidth, frozen,
            _deathShardCount, deathDurationSeconds ?? _deathDurationSeconds, _deathShardSpeed);
        RemoveEnemy(index);
    }

    private void RemoveEnemy(int index)
    {
        ActiveEnemy enemy = ACTIVE_ENEMIES[index];
        ReturnRenderer(enemy.Renderer);
        ReturnRenderer(enemy.PathRenderer);
        ReturnRenderer(enemy.TargetRenderer);
        ACTIVE_ENEMIES.RemoveAt(index);
        if (!ACTIVE_ENEMIES.Exists(other => other.Wave == enemy.Wave) && enemy.Wave != _warningWave)
            enemy.Wave?.Finish();
    }

    public bool IsItemPositionClear(Vector2 position, float enemyDistance)
    {
        float distanceSquared = enemyDistance * enemyDistance;
        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
        {
            Vector2 enemyPosition = enemy.Renderer.transform.position;
            if ((position - enemyPosition).sqrMagnitude < distanceSquared)
                return false;
        }

        if (_warningWave != null)
        {
            for (int i = 0; i < WARNING_ENEMIES.Count; i++)
            {
                PendingEnemy pending = WARNING_ENEMIES[i];
                if (pending == null)
                    continue;
                EnemyPlacement placement = _warningWave.Plan.Placements[i];
                if ((position - pending.Position).sqrMagnitude < distanceSquared
                    || (position - placement.SpawnPosition).sqrMagnitude < distanceSquared)
                    return false;
            }
        }

        return true;
    }

    public void RestartRound()
    {
        BeginRound();
    }

    public bool BeginRound()
    {
        if (!TryInitialize())
            return false;

        ResetRound(true);
        return true;
    }

    public void StopRound()
    {
        if (!_initialized)
        {
            _roundInProgress = false;
            _canRestartRound = false;
            _readyRemainingSeconds = 0f;
            if (_playerMovement != null)
            {
                _playerMovement.enabled = false;
                _playerMovement.gameObject.SetActive(false);
            }
            return;
        }

        ResetRound(false);
    }

    private void ResetRound(bool startRound)
    {
        _playerMovement.enabled = false;
        _playerMovement.transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        _playerMovement.ClearTrail();
        _previousPlayerPosition = _spawnPosition;
        _playerVelocity = Vector2.zero;

        foreach (PendingEnemy pending in WARNING_ENEMIES)
        {
            if (pending == null)
                continue;
            ReturnRenderer(pending.Renderer);
            ReturnRenderer(pending.PathRenderer);
            ReturnRenderer(pending.TargetRenderer);
        }

        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
        {
            ReturnRenderer(enemy.Renderer);
            ReturnRenderer(enemy.PathRenderer);
            ReturnRenderer(enemy.TargetRenderer);
            enemy.Wave?.Finish();
        }

        WARNING_ENEMIES.Clear();
        ACTIVE_ENEMIES.Clear();
        _nextEnemyId = 0;
        _warningWave?.Finish();
        _warningWave = null;

        _waveDirector.Reset(Random.Range(0, int.MaxValue));
        _survivalSeconds = 0f;
        _nextInterceptorAtSeconds = _gameBalance.InterceptorPattern != null
            ? Mathf.Max(1f, _gameBalance.InterceptorPattern.SpawnIntervalSeconds) : float.PositiveInfinity;
        _interceptorRetryAtSeconds = 0f;
        _readyRemainingSeconds = startRound ? READY_SECONDS : 0f;
        _roundInProgress = startRound;
        _canRestartRound = startRound;

        _itemSystem.ResetRound(Random.Range(0, int.MaxValue));
        _deathVisuals?.Reset();
        if (_shatterAudio != null)
            _shatterAudio.Stop();
        _roundScore.Reset();
        _playerMovement.gameObject.SetActive(startRound);
        ScoreChanged?.Invoke(Score);
    }

    private SpriteRenderer RentRenderer()
    {
        if (FREE_RENDERERS.Count > 0)
            return FREE_RENDERERS.Pop();

        var enemyObj = new GameObject("Chase Enemy", typeof(SpriteRenderer));
        enemyObj.transform.SetParent(transform, false);
        SpriteRenderer renderer = enemyObj.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = 4;
        return renderer;
    }

    private void ReturnRenderer(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;
        renderer.enabled = false;
        renderer.color = Color.white;
        renderer.transform.rotation = Quaternion.identity;
        FREE_RENDERERS.Push(renderer);
    }

    private static void ShowSprite(SpriteRenderer renderer, Sprite sprite, Vector2 position, float width)
    {
        renderer.sprite = sprite;
        renderer.transform.position = position;
        renderer.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
        renderer.enabled = true;
    }

    private void OnDestroy()
    {
        _deathVisuals?.Dispose();
        if (_warningSprite != null)
            Destroy(_warningSprite);
        if (_pathSprite != null)
            Destroy(_pathSprite);
    }
}
