using UnityEngine;

[DisallowMultipleComponent]
public sealed class MusicToastPresenter_presenter : MonoBehaviour
{
    [SerializeField] private BackgroundMusicPlayer _musicPlayer;
    [SerializeField] private MusicToast_view _view;

    private void OnEnable()
    {
        if (_musicPlayer == null || _view == null)
        {
            Debug.LogError("음악 플레이어와 제목 알림 View를 연결해 주세요.", this);
            enabled = false;
            return;
        }

        _musicPlayer.TrackChanged += OnTrackChanged;
        if (!string.IsNullOrEmpty(_musicPlayer.CurrentTitle))
            OnTrackChanged(_musicPlayer.CurrentTitle);
        else
            _view.Hide();
    }

    private void Update()
    {
        _view.SetPaused(_musicPlayer.IsPlaybackPaused);
    }

    private void OnTrackChanged(string title)
    {
        _view.Show(title);
        _view.SetPaused(_musicPlayer.IsPlaybackPaused);
    }

    private void OnDisable()
    {
        if (_musicPlayer != null)
            _musicPlayer.TrackChanged -= OnTrackChanged;
        if (_view != null)
            _view.Hide();
    }
}
