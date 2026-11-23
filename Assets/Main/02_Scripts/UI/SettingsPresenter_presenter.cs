using UnityEngine;

[DisallowMultipleComponent]
public sealed class SettingsPresenter_presenter : MonoBehaviour
{
    [SerializeField] private SettingsPanel_view _view;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private BackgroundMusicPlayer _backgroundMusic;

    private SettingsModel_model _model;
    private bool _waitingForCalibration;

    private void Update()
    {
        if (!_playerMovement.gameObject.activeInHierarchy && _playerMovement.IsTiltCalibrating)
        {
            _playerMovement.UpdateTiltCalibration(Time.unscaledDeltaTime);
        }
        if (_view.isActiveAndEnabled)
        {
            _view.SetTiltSensorAvailable(_playerMovement.HasTiltSensor);
        }
        if (!_waitingForCalibration || _playerMovement.IsTiltCalibrating)
        {
            return;
        }
        _waitingForCalibration = false;
        if (_playerMovement.TiltCalibrationFailed)
        {
            _view.ShowCalibrationFailed();
        }
        else
        {
            _view.ShowCalibrationCompleted();
        }
    }

    public void Initialize()
    {
        _model = new SettingsModel_model();
        GameLocalization.SetLanguage(_model.Language);
        if (Application.isMobilePlatform)
        {
            _view.InitializeMobileControls(OnMobileControlsActioned);
        }
        ApplySettings();
        _view.Hide();
    }

    public void Show()
    {
        _waitingForCalibration = _playerMovement.IsTiltCalibrating;
        _view.Show(_model, _playerMovement.UsesTiltInput, _playerMovement.HasTiltSensor);
        if (_waitingForCalibration)
        {
            _view.ShowCalibrationRequested();
        }
        else if (_playerMovement.UsesTiltInput && _playerMovement.TiltCalibrationFailed)
        {
            _view.ShowCalibrationFailed();
        }
    }

    public void OnBgmVolumeChanged(float volume)
    {
        _model.SetBgmVolume(volume);
        ApplySettings();
        _view.Refresh(_model);
    }

    public void OnSfxVolumeChanged(float volume)
    {
        _model.SetSfxVolume(volume);
        ApplySettings();
        _view.Refresh(_model);
    }

    public void OnSensitivityChanged(float sensitivity)
    {
        if (!_playerMovement.UsesTiltInput)
        {
            return;
        }
        _model.SetTiltSensitivity(sensitivity);
        ApplySettings();
        _view.Refresh(_model);
    }

    public void OnRecenterActioned()
    {
        if (!_playerMovement.UsesTiltInput || !_playerMovement.HasTiltSensor)
        {
            return;
        }
        _playerMovement.RecenterTilt();
        _waitingForCalibration = true;
        _view.ShowCalibrationRequested();
    }

    private void OnMobileControlsActioned()
    {
        _model.ToggleMobileControls();
        ApplySettings();
        Show();
        _model.Save();
    }

    public void OnLanguageActioned()
    {
        _model.ToggleLanguage();
        GameLocalization.SetLanguage(_model.Language);
        _view.Refresh(_model);
        _model.Save();
    }

    public void Hide()
    {
        if (_view != null)
        {
            _view.Hide();
        }
        _model?.Save();
    }

    private void ApplySettings()
    {
        AudioListener.volume = _model.SfxVolume;
        _backgroundMusic.SetVolume(_model.BgmVolume);
        _playerMovement.SetTiltSensitivity(_model.TiltSensitivity);
        _playerMovement.SetMobileJoystick(_model.UseJoystick);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            _model?.Save();
        }
    }

    private void OnDisable()
    {
        _model?.Save();
    }
}
