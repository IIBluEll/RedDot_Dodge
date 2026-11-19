using UnityEngine;

[DisallowMultipleComponent]
public sealed class SettingsPresenter_presenter : MonoBehaviour
{
    [SerializeField] private SettingsPanel_view _view;
    [SerializeField] private PlayerMovement _playerMovement;

    private SettingsModel_model _model;

    public void Initialize()
    {
        _model = new SettingsModel_model();
        ApplySettings();
        _view.Hide();
    }

    public void Show()
    {
        _view.Show(_model, Application.isMobilePlatform, _playerMovement.gameObject.activeInHierarchy);
    }

    public void OnVolumeChanged(float volume)
    {
        _model.SetVolume(volume);
        ApplySettings();
        _view.Refresh(_model);
    }

    public void OnMuteActioned()
    {
        _model.ToggleMute();
        ApplySettings();
        _view.Refresh(_model);
    }

    public void OnSensitivityChanged(float sensitivity)
    {
        _model.SetTiltSensitivity(sensitivity);
        ApplySettings();
        _view.Refresh(_model);
    }

    public void OnRecenterActioned()
    {
        if (!_playerMovement.gameObject.activeInHierarchy)
            return;
        _playerMovement.RecenterTilt();
        _view.ShowCalibrationRequested();
    }

    public void Hide()
    {
        if (_view != null)
            _view.Hide();
        _model?.Save();
    }

    private void ApplySettings()
    {
        AudioListener.volume = _model.IsMuted ? 0f : _model.Volume;
        _playerMovement.SetTiltSensitivity(_model.TiltSensitivity);
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            _model?.Save();
    }

    private void OnDisable()
    {
        _model?.Save();
    }
}
