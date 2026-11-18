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

    [SerializeField] private GameBalance _gameBalance;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private Texture2D _warningTexture;
    [SerializeField] private Sprite _enemySprite;
    [SerializeField, FormerlySerializedAs("_bombItem")] private ItemSystem _itemSystem;
    [SerializeField] private AudioSource _shatterAudio;

    [Header("Enemy Animation")]
    [SerializeField, Min(0.01f)] private float _spawnBlinkStartInterval = 0.28f;
    [SerializeField, Min(0.01f)] private float _spawnBlinkEndInterval = 0.1f;
    [SerializeField, Min(0.01f)] private float _spawnRevealSeconds = 0.12f;
    [SerializeField, Range(5, 8)] private int _deathShardCount = 7;
    [SerializeField, Min(0.01f)] private float _deathDurationSeconds = 0.32f;
    [SerializeField, Min(0f)] private float _deathShardSpeed = 2.2f;

    private readonly List<SpriteRenderer> WARNING_RENDERERS = new();
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
    }

    private EnemyWaveDirector _waveDirector;
    private RoundScore _roundScore;
    private WaveState _warningWave;
    private Sprite _warningSprite;
    private EnemyDeathVisuals _deathVisuals;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private Vector2 _previousPlayerPosition;
    private float _readyRemainingSeconds;
    private float _survivalSeconds;
    private bool _roundInProgress;
    private int _nextEnemyId;

    public bool HasWarning => _warningWave != null;
    public bool HasFormation => false;
    public long Score => _roundScore?.Score ?? 0L;
    public int Combo => _roundScore?.Combo ?? 0;
    public int MaximumCombo => _roundScore?.MaximumCombo ?? 0;
    public int KillCount => _roundScore?.KillCount ?? 0;
    public float ComboRemainingSeconds => _roundScore?.GetRemainingSeconds(_survivalSeconds) ?? 0f;
    public float ComboWindowSeconds => _gameBalance != null ? _gameBalance.ComboWindowSeconds : 0f;
    public float SurvivalSeconds => _survivalSeconds;
    public event System.Action<long> ScoreChanged;

    private void Start()
    {
        if (_gameBalance == null || _playerMovement == null || _warningTexture == null || _enemySprite == null)
        {
            Debug.LogError("EdgeChaseWaveHost의 Inspector 참조를 채워 주세요.", this);
            enabled = false;
            return;
        }

        if (_itemSystem == null || !_itemSystem.Initialize(_gameBalance, this, Random.Range(0, int.MaxValue), _playerMovement.transform))
        {
            Debug.LogError("EdgeChaseWaveHost의 Item System 연결과 아이템 리소스를 확인해 주세요.", this);
            enabled = false;
            _playerMovement.enabled = false;
            return;
        }

        _roundScore = new RoundScore(_gameBalance);
        _waveDirector = new EnemyWaveDirector(_gameBalance, Random.Range(0, int.MaxValue));
        Rect warningRect = new Rect(0f, 0f, _warningTexture.width, _warningTexture.height);
        _warningSprite = Sprite.Create(_warningTexture, warningRect, Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
        _deathVisuals = new EnemyDeathVisuals(transform);

        _spawnPosition = _playerMovement.transform.position;
        _spawnRotation = _playerMovement.transform.rotation;
        RestartRound();
    }

    private void Update()
    {
        if (Keyboard.current?.rKey.wasPressedThisFrame == true)
            RestartRound();

        // Let cosmetic fragments finish after a lethal hit; gameplay remains stopped.
        _deathVisuals?.Tick(Time.deltaTime);
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
        _previousPlayerPosition = playerPosition;

        if (killCount > 0)
        {
            _roundScore.AddKills(killCount, _survivalSeconds);
            ScoreChanged?.Invoke(Score);
        }

        if (playerHit)
        {
            _roundInProgress = false;
            _playerMovement.enabled = false;
            Debug.Log($"플레이어 피격 ({_survivalSeconds:F2}초), 점수 {Score}. R 키로 다시 시작합니다.", this);
            return;
        }

        _waveDirector.TryStartWave(_survivalSeconds, this, out _);
    }

    public WavePlan BuildWavePlan(WaveRequest request)
    {
        if (request.Pattern is not EdgeChasePatternData pattern)
            return null;

        int edge = Random.Range(0, 4);
        bool verticalEdge = edge < 2;
        float side = edge % 2 == 0 ? -1f : 1f;
        Vector2 outward = verticalEdge ? Vector2.right * side : Vector2.up * side;
        Vector2 tangent = verticalEdge ? Vector2.up : Vector2.right;
        Vector2 halfSize = _gameBalance.ArenaSize * 0.5f;
        Vector2 limits = GetSpawnLimits();
        if (limits.x <= 0f || limits.y <= 0f || request.ChaseCount <= 0)
            return null;

        float edgeDistance = verticalEdge ? halfSize.x : halfSize.y;
        float edgeLength = (verticalEdge ? limits.y : limits.x) * 2f;
        float span = edgeLength * Mathf.Clamp01(pattern.EdgeCoverage);
        float segmentWidth = span / request.ChaseCount;
        float padding = halfSize.x - limits.x;
        float minimumInset = Mathf.Min(edgeDistance, Mathf.Max(padding, pattern.MinimumSpawnInset));
        float maximumInset = Mathf.Min(edgeDistance, Mathf.Max(minimumInset, pattern.MaximumSpawnInset));
        var placements = new EnemyPlacement[request.ChaseCount];

        for (int i = 0; i < placements.Length; i++)
        {
            float offset = -span * 0.5f + (i + Random.Range(0.2f, 0.8f)) * segmentWidth;
            float inset = Random.Range(minimumInset, maximumInset);
            Vector2 alongEdge = tangent * offset;
            Vector2 position = alongEdge + outward * (edgeDistance - inset);
            placements[i] = new EnemyPlacement(position, position);
        }

        return new WavePlan(placements, outward * edgeDistance, -outward);
    }

    public bool TryBeginWave(WaveState wave, int enemyCapacity)
    {
        if (HasWarning || ACTIVE_ENEMIES.Count + wave.Plan.EnemyCount > enemyCapacity)
            return false;

        Vector2 limits = GetSpawnLimits();
        foreach (EnemyPlacement placement in wave.Plan.Placements)
        {
            if (Mathf.Abs(placement.SpawnPosition.x) > limits.x || Mathf.Abs(placement.SpawnPosition.y) > limits.y
                || Mathf.Abs(placement.WarningPosition.x) > limits.x || Mathf.Abs(placement.WarningPosition.y) > limits.y)
                return false;
        }

        _warningWave = wave;
        foreach (EnemyPlacement placement in wave.Plan.Placements)
        {
            SpriteRenderer renderer = RentRenderer();
            ShowSprite(renderer, _warningSprite, placement.WarningPosition, WARNING_WIDTH);
            WARNING_RENDERERS.Add(renderer);
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

    private void RefreshWarningVisuals()
    {
        if (!HasWarning)
            return;

        float elapsed = Mathf.Max(0f, _survivalSeconds - _warningWave.WarningStartedAtSeconds);
        float duration = Mathf.Max(0.01f, _warningWave.Request.WarningDurationSeconds);
        float progress = Mathf.Clamp01(elapsed / duration);
        float startFrequency = 1f / Mathf.Max(0.01f, _spawnBlinkStartInterval);
        float endFrequency = 1f / Mathf.Max(0.01f, _spawnBlinkEndInterval);
        float cycles = startFrequency * elapsed + (endFrequency - startFrequency) * elapsed * progress * 0.5f;
        float pulse = (1f - Mathf.Cos(cycles * Mathf.PI * 2f)) * 0.5f;
        float alpha = Mathf.Lerp(0.18f, 1f, pulse) * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(elapsed / 0.12f));
        float scale = Mathf.Lerp(0.85f, 1f, progress) + pulse * 0.08f;

        for (int i = 0; i < WARNING_RENDERERS.Count; i++)
        {
            SpriteRenderer renderer = WARNING_RENDERERS[i];
            ShowSprite(renderer, _warningSprite, _warningWave.Plan.Placements[i].WarningPosition, WARNING_WIDTH * scale);
            renderer.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    private void ActivateWarning()
    {
        if (!HasWarning || !_warningWave.TryActivate(_survivalSeconds))
            return;

        float speed = _warningWave.Request.Stage.ChaseSpeed;

        for (int i = 0; i < WARNING_RENDERERS.Count; i++)
        {
            SpriteRenderer renderer = WARNING_RENDERERS[i];
            ShowSprite(renderer, _enemySprite, _warningWave.Plan.Placements[i].SpawnPosition, ENEMY_WIDTH);
            Vector2 spawnPosition = _warningWave.Plan.Placements[i].SpawnPosition;
            renderer.color = Color.white;
            ACTIVE_ENEMIES.Add(new ActiveEnemy { Id = ++_nextEnemyId, Renderer = renderer, PreviousPosition = spawnPosition, Speed = speed, ActivatedAtSeconds = _survivalSeconds });
        }

        WARNING_RENDERERS.Clear();
        _warningWave = null;
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
                enemy.Renderer.transform.position = Vector2.MoveTowards(enemy.PreviousPosition, playerPosition, enemy.Speed * deltaTime);
            }
            ACTIVE_ENEMIES[i] = enemy;
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

        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
        {
            if (!TouchesPlayer(enemy, playerPosition) || enemy.FrozenUntilSeconds > _survivalSeconds)
                continue;

            var hit = new PlayerHit(playerPosition, enemy.Renderer.transform.position, _survivalSeconds);
            if (!_itemSystem.TryBlockPlayerHit(hit))
                return true;
        }

        for (int i = ACTIVE_ENEMIES.Count - 1; i >= 0; i--)
        {
            ActiveEnemy enemy = ACTIVE_ENEMIES[i];
            if (enemy.FrozenUntilSeconds <= _survivalSeconds || !TouchesPlayer(enemy, playerPosition))
                continue;

            KillEnemy(i);
            shatterCount++;
        }

        return false;
    }

    private bool TouchesPlayer(ActiveEnemy enemy, Vector2 playerPosition)
    {
        Vector2 enemyPosition = enemy.Renderer.transform.position;
        Vector2 relativeStart = enemy.PreviousPosition - _previousPlayerPosition;
        Vector2 relativeEnd = enemyPosition - playerPosition;
        float radius = _gameBalance.PlayerRadius + _gameBalance.EnemyRadius;
        return ItemGeometry.DistanceToSegmentSquared(Vector2.zero, relativeStart, relativeEnd) <= radius * radius;
    }

    private void RefreshEnemyVisuals()
    {
        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
        {
            float remainingSeconds = enemy.FrozenUntilSeconds - _survivalSeconds;
            bool frozen = remainingSeconds > 0f;
            Sprite frozenSprite = _itemSystem.FrozenEnemySprite;
            Sprite sprite = frozen && frozenSprite != null ? frozenSprite : _enemySprite;
            float reveal = Mathf.Clamp01((_survivalSeconds - enemy.ActivatedAtSeconds) / Mathf.Max(0.01f, _spawnRevealSeconds));
            ShowSprite(enemy.Renderer, sprite, enemy.Renderer.transform.position, ENEMY_WIDTH * Mathf.Lerp(0.72f, 1f, reveal));
            Color color = frozen && frozenSprite == null ? Color.cyan : Color.white;
            if (!frozen)
                color *= Mathf.Lerp(1.7f, 1f, reveal);
            color.a = 1f;

            if (frozen && remainingSeconds <= _gameBalance.FreezeWarningSeconds)
                color.a = Mathf.Repeat(remainingSeconds, _gameBalance.ItemBlinkPeriodSeconds) < _gameBalance.ItemBlinkPeriodSeconds * 0.5f ? 0.3f : 1f;

            enemy.Renderer.color = color;
        }
    }

    private void KillEnemy(int index)
    {
        ActiveEnemy enemy = ACTIVE_ENEMIES[index];
        bool frozen = enemy.FrozenUntilSeconds > _survivalSeconds;
        Sprite deathSprite = frozen && _itemSystem.FrozenEnemySprite != null ? _itemSystem.FrozenEnemySprite : _enemySprite;
        _deathVisuals?.Play(enemy.Renderer.transform.position, deathSprite, ENEMY_WIDTH, frozen,
            _deathShardCount, _deathDurationSeconds, _deathShardSpeed);
        ReturnRenderer(enemy.Renderer);
        ACTIVE_ENEMIES.RemoveAt(index);
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
            foreach (EnemyPlacement placement in _warningWave.Plan.Placements)
            {
                if ((position - placement.WarningPosition).sqrMagnitude < distanceSquared
                    || (position - placement.SpawnPosition).sqrMagnitude < distanceSquared)
                    return false;
            }
        }

        return true;
    }

    public void RestartRound()
    {
        if (_waveDirector == null)
            return;

        _playerMovement.enabled = false;
        _playerMovement.transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        _playerMovement.ClearTrail();
        _previousPlayerPosition = _spawnPosition;

        foreach (SpriteRenderer renderer in WARNING_RENDERERS)
            ReturnRenderer(renderer);

        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
            ReturnRenderer(enemy.Renderer);

        WARNING_RENDERERS.Clear();
        ACTIVE_ENEMIES.Clear();
        _nextEnemyId = 0;
        _warningWave?.Finish();
        _warningWave = null;

        _waveDirector.Reset(Random.Range(0, int.MaxValue));
        _survivalSeconds = 0f;
        _readyRemainingSeconds = READY_SECONDS;
        _roundInProgress = true;

        _itemSystem.ResetRound(Random.Range(0, int.MaxValue));
        _deathVisuals?.Reset();
        if (_shatterAudio != null)
            _shatterAudio.Stop();
        _roundScore.Reset();
        ScoreChanged?.Invoke(Score);
    }

    private SpriteRenderer RentRenderer()
    {
        if (FREE_RENDERERS.Count > 0)
            return FREE_RENDERERS.Pop();

        var enemyObj = new GameObject("Chase Enemy", typeof(SpriteRenderer));
        enemyObj.transform.SetParent(transform, false);
        SpriteRenderer renderer = enemyObj.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = 1;
        return renderer;
    }

    private void ReturnRenderer(SpriteRenderer renderer)
    {
        renderer.enabled = false;
        renderer.color = Color.white;
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
    }
}
