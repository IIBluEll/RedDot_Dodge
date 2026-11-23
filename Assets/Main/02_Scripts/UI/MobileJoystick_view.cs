using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MobileJoystick_view : MaskableGraphic, IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler, IDragHandler, IEndDragHandler
{
    private const float DEAD_ZONE = 0.10f;
    private const float RESPONSE_EXPONENT = 1.25f;
    private const float JOYSTICK_RADIUS = 104f;
    private const float TRAVEL_RADIUS = JOYSTICK_RADIUS * 0.60f;
    private const int CIRCLE_SEGMENTS = 32;
    private int? _pointerId;
    private Vector2 _stickOrigin;
    private Vector2 _stickPosition;
    private Camera _pressCamera;

    public event Action<Vector2> DirectionChanged;

    protected override void Awake()
    {
        base.Awake();
        canvasRenderer.cullTransparentMesh = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_pointerId.HasValue || eventData.button != PointerEventData.InputButton.Left)
            return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 position)
            || !rectTransform.rect.Contains(position))
        {
            return;
        }

        _pointerId = eventData.pointerId;
        _pressCamera = eventData.pressEventCamera;
        _stickOrigin = position;
        _stickPosition = Vector2.zero;
        DirectionChanged?.Invoke(Vector2.zero);
        SetVerticesDirty();
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_pointerId != eventData.pointerId
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, _pressCamera, out Vector2 position))
        {
            return;
        }

        Vector2 offset = position - _stickOrigin;
        Vector2 limitedOffset = Vector2.ClampMagnitude(offset, TRAVEL_RADIUS);
        if (offset.sqrMagnitude > TRAVEL_RADIUS * TRAVEL_RADIUS)
        {
            // 최대 거리를 넘겨 민 만큼 중심도 옮겨, 처음 누른 곳까지 돌아가지 않아도 방향을 바꿀 수 있게 합니다.
            _stickOrigin = position - limitedOffset;
        }

        _stickPosition = limitedOffset / TRAVEL_RADIUS;
        float strength = Mathf.InverseLerp(DEAD_ZONE, 1f, _stickPosition.magnitude);
        DirectionChanged?.Invoke(_stickPosition.normalized * Mathf.Pow(strength, RESPONSE_EXPONENT));
        SetVerticesDirty();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (_pointerId == eventData.pointerId)
            ResetInput();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        OnPointerUp(eventData);
    }

    protected override void OnDisable()
    {
        ResetInput();
        base.OnDisable();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (!_pointerId.HasValue)
        {
            // 표시가 없을 때도 GraphicRaycaster가 터치 영역의 깊이를 얻을 수 있게 합니다.
            Vector2 idleCenter = rectTransform.rect.center;
            vertexHelper.AddVert(idleCenter, Color.clear, Vector2.zero);
            vertexHelper.AddVert(idleCenter + Vector2.up, Color.clear, Vector2.zero);
            vertexHelper.AddVert(idleCenter + Vector2.right, Color.clear, Vector2.zero);
            vertexHelper.AddTriangle(0, 1, 2);
            return;
        }

        GetVisualLayout(out Vector2 center, out float radius);
        AddCircle(vertexHelper, center, radius, new Color(0.18f, 0.78f, 1f, 0.62f));
        AddCircle(vertexHelper, center, radius * 0.97f, new Color(0.025f, 0.06f, 0.14f, 0.82f));
        AddCircle(vertexHelper, center + _stickPosition * radius * 0.60f, radius * 0.36f, new Color(0.38f, 0.88f, 1f, 0.86f));
    }

    private void GetVisualLayout(out Vector2 center, out float radius)
    {
        center = _stickOrigin;
        radius = JOYSTICK_RADIUS;
        Rect safeArea = Screen.safeArea;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, safeArea.min, _pressCamera, out Vector2 minimum)
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, safeArea.max, _pressCamera, out Vector2 maximum))
        {
            return;
        }

        Vector2 size = maximum - minimum;
        radius = Mathf.Min(radius, Mathf.Min(size.x, size.y) * 0.5f);
        center.x = Mathf.Clamp(center.x, minimum.x + radius, maximum.x - radius);
        center.y = Mathf.Clamp(center.y, minimum.y + radius, maximum.y - radius);
    }

    private void ResetInput()
    {
        _pointerId = null;
        _pressCamera = null;
        _stickPosition = Vector2.zero;
        DirectionChanged?.Invoke(Vector2.zero);
        SetVerticesDirty();
    }

    private static void AddCircle(VertexHelper vertexHelper, Vector2 center, float radius, Color32 color)
    {
        int first = vertexHelper.currentVertCount;
        vertexHelper.AddVert(center, color, Vector2.zero);
        for (int i = 0; i < CIRCLE_SEGMENTS; i++)
        {
            float angle = -i * Mathf.PI * 2f / CIRCLE_SEGMENTS;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vertexHelper.AddVert(point, color, Vector2.zero);
            vertexHelper.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % CIRCLE_SEGMENTS);
        }
    }
}
