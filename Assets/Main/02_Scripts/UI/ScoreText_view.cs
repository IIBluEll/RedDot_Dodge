using TMPro;
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

    [Header("TextMeshPro")]
    [SerializeField] private TMP_Text _scoreTmpTxt;
    [SerializeField] private TMP_Text _comboTmpTxt;
    [SerializeField] private TMP_Text _comboRemainingTmpTxt;
    [SerializeField] private TMP_Text _elapsedTmpTxt;

    [Header("Ready Countdown")]
    [SerializeField] private TMP_Text _readyCountdownTmpTxt;

    private int _displayedCombo = -1;
    private int _displayedRemainingTenths = -1;
    private int _displayedElapsedSeconds = -1;
    private int _displayedReadyCount = -1;

    public void ShowScore(long score)
    {
        SetText(_scoreTxt, _scoreTmpTxt, score.ToString("N0"));
    }

    public void ShowCombo(int combo, float remainingSeconds, float windowSeconds)
    {
        if (_displayedCombo != combo)
        {
            SetText(_comboTxt, _comboTmpTxt, $"×{combo}");
            _displayedCombo = combo;
        }

        int remainingTenths = Mathf.CeilToInt(remainingSeconds * 10f);
        if (_displayedRemainingTenths != remainingTenths)
        {
            SetText(_comboRemainingTxt, _comboRemainingTmpTxt, $"{remainingTenths / 10f:F1}s");
            _displayedRemainingTenths = remainingTenths;
        }

        if (_comboFillImg != null)
            _comboFillImg.fillAmount = windowSeconds > 0f ? Mathf.Clamp01(remainingSeconds / windowSeconds) : 0f;
    }

    public void ShowElapsed(float survivalSeconds)
    {
        int seconds = Mathf.FloorToInt(survivalSeconds);
        if (_displayedElapsedSeconds == seconds)
            return;

        _displayedElapsedSeconds = seconds;
        SetText(_elapsedTxt, _elapsedTmpTxt, $"{seconds / 60:00}:{seconds % 60:00}");
    }

    public void ShowReadyCountdown(float remainingSeconds)
    {
        if (_readyCountdownTmpTxt == null)
            return;

        int count = Mathf.CeilToInt(Mathf.Max(0f, remainingSeconds));
        bool visible = count > 0;
        if (_readyCountdownTmpTxt.enabled != visible)
            _readyCountdownTmpTxt.enabled = visible;
        if (_displayedReadyCount == count)
            return;

        _displayedReadyCount = count;
        if (visible)
            _readyCountdownTmpTxt.text = count.ToString();
    }

    private static void SetText(Text legacyTxt, TMP_Text tmpTxt, string value)
    {
        if (legacyTxt != null)
            legacyTxt.text = value;
        if (tmpTxt != null)
            tmpTxt.text = value;
    }
}
