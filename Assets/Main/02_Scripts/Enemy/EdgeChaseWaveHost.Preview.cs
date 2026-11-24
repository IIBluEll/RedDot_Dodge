using UnityEngine;

public sealed partial class EdgeChaseWaveHost
{
    internal void InitializePatternPreview(GameBalance balance, PlayerMovement playerMovement, Texture2D warningTexture, Sprite enemySprite)
    {
        enabled = false;
        _gameBalance = balance;
        _playerMovement = playerMovement;
        _warningTexture = warningTexture;
        _enemySprite = enemySprite;
        InitializeEnemyBalance(Application.isMobilePlatform);
        InitializeVisualAssets();
    }

    internal void SetPatternPreviewTiming(bool useMobileTiming)
    {
        InitializeEnemyBalance(useMobileTiming);
    }

    internal bool TryStartPatternPreview(WAVE_PATTERN kind)
    {
        ClearWaveActors();
        _survivalSeconds = 0f;
        _previousPlayerPosition = _playerMovement.transform.position;
        _playerVelocity = Vector2.zero;

        DifficultyStage stage = _gameBalance.GetFirstStageForPattern(kind);
        if (stage == null)
        {
            return false;
        }

        AEnemyPatternData pattern = kind == WAVE_PATTERN.INTERCEPTOR ? _gameBalance.InterceptorPattern : _gameBalance.GetPattern(kind);
        WaveRequest request = _waveDirector.CreateRequest(pattern, stage);
        WavePlan plan = BuildWavePlan(request);
        return plan != null && TryBeginWave(new WaveState(request, plan, 0f), _gameBalance.EnemyCapacity);
    }

    internal bool TickPatternPreview(float deltaTime)
    {
        _survivalSeconds += deltaTime;
        Vector2 playerPosition = _playerMovement.transform.position;
        UpdatePlayerVelocity(playerPosition, deltaTime);
        TickActiveEnemies(playerPosition, deltaTime);
        RefreshWarningVisuals();
        ActivateWarning();
        bool isHit = TryFindDangerContact(_previousPlayerPosition, playerPosition, _gameBalance.PlayerRadius, out _);
        RefreshEnemyVisuals();
        RetireExitedEnemies();
        _previousPlayerPosition = playerPosition;
        return isHit;
    }

    internal void ClearPatternPreview()
    {
        ClearWaveActors();
    }

    private void InitializeVisualAssets()
    {
        Rect warningRect = new Rect(0f, 0f, _warningTexture.width, _warningTexture.height);
        _warningSprite = Sprite.Create(_warningTexture, warningRect, Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
        _interceptorSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f, 0, SpriteMeshType.FullRect);
    }
}
