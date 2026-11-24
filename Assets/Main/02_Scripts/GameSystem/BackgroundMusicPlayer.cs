using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BackgroundMusicTrack
{
    [SerializeField, Tooltip("비워 두면 AudioClip 이름을 표시합니다.")] private string _title;
    [SerializeField] private AudioClip _clip;

    public AudioClip Clip => _clip;
    public string Title => string.IsNullOrWhiteSpace(_title) ? (_clip != null ? _clip.name : string.Empty) : _title.Trim();
}

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class BackgroundMusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private List<BackgroundMusicTrack> _tracks = new();
    [SerializeField, Min(0f)] private float _startDelaySeconds = 2f;
    [SerializeField] private bool _loopPlaylist = true;

    private int _currentTrackIndex = -1;
    private BackgroundMusicTrack _pendingTrack;
    private bool _playlistActive;
    private bool _waitingForLoad;
    private bool _applicationPaused;
    private bool _hasFocus = true;
    private bool _pausedByApplication;
    private bool _startRequested;
    private Coroutine _startRoutine;
    private float _baseVolume;
    private float _volumeMultiplier = 1f;
    private float _duckMultiplier = 1f;
    private bool _audioInitialized;

    public string CurrentTitle { get; private set; }
    public bool IsPlaybackPaused => _applicationPaused || !_hasFocus;
    public event Action<string> TrackChanged;

    private void Awake()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
        _hasFocus = Application.isFocused;
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
        // 게임 일시정지의 효과음 정지와 BGM 재생을 분리합니다.
        _audioSource.ignoreListenerPause = true;
        _audioSource.ignoreListenerVolume = true;
        _baseVolume = _audioSource.volume;
        _audioInitialized = true;
        ApplyVolume();
    }

    public void SetVolume(float volume)
    {
        _volumeMultiplier = Mathf.Clamp01(volume);
        ApplyVolume();
    }

    public void SetDuckMultiplier(float multiplier)
    {
        _duckMultiplier = Mathf.Clamp01(multiplier);
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (_audioInitialized)
        {
            _audioSource.volume = _baseVolume * _volumeMultiplier * _duckMultiplier;
        }
    }

    public void StartAfterMenuShown()
    {
        if (_startRequested)
            return;
        _startRequested = true;
        _startRoutine = StartCoroutine(StartAfterMenuShown_cor());
    }

    private IEnumerator StartAfterMenuShown_cor()
    {
        yield return new WaitForEndOfFrame();
        PrepareNextTrack();
        yield return new WaitForSecondsRealtime(_startDelaySeconds);
        _startRoutine = null;
        PlayPlaylist();
    }

    public void PlayPlaylist()
    {
        StopPlaylist();
        _playlistActive = true;
        if (!TryPlayNextTrack())
            StopPlaylist();
    }

    public void StopPlaylist()
    {
        if (_startRoutine != null)
        {
            StopCoroutine(_startRoutine);
            _startRoutine = null;
        }
        _playlistActive = false;
        _waitingForLoad = false;
        _pausedByApplication = false;
        _pendingTrack = null;
        _currentTrackIndex = -1;
        CurrentTitle = string.Empty;
        if (_audioSource != null)
            _audioSource.Stop();
    }

    private void Update()
    {
        if (!_playlistActive || IsPlaybackPaused)
            return;

        if (_waitingForLoad)
        {
            AudioClip clip = _pendingTrack?.Clip;
            if (clip == null || clip.loadState == AudioDataLoadState.Failed)
            {
                if (!TryPlayNextTrack())
                    StopPlaylist();
            }
            else if (clip.loadState == AudioDataLoadState.Loaded)
                StartLoadedTrack();
            return;
        }

        if (!_audioSource.isPlaying && !TryPlayNextTrack())
            StopPlaylist();
    }

    private bool TryPlayNextTrack()
    {
        int nextIndex = _currentTrackIndex;
        for (int attempt = 0; attempt < _tracks.Count; attempt++)
        {
            nextIndex++;
            if (nextIndex >= _tracks.Count)
            {
                if (!_loopPlaylist)
                    return false;
                nextIndex = 0;
            }

            BackgroundMusicTrack track = _tracks[nextIndex];
            AudioClip clip = track?.Clip;
            if (clip == null || clip.loadState == AudioDataLoadState.Failed)
                continue;
            if (clip.loadState == AudioDataLoadState.Unloaded)
            {
                bool loaded = clip.LoadAudioData();
                // 백그라운드 로딩이 시작됐으면 완료되지 않았어도 이 곡을 기다립니다.
                if (!loaded && clip.loadState == AudioDataLoadState.Unloaded)
                    continue;
            }
            if (clip.loadState == AudioDataLoadState.Failed)
                continue;

            _currentTrackIndex = nextIndex;
            _pendingTrack = track;
            _waitingForLoad = true;
            if (clip.loadState == AudioDataLoadState.Loaded && !IsPlaybackPaused)
                StartLoadedTrack();
            return true;
        }
        return false;
    }

    private void StartLoadedTrack()
    {
        _audioSource.clip = _pendingTrack.Clip;
        _audioSource.Play();
        _waitingForLoad = false;
        CurrentTitle = _pendingTrack.Title;
        TrackChanged?.Invoke(CurrentTitle);
        PrepareNextTrack();
    }

    private void PrepareNextTrack()
    {
        int nextIndex = _currentTrackIndex;
        for (int attempt = 0; attempt < _tracks.Count; attempt++)
        {
            nextIndex++;
            if (nextIndex >= _tracks.Count)
            {
                if (!_loopPlaylist)
                    return;
                nextIndex = 0;
            }

            AudioClip clip = _tracks[nextIndex]?.Clip;
            if (clip == null || clip.loadState == AudioDataLoadState.Failed)
                continue;
            if (clip.loadState != AudioDataLoadState.Unloaded)
                return;
            // 동기 로딩 설정의 음원을 재생 도중 미리 불러오면 같은 끊김이 생깁니다.
            if (!clip.loadInBackground)
                return;

            bool loaded = clip.LoadAudioData();
            if (clip.loadState == AudioDataLoadState.Failed || (!loaded && clip.loadState == AudioDataLoadState.Unloaded))
                continue;
            return;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        _applicationPaused = paused;
        RefreshApplicationPause();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        _hasFocus = hasFocus;
        RefreshApplicationPause();
    }

    private void RefreshApplicationPause()
    {
        if (!_playlistActive || _waitingForLoad || _audioSource == null)
        {
            return;
        }

        if (IsPlaybackPaused)
        {
            if (!_pausedByApplication)
            {
                _audioSource.Pause();
                _pausedByApplication = true;
            }
        }
        else if (_pausedByApplication)
        {
            _audioSource.UnPause();
            _pausedByApplication = false;
        }
    }

    private void OnDisable()
    {
        StopPlaylist();
    }
}
