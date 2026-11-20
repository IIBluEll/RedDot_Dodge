using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerMovement : MonoBehaviour
{
    private static readonly Vector2 DEFAULT_ARENA_SIZE = new Vector2(16f, 9f);
    private const float DEFAULT_PLAYER_RADIUS = 0.25f;
    private const float MIN_WALL_IMPACT_SPEED_RATIO = 0.6f;
    private const float TILT_CALIBRATION_SETTLE_SECONDS = 0.15f;
    private const float TILT_CALIBRATION_SECONDS = 0.25f;
    private const float TILT_CALIBRATION_MAX_ANGLE = 2f;
    private const float TILT_CALIBRATION_TIMEOUT_SECONDS = 2f;
    private const float GRAVITY_SENSOR_WAIT_SECONDS = 0.5f;

    [SerializeField] private GameBalance _gameBalance;
    [SerializeField] private Sprite _playerSprite;
    [SerializeField, Min(0.01f)] private float _maxSpeed = 6f;
    [SerializeField, Min(0.01f)] private float _mouseSlowdownDistance = 0.8f;
    [SerializeField, Range(0f, 0.95f)] private float _tiltDeadZone = 0.1f;
    [SerializeField, Min(0.01f)] private float _fullSpeedTilt = 0.35f;
    [SerializeField, Min(0.01f)] private float _tiltResponse = 16f;
    [SerializeField, Range(1f, 3f)] private float _tiltCurveExponent = 1.7f;
    [SerializeField, Range(0f, 1f)] private float _wallRestitution = 0.6f;
    [SerializeField, Min(0.01f)] private float _wallBounceDeceleration = 18f;

    private Camera _inputCamera;
    private PlayerTrail _playerTrail;
    private bool _useTilt;
    private Vector2? _mouseTarget;
    private Sensor _tiltSensor;
    private bool _enabledTiltSensor;
    private bool _tiltNeedsCalibration;
    private ScreenOrientation _tiltOrientation;
    private Quaternion _neutralTiltRotation;
    private Vector3 _calibrationGravitySum;
    private float _calibrationSettleRemainingSeconds;
    private float _calibrationStableSeconds;
    private float _calibrationRemainingSeconds;
    private float _sensorWaitSeconds;
    private bool _hasTiltCalibration;
    private Vector2 _filteredTilt;
    private Vector2 _tiltInput;
    private float _tiltSensitivity = 1f;
    private WallBounce _horizontalBounce;
    private WallBounce _verticalBounce;

    public float MaximumSpeed => _maxSpeed;
    public bool UsesTiltInput => _useTilt || Application.isMobilePlatform;
    public bool IsTiltCalibrating => _tiltNeedsCalibration;
    public bool TiltCalibrationFailed { get; private set; }

    private struct WallBounce
    {
        public float Velocity;
        public float WallSide;
    }

    private void Awake()
    {
        if (_playerSprite != null)
        {
            GetComponent<SpriteRenderer>().sprite = _playerSprite;
        }

        _playerTrail = GetComponent<PlayerTrail>();
        if (_playerTrail == null)
        {
            _playerTrail = gameObject.AddComponent<PlayerTrail>();
        }

        _useTilt = Application.isMobilePlatform;
        if (!_useTilt)
        {
            _inputCamera = Camera.main;
        }
        else if (Application.targetFrameRate < 60)
        {
            Application.targetFrameRate = 60;
        }
    }

    private void OnEnable()
    {
        if (_useTilt && !_hasTiltCalibration && !_tiltNeedsCalibration)
        {
            RecenterTilt();
        }
        ClearTrail();
    }

    private void OnDisable()
    {
        ReleaseTiltSensor();
        _tiltInput = Vector2.zero;
        _filteredTilt = Vector2.zero;
        _mouseTarget = null;
        _horizontalBounce = default;
        _verticalBounce = default;
    }

    public void RecenterTilt()
    {
        _tiltNeedsCalibration = true;
        TiltCalibrationFailed = false;
        _tiltOrientation = Screen.orientation;
        _tiltInput = Vector2.zero;
        _filteredTilt = Vector2.zero;
        _calibrationGravitySum = Vector3.zero;
        _calibrationStableSeconds = 0f;
        _calibrationRemainingSeconds = TILT_CALIBRATION_TIMEOUT_SECONDS;
        _sensorWaitSeconds = 0f;
        _calibrationSettleRemainingSeconds = TILT_CALIBRATION_SETTLE_SECONDS;
        _horizontalBounce = default;
        _verticalBounce = default;
    }

    // The settings presenter stays active in the menu, where this object is inactive.
    // It only advances calibration; it never enables gameplay or moves the player.
    public void UpdateTiltCalibration(float deltaTime)
    {
        if (isActiveAndEnabled || !_tiltNeedsCalibration)
        {
            return;
        }
        ReadTilt(deltaTime);
        if (!_tiltNeedsCalibration)
        {
            ReleaseTiltSensor();
        }
    }

    public void SetTiltSensitivity(float sensitivity)
    {
        _tiltSensitivity = Mathf.Max(0.01f, sensitivity);
    }

    public void ClearTrail()
    {
        if (_playerTrail == null)
        {
            _playerTrail = GetComponent<PlayerTrail>();
        }
        if (_playerTrail != null)
        {
            _playerTrail.ClearTrail();
        }
    }

    private void Update()
    {
        if (_useTilt)
        {
            ReadTilt(Time.unscaledDeltaTime);
            return;
        }

        _mouseTarget = null;
        Mouse mouse = Mouse.current;
        if (_inputCamera == null || mouse == null)
        {
            return;
        }

        Ray ray = _inputCamera.ScreenPointToRay(mouse.position.ReadValue());
        Plane movementPlane = new Plane(Vector3.forward, transform.position);
        if (movementPlane.Raycast(ray, out float distance))
        {
            _mouseTarget = ray.GetPoint(distance);
        }
    }

    private void ReadTilt(float deltaTime)
    {
        if (_tiltOrientation != Screen.orientation)
        {
            RecenterTilt();
        }

        if (_tiltNeedsCalibration)
        {
            _calibrationRemainingSeconds -= deltaTime;
            if (_calibrationRemainingSeconds <= 0f)
            {
                _tiltNeedsCalibration = false;
                TiltCalibrationFailed = true;
                _tiltInput = Vector2.zero;
                _filteredTilt = Vector2.zero;
                return;
            }
        }

        if (_tiltSensor == null || !_tiltSensor.added)
        {
            _tiltSensor = GravitySensor.current;
            if (_tiltSensor == null)
            {
                _tiltSensor = Accelerometer.current;
            }
            if (_tiltSensor == null)
            {
                _tiltInput = Vector2.zero;
                return;
            }
            EnableTiltSensor();
        }

        if (!_tiltSensor.enabled)
        {
            EnableTiltSensor();
        }

        Vector3 gravity = _tiltSensor is GravitySensor gravitySensor
            ? gravitySensor.gravity.ReadValue() : ((Accelerometer)_tiltSensor).acceleration.ReadValue();
        // Ignore the zero sample before the sensor has reported its first reading.
        if (gravity.sqrMagnitude < 0.01f)
        {
            _tiltInput = Vector2.zero;
            _sensorWaitSeconds += deltaTime;
            if (_tiltSensor is GravitySensor && _sensorWaitSeconds >= GRAVITY_SENSOR_WAIT_SECONDS && Accelerometer.current != null)
            {
                ReleaseTiltSensor();
                _tiltSensor = Accelerometer.current;
                EnableTiltSensor();
                _sensorWaitSeconds = 0f;
                _calibrationGravitySum = Vector3.zero;
                _calibrationStableSeconds = 0f;
            }
            return;
        }
        _sensorWaitSeconds = 0f;
        gravity.Normalize();

        if (_tiltNeedsCalibration)
        {
            CalibrateTilt(gravity, deltaTime);
            return;
        }
        if (!_hasTiltCalibration)
        {
            _tiltInput = Vector2.zero;
            return;
        }

        // Rotate the calibrated gravity direction onto the screen normal so upright
        // and flat holding positions have the same neutral point and response.
        Vector2 tilt = _neutralTiltRotation * gravity;
        _filteredTilt = Vector2.Lerp(_filteredTilt, tilt, 1f - Mathf.Exp(-_tiltResponse * deltaTime));
        float strength = Mathf.InverseLerp(_tiltDeadZone, 1f, _filteredTilt.magnitude * _tiltSensitivity / _fullSpeedTilt);
        _tiltInput = _filteredTilt.normalized * Mathf.Pow(strength, _tiltCurveExponent);
    }

    private void CalibrateTilt(Vector3 gravity, float deltaTime)
    {
        // Measure real elapsed time. Some stationary sensors keep the same timestamp,
        // so completion must not depend on lastUpdateTime advancing.
        _calibrationSettleRemainingSeconds -= deltaTime;
        if (_calibrationSettleRemainingSeconds > 0f)
        {
            return;
        }

        if (_calibrationStableSeconds > 0f && Vector3.Angle(_calibrationGravitySum, gravity) > TILT_CALIBRATION_MAX_ANGLE)
        {
            _calibrationGravitySum = Vector3.zero;
            _calibrationStableSeconds = 0f;
        }
        _calibrationGravitySum += gravity * deltaTime;
        _calibrationStableSeconds += deltaTime;

        if (_calibrationStableSeconds < TILT_CALIBRATION_SECONDS)
        {
            return;
        }

        _neutralTiltRotation = Quaternion.FromToRotation(_calibrationGravitySum.normalized, Vector3.back);
        _hasTiltCalibration = true;
        _tiltNeedsCalibration = false;
        _filteredTilt = Vector2.zero;
        _tiltInput = Vector2.zero;
    }

    private void EnableTiltSensor()
    {
        if (!_tiltSensor.enabled)
        {
            InputSystem.EnableDevice(_tiltSensor);
            _enabledTiltSensor = true;
        }
        _tiltSensor.samplingFrequency = 60f;
    }

    private void ReleaseTiltSensor()
    {
        if (_enabledTiltSensor && _tiltSensor != null && _tiltSensor.added)
        {
            InputSystem.DisableDevice(_tiltSensor);
        }
        _tiltSensor = null;
        _enabledTiltSensor = false;
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
        {
            bounce.WallSide = 0f;
        }

        // Let the rebound finish before outward input takes over again.
        float velocity = desiredVelocity;
        if (bounce.Velocity != 0f && (velocity * bounce.Velocity <= 0f || Mathf.Abs(velocity) < Mathf.Abs(bounce.Velocity)))
        {
            velocity = bounce.Velocity;
        }

        bounce.Velocity = Mathf.MoveTowards(bounce.Velocity, 0f, _wallBounceDeceleration * deltaTime);
        float nextPosition = position + velocity * deltaTime;
        float nextWallSide = 0f;
        if (velocity > 0f && nextPosition >= limit)
        {
            nextWallSide = 1f;
        }
        else if (velocity < 0f && nextPosition <= -limit)
        {
            nextWallSide = -1f;
        }

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
