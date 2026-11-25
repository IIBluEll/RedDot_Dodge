public readonly struct RoundResult
{
    public long Score { get; }
    public int MaximumCombo { get; }
    public int KillCount { get; }
    public float SurvivalSeconds { get; }

    public RoundResult(long score, int maximumCombo, int killCount, float survivalSeconds)
    {
        Score = score;
        MaximumCombo = maximumCombo;
        KillCount = killCount;
        SurvivalSeconds = survivalSeconds;
    }
}
