using UnityEngine;
using Random = System.Random;

public interface IItemSpawnHost
{
    int PickupCount { get; }
    bool IsSpawnPositionClear(Vector2 position);
    void SpawnPickup(ItemData data, Vector2 position, float survivalSeconds);
}

public sealed class ItemSpawnDirector
{
    private readonly GameBalance BALANCE;
    private Random _random;

    public float NextAttemptAtSeconds { get; private set; }

    public ItemSpawnDirector(GameBalance balance, int randomSeed)
    {
        BALANCE = balance;
        BALANCE.ValidateItems();
        Reset(randomSeed);
    }

    public void Reset(int randomSeed)
    {
        _random = new Random(randomSeed);
        NextAttemptAtSeconds = Sample(BALANCE.FirstItemDelaySeconds);
    }

    public bool Tick(float survivalSeconds, Vector2 playerPosition, IItemSpawnHost host)
    {
        if (survivalSeconds < NextAttemptAtSeconds)
            return false;

        if (host.PickupCount >= BALANCE.ItemCapacity)
        {
            ScheduleNext(survivalSeconds);
            return false;
        }

        Vector2 limits = BALANCE.ArenaSize * 0.5f - Vector2.one * BALANCE.ItemPickupRadius;
        float playerDistanceSquared = BALANCE.ItemMinPlayerDistance * BALANCE.ItemMinPlayerDistance;

        if (limits.x >= 0f && limits.y >= 0f)
        {
            for (int i = 0; i < BALANCE.ItemPlacementAttempts; i++)
            {
                Vector2 position = new Vector2(Sample(new Vector2(-limits.x, limits.x)), Sample(new Vector2(-limits.y, limits.y)));
                if ((position - playerPosition).sqrMagnitude < playerDistanceSquared || !host.IsSpawnPositionClear(position))
                    continue;

                // 위치가 확정된 경우에만 종류를 추첨합니다.
                host.SpawnPickup(ChooseItem(), position, survivalSeconds);
                ScheduleNext(survivalSeconds);
                return true;
            }
        }

        // 정규 공급을 별도로 예약하지 않고 실패한 한 건만 다시 시도합니다.
        NextAttemptAtSeconds = survivalSeconds + BALANCE.ItemRetrySeconds;
        return false;
    }

    private ItemData ChooseItem()
    {
        long totalWeight = 0;
        foreach (ItemData item in BALANCE.Items)
            totalWeight += item.Weight;

        double roll = _random.NextDouble() * totalWeight;
        foreach (ItemData item in BALANCE.Items)
        {
            roll -= item.Weight;
            if (roll < 0d)
                return item;
        }

        for (int i = BALANCE.Items.Count - 1; i >= 0; i--)
        {
            if (BALANCE.Items[i].Weight > 0)
                return BALANCE.Items[i];
        }

        throw new System.InvalidOperationException("등장 가능한 아이템이 없습니다.");
    }

    private void ScheduleNext(float survivalSeconds)
    {
        NextAttemptAtSeconds = survivalSeconds + Sample(BALANCE.ItemSpawnIntervalSeconds);
    }

    private float Sample(Vector2 range)
    {
        return Mathf.Lerp(range.x, range.y, (float)_random.NextDouble());
    }
}
