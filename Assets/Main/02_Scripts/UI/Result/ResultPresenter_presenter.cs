using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResultPresenter_presenter : MonoBehaviour
{
    [SerializeField] private ResultPanel_view _view;
    [SerializeField] private TMP_Text _hudBestScoreTxt;

    private PlayerPrefsScoreStorage _scoreStorage;
    private RoundResult _result;
    private bool _isNewRecord;

    public void Initialize()
    {
        if (_scoreStorage == null)
            _scoreStorage = new PlayerPrefsScoreStorage();
        RefreshHudBestScore();
        Hide();
    }

    public void PrepareResult(RoundResult result)
    {
        _result = result;
        _isNewRecord = _scoreStorage.TrySaveBestScore(result.Score);
        RefreshHudBestScore();
    }

    public void ShowResult()
    {
        _view.Show(_result, _scoreStorage.BestScore, _isNewRecord);
    }

    public RectTransform PrepareMenuReturn()
    {
        return _view.PrepareMenuReturn();
    }

    public void Hide()
    {
        if (_view != null)
            _view.Hide();
    }

    private void RefreshHudBestScore()
    {
        _hudBestScoreTxt.text = _scoreStorage.BestScore.ToString("N0");
    }
}
