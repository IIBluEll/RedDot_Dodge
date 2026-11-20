using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PausePanel_view : MonoBehaviour
{
    [SerializeField] private Image _overlayImg;
    [SerializeField] private RectTransform _panelRectTrans;
    [SerializeField] private GameObject _pauseContentObj;
    [SerializeField] private GameObject _confirmationContentObj;
    [SerializeField] private Button _continueBtn;
    [SerializeField] private Button _cancelBtn;
    [SerializeField] private Button _recalibrateBtn;
    [SerializeField] private RectTransform _resumeMarkerRectTrans;

    public void ShowPause(bool showTiltCalibration)
    {
        gameObject.SetActive(true);
        SetOverlayAlpha(0.72f);
        _panelRectTrans.gameObject.SetActive(true);
        _panelRectTrans.sizeDelta = new Vector2(_panelRectTrans.sizeDelta.x, showTiltCalibration ? 820f : 720f);
        _pauseContentObj.SetActive(true);
        _confirmationContentObj.SetActive(false);
        _recalibrateBtn.gameObject.SetActive(showTiltCalibration);
        ShowTiltCalibration(false, false);
        _resumeMarkerRectTrans.gameObject.SetActive(false);
        _continueBtn.Select();
    }

    public void ShowTiltCalibration(bool isCalibrating, bool completed, bool failed = false)
    {
        _recalibrateBtn.interactable = !isCalibrating;
        TMP_Text labelTxt = _recalibrateBtn.GetComponentInChildren<TMP_Text>(true);
        if (labelTxt != null)
        {
            labelTxt.text = failed ? GameLocalization.GetText("보정 실패 · 다시 시도", "Calibration failed · Retry")
                : isCalibrating
                ? GameLocalization.GetText("가만히 유지해 주세요", "Hold your device still")
                : completed ? GameLocalization.GetText("보정 완료 · 다시 보정", "Calibrated · Recalibrate")
                : GameLocalization.GetText("기울기 재보정", "Recalibrate tilt");
        }
    }

    public void ShowExitConfirmation()
    {
        _pauseContentObj.SetActive(false);
        _confirmationContentObj.SetActive(true);
        _cancelBtn.Select();
    }

    public void ShowResume()
    {
        SetOverlayAlpha(0.12f);
        _panelRectTrans.gameObject.SetActive(false);
        _resumeMarkerRectTrans.gameObject.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void UpdateResume(Vector2 playerScreenPosition)
    {
        var parentRectTrans = (RectTransform)_resumeMarkerRectTrans.parent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRectTrans, playerScreenPosition, null, out Vector2 position))
            _resumeMarkerRectTrans.anchoredPosition = position;
        _resumeMarkerRectTrans.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 10f) * 0.08f);
    }

    public void Hide()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
        gameObject.SetActive(false);
    }

    private void SetOverlayAlpha(float alpha)
    {
        Color color = _overlayImg.color;
        color.a = alpha;
        _overlayImg.color = color;
    }
}
