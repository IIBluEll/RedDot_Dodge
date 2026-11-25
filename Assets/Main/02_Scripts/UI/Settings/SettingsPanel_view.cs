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
    private bool _tiltSensorAvailable;
    private bool _calibrationRequested;
    private bool _calibrationCompleted;
    private bool _calibrationFailed;
    private bool _useJoystick;
    private TMP_Text _mobileControlsLabelTxt;
    private TMP_Text _mobileControlsValueTxt;
    private TMP_Text _sensitivityLabelTxt;

    private void OnEnable()
    {
        GameLocalization.LanguageChanged += RefreshLanguage;
        RefreshLanguage();
    }

    private void OnDisable()
    {
        GameLocalization.LanguageChanged -= RefreshLanguage;
    }

    public void InitializeMobileControls(UnityEngine.Events.UnityAction onControlsChanged)
    {
        if (_mobileControlsValueTxt != null)
            return;

        // 기존 언어 선택 행의 글꼴과 좌우 버튼을 재사용합니다.
        var languageRowRectTrans = (RectTransform)_languageTxt.transform.parent;
        RectTransform controlsRowRectTrans = Instantiate(languageRowRectTrans, languageRowRectTrans.parent, false);
        controlsRowRectTrans.name = "Mobile Controls";
        var label = controlsRowRectTrans.GetComponentInChildren<LocalizedText>(true);
        _mobileControlsLabelTxt = label.GetComponent<TMP_Text>();
        label.enabled = false;
        foreach (TMP_Text text in controlsRowRectTrans.GetComponentsInChildren<TMP_Text>(true))
            if (text != _mobileControlsLabelTxt)
                _mobileControlsValueTxt = text;
        foreach (Button button in controlsRowRectTrans.GetComponentsInChildren<Button>(true))
        {
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onControlsChanged);
        }
        controlsRowRectTrans.GetComponentInChildren<SettingsIconImage>(true).ShowJoystickIcon();

        var sensitivityLabel = _tiltGroup.GetComponentInChildren<LocalizedText>(true);
        _sensitivityLabelTxt = sensitivityLabel.GetComponent<TMP_Text>();
        sensitivityLabel.enabled = false;

        SetRowHeight((RectTransform)_bgmSlider.transform.parent, 198f);
        SetRowHeight((RectTransform)_sfxSlider.transform.parent, 113f);
        SetRowHeight(controlsRowRectTrans, 28f);
        SetRowHeight((RectTransform)_tiltGroup.transform, -65f);
        SetRowHeight(languageRowRectTrans, -213f);
    }

    private static void SetRowHeight(RectTransform rowRectTrans, float y)
    {
        rowRectTrans.anchoredPosition = new Vector2(rowRectTrans.anchoredPosition.x, y);
    }

    public void Show(SettingsModel_model model, bool showTiltSettings, bool tiltSensorAvailable)
    {
        gameObject.SetActive(true);
        _showTiltSettings = showTiltSettings;
        _tiltSensorAvailable = tiltSensorAvailable;
        _calibrationRequested = false;
        _calibrationCompleted = false;
        _calibrationFailed = false;
        bool showSensitivitySettings = showTiltSettings || (Application.isMobilePlatform && model.UseJoystick);
        _tiltGroup.alpha = showSensitivitySettings ? 1f : 0.4f;
        _tiltGroup.interactable = showSensitivitySettings;
        _recalibrateBtn.gameObject.SetActive(showTiltSettings);
        Refresh(model);
        _bgmSlider.Select();
    }

    public void Refresh(SettingsModel_model model)
    {
        _useJoystick = Application.isMobilePlatform && model.UseJoystick;
        _bgmSlider.SetValueWithoutNotify(model.BgmVolume);
        _bgmVolumeTxt.text = $"{Mathf.RoundToInt(model.BgmVolume * 100f)}%";
        _sfxSlider.SetValueWithoutNotify(model.SfxVolume);
        _sfxVolumeTxt.text = $"{Mathf.RoundToInt(model.SfxVolume * 100f)}%";
        float sensitivity = _useJoystick ? model.JoystickSensitivity : model.TiltSensitivity;
        _sensitivitySlider.SetValueWithoutNotify(sensitivity);
        _sensitivityTxt.text = $"{Mathf.RoundToInt(sensitivity * 100f)}%";
        RefreshLanguage();
    }

    private void RefreshLanguage()
    {
        _languageTxt.text = GameLocalization.Language == GAME_LANGUAGE.KOREAN ? "한국어" : "English";
        if (_mobileControlsLabelTxt != null)
        {
            _mobileControlsLabelTxt.text = GameLocalization.GetText("조작 방식", "Controls");
            _mobileControlsValueTxt.text = _useJoystick ? GameLocalization.GetText("조이스틱", "Joystick") : GameLocalization.GetText("기울기", "Tilt");
        }
        if (_sensitivityLabelTxt != null)
        {
            string label = _useJoystick ? GameLocalization.GetText("조이스틱 감도", "Joystick sensitivity") : GameLocalization.GetText("기울기 감도", "Tilt sensitivity");
            _sensitivityLabelTxt.text = label + "\n<size=21><color=#92A5BF>(Android)</color></size>";
        }
        _calibrationHintTxt.text = string.Empty;
        if (_showTiltSettings && !_tiltSensorAvailable)
        {
            _calibrationHintTxt.text = GameLocalization.GetText("센서 미지원 · 조이스틱을 선택하세요", "No tilt sensor. Choose Joystick.");
        }
        else if (_showTiltSettings)
        {
            _calibrationHintTxt.text = GameLocalization.GetText("현재 자세를 기준으로 보정합니다", "Use your current tilt as neutral");
        }
        if (_showTiltSettings && _tiltSensorAvailable && _calibrationRequested)
        {
            _calibrationHintTxt.text = _calibrationFailed
                ? GameLocalization.GetText("보정하지 못했습니다. 자세를 고정하고 다시 눌러 주세요", "Calibration failed. Hold still and try again")
                : _calibrationCompleted
                ? GameLocalization.GetText("현재 자세로 보정했습니다", "Current tilt set as neutral")
                : GameLocalization.GetText("휴대폰을 편한 자세로 가만히 유지해 주세요", "Hold your device still in a comfortable position");
        }
        _recalibrateBtn.interactable = _showTiltSettings && _tiltSensorAvailable && (!_calibrationRequested || _calibrationCompleted || _calibrationFailed);
    }

    public void SetTiltSensorAvailable(bool available)
    {
        if (_tiltSensorAvailable == available)
            return;
        _tiltSensorAvailable = available;
        RefreshLanguage();
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
        _calibrationRequested = true;
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
