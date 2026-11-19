using UnityEngine;

[DisallowMultipleComponent]
public sealed class PausePresenter_presenter : MonoBehaviour
{
    [SerializeField] private PausePanel_view _view;
    [SerializeField] private PlayerMovement _playerMovement;
    [SerializeField] private Camera _gameCamera;

    public void Initialize()
    {
        Hide();
    }

    public void ShowPause()
    {
        _view.ShowPause(Application.isMobilePlatform);
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
    }

    public void Hide()
    {
        if (_view != null)
            _view.Hide();
    }
}
