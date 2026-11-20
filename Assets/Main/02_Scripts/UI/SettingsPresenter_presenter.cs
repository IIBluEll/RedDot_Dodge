using UnityEngine;

[DisallowMultipleComponent]
public sealed class SettingsPresenter_presenter : MonoBehaviour
{
    [SerializeField] private SettingsPanel_view _view;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private BackgroundMusicPlayer _backgroundMusic;

    private SettingsModel_model _model;

    public void Initialize()
    {
        _model = new SettingsModel_model();
        GameLocalization.SetLanguage(_model.Language);
        ApplySettings();
        _view.Hide();
    }

    public void Show()
    {
        _view.Show(_model, Application.isMobilePlatform, _playerMovement.gameObject.activeInHierarchy);
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
        if (!Application.isMobilePlatform)
        {
            return;
        }
        _model.SetTiltSensitivity(sensitivity);
        ApplySettings();
        _view.Refresh(_model);
    }

    public void OnRecenterActioned()
    {
        if (!Application.isMobilePlatform || !_playerMovement.gameObject.activeInHierarchy)
        {
            return;
        }
        _playerMovement.RecenterTilt();
        _view.ShowCalibrationRequested();
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
