using System.Globalization;
using UnityEngine;

public sealed class PlayerPrefsScoreStorage
{
    private const string BEST_SCORE_KEY = "RedDotDodge.BestScore.v1";
    private readonly string SCORE_KEY;

    public long BestScore { get; private set; }

    public PlayerPrefsScoreStorage(string scoreKey = BEST_SCORE_KEY)
    {
        SCORE_KEY = scoreKey;
        string savedScore = PlayerPrefs.GetString(SCORE_KEY, "0");
        BestScore = long.TryParse(savedScore, NumberStyles.None, CultureInfo.InvariantCulture, out long score) && score >= 0 ? score : 0L;
    }

    public bool TrySaveBestScore(long score)
    {
        if (score <= BestScore)
            return false;

        // PlayerPrefs의 Int는 32비트이므로 64비트 점수는 문자열로 보존합니다.
        PlayerPrefs.SetString(SCORE_KEY, score.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        BestScore = score;
        return true;
    }
}
