using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScorePresenter_presenter : MonoBehaviour
{
    [SerializeField] private EdgeChaseWaveHost _waveHost;
    [SerializeField] private ScoreText_view _view;
    [SerializeField] private InGameFlowController _flow;

    private void OnEnable()
    {
        if (_waveHost == null || _view == null)
        {
            Debug.LogError("ScorePresenter의 Inspector 참조를 채워 주세요.", this);
            enabled = false;
            return;
        }

        _waveHost.ScoreChanged += OnScoreChanged;
        _view.ShowScore(_waveHost.Score);
        RefreshCombo();
    }

    private void OnDisable()
    {
        if (_waveHost != null)
            _waveHost.ScoreChanged -= OnScoreChanged;
    }

    private void Update()
    {
        RefreshCombo();
    }

    private void OnScoreChanged(long score)
    {
        _view.ShowScore(score);
        RefreshCombo();
    }

    private void RefreshCombo()
    {
        _view.ShowCombo(_waveHost.Combo, _waveHost.ComboRemainingSeconds, _waveHost.ComboWindowSeconds);
        _view.ShowElapsed(_waveHost.SurvivalSeconds);
        _view.ShowReadyCountdown(_flow != null ? _flow.CountdownRemainingSeconds : _waveHost.ReadyRemainingSeconds);
    }
}
