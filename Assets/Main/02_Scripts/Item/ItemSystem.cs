using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ItemVisual
{
    [SerializeField] private ITEM_TYPE _type;
    [SerializeField] private Sprite _pickupSprite;
    [SerializeField] private Sprite[] _effectSprites;
    [SerializeField] private Material _effectMaterial;
    [SerializeField] private ItemEffectVisual _effectPrefab;

    public ITEM_TYPE Type => _type;
    public Sprite PickupSprite => _pickupSprite;
    public Sprite[] EffectSprites => _effectSprites;
    public Material EffectMaterial => _effectMaterial;
    public ItemEffectVisual EffectPrefab => _effectPrefab;

    public ItemVisual(ITEM_TYPE type, Sprite pickupSprite, Sprite[] effectSprites, Material effectMaterial)
    {
        _type = type;
        _pickupSprite = pickupSprite;
        _effectSprites = effectSprites;
        _effectMaterial = effectMaterial;
    }

    public ItemVisual(ITEM_TYPE type, Sprite pickupSprite, ItemEffectVisual effectPrefab)
    {
        _type = type;
        _pickupSprite = pickupSprite;
        _effectPrefab = effectPrefab;
    }

    public bool IsValid()
    {
        if (_pickupSprite == null || _pickupSprite.bounds.size.x <= 0f)
            return false;

        if (_effectPrefab != null)
            return true;

        if (_effectSprites == null || _effectSprites.Length == 0)
            return false;

        foreach (Sprite sprite in _effectSprites)
        {
            if (sprite == null || sprite.bounds.size.x <= 0f)
                return false;
        }

        return true;
    }
}

[DisallowMultipleComponent]
public class ItemSystem : MonoBehaviour, IItemSpawnHost
{
    [Header("Renderer Templates")]
    [SerializeField] private SpriteRenderer _pickupRenderer;
    [SerializeField] private SpriteRenderer _effectRenderer;
    [Header("Existing Bomb Visuals")]
    [SerializeField] private Sprite _pickupSprite;
    [SerializeField] private Sprite[] _effectSprites;
    [Header("Additional Item Visuals")]
    [SerializeField] private ItemVisual[] _itemVisuals = Array.Empty<ItemVisual>();
    [SerializeField] private Sprite _frozenEnemySprite;

    private sealed class ActiveEffect
    {
        public readonly AItemEffect EFFECT;
        public readonly ItemEffectVisual VISUAL;
        public readonly Stack<ItemEffectVisual> POOL;
        public readonly ITEM_TYPE TYPE;

        public ActiveEffect(AItemEffect effect, ItemEffectVisual visual, Stack<ItemEffectVisual> pool, ITEM_TYPE type)
        {
            EFFECT = effect;
            VISUAL = visual;
            POOL = pool;
            TYPE = type;
        }
    }

    private static readonly Comparison<ActiveEffect> EFFECT_COMPARISON = (left, right) => left.EFFECT.Priority.CompareTo(right.EFFECT.Priority);
    private readonly Dictionary<ITEM_TYPE, ItemVisual> VISUALS = new();
    private readonly List<ItemPickup> PICKUPS = new();
    private readonly List<ActiveEffect> EFFECTS = new();
    private readonly Stack<SpriteRenderer> FREE_PICKUP_RENDERERS = new();
    private readonly Stack<ItemEffectVisual> FREE_SPRITE_EFFECT_VISUALS = new();
    private readonly Dictionary<ItemEffectVisual, Stack<ItemEffectVisual>> PREFAB_POOLS = new();
    private GameBalance _gameBalance;
    private IItemEffectTarget _target;
    private ItemSpawnDirector _spawnDirector;
    private Transform _ownerTrans;

    public int PickupCount => PICKUPS.Count;
    public int ActiveEffectCount => EFFECTS.Count;
    public float NextAttemptAtSeconds => _spawnDirector.NextAttemptAtSeconds;
    public Sprite FrozenEnemySprite => _frozenEnemySprite;

    public bool Initialize(GameBalance gameBalance, IItemEffectTarget target, int randomSeed, Transform ownerTrans = null)
    {
        if (gameBalance == null || target == null || _pickupRenderer == null || _pickupRenderer == _effectRenderer)
        {
            Debug.LogError("ItemSystem의 밸런스와 Renderer Template 참조를 확인해 주세요.", this);
            return false;
        }

        gameBalance.ValidateItems();
        VISUALS.Clear();

        // 기존 BombItem 컴포넌트에 연결한 폭탄 리소스는 그대로 사용합니다.
        var existingBomb = new ItemVisual(ITEM_TYPE.BOMB, _pickupSprite, _effectSprites, _effectRenderer != null ? _effectRenderer.sharedMaterial : null);
        if (existingBomb.IsValid())
            VISUALS.Add(ITEM_TYPE.BOMB, existingBomb);

        foreach (ItemVisual visual in _itemVisuals)
        {
            if (visual != null)
                VISUALS[visual.Type] = visual;
        }

        bool usesSpriteEffects = false;
        foreach (ItemData item in gameBalance.Items)
        {
            if (item.Weight <= 0)
                continue;

            if (!VISUALS.TryGetValue(item.Type, out ItemVisual visual) || !visual.IsValid())
            {
                Debug.LogError($"ItemSystem의 {item.Type} 아이콘과 효과 프리팹 또는 프레임을 연결해 주세요.", this);
                return false;
            }

            if (item.RequiresOwner && ownerTrans == null)
            {
                Debug.LogError($"{item.Type} 효과에는 플레이어 Transform 연결이 필요합니다.", this);
                return false;
            }

            if (item.EffectDefinition != null && visual.EffectPrefab == null)
            {
                Debug.LogError($"{item.Type}의 사용자 정의 효과에는 Effect Prefab을 연결해 주세요.", this);
                return false;
            }

            usesSpriteEffects |= visual.EffectPrefab == null;
        }

        if (usesSpriteEffects && _effectRenderer == null)
        {
            Debug.LogError("프레임 방식의 효과에는 Effect Renderer 템플릿이 필요합니다.", this);
            return false;
        }

        _gameBalance = gameBalance;
        _target = target;
        _ownerTrans = ownerTrans;
        _spawnDirector = new ItemSpawnDirector(gameBalance, randomSeed);
        _pickupRenderer.enabled = false;
        if (_effectRenderer != null)
            _effectRenderer.enabled = false;

        while (FREE_PICKUP_RENDERERS.Count < gameBalance.ItemCapacity)
            FREE_PICKUP_RENDERERS.Push(CreateRenderer(_pickupRenderer, "Item Pickup"));
        if (usesSpriteEffects)
        {
            while (FREE_SPRITE_EFFECT_VISUALS.Count < gameBalance.ItemCapacity)
                FREE_SPRITE_EFFECT_VISUALS.Push(CreateSpriteEffectVisual());
        }

        ResetRound(randomSeed);
        return true;
    }

    public void ResetRound(int randomSeed)
    {
        foreach (ItemPickup pickup in PICKUPS)
            ReturnRenderer(pickup.Renderer, FREE_PICKUP_RENDERERS);
        foreach (ActiveEffect effect in EFFECTS)
            ReleaseEffect(effect);

        PICKUPS.Clear();
        EFFECTS.Clear();
        _spawnDirector.Reset(randomSeed);
    }

    public void TickLifetimes(float survivalSeconds)
    {
        for (int i = PICKUPS.Count - 1; i >= 0; i--)
        {
            if (PICKUPS[i].Tick(survivalSeconds))
                continue;

            ReturnRenderer(PICKUPS[i].Renderer, FREE_PICKUP_RENDERERS);
            PICKUPS.RemoveAt(i);
        }

        for (int i = EFFECTS.Count - 1; i >= 0; i--)
        {
            ActiveEffect active = EFFECTS[i];
            if (!active.EFFECT.Tick(survivalSeconds))
                RemoveEffectAt(i);
            else
                active.VISUAL.Tick();
        }
    }

    public void TickSupply(float survivalSeconds, Vector2 playerPosition)
    {
        _spawnDirector.Tick(survivalSeconds, playerPosition, this);
    }

    public void CollectPickups(Vector2 playerStart, Vector2 playerEnd, float playerRadius, float survivalSeconds)
    {
        bool addedEffect = false;
        for (int i = PICKUPS.Count - 1; i >= 0; i--)
        {
            ItemPickup pickup = PICKUPS[i];
            if (!pickup.TouchesPlayer(playerStart, playerEnd, playerRadius))
                continue;

            ItemVisual visual = VISUALS[pickup.Data.Type];
            if (pickup.Data.EffectDefinition != null && pickup.Data.EffectDefinition.ReplaceExisting)
            {
                for (int effectIndex = EFFECTS.Count - 1; effectIndex >= 0; effectIndex--)
                {
                    if (EFFECTS[effectIndex].TYPE == pickup.Data.Type && EFFECTS[effectIndex].EFFECT.CanBeReplaced)
                        RemoveEffectAt(effectIndex);
                }
            }

            var context = new ItemEffectContext(_target, _ownerTrans, pickup.Position, survivalSeconds);
            AItemEffect effect = ItemEffectFactory.Create(pickup.Data, context);
            Stack<ItemEffectVisual> pool = GetEffectPool(visual.EffectPrefab);
            ItemEffectVisual presentation = RentEffectVisual(visual, pickup.Data, pool);
            effect.Begin();
            presentation.Begin(effect);
            EFFECTS.Add(new ActiveEffect(effect, presentation, pool, pickup.Data.Type));
            ReturnRenderer(pickup.Renderer, FREE_PICKUP_RENDERERS);
            PICKUPS.RemoveAt(i);
            addedEffect = true;
        }

        if (addedEffect)
            EFFECTS.Sort(EFFECT_COMPARISON);
    }

    public int ApplyEffects()
    {
        int killCount = 0;
        for (int i = 0; i < EFFECTS.Count; i++)
        {
            ActiveEffect active = EFFECTS[i];
            if (!active.EFFECT.IsFinished)
                killCount += active.EFFECT.Apply();

            if (active.EFFECT.IsFinished)
                RemoveEffectAt(i--);
            else
                active.VISUAL.Tick();
        }

        return killCount;
    }

    public bool TryBlockPlayerHit(PlayerHit hit)
    {
        for (int i = 0; i < EFFECTS.Count; i++)
        {
            AItemEffect effect = EFFECTS[i].EFFECT;
            if (!(effect is IPlayerHitInterceptor interceptor) || effect.IsFinished)
                continue;

            bool blocked = interceptor.TryBlockHit(hit);
            if (effect.IsFinished)
                RemoveEffectAt(i--);
            if (blocked)
                return true;
        }

        return false;
    }

    public bool IsSpawnPositionClear(Vector2 position)
    {
        float enemyDistance = Mathf.Max(_gameBalance.ItemMinEnemyDistance, _gameBalance.ItemPickupRadius + _gameBalance.EnemyRadius);
        if (!_target.IsItemPositionClear(position, enemyDistance))
            return false;

        float minDistance = Mathf.Max(_gameBalance.ItemMinPickupDistance, _gameBalance.ItemPickupRadius * 2f);
        float minDistanceSquared = minDistance * minDistance;
        foreach (ItemPickup pickup in PICKUPS)
        {
            if ((position - pickup.Position).sqrMagnitude < minDistanceSquared)
                return false;
        }

        return true;
    }

    public void SpawnPickup(ItemData data, Vector2 position, float survivalSeconds)
    {
        ItemVisual visual = VISUALS[data.Type];
        SpriteRenderer renderer = RentRenderer(FREE_PICKUP_RENDERERS, _pickupRenderer, "Item Pickup");
        PICKUPS.Add(new ItemPickup(data, position, survivalSeconds, _gameBalance, renderer, visual.PickupSprite));
    }

    private Stack<ItemEffectVisual> GetEffectPool(ItemEffectVisual prefab)
    {
        if (prefab == null)
            return FREE_SPRITE_EFFECT_VISUALS;

        if (!PREFAB_POOLS.TryGetValue(prefab, out Stack<ItemEffectVisual> pool))
            PREFAB_POOLS.Add(prefab, pool = new Stack<ItemEffectVisual>());

        return pool;
    }

    private ItemEffectVisual RentEffectVisual(ItemVisual visual, ItemData data, Stack<ItemEffectVisual> pool)
    {
        ItemEffectVisual presentation = pool.Count > 0 ? pool.Pop()
            : visual.EffectPrefab != null ? Instantiate(visual.EffectPrefab, transform) : CreateSpriteEffectVisual();

        if (visual.EffectPrefab == null)
        {
            SpriteRenderer renderer = presentation.GetComponent<SpriteRenderer>();
            Material material = visual.EffectMaterial != null ? visual.EffectMaterial : _effectRenderer.sharedMaterial;
            ((SpriteItemEffectVisual)presentation).Configure(renderer, visual.EffectSprites, material, data.Radius * 2f);
        }

        return presentation;
    }

    private SpriteItemEffectVisual CreateSpriteEffectVisual()
    {
        SpriteRenderer renderer = CreateRenderer(_effectRenderer, "Item Effect");
        SpriteItemEffectVisual visual = renderer.gameObject.AddComponent<SpriteItemEffectVisual>();
        visual.Stop();
        return visual;
    }

    private void RemoveEffectAt(int index)
    {
        ReleaseEffect(EFFECTS[index]);
        EFFECTS.RemoveAt(index);
    }

    private void ReleaseEffect(ActiveEffect effect)
    {
        effect.EFFECT.Finish();
        effect.VISUAL.Stop();
        effect.VISUAL.transform.SetParent(transform, false);
        effect.POOL.Push(effect.VISUAL);
    }

    private void OnDestroy()
    {
        foreach (ActiveEffect effect in EFFECTS)
            effect.EFFECT.Finish();
    }

    private SpriteRenderer RentRenderer(Stack<SpriteRenderer> pool, SpriteRenderer template, string objectName)
    {
        return pool.Count > 0 ? pool.Pop() : CreateRenderer(template, objectName);
    }

    private SpriteRenderer CreateRenderer(SpriteRenderer template, string objectName)
    {
        SpriteRenderer renderer = Instantiate(template, transform);
        renderer.name = objectName;
        renderer.enabled = false;
        return renderer;
    }

    private static void ReturnRenderer(SpriteRenderer renderer, Stack<SpriteRenderer> pool)
    {
        renderer.enabled = false;
        renderer.color = Color.white;
        pool.Push(renderer);
    }
}
