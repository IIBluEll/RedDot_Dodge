using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsPanel_view : MonoBehaviour
{
    [SerializeField] private RectTransform _panelRectTrans;
    [SerializeField] private Slider _volumeSlider;
    [SerializeField] private TMP_Text _volumeTxt;
    [SerializeField] private TMP_Text _muteTxt;
    [SerializeField] private GameObject _tiltSettingsObj;
    [SerializeField] private Slider _sensitivitySlider;
    [SerializeField] private TMP_Text _sensitivityTxt;
    [SerializeField] private Button _recalibrateBtn;
    [SerializeField] private TMP_Text _calibrationHintTxt;

    public void Show(SettingsModel_model model, bool showTiltSettings, bool canRecalibrate)
    {
        gameObject.SetActive(true);
        _panelRectTrans.sizeDelta = new Vector2(_panelRectTrans.sizeDelta.x, showTiltSettings ? 880f : 600f);
        _tiltSettingsObj.SetActive(showTiltSettings);
        _recalibrateBtn.interactable = canRecalibrate;
        _calibrationHintTxt.text = canRecalibrate ? "편한 자세에서 현재 기울기를 기준으로 잡으세요." : "게임 시작 시 현재 자세를 기준으로 자동 보정합니다.";
        Refresh(model);
        _volumeSlider.Select();
    }

    public void Refresh(SettingsModel_model model)
    {
        _volumeSlider.SetValueWithoutNotify(model.Volume);
        _volumeTxt.text = $"{Mathf.RoundToInt(model.Volume * 100f)}%";
        _muteTxt.text = model.IsMuted ? "음소거 해제" : "음소거 켜기";
        _sensitivitySlider.SetValueWithoutNotify(model.TiltSensitivity);
        _sensitivityTxt.text = $"×{model.TiltSensitivity:F2}";
    }

    public void ShowCalibrationRequested()
    {
        _calibrationHintTxt.text = "현재 자세를 기준으로 보정합니다.";
    }

    public void Hide()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
        gameObject.SetActive(false);
    }
}
