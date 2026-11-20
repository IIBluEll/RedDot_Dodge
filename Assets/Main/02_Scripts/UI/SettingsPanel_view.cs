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
            : GameLocalization.GetText("시작 시 현재 자세로 자동 보정합니다", "Tilt is calibrated when the run starts");
    }

    public void ShowCalibrationRequested()
    {
        _calibrationHintTxt.text = GameLocalization.GetText("현재 자세를 기준으로 보정합니다.", "Current tilt set as neutral.");
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
