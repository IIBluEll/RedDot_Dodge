using System;
using UnityEngine;

public enum ITEM_TYPE
{
    BOMB = 0,
    FREEZE = 1,
    SHIELD = 2,
    SPIKES = 3,
    HOMING_MISSILE = 4
}

[Serializable]
public sealed class ItemData
{
    [SerializeField] private ITEM_TYPE _type;
    [SerializeField, Min(0)] private int _weight = 1;
    [SerializeField] private AItemEffectDefinition _effectDefinition;
    [Header("Built-in Bomb / Freeze Settings")]
    [SerializeField, Min(0.01f)] private float _radius;
    [SerializeField, Min(0.01f)] private float _durationSeconds;
    [SerializeField, Min(0.01f)] private float _visualDurationSeconds;

    public ITEM_TYPE Type => _type;
    public int Weight => _weight;
    public float Radius => _radius;
    public float DurationSeconds => _durationSeconds;
    public float VisualDurationSeconds => _type == ITEM_TYPE.BOMB ? _durationSeconds : _visualDurationSeconds;
    public int EffectPriority => _effectDefinition != null ? _effectDefinition.Priority : _type == ITEM_TYPE.BOMB ? 0 : 1;
    public AItemEffectDefinition EffectDefinition => _effectDefinition;
    public bool RequiresOwner => _effectDefinition != null && _effectDefinition.RequiresOwner;

    public ItemData(ITEM_TYPE type, int weight, float radius, float durationSeconds, float visualDurationSeconds)
    {
        _type = type;
        _weight = weight;
        _radius = radius;
        _durationSeconds = durationSeconds;
        _visualDurationSeconds = visualDurationSeconds;
    }

    public ItemData(ITEM_TYPE type, int weight, AItemEffectDefinition effectDefinition)
    {
        _type = type;
        _weight = weight;
        _effectDefinition = effectDefinition;
    }

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(ITEM_TYPE), _type) || _weight < 0)
            throw new InvalidOperationException("아이템 종류와 가중치를 확인하세요.");

        if (_effectDefinition != null)
        {
            _effectDefinition.Validate();
            return;
        }

        if (_type != ITEM_TYPE.BOMB && _type != ITEM_TYPE.FREEZE)
            throw new InvalidOperationException($"{_type}의 Effect Definition을 연결하세요.");

        if (_radius <= 0f || _durationSeconds <= 0f || VisualDurationSeconds <= 0f)
            throw new InvalidOperationException("폭탄·빙결 효과의 반경과 시간을 확인하세요.");
    }
}
