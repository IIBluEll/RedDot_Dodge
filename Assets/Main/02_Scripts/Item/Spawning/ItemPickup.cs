using UnityEngine;

public sealed class ItemPickup
{
    private readonly SpriteRenderer RENDERER;
    private readonly GameBalance BALANCE;

    public ItemData Data { get; }
    public Vector2 Position { get; }
    public float ExpiresAtSeconds { get; }
    public SpriteRenderer Renderer => RENDERER;

    public ItemPickup(ItemData data, Vector2 position, float survivalSeconds, GameBalance balance, SpriteRenderer renderer, Sprite sprite)
    {
        Data = data;
        Position = position;
        BALANCE = balance;
        RENDERER = renderer;
        ExpiresAtSeconds = survivalSeconds + balance.ItemLifetimeSeconds;

        renderer.sprite = sprite;
        renderer.transform.position = position;
        renderer.transform.localScale = Vector3.one * (balance.ItemPickupRadius * 2f / sprite.bounds.size.x);
        renderer.color = Color.white;
        renderer.enabled = true;
    }

    public bool Tick(float survivalSeconds)
    {
        float remainingSeconds = ExpiresAtSeconds - survivalSeconds;
        if (remainingSeconds <= 0f)
            return false;

        Color color = Color.white;
        if (remainingSeconds <= BALANCE.ItemDespawnWarningSeconds)
            color.a = Mathf.Repeat(remainingSeconds, BALANCE.ItemBlinkPeriodSeconds) < BALANCE.ItemBlinkPeriodSeconds * 0.5f ? 0.3f : 1f;

        RENDERER.color = color;
        return true;
    }

    public bool TouchesPlayer(Vector2 playerStart, Vector2 playerEnd, float playerRadius)
    {
        float radius = playerRadius + BALANCE.ItemPickupRadius;
        return ItemGeometry.DistanceToSegmentSquared(Position, playerStart, playerEnd) <= radius * radius;
    }
}
