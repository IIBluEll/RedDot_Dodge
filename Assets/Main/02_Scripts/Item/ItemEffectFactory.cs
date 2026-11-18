using System;

public static class ItemEffectFactory
{
    public static AItemEffect Create(ItemData data, ItemEffectContext context)
    {
        if (data.EffectDefinition != null)
        {
            AItemEffect effect = data.EffectDefinition.CreateEffect(data, context);
            return effect ?? throw new InvalidOperationException("Effect Definition이 효과를 생성하지 않았습니다.");
        }

        switch (data.Type)
        {
            case ITEM_TYPE.BOMB:
                return new BombEffect(data, context);
            case ITEM_TYPE.FREEZE:
                return new FreezeEffect(data, context);
            default:
                throw new ArgumentOutOfRangeException(nameof(data), data.Type, "Effect Definition이 필요합니다.");
        }
    }
}
