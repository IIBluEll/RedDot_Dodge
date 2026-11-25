using DG.Tweening;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MusicToast_view : MonoBehaviour
{
    [SerializeField] private RectTransform _panelRectTrans;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private TMP_Text _titleTxt;
    [SerializeField] private Vector2 _shownPosition = new Vector2(32f, 32f);
    [SerializeField, Min(0.01f)] private float _enterSeconds = 0.25f;
    [SerializeField, Min(0f)] private float _displaySeconds = 3.5f;
    [SerializeField, Min(0.01f)] private float _exitSeconds = 0.22f;

    private Sequence _animation;

    public void Show(string title)
    {
        KillAnimation();
        _titleTxt.text = title;
        _titleTxt.richText = false;
        gameObject.SetActive(true);
        Vector2 hiddenPosition = _shownPosition + Vector2.left * (_panelRectTrans.rect.width + _shownPosition.x + 24f);
        _panelRectTrans.anchoredPosition = hiddenPosition;
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;

        _animation = DOTween.Sequence().SetUpdate(true);
        _animation.Append(_panelRectTrans.DOAnchorPos(_shownPosition, _enterSeconds).SetEase(Ease.OutCubic));
        _animation.Join(_canvasGroup.DOFade(1f, _enterSeconds));
        _animation.AppendInterval(_displaySeconds);
        _animation.Append(_panelRectTrans.DOAnchorPos(hiddenPosition, _exitSeconds).SetEase(Ease.InCubic));
        _animation.Join(_canvasGroup.DOFade(0f, _exitSeconds));
        _animation.OnComplete(() =>
        {
            _animation = null;
            gameObject.SetActive(false);
        });
    }

    public void SetPaused(bool paused)
    {
        if (_animation == null || !_animation.IsActive())
            return;
        if (paused)
            _animation.Pause();
        else
            _animation.Play();
    }

    public void Hide()
    {
        KillAnimation();
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        KillAnimation();
    }

    private void KillAnimation()
    {
        if (_animation != null && _animation.IsActive())
            _animation.Kill();
        _animation = null;
    }
}
