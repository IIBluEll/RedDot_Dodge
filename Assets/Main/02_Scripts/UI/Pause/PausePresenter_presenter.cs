using UnityEngine;

[DisallowMultipleComponent]
public sealed class PausePresenter_presenter : MonoBehaviour
{
    [SerializeField] private PausePanel_view _view;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private Camera _gameCamera;
    private (bool Available, bool Calibrating, bool Failed, bool Ready)? _tiltStatus;

    private void Update()
    {
        RefreshTiltStatus();
    }

    private void RefreshTiltStatus()
    {
        if (!_view.isActiveAndEnabled || !_playerMovement.UsesTiltInput)
            return;

        var status = (Available: _playerMovement.HasTiltSensor, Calibrating: _playerMovement.IsTiltCalibrating,
            Failed: _playerMovement.TiltCalibrationFailed, Ready: _playerMovement.CanPlayWithTilt);
        if (_tiltStatus == status)
            return;

        _tiltStatus = status;
        _view.ShowTiltCalibration(status.Available, status.Calibrating, status.Failed, status.Ready);
    }

    public void Initialize()
    {
        Hide();
    }

    public void ShowPause()
    {
        _tiltStatus = null;
        _view.ShowPause(_playerMovement.UsesTiltInput);
        RefreshTiltStatus();
    }

    public void ShowExitConfirmation()
    {
        _view.ShowExitConfirmation();
    }

    public void BeginResume()
    {
        _view.ShowResume();
        UpdateResume();
    }

    public void UpdateResume()
    {
        _view.UpdateResume(_gameCamera.WorldToScreenPoint(_playerMovement.transform.position));
    }

    public void RecenterTilt()
    {
        if (!_playerMovement.UsesTiltInput || !_playerMovement.HasTiltSensor)
            return;

        _playerMovement.RecenterTilt();
        RefreshTiltStatus();
    }

    public void Hide()
    {
        if (_view != null)
            _view.Hide();
    }
}
