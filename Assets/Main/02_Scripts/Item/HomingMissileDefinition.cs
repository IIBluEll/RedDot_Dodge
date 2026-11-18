using System;
using UnityEngine;

[CreateAssetMenu(menuName = "RedDot Dodge/Item Effects/Homing Missiles")]
public sealed class HomingMissileDefinition : AItemEffectDefinition
{
    [SerializeField, Range(1, 16)] private int _missileCount = 5;
    [SerializeField, Min(0.01f)] private float _speed = 9f;
    [SerializeField, Min(0.01f)] private float _turnSpeed = 540f;
    [SerializeField, Min(0.01f)] private float _lifetimeSeconds = 7.5f;
    [SerializeField, Min(0.01f)] private float _contactRadius = 0.09f;
    [SerializeField, Min(0.01f)] private float _blastRadius = 0.75f;
    [SerializeField, Min(0.01f)] private float _blastDurationSeconds = 0.22f;

    public int MissileCount => _missileCount;
    public float Speed => _speed;
    public float TurnSpeed => _turnSpeed;
    public float LifetimeSeconds => _lifetimeSeconds;
    public float ContactRadius => _contactRadius;
    public float BlastRadius => _blastRadius;
    public float BlastDurationSeconds => _blastDurationSeconds;
    public override int Priority => 2;

    public override AItemEffect CreateEffect(ItemData item, ItemEffectContext context)
    {
        Validate();
        return new HomingMissileEffect(this, context);
    }

    public override void Validate()
    {
        if (_missileCount < 1 || _missileCount > 16 || _speed <= 0f || _turnSpeed <= 0f || _lifetimeSeconds <= 0f
            || _contactRadius <= 0f || _blastRadius < _contactRadius || _blastDurationSeconds <= 0f)
            throw new InvalidOperationException("유도 미사일의 수량·속도·수명·폭발 설정을 확인하세요.");
    }
}
