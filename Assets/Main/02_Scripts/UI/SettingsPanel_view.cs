using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class SettingsPanel_view : MonoBehaviour
{
    [SerializeField] private Slider _bgmSlider;
    [SerializeField] private TMP_Text _bgmVolumeTxt;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private TMP_Text _sfxVolumeTxt;
    [SerializeField] private CanvasGroup _tiltGroup;
    [SerializeField] private Slider _sensitivitySlider;
    [SerializeField] private TMP_Text _sensitivityTxt;
    [SerializeField] private Button _recalibrateBtn;
    [SerializeField] private TMP_Text _calibrationHintTxt;
    [SerializeField] private TMP_Text _languageTxt;

    private bool _showTiltSettings;
    private bool _canRecalibrate;
    private bool _calibrationRequested;
    private bool _calibrationCompleted;
    private bool _calibrationFailed;

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += RefreshLanguage;
        RefreshLanguage();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= RefreshLanguage;
    }

    public void Show(SettingsModel_model model, bool showTiltSettings, bool canRecalibrate)
    {
        gameObject.SetActive(true);
        _showTiltSettings = showTiltSettings;
        _canRecalibrate = canRecalibrate;
        _calibrationRequested = false;
        _calibrationCompleted = false;
        _calibrationFailed = false;
        _tiltGroup.alpha = showTiltSettings ? 1f : 0.4f;
        _tiltGroup.interactable = showTiltSettings;
        _recalibrateBtn.gameObject.SetActive(showTiltSettings);
        _recalibrateBtn.interactable = showTiltSettings && canRecalibrate;
        Refresh(model);
        _bgmSlider.Select();
    }

    public void Refresh(SettingsModel_model model)
    {
        _bgmSlider.SetValueWithoutNotify(model.BgmVolume);
        _bgmVolumeTxt.text = $"{Mathf.RoundToInt(model.BgmVolume * 100f)}%";
        _sfxSlider.SetValueWithoutNotify(model.SfxVolume);
        _sfxVolumeTxt.text = $"{Mathf.RoundToInt(model.SfxVolume * 100f)}%";
        _sensitivitySlider.SetValueWithoutNotify(model.TiltSensitivity);
        _sensitivityTxt.text = $"{Mathf.RoundToInt(model.TiltSensitivity * 100f)}%";
        RefreshLanguage();
    }

    private void RefreshLanguage()
    {
        _languageTxt.text = GameLocalization.Language == GAME_LANGUAGE.KOREAN ? "한국어" : "English";
        _calibrationHintTxt.text = !_showTiltSettings ? string.Empty : _canRecalibrate
            ? GameLocalization.GetText("현재 자세를 기준으로 보정합니다", "Use your current tilt as neutral")
            : GameLocalization.GetText("모바일에서 기울기를 보정할 수 있습니다", "Tilt calibration is available on mobile");
        if (_showTiltSettings && _calibrationRequested)
        {
            _calibrationHintTxt.text = _calibrationFailed
                ? GameLocalization.GetText("보정하지 못했습니다. 자세를 고정하고 다시 눌러 주세요", "Calibration failed. Hold still and try again")
                : _calibrationCompleted
                ? GameLocalization.GetText("현재 자세로 보정했습니다", "Current tilt set as neutral")
                : GameLocalization.GetText("휴대폰을 편한 자세로 가만히 유지해 주세요", "Hold your device still in a comfortable position");
        }
        _recalibrateBtn.interactable = _showTiltSettings && _canRecalibrate && (!_calibrationRequested || _calibrationCompleted || _calibrationFailed);
    }

    public void ShowCalibrationRequested()
    {
        _calibrationRequested = true;
        _calibrationCompleted = false;
        _calibrationFailed = false;
        RefreshLanguage();
    }

    public void ShowCalibrationCompleted()
    {
        _calibrationCompleted = true;
        RefreshLanguage();
    }

    public void ShowCalibrationFailed()
    {
        _calibrationFailed = true;
        RefreshLanguage();
    }

    public void Hide()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
            && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        gameObject.SetActive(false);
    }
}
