using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public sealed class EnemySpawnPatternPrototype : MonoBehaviour
{
    private const float TRIAL_DURATION_SECONDS = 10f;
    private const float CAMERA_PADDING = 0.35f;
    private const float CAMERA_Y_OFFSET = 0.98f;
    private static readonly Color HIT_COLOR = new Color32(255, 125, 103, 255);

    private static readonly (WAVE_PATTERN Kind, Key Shortcut, string Label, string Title)[] TRIALS =
    {
        (WAVE_PATTERN.EDGE_CHASE, Key.Digit1, "1 Edge chase", "Edge chase"),
        (WAVE_PATTERN.WALL, Key.Digit2, "2 Wall", "Wall"),
        (WAVE_PATTERN.ENCIRCLE, Key.Digit3, "3 Encircle", "Encircle"),
        (WAVE_PATTERN.CHASE_WALL, Key.Digit4, "4 Chase + wall", "Chase + wall"),
        (WAVE_PATTERN.ARROW_DASH, Key.Digit5, "5 Arrow dash", "Arrow dash"),
        (WAVE_PATTERN.INTERCEPTOR, Key.Digit6, "6 Interceptor", "Interceptor"),
        (WAVE_PATTERN.REBOUND_DASH, Key.Digit7, "7 Rebound", "Rebound dash"),
        (WAVE_PATTERN.ROTATING_ENCIRCLE, Key.Digit8, "8 Spin ring", "Rotating encircle"),
        (WAVE_PATTERN.TAIL_CHASE, Key.Digit9, "9 Tail chase", "Tail chase"),
        (WAVE_PATTERN.FAN_DASH, Key.Digit0, "0 Fan split", "Fan split"),
        (WAVE_PATTERN.STAGGERED_CROSS, Key.Minus, "- Cross dash", "Staggered cross")
    };

    [SerializeField] private GameBalance _gameBalance;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private Camera _previewCamera;
    [SerializeField] private Sprite _enemySprite;
    [SerializeField] private Texture2D _warningTexture;
    [SerializeField] private Text _statusTxt;
    [SerializeField] private Button _patternButtonTemplateBtn;

    private EdgeChaseWaveHost _previewHost;
    private SpriteRenderer _playerRenderer;
    private Text _timingTxt;
    private int _selectedTrialIndex = -1;
    private float _survivalRemaining;
    private bool _useMobileTiming;
    private bool _trialRunning;

    private void Awake()
    {
        if (_gameBalance == null || _playerMovement == null || _previewCamera == null || _enemySprite == null
            || _warningTexture == null || _statusTxt == null || _patternButtonTemplateBtn == null)
        {
            Debug.LogError("적 패턴 프로토타입의 씬 참조를 확인하세요.", this);
            enabled = false;
            return;
        }

        _playerRenderer = _playerMovement.GetComponent<SpriteRenderer>();
        var hostObj = new GameObject("Pattern Preview Host");
        hostObj.transform.SetParent(transform, false);
        _previewHost = hostObj.AddComponent<EdgeChaseWaveHost>();
        _previewHost.InitializePatternPreview(_gameBalance, _playerMovement, _warningTexture, _enemySprite);

        _useMobileTiming = Application.isMobilePlatform;
        CreateControls();
        SetStatus("Choose a pattern / R to retry");
    }

    private void CreateControls()
    {
        for (int i = 0; i < TRIALS.Length; i++)
        {
            int trialIndex = i;
            var trial = TRIALS[trialIndex];
            Button patternBtn = CreateButton(trial.Title + " Button", trial.Label, trialIndex);
            patternBtn.onClick.AddListener(() => OnPatternActioned(trialIndex));
        }

        Button timingBtn = CreateButton("Timing Button", string.Empty, TRIALS.Length);
        _timingTxt = timingBtn.GetComponentInChildren<Text>();
        timingBtn.onClick.AddListener(OnTimingActioned);
        UpdateTimingLabel();
    }

    private Button CreateButton(string objectName, string label, int slot)
    {
        Button button = Instantiate(_patternButtonTemplateBtn, _patternButtonTemplateBtn.transform.parent);
        button.name = objectName;
        var buttonRectTrans = (RectTransform)button.transform;
        buttonRectTrans.anchoredPosition = new Vector2(-250f + slot % 6 * 188f, slot / 6 * 76f);
        button.GetComponentInChildren<Text>(true).text = label;
        button.gameObject.SetActive(true);
        return button;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }
        if (keyboard.tKey.wasPressedThisFrame)
        {
            OnTimingActioned();
            return;
        }
        if (keyboard.rKey.wasPressedThisFrame && _selectedTrialIndex >= 0)
        {
            OnPatternActioned(_selectedTrialIndex);
            return;
        }
        for (int i = 0; i < TRIALS.Length; i++)
        {
            if (keyboard[TRIALS[i].Shortcut].wasPressedThisFrame)
            {
                OnPatternActioned(i);
                return;
            }
        }
    }

    private void LateUpdate()
    {
        Vector2 halfSize = _gameBalance.ArenaSize * 0.5f;
        float height = halfSize.y + CAMERA_PADDING + CAMERA_Y_OFFSET;
        float width = (Mathf.Max(8f, halfSize.x) + CAMERA_PADDING) / _previewCamera.aspect;
        _previewCamera.orthographicSize = Mathf.Max(height, width);

        Vector3 cameraPosition = _previewCamera.transform.localPosition;
        _previewCamera.transform.localPosition = new Vector3(cameraPosition.x, CAMERA_Y_OFFSET, cameraPosition.z);
    }

    private void OnPatternActioned(int trialIndex)
    {
        _selectedTrialIndex = trialIndex;
        var trial = TRIALS[trialIndex];

        _playerMovement.enabled = false;
        _playerMovement.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _playerMovement.ClearTrail();
        _playerRenderer.color = Color.white;
        _playerMovement.enabled = true;
        _playerMovement.PrepareTiltCalibration(retryFailed: true);

        _trialRunning = _previewHost.TryStartPatternPreview(trial.Kind);
        _survivalRemaining = TRIAL_DURATION_SECONDS;
        SetStatus(_trialRunning ? trial.Title + " / warning" : trial.Title + " / no layout. Retry");
    }

    private void OnTimingActioned()
    {
        _useMobileTiming = !_useMobileTiming;
        _previewHost.SetPatternPreviewTiming(_useMobileTiming);
        UpdateTimingLabel();
        if (_selectedTrialIndex >= 0)
        {
            OnPatternActioned(_selectedTrialIndex);
        }
    }

    private void UpdateTimingLabel()
    {
        _timingTxt.text = _useMobileTiming ? "T Mobile timing" : "T PC timing";
    }

    private void FixedUpdate()
    {
        if (!_trialRunning)
        {
            return;
        }

        if (!_playerMovement.CanPlayWithTilt)
        {
            SetStatus(_playerMovement.TiltCalibrationFailed ? "Tilt unavailable / retry pattern" : "Tilt calibrating / hold still");
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        string title = TRIALS[_selectedTrialIndex].Title;
        if (_previewHost.TickPatternPreview(deltaTime))
        {
            _trialRunning = false;
            _playerMovement.enabled = false;
            _playerRenderer.color = HIT_COLOR;
            SetStatus("Hit / R or select to retry");
            return;
        }
        if (_previewHost.HasWarning)
        {
            SetStatus(title + " / warning");
            return;
        }

        _survivalRemaining -= deltaTime;
        if (_previewHost.EnemyCount == 0 || _survivalRemaining <= 0f)
        {
            _trialRunning = false;
            _previewHost.ClearPatternPreview();
            SetStatus("Survived / R or select a pattern");
            return;
        }

        SetStatus($"{title} / {Mathf.CeilToInt(_survivalRemaining)}s");
    }

    private void SetStatus(string text)
    {
        if (_statusTxt.text != text)
        {
            _statusTxt.text = text;
        }
    }
}
