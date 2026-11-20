using UnityEngine;

[DisallowMultipleComponent]
public sealed class PausePresenter_presenter : MonoBehaviour
{
    [SerializeField] private PausePanel_view _view;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private Camera _gameCamera;
    private bool _waitingForCalibration;

    private void Update()
    {
        if (!_waitingForCalibration || _playerMovement.IsTiltCalibrating)
        {
            return;
        }
        _waitingForCalibration = false;
        _view.ShowTiltCalibration(false, !_playerMovement.TiltCalibrationFailed, _playerMovement.TiltCalibrationFailed);
    }

    public void Initialize()
    {
        Hide();
    }

    public void ShowPause()
    {
        _waitingForCalibration = _playerMovement.IsTiltCalibrating;
        _view.ShowPause(_playerMovement.UsesTiltInput);
        if (_waitingForCalibration)
        {
            _view.ShowTiltCalibration(true, false);
        }
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
        _playerMovement.RecenterTilt();
        _waitingForCalibration = true;
        _view.ShowTiltCalibration(true, false);
    }

    public void Hide()
    {
        if (_view != null)
            _view.Hide();
    }
}
