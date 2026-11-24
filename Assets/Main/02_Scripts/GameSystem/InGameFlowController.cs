using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class InGameFlowController : MonoBehaviour
{
    private enum FLOW_STATE { MENU, HOW_TO, SETTINGS, START_TRANSITION, IN_GAME, PAUSED, CONFIRM_EXIT, RESUMING, DEATH_TRANSITION, RESULT, RETURN_TRANSITION }
    private const float GAME_OVER_BGM_MULTIPLIER = 0.3f;

    [Header("Scene")]
    [SerializeField] private GameObject _mainMenuCanvasObj;
    [SerializeField] private GameObject _inGameCanvasObj;
    [SerializeField] private GameObject _gameplayObj;
    [SerializeField] private EdgeChaseWaveHost _waveHost;
    [SerializeField] private ResultPresenter_presenter _resultPresenter;
    [SerializeField] private InGameArenaCamera _arenaCamera;
    [SerializeField] private PausePresenter_presenter _pausePresenter;
    [SerializeField] private HowToPresenter_presenter _howToPresenter;
    [SerializeField] private SettingsPresenter_presenter _settingsPresenter;
    [SerializeField] private BackgroundMusicPlayer _backgroundMusic;
    [SerializeField] private AudioSource _gameOverAudio;

    [Header("Menu Transition")]
    [SerializeField] private CanvasGroup _menuContentGroup;
    [SerializeField] private RectTransform _menuBackgroundRectTrans;
    [SerializeField] private RectTransform _titleRectTrans;
    [SerializeField] private RectTransform _startBtnRectTrans;
    [SerializeField] private RectTransform _howToBtnRectTrans;
    [SerializeField] private RectTransform _settingsBtnRectTrans;
    [SerializeField] private RectTransform _quitBtnRectTrans;

    [Header("InGame Transition")]
    [SerializeField] private CanvasGroup _inGameGroup;
    [SerializeField] private RectTransform _hudRectTrans;
    [SerializeField] private RectTransform _arenaRectTrans;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float _startPanSeconds = 0.65f;
    [SerializeField, Min(0.01f)] private float _gameEnterSeconds = 0.26f;
    [SerializeField, Min(0.01f)] private float _returnSeconds = 0.65f;
    [SerializeField, Min(0f)] private float _deathBurstSeconds = 0.70f;
    [SerializeField, Min(0.01f)] private float _deathExitSeconds = 0.65f;
    [SerializeField, Min(0.01f)] private float _resumePrepareSeconds = 1f;

    private RectTransform[] _menuRectTransforms;
    private Vector2[] _menuPositions;
    private Vector2 _menuBackgroundPosition;
    private Vector2 _hudPosition;
    private Vector2 _arenaPosition;
    private RectTransform _returningResultRectTrans;
    private Vector2 _resultReturnPosition;
    private Sequence _transition;
    private MobileJoystick_view _joystickView;
    private FLOW_STATE _state;
    private FLOW_STATE _dialogReturnState;
    private bool _initialized;
    private bool _hasFocus;
    private bool _applicationPaused;
    private bool _ownsPause;
    private float _timeScaleBeforePause;
    private bool _audioPausedBeforePause;
    private float _resumeRemainingSeconds;

    public float CountdownRemainingSeconds
    {
        get
        {
            if (_state == FLOW_STATE.RESUMING)
                return _resumeRemainingSeconds;
            return _state == FLOW_STATE.IN_GAME ? _waveHost.ReadyRemainingSeconds : 0f;
        }
    }

    private void Awake()
    {
        if (_mainMenuCanvasObj == null || _inGameCanvasObj == null || _gameplayObj == null || _waveHost == null || _resultPresenter == null || _arenaCamera == null || _pausePresenter == null
            || _menuContentGroup == null || _menuBackgroundRectTrans == null || _titleRectTrans == null || _startBtnRectTrans == null
            || _howToBtnRectTrans == null || _settingsBtnRectTrans == null || _quitBtnRectTrans == null || _inGameGroup == null
            || _hudRectTrans == null || _arenaRectTrans == null || _howToPresenter == null || _settingsPresenter == null)
        {
            Debug.LogError("InGameFlowController의 메뉴·인게임·결과·일시정지·안내·설정 참조를 연결해 주세요.", this);
            enabled = false;
            return;
        }

        _menuRectTransforms = new[] { _titleRectTrans, _startBtnRectTrans, _howToBtnRectTrans, _settingsBtnRectTrans, _quitBtnRectTrans };
        _menuPositions = new Vector2[_menuRectTransforms.Length];
        for (int i = 0; i < _menuRectTransforms.Length; i++)
        {
            _menuPositions[i] = _menuRectTransforms[i].anchoredPosition;
        }
        _menuBackgroundPosition = _menuBackgroundRectTrans.anchoredPosition;
        _hudPosition = _hudRectTrans.anchoredPosition;
        _arenaPosition = _arenaRectTrans.anchoredPosition;
        _resultPresenter.Initialize();
        _pausePresenter.Initialize();
        _howToPresenter.Hide();
        _settingsPresenter.Initialize();
        _hasFocus = Application.isFocused;
        _initialized = true;
    }

    private void OnEnable()
    {
        if (_initialized)
        {
            _waveHost.RoundEnded += OnRoundEnded;
            ShowMenuImmediately();
        }
    }

    private void OnDisable()
    {
        if (_waveHost != null)
            _waveHost.RoundEnded -= OnRoundEnded;
        if (_initialized && _resultPresenter != null)
            _resultPresenter.Hide();
        if (_initialized && _pausePresenter != null)
            _pausePresenter.Hide();
        if (_initialized && _howToPresenter != null)
            _howToPresenter.Hide();
        if (_initialized && _settingsPresenter != null)
            _settingsPresenter.Hide();
        HideJoystick();
        ReleasePause();
        KillTransition();
    }

    private void Update()
    {
        if (!_initialized)
            return;

        CheckTiltPause();

        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            if (_state == FLOW_STATE.HOW_TO || _state == FLOW_STATE.SETTINGS)
                OnCloseDialogActioned();
            else if (_state == FLOW_STATE.PAUSED)
                OnResumeActioned();
            else if (_state == FLOW_STATE.CONFIRM_EXIT)
                OnCancelMenuExitActioned();
            else
                OnPauseActioned();
        }

        if (_state != FLOW_STATE.RESUMING)
            return;
        if (!_hasFocus || _applicationPaused || !_waveHost.Player.CanPlayWithTilt)
        {
            OnPauseActioned();
            return;
        }

        _resumeRemainingSeconds = Mathf.Max(0f, _resumeRemainingSeconds - Time.unscaledDeltaTime);
        _pausePresenter.UpdateResume();
        if (_resumeRemainingSeconds > 0f)
            return;

        _pausePresenter.Hide();
        ReleasePause();
        _inGameGroup.interactable = true;
        _inGameGroup.blocksRaycasts = true;
        _state = FLOW_STATE.IN_GAME;
    }

    private void CheckTiltPause()
    {
        if (_state != FLOW_STATE.IN_GAME && _state != FLOW_STATE.RESUMING)
            return;

        PlayerMovement player = _waveHost.Player;
        if (!player.UsesTiltInput)
            return;

        bool orientationChanged = player.HasTiltOrientationChanged;
        player.PrepareTiltCalibration();
        if (orientationChanged || player.TiltCalibrationFailed || !player.HasTiltSensor
            || (_waveHost.ReadyRemainingSeconds <= 0f && !player.CanPlayWithTilt))
        {
            OnPauseActioned();
        }
    }

    private void ShowJoystick()
    {
        if (!_waveHost.Player.UsesJoystickInput)
        {
            return;
        }

        if (_joystickView == null)
        {
            SafeAreaPanel safeArea = _hudRectTrans.GetComponentInParent<SafeAreaPanel>();
            var joystickObj = new GameObject("Mobile Joystick", typeof(RectTransform), typeof(MobileJoystick_view));
            _joystickView = joystickObj.GetComponent<MobileJoystick_view>();
            RectTransform joystickRectTrans = _joystickView.rectTransform;
            joystickRectTrans.SetParent(_inGameCanvasObj.transform, false);
            // 배경 위에서 터치를 받고, HUD 버튼은 그보다 앞에서 터치를 받습니다.
            if (safeArea != null && safeArea.transform.parent == joystickRectTrans.parent)
                joystickRectTrans.SetSiblingIndex(safeArea.transform.GetSiblingIndex());
            joystickRectTrans.anchorMin = Vector2.zero;
            joystickRectTrans.anchorMax = new Vector2(0.5f, 1f);
            joystickRectTrans.offsetMin = Vector2.zero;
            joystickRectTrans.offsetMax = Vector2.zero;
            _joystickView.DirectionChanged += _waveHost.Player.SetJoystickInput;
        }

        if (!_joystickView.gameObject.activeSelf)
            _joystickView.gameObject.SetActive(true);
    }

    private void HideJoystick()
    {
        if (_joystickView != null)
            _joystickView.gameObject.SetActive(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        _hasFocus = hasFocus;
        if (!hasFocus)
            OnPauseActioned();
    }

    private void OnApplicationPause(bool paused)
    {
        _applicationPaused = paused;
        if (paused)
            OnPauseActioned();
    }

    public void OnPauseActioned()
    {
        if (!_initialized || (_state != FLOW_STATE.IN_GAME && _state != FLOW_STATE.RESUMING))
            return;

        if (!_ownsPause)
        {
            _timeScaleBeforePause = Time.timeScale;
            _audioPausedBeforePause = AudioListener.pause;
            _ownsPause = true;
        }
        Time.timeScale = 0f;
        AudioListener.pause = true;
        _resumeRemainingSeconds = 0f;
        _inGameGroup.interactable = false;
        _inGameGroup.blocksRaycasts = false;
        _state = FLOW_STATE.PAUSED;
        HideJoystick();
        _pausePresenter.ShowPause();
    }

    public void OnResumeActioned()
    {
        if (!_initialized || _state != FLOW_STATE.PAUSED || !_hasFocus || _applicationPaused || !_waveHost.Player.CanPlayWithTilt)
            return;

        _state = FLOW_STATE.RESUMING;
        _resumeRemainingSeconds = _resumePrepareSeconds;
        _pausePresenter.BeginResume();
        _inGameGroup.blocksRaycasts = _waveHost.Player.UsesJoystickInput;
        ShowJoystick();
    }

    public void OnPauseMenuActioned()
    {
        if (!_initialized || _state != FLOW_STATE.PAUSED)
            return;
        _state = FLOW_STATE.CONFIRM_EXIT;
        _pausePresenter.ShowExitConfirmation();
    }

    public void OnCancelMenuExitActioned()
    {
        if (!_initialized || _state != FLOW_STATE.CONFIRM_EXIT)
            return;
        _state = FLOW_STATE.PAUSED;
        _pausePresenter.ShowPause();
    }

    public void OnConfirmMenuExitActioned()
    {
        if (_initialized && _state == FLOW_STATE.CONFIRM_EXIT)
            ReturnToMenu();
    }

    public void OnRecenterTiltActioned()
    {
        if (_initialized && _state == FLOW_STATE.PAUSED)
            _pausePresenter.RecenterTilt();
    }

    public void OnHowToActioned()
    {
        if (!_initialized || _state != FLOW_STATE.MENU)
            return;

        _dialogReturnState = FLOW_STATE.MENU;
        _state = FLOW_STATE.HOW_TO;
        _menuContentGroup.interactable = false;
        _menuContentGroup.blocksRaycasts = false;
        _howToPresenter.Show();
    }

    public void OnSettingsActioned()
    {
        if (!_initialized || (_state != FLOW_STATE.MENU && _state != FLOW_STATE.PAUSED))
            return;

        _dialogReturnState = _state;
        if (_state == FLOW_STATE.PAUSED)
            _pausePresenter.Hide();
        else
        {
            _menuContentGroup.interactable = false;
            _menuContentGroup.blocksRaycasts = false;
        }
        _state = FLOW_STATE.SETTINGS;
        _settingsPresenter.Show();
    }

    public void OnCloseDialogActioned()
    {
        if (!_initialized || (_state != FLOW_STATE.HOW_TO && _state != FLOW_STATE.SETTINGS))
            return;

        bool wasSettings = _state == FLOW_STATE.SETTINGS;
        if (wasSettings)
            _settingsPresenter.Hide();
        else
            _howToPresenter.Hide();

        _state = _dialogReturnState;
        if (_state == FLOW_STATE.PAUSED)
            _pausePresenter.ShowPause();
        else
        {
            _menuContentGroup.interactable = true;
            _menuContentGroup.blocksRaycasts = true;
            RectTransform buttonRectTrans = wasSettings ? _settingsBtnRectTrans : _howToBtnRectTrans;
            buttonRectTrans.GetComponent<Button>().Select();
        }
    }

    public void OnQuitActioned()
    {
        if (_initialized && _state == FLOW_STATE.MENU)
        {
            Application.Quit();
        }
    }

    private void ReleasePause()
    {
        if (!_ownsPause)
            return;
        Time.timeScale = _timeScaleBeforePause;
        AudioListener.pause = _audioPausedBeforePause;
        _resumeRemainingSeconds = 0f;
        _ownsPause = false;
    }

    public void OnGameStartActioned()
    {
        if (!_initialized || _state != FLOW_STATE.MENU)
        {
            return;
        }

        _state = FLOW_STATE.START_TRANSITION;
        _menuContentGroup.interactable = false;
        _menuContentGroup.blocksRaycasts = false;
        _inGameGroup.alpha = 1f;
        _inGameGroup.interactable = false;
        _inGameGroup.blocksRaycasts = false;
        ShowInGameForTransition();
        Canvas.ForceUpdateCanvases();

        // 각 Canvas의 높이를 사용해 화면비가 달라도 같은 화면 거리만큼 함께 이동합니다.
        float menuTravel = ((RectTransform)_mainMenuCanvasObj.transform).rect.height;
        float gameTravel = ((RectTransform)_inGameCanvasObj.transform).rect.height;
        _hudRectTrans.anchoredPosition = _hudPosition + Vector2.up * gameTravel;
        _arenaRectTrans.anchoredPosition = _arenaPosition + Vector2.up * gameTravel;

        _transition = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < _menuRectTransforms.Length; i++)
        {
            Vector2 destination = _menuPositions[i] + Vector2.down * menuTravel;
            _transition.Insert(0f, _menuRectTransforms[i].DOAnchorPos(destination, _startPanSeconds).SetEase(Ease.InOutCubic));
        }
        _transition.Insert(0f, _menuBackgroundRectTrans.DOAnchorPos(_menuBackgroundPosition + Vector2.down * menuTravel, _startPanSeconds).SetEase(Ease.InOutCubic));
        _transition.Insert(0f, _hudRectTrans.DOAnchorPos(_hudPosition, _startPanSeconds).SetEase(Ease.InOutCubic));
        _transition.Insert(0f, _arenaRectTrans.DOAnchorPos(_arenaPosition, _startPanSeconds).SetEase(Ease.InOutCubic));
        _transition.OnComplete(OnStartTransitionCompleted);
    }

    private void ShowInGameForTransition()
    {
        _gameplayObj.SetActive(true);
        _waveHost.StopRound();
        _inGameCanvasObj.SetActive(true);
    }

    private void OnStartTransitionCompleted()
    {
        _transition = null;
        _mainMenuCanvasObj.SetActive(false);
        _arenaCamera.enabled = true;
        if (!_waveHost.BeginRound())
        {
            ShowMenuImmediately();
            return;
        }

        _inGameGroup.interactable = true;
        _inGameGroup.blocksRaycasts = true;
        _state = FLOW_STATE.IN_GAME;
        if (!_hasFocus || _applicationPaused)
        {
            OnPauseActioned();
        }
        else
        {
            ShowJoystick();
            CheckTiltPause();
        }
    }

    private void OnRoundEnded(RoundResult result)
    {
        if (_state != FLOW_STATE.IN_GAME)
        {
            return;
        }

        _state = FLOW_STATE.DEATH_TRANSITION;
        HideJoystick();
        _inGameGroup.interactable = false;
        _inGameGroup.blocksRaycasts = false;
        _resultPresenter.PrepareResult(result);
        _arenaCamera.enabled = false;
        Canvas.ForceUpdateCanvases();
        float travel = ((RectTransform)_inGameCanvasObj.transform).rect.height + _hudRectTrans.rect.height;

        _transition = DOTween.Sequence().SetUpdate(true);
        if (_deathBurstSeconds > 0f)
        {
            _arenaCamera.BeginImpactZoom(_waveHost.Player.transform.position);
            float zoomOutStart = _deathBurstSeconds * 0.5f;
            _transition.Insert(0f, DOVirtual.Float(0f, 1f, _deathBurstSeconds * 0.24f, _arenaCamera.SetImpactZoom).SetEase(Ease.OutCubic));
            _transition.Insert(zoomOutStart, DOVirtual.Float(1f, 0f, _deathBurstSeconds - zoomOutStart, _arenaCamera.SetImpactZoom).SetEase(Ease.InOutSine));
        }
        _transition.Insert(_deathBurstSeconds, _hudRectTrans.DOAnchorPosY(_hudPosition.y + travel, _deathExitSeconds).SetEase(Ease.InOutCubic));
        _transition.Insert(_deathBurstSeconds + 0.07f, _arenaRectTrans.DOAnchorPosY(_arenaPosition.y - travel, _deathExitSeconds).SetEase(Ease.InOutCubic));
        if (_gameOverAudio != null && _gameOverAudio.clip != null)
        {
            _gameOverAudio.Play();
        }
        if (_backgroundMusic != null)
        {
            _transition.Insert(0f, DOVirtual.Float(1f, GAME_OVER_BGM_MULTIPLIER, 0.10f, _backgroundMusic.SetDuckMultiplier));
            _transition.Insert(_deathBurstSeconds, DOVirtual.Float(GAME_OVER_BGM_MULTIPLIER, 1f, _deathExitSeconds, _backgroundMusic.SetDuckMultiplier));
        }
        _transition.OnComplete(OnDeathTransitionCompleted);
    }

    private void OnDeathTransitionCompleted()
    {
        _transition = null;
        _arenaCamera.EndImpactZoom();
        _waveHost.ClearDeathVisuals();
        _hudRectTrans.gameObject.SetActive(false);
        _arenaRectTrans.gameObject.SetActive(false);
        _state = FLOW_STATE.RESULT;
        _resultPresenter.ShowResult();
    }

    public void OnRetryActioned()
    {
        if (!_initialized || _state != FLOW_STATE.RESULT)
            return;

        if (_gameOverAudio != null)
        {
            _gameOverAudio.Stop();
        }
        _resultPresenter.Hide();
        _state = FLOW_STATE.START_TRANSITION;
        _hudRectTrans.anchoredPosition = _hudPosition + Vector2.up * 48f;
        _arenaRectTrans.anchoredPosition = _arenaPosition + Vector2.down * 72f;
        _hudRectTrans.gameObject.SetActive(true);
        _arenaRectTrans.gameObject.SetActive(true);
        _transition = DOTween.Sequence().SetUpdate(true);
        _transition.Append(_hudRectTrans.DOAnchorPos(_hudPosition, _gameEnterSeconds).SetEase(Ease.OutCubic));
        _transition.Join(_arenaRectTrans.DOAnchorPos(_arenaPosition, _gameEnterSeconds).SetEase(Ease.OutCubic));
        _transition.OnComplete(OnStartTransitionCompleted);
    }

    public void ReturnToMenu()
    {
        if (!_initialized || _state == FLOW_STATE.MENU || _state == FLOW_STATE.RETURN_TRANSITION)
        {
            return;
        }
        if (_state == FLOW_STATE.START_TRANSITION)
        {
            ShowMenuImmediately();
            return;
        }

        bool returningFromResult = _state == FLOW_STATE.RESULT;
        _state = FLOW_STATE.RETURN_TRANSITION;
        HideJoystick();
        KillTransition();
        ReleasePause();
        _pausePresenter.Hide();
        _howToPresenter.Hide();
        _settingsPresenter.Hide();
        if (!returningFromResult)
        {
            _resultPresenter.Hide();
        }
        _waveHost.StopRound();
        _menuContentGroup.alpha = 1f;
        _menuContentGroup.interactable = false;
        _menuContentGroup.blocksRaycasts = false;
        _inGameGroup.alpha = 1f;
        _inGameGroup.interactable = false;
        _inGameGroup.blocksRaycasts = false;
        _mainMenuCanvasObj.SetActive(true);
        Canvas.ForceUpdateCanvases();

        float menuTravel = ((RectTransform)_mainMenuCanvasObj.transform).rect.height;
        float gameTravel = ((RectTransform)_inGameCanvasObj.transform).rect.height;
        _menuBackgroundRectTrans.anchoredPosition = _menuBackgroundPosition + Vector2.down * menuTravel;
        _transition = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < _menuRectTransforms.Length; i++)
        {
            _menuRectTransforms[i].anchoredPosition = _menuPositions[i] + Vector2.down * menuTravel;
            _transition.Insert(0f, _menuRectTransforms[i].DOAnchorPos(_menuPositions[i], _returnSeconds).SetEase(Ease.InOutCubic));
        }
        _transition.Insert(0f, _menuBackgroundRectTrans.DOAnchorPos(_menuBackgroundPosition, _returnSeconds).SetEase(Ease.InOutCubic));
        _transition.Insert(0f, _hudRectTrans.DOAnchorPos(_hudRectTrans.anchoredPosition + Vector2.up * gameTravel, _returnSeconds).SetEase(Ease.InOutCubic));
        _transition.Insert(0f, _arenaRectTrans.DOAnchorPos(_arenaRectTrans.anchoredPosition + Vector2.up * gameTravel, _returnSeconds).SetEase(Ease.InOutCubic));
        if (returningFromResult)
        {
            _returningResultRectTrans = _resultPresenter.PrepareMenuReturn();
            _resultReturnPosition = _returningResultRectTrans.anchoredPosition;
            float resultTravel = ((RectTransform)_returningResultRectTrans.GetComponentInParent<Canvas>().transform).rect.height;
            _transition.Insert(0f, _returningResultRectTrans.DOAnchorPos(_resultReturnPosition + Vector2.up * resultTravel, _returnSeconds).SetEase(Ease.InOutCubic));
        }
        _transition.OnComplete(ShowMenuImmediately);
    }

    private void ShowMenuImmediately()
    {
        HideJoystick();
        KillTransition();
        ReleasePause();
        _pausePresenter.Hide();
        _howToPresenter.Hide();
        _settingsPresenter.Hide();
        _resultPresenter.Hide();
        if (_returningResultRectTrans != null)
        {
            _returningResultRectTrans.anchoredPosition = _resultReturnPosition;
            _returningResultRectTrans = null;
        }
        _waveHost.StopRound();
        _inGameCanvasObj.SetActive(false);
        _gameplayObj.SetActive(false);
        for (int i = 0; i < _menuRectTransforms.Length; i++)
        {
            _menuRectTransforms[i].anchoredPosition = _menuPositions[i];
        }
        _menuBackgroundRectTrans.anchoredPosition = _menuBackgroundPosition;
        _hudRectTrans.anchoredPosition = _hudPosition;
        _arenaRectTrans.anchoredPosition = _arenaPosition;
        _hudRectTrans.gameObject.SetActive(true);
        _arenaRectTrans.gameObject.SetActive(true);
        _arenaCamera.enabled = true;
        _menuContentGroup.alpha = 1f;
        _menuContentGroup.interactable = true;
        _menuContentGroup.blocksRaycasts = true;
        _inGameGroup.alpha = 1f;
        _mainMenuCanvasObj.SetActive(true);
        _state = FLOW_STATE.MENU;
        if (_backgroundMusic != null)
            _backgroundMusic.StartAfterMenuShown();
    }

    private void KillTransition()
    {
        if (_transition != null && _transition.IsActive())
        {
            _transition.Kill();
        }
        _transition = null;
        if (_arenaCamera != null)
        {
            _arenaCamera.EndImpactZoom();
        }
        if (_gameOverAudio != null)
        {
            _gameOverAudio.Stop();
        }
        if (_backgroundMusic != null)
        {
            _backgroundMusic.SetDuckMultiplier(1f);
        }
    }
}
