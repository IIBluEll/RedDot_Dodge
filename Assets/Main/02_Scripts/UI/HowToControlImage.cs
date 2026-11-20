using UnityEngine;
using UnityEngine.UI;

// 조작 안내의 선 아이콘을 UI 메시로 그려 해상도에 관계없이 선명하게 표시합니다.
public sealed class HowToControlImage : MaskableGraphic
{
    private enum CONTROL_ICON { MOUSE, TILT_PHONE }

    [SerializeField] private CONTROL_ICON _controlIcon;
    [SerializeField, Min(1f)] private float _lineWidth = 2.5f;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (_controlIcon == CONTROL_ICON.MOUSE)
        {
            AddOutline(vertexHelper, new Vector2(42f, 66f), 20f, 0f);
            AddLine(vertexHelper, new Vector2(-21f, 9f), new Vector2(21f, 9f));
            AddLine(vertexHelper, new Vector2(0f, 9f), new Vector2(0f, 33f));
            AddLine(vertexHelper, new Vector2(0f, 23f), new Vector2(0f, 17f), 0f, _lineWidth * 2f);
        }
        else
        {
            AddOutline(vertexHelper, new Vector2(78f, 44f), 6f, 16f);
            AddOutline(vertexHelper, new Vector2(57f, 34f), 2f, 16f);
            AddLine(vertexHelper, new Vector2(33f, -4f), new Vector2(33f, 4f), 16f);
            AddLine(vertexHelper, new Vector2(-45f, -19f), new Vector2(-48f, -7f), 16f);
            AddLine(vertexHelper, new Vector2(45f, 19f), new Vector2(48f, 7f), 16f);
        }
    }

    private void AddOutline(VertexHelper vertexHelper, Vector2 size, float radius, float rotation)
    {
        Vector2 halfSize = size * 0.5f;
        Vector2 first = default;
        Vector2 previous = default;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 center = new Vector2(corner == 0 || corner == 3 ? halfSize.x - radius : -halfSize.x + radius,
                corner < 2 ? halfSize.y - radius : -halfSize.y + radius);
            for (int step = 0; step <= 8; step++)
            {
                float angle = (corner * 90f + step * 90f / 8f) * Mathf.Deg2Rad;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (corner == 0 && step == 0)
                {
                    first = point;
                }
                else
                {
                    AddLine(vertexHelper, previous, point, rotation);
                }
                previous = point;
            }
        }
        AddLine(vertexHelper, previous, first, rotation);
    }

    private void AddLine(VertexHelper vertexHelper, Vector2 start, Vector2 end, float rotation = 0f, float width = 0f)
    {
        float radians = rotation * Mathf.Deg2Rad;
        start = Rotate(start, radians);
        end = Rotate(end, radians);
        Vector2 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * ((width > 0f ? width : _lineWidth) * 0.5f);
        float scale = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) / 96f;
        Vector2 center = rectTransform.rect.center;
        int index = vertexHelper.currentVertCount;
        vertexHelper.AddVert(center + (start - normal) * scale, color, Vector2.zero);
        vertexHelper.AddVert(center + (start + normal) * scale, color, Vector2.zero);
        vertexHelper.AddVert(center + (end + normal) * scale, color, Vector2.zero);
        vertexHelper.AddVert(center + (end - normal) * scale, color, Vector2.zero);
        vertexHelper.AddTriangle(index, index + 1, index + 2);
        vertexHelper.AddTriangle(index, index + 2, index + 3);
    }

    private static Vector2 Rotate(Vector2 point, float radians)
    {
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);
        return new Vector2(point.x * cosine - point.y * sine, point.x * sine + point.y * cosine);
    }
}
