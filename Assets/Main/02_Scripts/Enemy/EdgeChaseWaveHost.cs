using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public sealed class EdgeChaseWaveHost : MonoBehaviour, IEnemyWaveHost
{
    private const float READY_SECONDS = 3f;
    private const float WARNING_WIDTH = 0.64f;
    private const float ENEMY_WIDTH = 0.36f;

    [SerializeField] private GameBalance _gameBalance;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private Texture2D _warningTexture;
    [SerializeField] private Sprite _enemySprite;

    private readonly List<SpriteRenderer> WARNING_RENDERERS = new();
    private readonly List<ActiveEnemy> ACTIVE_ENEMIES = new();
    private readonly Stack<SpriteRenderer> FREE_RENDERERS = new();

    private struct ActiveEnemy
    {
        public SpriteRenderer Renderer;
        public float Speed;
        public float ExpiresAtSeconds;
    }

    private EnemyWaveDirector _waveDirector;
    private WaveState _warningWave;
    private Sprite _warningSprite;
    private Vector3 _spawnPosition;
    private Quaternion _spawnRotation;
    private Vector2 _previousPlayerPosition;
    private float _readyRemainingSeconds;
    private float _survivalSeconds;
    private bool _roundInProgress;

    public bool HasWarning => _warningWave != null;
    public bool HasFormation => false;

    private void Start()
    {
        if (_gameBalance == null || _playerMovement == null || _warningTexture == null || _enemySprite == null)
        {
            Debug.LogError("EdgeChaseWaveHost의 Inspector 참조를 채워 주세요.", this);
            enabled = false;
            return;
        }

        _waveDirector = new EnemyWaveDirector(_gameBalance, Random.Range(0, int.MaxValue));
        Rect warningRect = new Rect(0f, 0f, _warningTexture.width, _warningTexture.height);
        _warningSprite = Sprite.Create(_warningTexture, warningRect, Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);

        _spawnPosition = _playerMovement.transform.position;
        _spawnRotation = _playerMovement.transform.rotation;
        RestartRound();
    }

    private void Update()
    {
        if (Keyboard.current?.rKey.wasPressedThisFrame == true)
            RestartRound();
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
        bool playerHit = TickActiveEnemies(playerPosition, deltaTime);
        _previousPlayerPosition = playerPosition;

        if (playerHit)
        {
            _roundInProgress = false;
            _playerMovement.enabled = false;
            Debug.Log($"플레이어 피격 ({_survivalSeconds:F2}초). R 키로 다시 시작합니다.", this);
            return;
        }

        ActivateWarning();
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
        float edgeDistance = verticalEdge ? halfSize.x : halfSize.y;
        float edgeLength = verticalEdge ? _gameBalance.ArenaSize.y : _gameBalance.ArenaSize.x;
        float span = edgeLength * pattern.EdgeCoverage;
        float segmentWidth = span / request.ChaseCount;
        var placements = new EnemyPlacement[request.ChaseCount];

        for (int i = 0; i < placements.Length; i++)
        {
            float offset = -span * 0.5f + (i + Random.Range(0.2f, 0.8f)) * segmentWidth;
            float outsideDistance = Random.Range(pattern.MinOutSideDistance, pattern.MaxOutSideDistance);
            Vector2 alongEdge = tangent * offset;
            Vector2 warningPosition = alongEdge + outward * (edgeDistance - WARNING_WIDTH * 0.5f);
            Vector2 spawnPosition = alongEdge + outward * (edgeDistance + outsideDistance);
            placements[i] = new EnemyPlacement(warningPosition, spawnPosition);
        }

        return new WavePlan(placements, outward * edgeDistance, -outward);
    }

    public bool TryBeginWave(WaveState wave, int enemyCapacity)
    {
        if (HasWarning || ACTIVE_ENEMIES.Count + wave.Plan.EnemyCount > enemyCapacity)
            return false;

        _warningWave = wave;
        foreach (EnemyPlacement placement in wave.Plan.Placements)
        {
            SpriteRenderer renderer = RentRenderer();
            ShowSprite(renderer, _warningSprite, placement.WarningPosition, WARNING_WIDTH);
            WARNING_RENDERERS.Add(renderer);
        }

        return true;
    }

    private void ActivateWarning()
    {
        if (!HasWarning || !_warningWave.TryActivate(_survivalSeconds))
            return;

        float speed = _warningWave.Request.Stage.ChaseSpeed;
        float expiresAtSeconds = _survivalSeconds + _gameBalance.EnemyLifetimeSeconds;

        for (int i = 0; i < WARNING_RENDERERS.Count; i++)
        {
            SpriteRenderer renderer = WARNING_RENDERERS[i];
            ShowSprite(renderer, _enemySprite, _warningWave.Plan.Placements[i].SpawnPosition, ENEMY_WIDTH);
            ACTIVE_ENEMIES.Add(new ActiveEnemy { Renderer = renderer, Speed = speed, ExpiresAtSeconds = expiresAtSeconds });
        }

        WARNING_RENDERERS.Clear();
        _warningWave = null;
    }

    private bool TickActiveEnemies(Vector2 playerPosition, float deltaTime)
    {
        float contactRadius = _gameBalance.PlayerRadius + _gameBalance.EnemyRadius;
        bool playerHit = false;

        for (int i = ACTIVE_ENEMIES.Count - 1; i >= 0; i--)
        {
            ActiveEnemy enemy = ACTIVE_ENEMIES[i];
            if (_survivalSeconds >= enemy.ExpiresAtSeconds)
            {
                ReturnRenderer(enemy.Renderer);
                ACTIVE_ENEMIES.RemoveAt(i);
                continue;
            }

            Vector2 previousEnemyPosition = enemy.Renderer.transform.position;
            Vector2 nextEnemyPosition = Vector2.MoveTowards(previousEnemyPosition, playerPosition, enemy.Speed * deltaTime);
            enemy.Renderer.transform.position = nextEnemyPosition;

            Vector2 relativeStart = previousEnemyPosition - _previousPlayerPosition;
            Vector2 relativeEnd = nextEnemyPosition - playerPosition;
            Vector2 relativeTravel = relativeEnd - relativeStart;
            float travelSquared = relativeTravel.sqrMagnitude;
            float closestTime = travelSquared > 0f ? Mathf.Clamp01(-Vector2.Dot(relativeStart, relativeTravel) / travelSquared) : 0f;
            Vector2 closestOffset = relativeStart + relativeTravel * closestTime;
            playerHit |= closestOffset.sqrMagnitude <= contactRadius * contactRadius;
        }

        return playerHit;
    }

    public void RestartRound()
    {
        if (_waveDirector == null)
            return;

        _playerMovement.enabled = false;
        _playerMovement.transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        _previousPlayerPosition = _spawnPosition;

        foreach (SpriteRenderer renderer in WARNING_RENDERERS)
            ReturnRenderer(renderer);

        foreach (ActiveEnemy enemy in ACTIVE_ENEMIES)
            ReturnRenderer(enemy.Renderer);

        WARNING_RENDERERS.Clear();
        ACTIVE_ENEMIES.Clear();
        _warningWave?.Finish();
        _warningWave = null;

        _waveDirector.Reset(Random.Range(0, int.MaxValue));
        _survivalSeconds = 0f;
        _readyRemainingSeconds = READY_SECONDS;
        _roundInProgress = true;
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
        if (_warningSprite != null)
            Destroy(_warningSprite);
    }
}
