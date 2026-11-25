using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ResultPanel_view : MonoBehaviour
{
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _panelRectTrans;
    [SerializeField] private TMP_Text _scoreTxt;
    [SerializeField] private TMP_Text _maximumComboTxt;
    [SerializeField] private TMP_Text _survivalTxt;
    [SerializeField] private TMP_Text _killCountTxt;
    [SerializeField] private TMP_Text _bestScoreTxt;
    [SerializeField] private TMP_Text _recordLabelTxt;
    [SerializeField] private Button _retryBtn;
    [SerializeField, Min(0f)] private float _revealDelaySeconds = 0f;
    [SerializeField, Min(0.01f)] private float _revealSeconds = 0.22f;

    private Sequence _reveal;
    private bool _isNewRecord;

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += RefreshRecordLabel;
        RefreshRecordLabel();
    }

    private void RefreshRecordLabel()
    {
        _recordLabelTxt.text = _isNewRecord ? GameLocalization.GetText("최고 기록!", "NEW BEST!") : GameLocalization.GetText("최고 점수", "BEST SCORE");
    }

    public void Show(RoundResult result, long bestScore, bool isNewRecord)
    {
        KillReveal();
        _scoreTxt.text = result.Score.ToString("N0");
        _maximumComboTxt.text = $"×{result.MaximumCombo:N0}";
        _killCountTxt.text = result.KillCount.ToString("N0");
        int hundredths = Mathf.FloorToInt(result.SurvivalSeconds * 100f);
        _survivalTxt.text = $"{hundredths / 6000:00}:{hundredths / 100 % 60:00}<size=65%>.{hundredths % 100:00}</size>";
        _bestScoreTxt.text = bestScore.ToString("N0");
        _isNewRecord = isNewRecord;
        RefreshRecordLabel();

        gameObject.SetActive(true);
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = true;
        _panelRectTrans.localScale = Vector3.one * 0.96f;
        _reveal = DOTween.Sequence().SetUpdate(true);
        _reveal.AppendInterval(_revealDelaySeconds);
        _reveal.Append(_canvasGroup.DOFade(1f, _revealSeconds));
        _reveal.Join(_panelRectTrans.DOScale(1f, _revealSeconds).SetEase(Ease.OutCubic));
        _reveal.OnComplete(() =>
        {
            _reveal = null;
            _canvasGroup.interactable = true;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(_retryBtn.gameObject);
        });
    }

    public RectTransform PrepareMenuReturn()
    {
        KillReveal();
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = true;
        return (RectTransform)transform;
    }

    public void Hide()
    {
        KillReveal();
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= RefreshRecordLabel;
        KillReveal();
    }

    private void KillReveal()
    {
        if (_reveal != null && _reveal.IsActive())
            _reveal.Kill();
        _reveal = null;
    }
}
