using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class InGameFlowController : MonoBehaviour
{
    private enum FLOW_STATE { MENU, HOW_TO, SETTINGS, START_TRANSITION, IN_GAME, PAUSED, CONFIRM_EXIT, RESUMING, DEATH_TRANSITION, RESULT, RETURN_TRANSITION }

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

    [Header("Menu Transition")]
    [SerializeField] private CanvasGroup _menuContentGroup;
    [SerializeField] private RectTransform _titleRectTrans;
    [SerializeField] private RectTransform _startBtnRectTrans;
    [SerializeField] private RectTransform _howToBtnRectTrans;
    [SerializeField] private RectTransform _settingsBtnRectTrans;

    [Header("InGame Transition")]
    [SerializeField] private CanvasGroup _inGameGroup;
    [SerializeField] private RectTransform _hudRectTrans;
    [SerializeField] private RectTransform _arenaRectTrans;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float _menuExitSeconds = 0.36f;
    [SerializeField, Min(0.01f)] private float _gameEnterSeconds = 0.26f;
    [SerializeField, Min(0.01f)] private float _returnSeconds = 0.22f;
    [SerializeField, Min(0f)] private float _deathBurstSeconds = 0.28f;
    [SerializeField, Min(0.01f)] private float _deathExitSeconds = 0.42f;
    [SerializeField, Min(0.01f)] private float _resumePrepareSeconds = 1f;

    private RectTransform[] _menuRectTransforms;
    private Vector2[] _menuPositions;
    private Vector2 _hudPosition;
    private Vector2 _arenaPosition;
    private Sequence _transition;
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
            || _menuContentGroup == null || _titleRectTrans == null || _startBtnRectTrans == null
            || _howToBtnRectTrans == null || _settingsBtnRectTrans == null || _inGameGroup == null
            || _hudRectTrans == null || _arenaRectTrans == null || _howToPresenter == null || _settingsPresenter == null)
        {
            Debug.LogError("InGameFlowController의 메뉴·인게임·결과·일시정지·안내·설정 참조를 연결해 주세요.", this);
            enabled = false;
            return;
        }

        _menuRectTransforms = new[] { _titleRectTrans, _startBtnRectTrans, _howToBtnRectTrans, _settingsBtnRectTrans };
        _menuPositions = new Vector2[_menuRectTransforms.Length];
        for (int i = 0; i < _menuRectTransforms.Length; i++)
            _menuPositions[i] = _menuRectTransforms[i].anchoredPosition;
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
        ReleasePause();
        KillTransition();
    }

    private void Update()
    {
        if (!_initialized)
            return;

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
        if (!_hasFocus || _applicationPaused)
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
        _pausePresenter.ShowPause();
    }

    public void OnResumeActioned()
    {
        if (!_initialized || _state != FLOW_STATE.PAUSED || !_hasFocus || _applicationPaused)
            return;

        _state = FLOW_STATE.RESUMING;
        _resumeRemainingSeconds = _resumePrepareSeconds;
        _pausePresenter.BeginResume();
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
            return;

        _state = FLOW_STATE.START_TRANSITION;
        _menuContentGroup.interactable = false;
        _menuContentGroup.blocksRaycasts = false;
        Canvas.ForceUpdateCanvases();
        float travel = ((RectTransform)_mainMenuCanvasObj.transform).rect.height + _titleRectTrans.rect.height;
        _hudRectTrans.anchoredPosition = _hudPosition + Vector2.up * 48f;
        _arenaRectTrans.anchoredPosition = _arenaPosition + Vector2.down * 72f;
        _inGameGroup.alpha = 0f;
        _inGameGroup.interactable = false;
        _inGameGroup.blocksRaycasts = false;

        _transition = DOTween.Sequence().SetUpdate(true);
        for (int i = 0; i < _menuRectTransforms.Length; i++)
        {
            float destination = _menuPositions[i].y + (i == 0 ? travel : -travel);
            float delay = i == 0 ? 0f : (i - 1) * 0.035f;
            _transition.Insert(delay, _menuRectTransforms[i].DOAnchorPosY(destination, _menuExitSeconds).SetEase(Ease.InBack));
        }
        _transition.Insert(_menuExitSeconds * 0.5f, _menuContentGroup.DOFade(0f, _menuExitSeconds * 0.5f));
        _transition.AppendCallback(ShowInGameForTransition);
        _transition.Append(_inGameGroup.DOFade(1f, _gameEnterSeconds).SetEase(Ease.OutQuad));
        _transition.Join(_hudRectTrans.DOAnchorPos(_hudPosition, _gameEnterSeconds).SetEase(Ease.OutCubic));
        _transition.Join(_arenaRectTrans.DOAnchorPos(_arenaPosition, _gameEnterSeconds).SetEase(Ease.OutCubic));
        _transition.OnComplete(OnStartTransitionCompleted);
    }

    private void ShowInGameForTransition()
    {
        _mainMenuCanvasObj.SetActive(false);
        _gameplayObj.SetActive(true);
        _waveHost.StopRound();
        _inGameCanvasObj.SetActive(true);
    }

    private void OnStartTransitionCompleted()
    {
        _transition = null;
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
            OnPauseActioned();
    }

    private void OnRoundEnded(RoundResult result)
    {
        if (_state != FLOW_STATE.IN_GAME)
            return;

        _state = FLOW_STATE.DEATH_TRANSITION;
        _inGameGroup.interactable = false;
        _inGameGroup.blocksRaycasts = false;
        _resultPresenter.PrepareResult(result);
        _arenaCamera.enabled = false;
        Canvas.ForceUpdateCanvases();
        float travel = ((RectTransform)_inGameCanvasObj.transform).rect.height + _hudRectTrans.rect.height;

        _transition = DOTween.Sequence().SetUpdate(true);
        _transition.Insert(_deathBurstSeconds, _hudRectTrans.DOAnchorPosY(_hudPosition.y + travel, _deathExitSeconds).SetEase(Ease.InBack));
        _transition.Insert(_deathBurstSeconds + 0.04f, _arenaRectTrans.DOAnchorPosY(_arenaPosition.y - travel, _deathExitSeconds).SetEase(Ease.InBack));
        _transition.OnComplete(OnDeathTransitionCompleted);
    }

    private void OnDeathTransitionCompleted()
    {
        _transition = null;
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
            return;
        if (_state == FLOW_STATE.START_TRANSITION)
        {
            ShowMenuImmediately();
            return;
        }

        _state = FLOW_STATE.RETURN_TRANSITION;
        KillTransition();
        ReleasePause();
        _pausePresenter.Hide();
        _howToPresenter.Hide();
        _settingsPresenter.Hide();
        _resultPresenter.Hide();
        _waveHost.StopRound();
        _inGameGroup.interactable = false;
        _inGameGroup.blocksRaycasts = false;
        _transition = DOTween.Sequence().SetUpdate(true);
        _transition.Append(_inGameGroup.DOFade(0f, _returnSeconds));
        _transition.Join(_hudRectTrans.DOAnchorPos(_hudPosition + Vector2.up * 48f, _returnSeconds).SetEase(Ease.InCubic));
        _transition.Join(_arenaRectTrans.DOAnchorPos(_arenaPosition + Vector2.down * 72f, _returnSeconds).SetEase(Ease.InCubic));
        _transition.OnComplete(ShowMenuImmediately);
    }

    private void ShowMenuImmediately()
    {
        KillTransition();
        ReleasePause();
        _pausePresenter.Hide();
        _howToPresenter.Hide();
        _settingsPresenter.Hide();
        _resultPresenter.Hide();
        _waveHost.StopRound();
        _inGameCanvasObj.SetActive(false);
        _gameplayObj.SetActive(false);
        for (int i = 0; i < _menuRectTransforms.Length; i++)
            _menuRectTransforms[i].anchoredPosition = _menuPositions[i];
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
            _transition.Kill();
        _transition = null;
    }
}
