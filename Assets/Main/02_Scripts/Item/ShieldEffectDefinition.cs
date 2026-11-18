using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Item Effects/Bubble Shield")]
public sealed class ShieldEffectDefinition : AItemEffectDefinition
{
    [SerializeField, Min(0.01f)] private float _shieldRadius = 0.65f;
    [SerializeField, Min(0.01f)] private float _burstRadius = 2f;
    [SerializeField, Min(0.01f)] private float _burstDurationSeconds = 0.35f;

    public float ShieldRadius => _shieldRadius;
    public float BurstRadius => _burstRadius;
    public float BurstDurationSeconds => _burstDurationSeconds;
    public override int Priority => 3;
    public override bool RequiresOwner => true;
    public override bool ReplaceExisting => true;

    public override AItemEffect CreateEffect(ItemData item, ItemEffectContext context)
    {
        Validate();
        return new ShieldEffect(this, context);
    }

    public override void Validate()
    {
        if (_shieldRadius <= 0f || _burstRadius < _shieldRadius || _burstDurationSeconds <= 0f)
            throw new InvalidOperationException("보호막 반경, 파괴 폭발 반경과 지속시간을 확인하세요.");
    }
}
