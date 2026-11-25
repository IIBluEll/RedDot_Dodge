public sealed class RoundScore
{
    private readonly GameBalance BALANCE;
    private float _comboExpiresAtSeconds;

    public long Score { get; private set; }
    public int Combo { get; private set; }
    public int MaximumCombo { get; private set; }
    public int KillCount { get; private set; }

    public RoundScore(GameBalance balance)
    {
        BALANCE = balance;
    }

    public void Reset()
    {
        Score = 0;
        Combo = 0;
        MaximumCombo = 0;
        KillCount = 0;
        _comboExpiresAtSeconds = 0f;
    }

    public void Tick(float survivalSeconds)
    {
        if (survivalSeconds >= _comboExpiresAtSeconds)
            Combo = 0;
    }

    public float GetRemainingSeconds(float survivalSeconds)
    {
        return Combo > 0 ? UnityEngine.Mathf.Max(0f, _comboExpiresAtSeconds - survivalSeconds) : 0f;
    }

    public long AddKills(int killCount, float survivalSeconds)
    {
        Tick(survivalSeconds);
        if (killCount <= 0)
            return 0;

        long comboSum = (long)killCount * (2L * Combo + killCount + 1L) / 2L;
        long addedScore = comboSum * BALANCE.BaseKillScore;
        Score += addedScore;
        Combo += killCount;
        KillCount += killCount;
        MaximumCombo = System.Math.Max(MaximumCombo, Combo);
        _comboExpiresAtSeconds = survivalSeconds + BALANCE.ComboWindowSeconds;
        return addedScore;
    }
}
