using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerMovement : MonoBehaviour
{
    private static readonly Vector2 DEFAULT_ARENA_SIZE = new Vector2(16f, 9f);
    private const float DEFAULT_PLAYER_RADIUS = 0.25f;
    private const float MIN_WALL_IMPACT_SPEED_RATIO = 0.6f;

    [SerializeField] private GameBalance _gameBalance;
    [SerializeField] private Sprite _playerSprite;
    [SerializeField, Min(0.01f)] private float _maxSpeed = 6f;
    [SerializeField, Min(0.01f)] private float _mouseSlowdownDistance = 0.8f;
    [SerializeField, Range(0f, 0.95f)] private float _tiltDeadZone = 0.1f;
    [SerializeField, Min(0.01f)] private float _fullSpeedTilt = 0.35f;
    [SerializeField, Min(0.01f)] private float _tiltResponse = 16f;
    [SerializeField, Range(0f, 1f)] private float _wallRestitution = 0.6f;
    [SerializeField, Min(0.01f)] private float _wallBounceDeceleration = 18f;

    private Camera _inputCamera;
    private PlayerTrail _playerTrail;
    private bool _useTilt;
    private Vector2? _mouseTarget;
    private Accelerometer _accelerometer;
    private bool _enabledAccelerometer;
    private bool _tiltNeedsCalibration;
    private ScreenOrientation _tiltOrientation;
    private Vector2 _neutralTilt;
    private Vector2 _filteredTilt;
    private Vector2 _tiltInput;
    private WallBounce _horizontalBounce;
    private WallBounce _verticalBounce;

    private struct WallBounce
    {
        public float Velocity;
        public float WallSide;
    }

    private void Awake()
    {
        if (_playerSprite != null)
            GetComponent<SpriteRenderer>().sprite = _playerSprite;

        _playerTrail = GetComponent<PlayerTrail>();
        if (_playerTrail == null)
            _playerTrail = gameObject.AddComponent<PlayerTrail>();

        _useTilt = Application.isMobilePlatform;
        if (!_useTilt)
            _inputCamera = Camera.main;
    }

    private void OnEnable()
    {
        RecenterTilt();
        ClearTrail();
    }

    private void OnDisable()
    {
        if (_enabledAccelerometer && _accelerometer != null && _accelerometer.added)
            InputSystem.DisableDevice(_accelerometer);

        _accelerometer = null;
        _enabledAccelerometer = false;
        _mouseTarget = null;
        _horizontalBounce = default;
        _verticalBounce = default;
    }

    private void OnApplicationPause(bool paused)
    {
        if (_useTilt)
            RecenterTilt();
    }

    public void RecenterTilt()
    {
        _tiltNeedsCalibration = true;
        _tiltInput = Vector2.zero;
    }

    public void ClearTrail()
    {
        if (_playerTrail == null)
            _playerTrail = GetComponent<PlayerTrail>();
        if (_playerTrail != null)
            _playerTrail.ClearTrail();
    }

    private void Update()
    {
        if (_useTilt)
        {
            ReadTilt(Time.deltaTime);
            return;
        }

        _mouseTarget = null;
        Mouse mouse = Mouse.current;
        if (_inputCamera == null || mouse == null)
            return;

        Ray ray = _inputCamera.ScreenPointToRay(mouse.position.ReadValue());
        Plane movementPlane = new Plane(Vector3.forward, transform.position);
        if (movementPlane.Raycast(ray, out float distance))
            _mouseTarget = ray.GetPoint(distance);
    }

    private void ReadTilt(float deltaTime)
    {
        if (_accelerometer == null || !_accelerometer.added)
        {
            RecenterTilt();
            _accelerometer = Accelerometer.current;
            if (_accelerometer == null)
                return;

            _enabledAccelerometer = !_accelerometer.enabled;
            if (_enabledAccelerometer)
                InputSystem.EnableDevice(_accelerometer);
        }

        Vector3 acceleration = _accelerometer.acceleration.ReadValue();
        ScreenOrientation orientation = Screen.orientation;

        if (_tiltOrientation != orientation)
            RecenterTilt();

        if (_tiltNeedsCalibration)
        {
            // Ignore the zero sample before the sensor has reported its first reading.
            if (acceleration.sqrMagnitude < 0.01f)
                return;

            _neutralTilt = acceleration;
            _filteredTilt = Vector2.zero;
            _tiltOrientation = orientation;
            _tiltNeedsCalibration = false;
        }

        Vector2 tilt = (Vector2)acceleration - _neutralTilt;
        _filteredTilt = Vector2.Lerp(_filteredTilt, tilt, 1f - Mathf.Exp(-_tiltResponse * deltaTime));
        float strength = Mathf.InverseLerp(_tiltDeadZone, 1f, _filteredTilt.magnitude / _fullSpeedTilt);
        _tiltInput = _filteredTilt.normalized * strength;
    }

    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;

        Vector3 currentPosition = transform.position;
        Vector2 halfSize = (_gameBalance != null ? _gameBalance.ArenaSize : DEFAULT_ARENA_SIZE) * 0.5f;

        float radius = _gameBalance != null ? _gameBalance.PlayerRadius : DEFAULT_PLAYER_RADIUS;

        Vector2 limits = Vector2.Max(halfSize - Vector2.one * radius, Vector2.zero);
        Vector2 velocity = _useTilt ? _tiltInput * _maxSpeed : GetMouseVelocity(currentPosition, halfSize, deltaTime);

        float x = MoveAlongAxis(currentPosition.x, velocity.x, limits.x, deltaTime, ref _horizontalBounce);
        float y = MoveAlongAxis(currentPosition.y, velocity.y, limits.y, deltaTime, ref _verticalBounce);

        transform.position = new Vector3(x, y, currentPosition.z);
        Vector2 movement = new Vector2(x - currentPosition.x, y - currentPosition.y);

        if (movement.sqrMagnitude > 0.000001f)
        {
            transform.rotation = Quaternion.Euler(0f , 0f , Mathf.Atan2(movement.y , movement.x) * Mathf.Rad2Deg - 90f);
        }

        if (_playerTrail != null)
        {
            _playerTrail.RecordMovement(currentPosition , transform.position , _maxSpeed , deltaTime);
        }
    }

    private Vector2 GetMouseVelocity(Vector2 position, Vector2 halfSize, float deltaTime)
    {
        if (!_mouseTarget.HasValue)
        {
            return Vector2.zero;
        }
        Vector2 cursor = _mouseTarget.Value;
        Vector2 target = new Vector2(Mathf.Clamp(cursor.x, -halfSize.x, halfSize.x), Mathf.Clamp(cursor.y, -halfSize.y, halfSize.y));
        Vector2 offset = target - position;
        float distance = offset.magnitude;
        float speed = _maxSpeed * Mathf.Clamp01(distance / _mouseSlowdownDistance);
        return offset.normalized * Mathf.Min(speed, distance / deltaTime);
    }

    private float MoveAlongAxis(float position, float desiredVelocity, float limit, float deltaTime, ref WallBounce bounce)
    {
        if (desiredVelocity * bounce.WallSide <= 0f)
            bounce.WallSide = 0f;

        // Let the rebound finish before outward input takes over again.
        float velocity = desiredVelocity;
        if (bounce.Velocity != 0f && (velocity * bounce.Velocity <= 0f || Mathf.Abs(velocity) < Mathf.Abs(bounce.Velocity)))
            velocity = bounce.Velocity;

        bounce.Velocity = Mathf.MoveTowards(bounce.Velocity, 0f, _wallBounceDeceleration * deltaTime);
        float nextPosition = position + velocity * deltaTime;
        float nextWallSide = 0f;
        if (velocity > 0f && nextPosition >= limit)
            nextWallSide = 1f;
        else if (velocity < 0f && nextPosition <= -limit)
            nextWallSide = -1f;

        if (nextWallSide != 0f && nextWallSide != bounce.WallSide)
        {
            bounce.WallSide = nextWallSide;
            float impactSpeed = Mathf.Max(Mathf.Abs(velocity), _maxSpeed * MIN_WALL_IMPACT_SPEED_RATIO);
            bounce.Velocity = -nextWallSide * impactSpeed * _wallRestitution;
            float wallPosition = limit * nextWallSide;
            nextPosition = wallPosition - (nextPosition - wallPosition) * _wallRestitution;
        }

        return Mathf.Clamp(nextPosition, -limit, limit);
    }
}
