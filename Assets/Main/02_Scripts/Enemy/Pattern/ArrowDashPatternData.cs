using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct ArrowSlot
{
    [SerializeField] private Vector2 _scatteredOffset;
    [SerializeField] private Vector2 _formationOffset;

    public Vector2 ScatteredOffset => _scatteredOffset;
    public Vector2 FormationOffset => _formationOffset;

    public ArrowSlot(Vector2 scatteredOffset , Vector2 formationOffset)
    {
        _scatteredOffset = scatteredOffset;
        _formationOffset = formationOffset;
    }
}

[CreateAssetMenu(menuName = "RedDot Dodge/Wave Patterns/Arrow Dash")]
public class ArrowDashPatternData : AEnemyPatternData
{
    [SerializeField, Min(0.01f)] private float _gatherDurationSeconds = 0.8f;
    [SerializeField, Min(0.01f)] private float _turnDurationSeconds = 0.25f;
    [SerializeField, Min(0.01f)] private float _holdDurationSeconds = 0.55f;
    [SerializeField, Min(0.01f)] private float _dashSpeed = 8f;

    // 대형의 로컬 +X 방향이 화살촉 방향
    [SerializeField]
    private ArrowSlot[] _slots =
    {
        new ArrowSlot(new Vector2(1.70f, 0.45f), new Vector2(1.16f, 0f)),
        new ArrowSlot(new Vector2(0.90f, 1.27f), new Vector2(0.58f, 0.46f)),
        new ArrowSlot(new Vector2(0.46f, -0.34f), new Vector2(0.58f, 0f)),
        new ArrowSlot(new Vector2(0.82f, -1.35f), new Vector2(0.58f, -0.46f)),
        new ArrowSlot(new Vector2(-0.15f, 2.10f), new Vector2(0f, 0.92f)),
        new ArrowSlot(new Vector2(-0.38f, 0.53f), Vector2.zero),
        new ArrowSlot(new Vector2(-0.14f, -2.02f), new Vector2(0f, -0.92f)),
        new ArrowSlot(new Vector2(-1.05f, -0.69f), new Vector2(-0.58f, 0f)),
        new ArrowSlot(new Vector2(-1.72f, 0.27f), new Vector2(-1.16f, 0f))
    };

    public override WAVE_PATTERN Kind => WAVE_PATTERN.ARROW_DASH;

    public IReadOnlyList<ArrowSlot> Slots => _slots;
    public int EnemyCount => _slots.Length;

    public float GatherDurationSeconds => _gatherDurationSeconds;
    public float TurnDurationSeconds => _turnDurationSeconds;
    public float HoldDurationSeconds => _holdDurationSeconds;
    public float DashSpeed => _dashSpeed;
    public float WarningDurationSeconds => _gatherDurationSeconds + _turnDurationSeconds + _holdDurationSeconds;
}
