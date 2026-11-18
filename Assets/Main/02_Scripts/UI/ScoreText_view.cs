using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ScoreText_view : MonoBehaviour
{
    [SerializeField] private Text _scoreTxt;
    [SerializeField] private Text _comboTxt;
    [SerializeField] private Text _comboRemainingTxt;
    [SerializeField] private Image _comboFillImg;
    [SerializeField] private Text _elapsedTxt;

    private int _displayedCombo = -1;
    private int _displayedRemainingTenths = -1;
    private int _displayedElapsedSeconds = -1;

    public void ShowScore(long score)
    {
        _scoreTxt.text = score.ToString("N0");
    }

    public void ShowCombo(int combo, float remainingSeconds, float windowSeconds)
    {
        if (_comboTxt != null && _displayedCombo != combo)
        {
            _comboTxt.text = $"×{combo}";
            _displayedCombo = combo;
        }

        int remainingTenths = Mathf.CeilToInt(remainingSeconds * 10f);
        if (_comboRemainingTxt != null && _displayedRemainingTenths != remainingTenths)
        {
            _comboRemainingTxt.text = $"{remainingTenths / 10f:F1}s";
            _displayedRemainingTenths = remainingTenths;
        }

        if (_comboFillImg != null)
            _comboFillImg.fillAmount = windowSeconds > 0f ? Mathf.Clamp01(remainingSeconds / windowSeconds) : 0f;
    }

    public void ShowElapsed(float survivalSeconds)
    {
        int seconds = Mathf.FloorToInt(survivalSeconds);
        if (_elapsedTxt == null || _displayedElapsedSeconds == seconds)
            return;

        _displayedElapsedSeconds = seconds;
        _elapsedTxt.text = $"{seconds / 60:00}:{seconds % 60:00}";
    }
}
