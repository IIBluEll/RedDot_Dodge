using DG.Tweening;
using UnityEngine;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class InGameFlowController : MonoBehaviour
{
    private enum FLOW_STATE { MENU, START_TRANSITION, IN_GAME, RETURN_TRANSITION }

    [Header("Scene")]
    [SerializeField] private GameObject _mainMenuCanvasObj;
    [SerializeField] private GameObject _inGameCanvasObj;
    [SerializeField] private GameObject _gameplayObj;
    [SerializeField] private EdgeChaseWaveHost _waveHost;

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

    private RectTransform[] _menuRectTransforms;
    private Vector2[] _menuPositions;
    private Vector2 _hudPosition;
    private Vector2 _arenaPosition;
    private Sequence _transition;
    private FLOW_STATE _state;
    private bool _initialized;

    private void Awake()
    {
        if (_mainMenuCanvasObj == null || _inGameCanvasObj == null || _gameplayObj == null || _waveHost == null
            || _menuContentGroup == null || _titleRectTrans == null || _startBtnRectTrans == null
            || _howToBtnRectTrans == null || _settingsBtnRectTrans == null || _inGameGroup == null
            || _hudRectTrans == null || _arenaRectTrans == null)
        {
            Debug.LogError("InGameFlowController의 메뉴·인게임 참조를 연결해 주세요.", this);
            enabled = false;
            return;
        }

        _menuRectTransforms = new[] { _titleRectTrans, _startBtnRectTrans, _howToBtnRectTrans, _settingsBtnRectTrans };
        _menuPositions = new Vector2[_menuRectTransforms.Length];
        for (int i = 0; i < _menuRectTransforms.Length; i++)
            _menuPositions[i] = _menuRectTransforms[i].anchoredPosition;
        _hudPosition = _hudRectTrans.anchoredPosition;
        _arenaPosition = _arenaRectTrans.anchoredPosition;
        _initialized = true;
    }

    private void OnEnable()
    {
        if (_initialized)
            ShowMenuImmediately();
    }

    private void OnDisable()
    {
        KillTransition();
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
        if (!_waveHost.BeginRound())
        {
            ShowMenuImmediately();
            return;
        }

        _inGameGroup.interactable = true;
        _inGameGroup.blocksRaycasts = true;
        _state = FLOW_STATE.IN_GAME;
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
        _waveHost.StopRound();
        _inGameCanvasObj.SetActive(false);
        _gameplayObj.SetActive(false);
        for (int i = 0; i < _menuRectTransforms.Length; i++)
            _menuRectTransforms[i].anchoredPosition = _menuPositions[i];
        _hudRectTrans.anchoredPosition = _hudPosition;
        _arenaRectTrans.anchoredPosition = _arenaPosition;
        _menuContentGroup.alpha = 1f;
        _menuContentGroup.interactable = true;
        _menuContentGroup.blocksRaycasts = true;
        _inGameGroup.alpha = 1f;
        _mainMenuCanvasObj.SetActive(true);
        _state = FLOW_STATE.MENU;
    }

    private void KillTransition()
    {
        if (_transition != null && _transition.IsActive())
            _transition.Kill();
        _transition = null;
    }
}
