using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class EnemySpawnPatternPrototype : MonoBehaviour
{
    private const float ARENA_HALF_WIDTH = 8f;
    private const float ARENA_HALF_HEIGHT = 4.5f;
    private const float PLAYER_RADIUS = 0.25f;
    private const float ENEMY_RADIUS = 0.12f;
    private const float PLAYER_SPEED = 6f;
    private const float CHASE_SPEED = 1.8f;
    private const float WALL_SPEED = 2.7f;
    private const float DASH_SPEED = 8f;
    private const float WALL_GAP_WIDTH = 1.5f;
    private const float WALL_MAX_SPACING = 0.68f;
    private const float CIRCLE_RADIUS = 2.5f;
    private const float CIRCLE_OPEN_ANGLE = 90f;
    private const int CHASE_ENEMY_COUNT = 6;
    private const int CIRCLE_ENEMY_COUNT = 8;
    private const float WARNING_DURATION = 1.5f;
    private const float ARROW_GATHER_DURATION = 0.8f;
    private const float ARROW_TURN_DURATION = 0.25f;
    private const float ARROW_HOLD_DURATION = 0.55f;
    private const float CHASE_LEAD_TIME = 2f;
    private const int INTERCEPTOR_COUNT = 4;
    private const float INTERCEPT_DELAY = 1.5f;
    private const float INTERCEPT_STAGGER = 0.75f;
    private const float INTERCEPT_LOOKAHEAD = 0.5f;
    private const float INTERCEPT_VELOCITY_SMOOTH_TIME = 0.12f;
    private const float INTERCEPT_WARNING_DURATION = 0.45f;
    private const float INTERCEPT_SPEED = 7f;
    private const float INTERCEPT_COOLDOWN = 1.25f;
    private const float SURVIVAL_DURATION = 10f;
    private const float ENEMY_SPRITE_WIDTH = 0.36f;
    private static readonly Color INTERCEPT_COLOR = new Color32(255, 186, 76, 255);

    // Positions are local to the arrow, with +X pointing toward its tip.
    private static readonly Vector2[] ARROW_FORMATION_OFFSETS =
    {
        new Vector2(1.16f, 0f),
        new Vector2(0.58f, 0.46f), new Vector2(0.58f, 0f), new Vector2(0.58f, -0.46f),
        new Vector2(0f, 0.92f), new Vector2(0f, 0f), new Vector2(0f, -0.92f),
        new Vector2(-0.58f, 0f),
        new Vector2(-1.16f, 0f)
    };

    private static readonly Vector2[] ARROW_SCATTER_OFFSETS =
    {
        new Vector2(1.7f, 0.45f),
        new Vector2(0.9f, 1.27f), new Vector2(0.46f, -0.34f), new Vector2(0.82f, -1.35f),
        new Vector2(-0.15f, 2.1f), new Vector2(-0.38f, 0.53f), new Vector2(-0.14f, -2.02f),
        new Vector2(-1.05f, -0.69f),
        new Vector2(-1.72f, 0.27f)
    };

    [SerializeField] private Camera _previewCamera;
    [SerializeField] private Sprite _enemySprite;
    [SerializeField] private Texture2D _warningTexture;
    [SerializeField] private Sprite _playerSprite;
    [SerializeField] private Text _statusTxt;

    private readonly List<PreviewEnemy> ENEMIES = new List<PreviewEnemy>();
    private readonly PreviewInterceptor[] INTERCEPTORS = new PreviewInterceptor[INTERCEPTOR_COUNT];
    private Sprite _warningSprite;
    private Sprite _squareSprite;
    private SpriteRenderer _playerRenderer;
    private Vector2 _playerPosition;
    private Vector2 _playerVelocity;
    private Vector2 _arrowCenter;
    private float _arrowInitialAngle;
    private float _arrowAimAngle;
    private ARROW_DASH_PHASE _arrowDashPhase;
    private string _patternName;
    private int _spawnCount;
    private int _activeCount;
    private float _warningRemaining;
    private float _wallDelay;
    private float _combinedWallSide;
    private float _survivalRemaining;
    private int _displayedSeconds;
    private bool _wasHit;
    private Key _heldPatternKey;

    private enum ARROW_DASH_PHASE
    {
        NONE,
        GATHERING,
        TURNING,
        HOLDING
    }

    private enum INTERCEPT_PHASE
    {
        NONE,
        WAITING,
        WARNING,
        MOVING,
        COOLDOWN
    }

    private sealed class PreviewEnemy
    {
        public SpriteRenderer Renderer { get; }
        public Vector2 SpawnPosition { get; set; }
        public Vector2 Velocity { get; set; }

        public PreviewEnemy(SpriteRenderer renderer)
        {
            Renderer = renderer;
        }
    }

    private sealed class PreviewInterceptor
    {
        public SpriteRenderer Renderer { get; }
        public SpriteRenderer PathRenderer { get; }
        public SpriteRenderer TargetRenderer { get; }
        public Vector2 Target { get; set; }
        public INTERCEPT_PHASE Phase { get; set; }
        public float Remaining { get; set; }

        public PreviewInterceptor(SpriteRenderer renderer, SpriteRenderer pathRenderer, SpriteRenderer targetRenderer)
        {
            Renderer = renderer;
            PathRenderer = pathRenderer;
            TargetRenderer = targetRenderer;
        }

        public void Reset()
        {
            Phase = INTERCEPT_PHASE.NONE;
            Remaining = 0f;
            Renderer.enabled = false;
            PathRenderer.enabled = false;
            TargetRenderer.enabled = false;
        }
    }

    private void Awake()
    {
        // The warning texture is sliced into individual dashes in the sprite importer.
        Rect warningRect = new Rect(0f, 0f, _warningTexture.width, _warningTexture.height);
        _warningSprite = Sprite.Create(_warningTexture, warningRect, Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
        _warningSprite.name = _warningTexture.name;
        _playerRenderer = CreateRenderer("Player", 2);
        SetSprite(_playerRenderer, _playerSprite, 0.7f);

        Texture2D whiteTexture = Texture2D.whiteTexture;
        _squareSprite = Sprite.Create(whiteTexture, new Rect(0f, 0f, whiteTexture.width, whiteTexture.height), Vector2.one * 0.5f, whiteTexture.width);
        for (int i = 0; i < INTERCEPTORS.Length; i++)
            INTERCEPTORS[i] = CreateInterceptor(i + 1);
    }

    private PreviewInterceptor CreateInterceptor(int enemyNumber)
    {
        Quaternion diamondRotation = Quaternion.Euler(0f, 0f, 45f);
        SpriteRenderer renderer = CreateRenderer($"Interceptor {enemyNumber}", 1);
        SetSprite(renderer, _squareSprite, ENEMY_RADIUS * 2f);
        renderer.transform.localRotation = diamondRotation;
        renderer.color = INTERCEPT_COLOR;

        SpriteRenderer pathRenderer = CreateRenderer($"Interceptor {enemyNumber} path", 0);
        pathRenderer.sprite = _squareSprite;
        pathRenderer.color = new Color(INTERCEPT_COLOR.r, INTERCEPT_COLOR.g, INTERCEPT_COLOR.b, 0.6f);

        SpriteRenderer targetRenderer = CreateRenderer($"Interceptor {enemyNumber} target", 0);
        SetSprite(targetRenderer, _squareSprite, 0.48f);
        targetRenderer.transform.localRotation = diamondRotation;
        targetRenderer.color = pathRenderer.color;

        var interceptor = new PreviewInterceptor(renderer, pathRenderer, targetRenderer);
        interceptor.Reset();
        return interceptor;
    }

    private void OnDestroy()
    {
        Destroy(_warningSprite);
        Destroy(_squareSprite);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Key key = Key.None;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.isPressed)
                key = Key.Digit1;
            else if (keyboard.digit2Key.isPressed)
                key = Key.Digit2;
            else if (keyboard.digit3Key.isPressed)
                key = Key.Digit3;
            else if (keyboard.digit4Key.isPressed)
                key = Key.Digit4;
            else if (keyboard.digit5Key.isPressed)
                key = Key.Digit5;
            else if (keyboard.digit6Key.isPressed)
                key = Key.Digit6;
        }

        if (key == _heldPatternKey)
            return;

        _heldPatternKey = key;
        if (key == Key.Digit1)
            OnChaseSpawnActioned();
        else if (key == Key.Digit2)
            OnLineSpawnActioned();
        else if (key == Key.Digit3)
            OnCircleSpawnActioned();
        else if (key == Key.Digit4)
            OnCombinedSpawnActioned();
        else if (key == Key.Digit5)
            OnDashSpawnActioned();
        else if (key == Key.Digit6)
            OnInterceptSpawnActioned();
    }

    private void LateUpdate()
    {
        const float VIEW_PADDING = 0.35f;
        const float TOOLBAR_HEIGHT = 1.2f;
        float height = ARENA_HALF_HEIGHT + VIEW_PADDING + TOOLBAR_HEIGHT * 0.5f;
        float width = (ARENA_HALF_WIDTH + VIEW_PADDING) / _previewCamera.aspect;
        _previewCamera.orthographicSize = Mathf.Max(height, width);
    }

    private void FixedUpdate()
    {
        if (_wasHit)
            return;

        float deltaTime = Time.fixedDeltaTime;
        Vector2 previousPlayerPosition = _playerPosition;
        MovePlayer(deltaTime);
        Vector2 measuredVelocity = (_playerPosition - previousPlayerPosition) / deltaTime;
        _playerVelocity = Vector2.Lerp(_playerVelocity, measuredVelocity, 1f - Mathf.Exp(-deltaTime / INTERCEPT_VELOCITY_SMOOTH_TIME));
        if (_spawnCount == 0)
            return;

        if (!AdvanceEnemies(previousPlayerPosition, deltaTime, out bool hasActiveEnemies)
            || (_activeCount > 0 && !AdvanceInterceptors(previousPlayerPosition, deltaTime)))
        {
            _wasHit = true;
            _playerRenderer.color = new Color32(255, 125, 103, 255);
            _statusTxt.text = "Hit / choose a pattern to retry";
            return;
        }

        if (_warningRemaining > 0f)
        {
            _warningRemaining = Mathf.Max(0f, _warningRemaining - deltaTime);
            if (_arrowDashPhase != ARROW_DASH_PHASE.NONE)
                AdvanceArrowWarning();
            else if (_warningRemaining == 0f)
                ActivateWarnings();
            return;
        }

        if (_wallDelay > 0f)
        {
            _wallDelay = Mathf.Max(0f, _wallDelay - deltaTime);
            if (_wallDelay == 0f)
            {
                QueueWall(_combinedWallSide);
                StartWarning();
                return;
            }
        }

        _survivalRemaining -= deltaTime;
        if (!hasActiveEnemies || _survivalRemaining <= 0f)
        {
            ClearEnemies();
            _statusTxt.text = "Survived / choose another pattern";
            return;
        }

        int seconds = Mathf.CeilToInt(_survivalRemaining);
        if (seconds != _displayedSeconds)
        {
            _displayedSeconds = seconds;
            _statusTxt.text = $"{_patternName} / {seconds}s";
        }
    }

    public void OnChaseSpawnActioned()
    {
        BeginTrial("Edge chase");
        QueueChase(Random.Range(0, 4));
        StartWarning();
    }

    public void OnLineSpawnActioned()
    {
        BeginTrial("Wall");
        QueueWall(_playerPosition.x > 0f ? -1f : 1f);
        StartWarning();
    }

    public void OnCircleSpawnActioned()
    {
        BeginTrial("Encircle");
        if (!TryQueueCircle())
        {
            _patternName = "Edge chase (near boundary)";
            QueueChase(Random.Range(0, 4));
        }

        StartWarning();
    }

    public void OnCombinedSpawnActioned()
    {
        BeginTrial("Chase + wall");
        int chaseEdge = Random.Range(0, 2);
        QueueChase(chaseEdge);
        _combinedWallSide = chaseEdge == 0 ? 1f : -1f;
        _wallDelay = CHASE_LEAD_TIME;
        StartWarning();
    }

    public void OnDashSpawnActioned()
    {
        BeginTrial("Arrow dash");
        float side = _playerPosition.x > 0f ? -1f : 1f;
        _arrowCenter = new Vector2(side * (ARENA_HALF_WIDTH - 2.5f), Random.Range(-1f, 1f));
        _arrowInitialAngle = side > 0f ? 180f : 0f;
        Vector2 inward = Vector2.left * side;
        for (int i = 0; i < ARROW_FORMATION_OFFSETS.Length; i++)
        {
            Vector2 position = GetArrowSlotPosition(ARROW_SCATTER_OFFSETS[i], inward);
            QueueEnemy(position, position, Vector2.zero);
            SpriteRenderer renderer = ENEMIES[i].Renderer;
            SetSprite(renderer, _enemySprite, ENEMY_SPRITE_WIDTH);
            renderer.color = new Color32(255, 255, 255, 186);
        }

        _arrowDashPhase = ARROW_DASH_PHASE.GATHERING;
        _warningRemaining = ARROW_GATHER_DURATION;
        _statusTxt.text = "Arrow dash / forming";
    }

    public void OnInterceptSpawnActioned()
    {
        BeginTrial($"Chase + {INTERCEPTORS.Length} interceptors");
        QueueChase(Random.Range(0, 4), CHASE_ENEMY_COUNT - 1);
        for (int i = 0; i < INTERCEPTORS.Length; i++)
        {
            INTERCEPTORS[i].Phase = INTERCEPT_PHASE.WAITING;
            INTERCEPTORS[i].Remaining = INTERCEPT_DELAY + i * INTERCEPT_STAGGER;
        }

        StartWarning();
    }

    private void BeginTrial(string patternName)
    {
        ClearEnemies();
        _patternName = patternName;
        _playerVelocity = Vector2.zero;
        if (_wasHit)
        {
            _playerPosition = Vector2.zero;
            _playerRenderer.transform.localPosition = Vector3.zero;
            _playerRenderer.transform.localRotation = Quaternion.identity;
            _playerRenderer.color = Color.white;
            _wasHit = false;
        }
    }

    private void MovePlayer(float deltaTime)
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector3 worldPosition = _previewCamera.ScreenToWorldPoint(mouse.position.ReadValue());
        Vector2 target = transform.InverseTransformPoint(worldPosition);
        if (!IsInsideArena(target, 0f))
            return;

        target = ClampToArena(target, PLAYER_RADIUS);
        Vector2 nextPosition = Vector2.MoveTowards(_playerPosition, target, PLAYER_SPEED * deltaTime);
        Vector2 movement = nextPosition - _playerPosition;
        if (movement.sqrMagnitude > 0.000001f)
            _playerRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg - 90f);

        _playerPosition = nextPosition;
        _playerRenderer.transform.localPosition = _playerPosition;
    }

    private void QueueChase(int edge, int enemyCount = CHASE_ENEMY_COUNT)
    {
        bool verticalEdge = edge < 2;
        float side = edge % 2 == 0 ? -1f : 1f;
        float edgeDistance = verticalEdge ? ARENA_HALF_WIDTH : ARENA_HALF_HEIGHT;
        float span = (verticalEdge ? ARENA_HALF_HEIGHT : ARENA_HALF_WIDTH) * 1.6f;
        float segmentWidth = span / enemyCount;
        Vector2 outward = verticalEdge ? new Vector2(side, 0f) : new Vector2(0f, side);
        Vector2 tangent = verticalEdge ? Vector2.up : Vector2.right;

        for (int i = 0; i < enemyCount; i++)
        {
            float offset = -span * 0.5f + (i + Random.Range(0.2f, 0.8f)) * segmentWidth;
            Vector2 alongEdge = tangent * offset;
            Vector2 spawnPosition = outward * (edgeDistance + Random.Range(0.2f, 0.75f)) + alongEdge;
            Vector2 warningPosition = outward * (edgeDistance - 0.25f) + alongEdge;
            QueueEnemy(spawnPosition, warningPosition, Vector2.zero);
        }
    }

    private void StartInterceptWarning(PreviewInterceptor interceptor)
    {
        interceptor.Target = ClampToArena(_playerPosition + _playerVelocity * INTERCEPT_LOOKAHEAD, PLAYER_RADIUS);

        Vector2 startPosition = interceptor.Renderer.transform.localPosition;
        if (!interceptor.Renderer.enabled)
        {
            // Start at the nearest edge on the first attempt. Later attempts start where the enemy stopped.
            startPosition = interceptor.Target;
            float distanceToSide = ARENA_HALF_WIDTH - Mathf.Abs(startPosition.x);
            float distanceToTopOrBottom = ARENA_HALF_HEIGHT - Mathf.Abs(startPosition.y);
            if (distanceToSide < distanceToTopOrBottom)
                startPosition.x = Mathf.Sign(startPosition.x) * (ARENA_HALF_WIDTH + ENEMY_RADIUS);
            else
                startPosition.y = Mathf.Sign(startPosition.y) * (ARENA_HALF_HEIGHT + ENEMY_RADIUS);

            interceptor.Renderer.transform.localPosition = startPosition;
        }

        Vector2 path = interceptor.Target - startPosition;
        Transform pathTrans = interceptor.PathRenderer.transform;
        pathTrans.localPosition = (startPosition + interceptor.Target) * 0.5f;
        pathTrans.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(path.y, path.x) * Mathf.Rad2Deg);
        pathTrans.localScale = new Vector3(path.magnitude, 0.06f, 1f);
        interceptor.PathRenderer.enabled = true;
        interceptor.TargetRenderer.transform.localPosition = interceptor.Target;
        interceptor.TargetRenderer.enabled = true;
        interceptor.Phase = INTERCEPT_PHASE.WARNING;
        interceptor.Remaining = INTERCEPT_WARNING_DURATION;
    }

    private bool AdvanceInterceptors(Vector2 previousPlayerPosition, float deltaTime)
    {
        foreach (PreviewInterceptor interceptor in INTERCEPTORS)
        {
            if (!AdvanceInterceptor(interceptor, previousPlayerPosition, deltaTime))
                return false;
        }

        return true;
    }

    private bool AdvanceInterceptor(PreviewInterceptor interceptor, Vector2 previousPlayerPosition, float deltaTime)
    {
        if (interceptor.Phase == INTERCEPT_PHASE.NONE)
            return true;

        Vector2 previousPosition = interceptor.Renderer.transform.localPosition;
        if (interceptor.Phase != INTERCEPT_PHASE.MOVING)
        {
            if (interceptor.Renderer.enabled && TouchesPlayer(previousPlayerPosition, _playerPosition, previousPosition, previousPosition))
                return false;

            interceptor.Remaining -= deltaTime;
            if (interceptor.Remaining > 0f)
                return true;

            if (interceptor.Phase == INTERCEPT_PHASE.WARNING)
            {
                interceptor.PathRenderer.enabled = false;
                interceptor.Renderer.enabled = true;
                interceptor.Phase = INTERCEPT_PHASE.MOVING;
            }
            else
            {
                StartInterceptWarning(interceptor);
            }

            return true;
        }

        Vector2 nextPosition = Vector2.MoveTowards(previousPosition, interceptor.Target, INTERCEPT_SPEED * deltaTime);
        interceptor.Renderer.transform.localPosition = nextPosition;
        if (nextPosition.Equals(interceptor.Target))
        {
            interceptor.TargetRenderer.enabled = false;
            interceptor.Phase = INTERCEPT_PHASE.COOLDOWN;
            interceptor.Remaining = INTERCEPT_COOLDOWN;
        }

        return !TouchesPlayer(previousPlayerPosition, _playerPosition, previousPosition, nextPosition);
    }

    private void QueueWall(float side)
    {
        float gapCenter = Random.Range(-1, 2) * 1.75f;
        float gapHalfSpacing = WALL_GAP_WIDTH * 0.5f + ENEMY_RADIUS;
        QueueWallSegment(side, -ARENA_HALF_HEIGHT + ENEMY_RADIUS, gapCenter - gapHalfSpacing);
        QueueWallSegment(side, gapCenter + gapHalfSpacing, ARENA_HALF_HEIGHT - ENEMY_RADIUS);
    }

    private void QueueWallSegment(float side, float minY, float maxY)
    {
        int intervals = Mathf.CeilToInt((maxY - minY) / WALL_MAX_SPACING);
        for (int i = 0; i <= intervals; i++)
        {
            float y = Mathf.Lerp(minY, maxY, i / (float)intervals);
            Vector2 spawnPosition = new Vector2(side * (ARENA_HALF_WIDTH + ENEMY_RADIUS), y);
            Vector2 warningPosition = new Vector2(side * (ARENA_HALF_WIDTH - 0.25f), y);
            QueueEnemy(spawnPosition, warningPosition, Vector2.left * (side * WALL_SPEED));
        }
    }

    private bool TryQueueCircle()
    {
        Vector2 center = _playerPosition;
        Vector2 exitDirection = center.sqrMagnitude > 0.01f ? -center.normalized : Vector2.right;
        float gapDirection = Mathf.Atan2(exitDirection.y, exitDirection.x) * Mathf.Rad2Deg;
        if (!IsInsideArena(center + exitDirection * CIRCLE_RADIUS, PLAYER_RADIUS))
            return false;

        Vector2[] positions = new Vector2[CIRCLE_ENEMY_COUNT];
        for (int i = 0; i < positions.Length; i++)
        {
            float angle = gapDirection + Mathf.Lerp(CIRCLE_OPEN_ANGLE * 0.5f, 360f - CIRCLE_OPEN_ANGLE * 0.5f, i / (float)(positions.Length - 1));
            angle *= Mathf.Deg2Rad;
            positions[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * CIRCLE_RADIUS;
            if (!IsInsideArena(positions[i], ENEMY_RADIUS))
                return false;
        }

        foreach (Vector2 position in positions)
            QueueEnemy(position, position, Vector2.zero);
        return true;
    }

    private void AdvanceArrowWarning()
    {
        if (_arrowDashPhase == ARROW_DASH_PHASE.GATHERING)
        {
            float progress = Mathf.SmoothStep(0f, 1f, 1f - _warningRemaining / ARROW_GATHER_DURATION);
            PlaceArrowFormation(_arrowInitialAngle, progress);
            if (_warningRemaining > 0f)
                return;

            Vector2 aim = _playerPosition - _arrowCenter;
            _arrowAimAngle = aim.sqrMagnitude > 0.01f ? Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg : _arrowInitialAngle;
            _arrowDashPhase = ARROW_DASH_PHASE.TURNING;
            _warningRemaining = ARROW_TURN_DURATION;
            _statusTxt.text = "Arrow dash / aiming";
            return;
        }

        if (_arrowDashPhase == ARROW_DASH_PHASE.TURNING)
        {
            float progress = Mathf.SmoothStep(0f, 1f, 1f - _warningRemaining / ARROW_TURN_DURATION);
            PlaceArrowFormation(Mathf.LerpAngle(_arrowInitialAngle, _arrowAimAngle, progress), 1f);
            if (_warningRemaining > 0f)
                return;

            _arrowDashPhase = ARROW_DASH_PHASE.HOLDING;
            _warningRemaining = ARROW_HOLD_DURATION;
            _statusTxt.text = "Arrow dash / ready";
            return;
        }

        if (_warningRemaining > 0f)
            return;

        Vector2 velocity = DirectionFromAngle(_arrowAimAngle) * DASH_SPEED;
        for (int i = 0; i < ARROW_FORMATION_OFFSETS.Length; i++)
        {
            PreviewEnemy enemy = ENEMIES[i];
            enemy.SpawnPosition = enemy.Renderer.transform.localPosition;
            enemy.Velocity = velocity;
        }

        _arrowDashPhase = ARROW_DASH_PHASE.NONE;
        ActivateWarnings();
    }

    private void PlaceArrowFormation(float angle, float gatherProgress)
    {
        Vector2 direction = DirectionFromAngle(angle);
        for (int i = 0; i < ARROW_FORMATION_OFFSETS.Length; i++)
        {
            Vector2 offset = Vector2.Lerp(ARROW_SCATTER_OFFSETS[i], ARROW_FORMATION_OFFSETS[i], gatherProgress);
            ENEMIES[i].Renderer.transform.localPosition = GetArrowSlotPosition(offset, direction);
        }
    }

    private Vector2 GetArrowSlotPosition(Vector2 offset, Vector2 direction)
    {
        return _arrowCenter + direction * offset.x + Vector2.Perpendicular(direction) * offset.y;
    }

    private static Vector2 DirectionFromAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private void StartWarning()
    {
        _warningRemaining = WARNING_DURATION;
        _statusTxt.text = $"{_patternName} / warning";
    }

    private void ActivateWarnings()
    {
        for (int i = _activeCount; i < _spawnCount; i++)
        {
            PreviewEnemy enemy = ENEMIES[i];
            enemy.Renderer.transform.localPosition = enemy.SpawnPosition;
            SetSprite(enemy.Renderer, _enemySprite, ENEMY_SPRITE_WIDTH);
            enemy.Renderer.color = Color.white;
        }

        _activeCount = _spawnCount;
        _survivalRemaining = SURVIVAL_DURATION;
        _displayedSeconds = (int)SURVIVAL_DURATION;
        _statusTxt.text = $"{_patternName} / {_displayedSeconds}s";
    }

    private bool AdvanceEnemies(Vector2 previousPlayerPosition, float deltaTime, out bool hasActiveEnemies)
    {
        hasActiveEnemies = false;
        bool hitPlayer = false;
        for (int i = 0; i < _activeCount; i++)
        {
            PreviewEnemy enemy = ENEMIES[i];
            if (!enemy.Renderer.enabled)
                continue;

            Transform enemyTrans = enemy.Renderer.transform;
            Vector2 previousPosition = enemyTrans.localPosition;
            Vector2 nextPosition = enemy.Velocity == Vector2.zero
                ? Vector2.MoveTowards(previousPosition, _playerPosition, CHASE_SPEED * deltaTime)
                : previousPosition + enemy.Velocity * deltaTime;
            enemyTrans.localPosition = nextPosition;

            hitPlayer |= TouchesPlayer(previousPlayerPosition, _playerPosition, previousPosition, nextPosition);

            bool passedX = enemy.Velocity.x * nextPosition.x > 0f && Mathf.Abs(nextPosition.x) > ARENA_HALF_WIDTH + ENEMY_RADIUS;
            bool passedY = enemy.Velocity.y * nextPosition.y > 0f && Mathf.Abs(nextPosition.y) > ARENA_HALF_HEIGHT + ENEMY_RADIUS;
            bool passedArena = passedX || passedY;
            enemy.Renderer.enabled = !passedArena;
            hasActiveEnemies |= !passedArena;
        }

        return !hitPlayer;
    }

    private static bool TouchesPlayer(Vector2 playerStart, Vector2 playerEnd, Vector2 enemyStart, Vector2 enemyEnd)
    {
        Vector2 relativeStart = enemyStart - playerStart;
        Vector2 relativeTravel = (enemyEnd - playerEnd) - relativeStart;
        float travelSquared = relativeTravel.sqrMagnitude;
        float closestTime = travelSquared > 0f ? Mathf.Clamp01(-Vector2.Dot(relativeStart, relativeTravel) / travelSquared) : 0f;
        float contactRadius = PLAYER_RADIUS + ENEMY_RADIUS;
        return (relativeStart + relativeTravel * closestTime).sqrMagnitude <= contactRadius * contactRadius;
    }

    private void QueueEnemy(Vector2 spawnPosition, Vector2 warningPosition, Vector2 velocity)
    {
        if (_spawnCount == ENEMIES.Count)
            ENEMIES.Add(new PreviewEnemy(CreateRenderer($"Enemy {_spawnCount + 1}", 1)));

        PreviewEnemy enemy = ENEMIES[_spawnCount++];
        enemy.SpawnPosition = spawnPosition;
        enemy.Velocity = velocity;
        enemy.Renderer.transform.localPosition = warningPosition;
        SetSprite(enemy.Renderer, _warningSprite, 0.64f);
        enemy.Renderer.color = Color.white;
        enemy.Renderer.enabled = true;
    }

    private void ClearEnemies()
    {
        for (int i = 0; i < _spawnCount; i++)
            ENEMIES[i].Renderer.enabled = false;

        _spawnCount = 0;
        _activeCount = 0;
        _warningRemaining = 0f;
        _wallDelay = 0f;
        _arrowDashPhase = ARROW_DASH_PHASE.NONE;
        foreach (PreviewInterceptor interceptor in INTERCEPTORS)
            interceptor.Reset();
    }

    private SpriteRenderer CreateRenderer(string objectName, int sortingOrder)
    {
        GameObject spriteObj = new GameObject(objectName, typeof(SpriteRenderer));
        spriteObj.transform.SetParent(transform, false);
        SpriteRenderer renderer = spriteObj.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static void SetSprite(SpriteRenderer renderer, Sprite sprite, float width)
    {
        renderer.sprite = sprite;
        renderer.transform.localScale = Vector3.one * (width / sprite.bounds.size.x);
    }

    private static bool IsInsideArena(Vector2 position, float padding)
    {
        return Mathf.Abs(position.x) <= ARENA_HALF_WIDTH - padding && Mathf.Abs(position.y) <= ARENA_HALF_HEIGHT - padding;
    }

    private static Vector2 ClampToArena(Vector2 position, float padding)
    {
        float x = Mathf.Clamp(position.x, -ARENA_HALF_WIDTH + padding, ARENA_HALF_WIDTH - padding);
        float y = Mathf.Clamp(position.y, -ARENA_HALF_HEIGHT + padding, ARENA_HALF_HEIGHT - padding);
        return new Vector2(x, y);
    }
}
