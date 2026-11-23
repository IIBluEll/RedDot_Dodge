using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerMovement : MonoBehaviour
{
    private static readonly Vector2 DEFAULT_ARENA_SIZE = new Vector2(16f, 9f);
    private const float DEFAULT_PLAYER_RADIUS = 0.25f;
    private const float MIN_WALL_IMPACT_SPEED_RATIO = 0.6f;
    private const float JOYSTICK_WALL_REBOUND_SCALE = 0.4f;
    private const float TILT_RESPONSE = 24f;
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
    [SerializeField, Range(1f, 3f)] private float _tiltCurveExponent = 1.7f;
    [SerializeField, Range(0f, 1f)] private float _wallRestitution = 0.6f;
    [SerializeField, Min(0.01f)] private float _wallBounceDeceleration = 18f;

    private Camera _inputCamera;
    private PlayerTrail _playerTrail;
    private bool _preferJoystick;
    private Vector2? _mouseTarget;
    private Vector2 _joystickInput;
    private Sensor _tiltSensor;
    private Sensor _calibratedTiltSensor;
    private bool _enabledTiltSensor;
    private bool _tiltNeedsCalibration;
    private ScreenOrientation _tiltOrientation;
    private Quaternion _neutralTiltRotation;
    private Vector3 _calibrationGravitySum;
    private float _calibrationSettleRemainingSeconds;
    private float _calibrationStableSeconds;
    private float _calibrationRemainingSeconds;
    private float _sensorWaitSeconds;
    private Vector2 _filteredTilt;
    private Vector2 _tiltInput;
    private float _tiltSensitivity = 1f;
    private WallBounce _horizontalBounce;
    private WallBounce _verticalBounce;

    public float MaximumSpeed => _maxSpeed;
    public bool UsesTiltInput => Application.isMobilePlatform && !_preferJoystick;
    public bool UsesJoystickInput => Application.isMobilePlatform && _preferJoystick;
    public bool HasTiltSensor => GravitySensor.current != null || Accelerometer.current != null;
    public bool HasTiltOrientationChanged => UsesTiltInput && _tiltOrientation != Screen.orientation;
    public bool IsTiltCalibrating => _tiltNeedsCalibration;
    public bool TiltCalibrationFailed { get; private set; }
    public bool CanPlayWithTilt => !UsesTiltInput || (!_tiltNeedsCalibration && !HasTiltOrientationChanged
        && _calibratedTiltSensor != null && _calibratedTiltSensor.added);

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

        if (!Application.isMobilePlatform)
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
        PrepareTiltCalibration();
        ClearTrail();
    }

    private void OnDisable()
    {
        ReleaseTiltSensor();
        _tiltInput = Vector2.zero;
        _filteredTilt = Vector2.zero;
        _mouseTarget = null;
        _joystickInput = Vector2.zero;
        _horizontalBounce = default;
        _verticalBounce = default;
    }

    public void PrepareTiltCalibration(bool retryFailed = false)
    {
        if (UsesTiltInput && (HasTiltOrientationChanged || (!_tiltNeedsCalibration && (!TiltCalibrationFailed || retryFailed)
            && (_calibratedTiltSensor == null || !_calibratedTiltSensor.added))))
        {
            RecenterTilt();
        }
    }

    public void RecenterTilt()
    {
        _calibratedTiltSensor = null;
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

    // 준비 시간과 메뉴에서는 이동 컴포넌트가 꺼져 있어 보정만 별도로 진행합니다.
    public void UpdateTiltCalibration(float deltaTime)
    {
        if (isActiveAndEnabled || !UsesTiltInput)
        {
            return;
        }

        PrepareTiltCalibration();
        if (!_tiltNeedsCalibration && !CanPlayWithTilt)
        {
            return;
        }
        ReadTilt(deltaTime);
        if (!_tiltNeedsCalibration && (!gameObject.activeInHierarchy || TiltCalibrationFailed))
        {
            ReleaseTiltSensor();
        }
    }

    public void SetTiltSensitivity(float sensitivity)
    {
        _tiltSensitivity = Mathf.Max(0.01f, sensitivity);
    }

    public void SetMobileJoystick(bool useJoystick)
    {
        if (_preferJoystick == useJoystick)
        {
            return;
        }

        _preferJoystick = useJoystick;
        _joystickInput = Vector2.zero;
        _horizontalBounce = default;
        _verticalBounce = default;
        if (UsesJoystickInput)
        {
            _tiltNeedsCalibration = false;
            _tiltInput = Vector2.zero;
            _filteredTilt = Vector2.zero;
            ReleaseTiltSensor();
        }
        else
        {
            PrepareTiltCalibration(retryFailed: true);
        }
    }

    public void SetJoystickInput(Vector2 direction)
    {
        _joystickInput = direction;
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
        if (UsesTiltInput)
        {
            ReadTilt(Time.unscaledDeltaTime);
            return;
        }
        if (UsesJoystickInput)
        {
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
        PrepareTiltCalibration();
        if (TiltCalibrationFailed)
        {
            return;
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
            _enabledTiltSensor = false;
            _tiltSensor = _calibratedTiltSensor ?? (Sensor)GravitySensor.current;
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
            _filteredTilt = Vector2.zero;
            _calibrationGravitySum = Vector3.zero;
            _calibrationStableSeconds = 0f;
            _sensorWaitSeconds += deltaTime;
            if (_sensorWaitSeconds >= GRAVITY_SENSOR_WAIT_SECONDS)
            {
                if (!_tiltNeedsCalibration)
                {
                    RecenterTilt();
                }
                if (_tiltSensor is GravitySensor && Accelerometer.current != null)
                {
                    ReleaseTiltSensor();
                    _tiltSensor = Accelerometer.current;
                    EnableTiltSensor();
                }
                _sensorWaitSeconds = 0f;
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
        if (!CanPlayWithTilt)
        {
            _tiltInput = Vector2.zero;
            return;
        }

        // Rotate the calibrated gravity direction onto the screen normal so upright
        // and flat holding positions have the same neutral point and response.
        Vector2 tilt = _neutralTiltRotation * gravity;
        _filteredTilt = Vector2.Lerp(_filteredTilt, tilt, 1f - Mathf.Exp(-TILT_RESPONSE * deltaTime));
        float tiltMagnitude = _filteredTilt.magnitude / _fullSpeedTilt;
        float strength = Mathf.Clamp01((tiltMagnitude - _tiltDeadZone) * _tiltSensitivity / (1f - _tiltDeadZone));
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
        _calibratedTiltSensor = _tiltSensor;
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
        if (!CanPlayWithTilt)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;

        Vector3 currentPosition = transform.position;
        Vector2 halfSize = (_gameBalance != null ? _gameBalance.ArenaSize : DEFAULT_ARENA_SIZE) * 0.5f;

        float radius = _gameBalance != null ? _gameBalance.PlayerRadius : DEFAULT_PLAYER_RADIUS;

        Vector2 limits = Vector2.Max(halfSize - Vector2.one * radius, Vector2.zero);
        Vector2 velocity = GetMovementVelocity(currentPosition, halfSize, deltaTime);

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

    private Vector2 GetMovementVelocity(Vector2 position, Vector2 halfSize, float deltaTime)
    {
        if (UsesTiltInput)
            return _tiltInput * _maxSpeed;
        if (UsesJoystickInput)
            return _joystickInput * _maxSpeed;
        return GetMouseVelocity(position, halfSize, deltaTime);
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
        bool useJoystick = UsesJoystickInput;
        if (desiredVelocity * bounce.WallSide <= 0f)
        {
            if (useJoystick)
                bounce = default;
            else
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
            float impactSpeed = Mathf.Abs(velocity);
            if (!Application.isMobilePlatform)
            {
                impactSpeed = Mathf.Max(impactSpeed, _maxSpeed * MIN_WALL_IMPACT_SPEED_RATIO);
            }
            float restitution = useJoystick ? _wallRestitution * JOYSTICK_WALL_REBOUND_SCALE : _wallRestitution;
            bounce.Velocity = -nextWallSide * impactSpeed * restitution;
            float wallPosition = limit * nextWallSide;
            nextPosition = wallPosition - (nextPosition - wallPosition) * restitution;
        }

        return Mathf.Clamp(nextPosition, -limit, limit);
    }
}
