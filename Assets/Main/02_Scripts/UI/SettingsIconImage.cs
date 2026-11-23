using UnityEngine;
using UnityEngine.UI;

// 설정창의 작은 선 아이콘을 UI 메시로 표시합니다.
public sealed class SettingsIconImage : MaskableGraphic
{
    private enum SETTINGS_ICON { MUSIC, SPEAKER, LANGUAGE, JOYSTICK }

    [SerializeField] private SETTINGS_ICON _settingsIcon;
    [SerializeField, Min(1f)] private float _lineWidth = 3.5f;

    public SettingsIconImage()
    {
        useLegacyMeshGeneration = false;
    }

    public void ShowJoystickIcon()
    {
        _settingsIcon = SETTINGS_ICON.JOYSTICK;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        switch (_settingsIcon)
        {
            case SETTINGS_ICON.MUSIC:
                AddLine(vertexHelper, new Vector2(-16f, -15f), new Vector2(-16f, 30f));
                AddLine(vertexHelper, new Vector2(22f, -5f), new Vector2(22f, 40f));
                AddLine(vertexHelper, new Vector2(-16f, 30f), new Vector2(22f, 40f));
                AddLine(vertexHelper, new Vector2(-16f, 21f), new Vector2(22f, 31f));
                AddArc(vertexHelper, new Vector2(-25f, -18f), new Vector2(10f, 7f), 0f, 360f);
                AddArc(vertexHelper, new Vector2(13f, -8f), new Vector2(10f, 7f), 0f, 360f);
                break;
            case SETTINGS_ICON.SPEAKER:
                AddLine(vertexHelper, new Vector2(-30f, -12f), new Vector2(-14f, -12f));
                AddLine(vertexHelper, new Vector2(-14f, -12f), new Vector2(7f, -30f));
                AddLine(vertexHelper, new Vector2(7f, -30f), new Vector2(7f, 30f));
                AddLine(vertexHelper, new Vector2(7f, 30f), new Vector2(-14f, 12f));
                AddLine(vertexHelper, new Vector2(-14f, 12f), new Vector2(-30f, 12f));
                AddLine(vertexHelper, new Vector2(-30f, 12f), new Vector2(-30f, -12f));
                AddArc(vertexHelper, new Vector2(7f, 0f), Vector2.one * 18f, -50f, 50f);
                AddArc(vertexHelper, new Vector2(7f, 0f), Vector2.one * 31f, -50f, 50f);
                break;
            case SETTINGS_ICON.LANGUAGE:
                AddArc(vertexHelper, Vector2.zero, Vector2.one * 32f, 0f, 360f);
                AddArc(vertexHelper, Vector2.zero, new Vector2(14f, 32f), 0f, 360f);
                AddLine(vertexHelper, new Vector2(-32f, 0f), new Vector2(32f, 0f));
                break;
            case SETTINGS_ICON.JOYSTICK:
                AddArc(vertexHelper, new Vector2(0f, -22f), new Vector2(32f, 11f), 0f, 360f);
                AddArc(vertexHelper, new Vector2(9f, 26f), Vector2.one * 11f, 0f, 360f);
                AddLine(vertexHelper, new Vector2(0f, -22f), new Vector2(7f, 15f));
                break;
        }
    }

    private void AddArc(VertexHelper vertexHelper, Vector2 center, Vector2 radius, float startAngle, float endAngle)
    {
        Vector2 previous = default;
        for (int step = 0; step <= 24; step++)
        {
            float radians = Mathf.Lerp(startAngle, endAngle, step / 24f) * Mathf.Deg2Rad;
            Vector2 point = center + new Vector2(Mathf.Cos(radians) * radius.x, Mathf.Sin(radians) * radius.y);
            if (step > 0)
            {
                AddLine(vertexHelper, previous, point);
            }
            previous = point;
        }
    }

    private void AddLine(VertexHelper vertexHelper, Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (_lineWidth * 0.5f);
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
}
