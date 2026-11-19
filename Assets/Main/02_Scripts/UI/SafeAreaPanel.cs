using UnityEngine;

// Place directly under a full-screen Screen Space Overlay canvas.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaPanel : MonoBehaviour
{
    private RectTransform _panelRectTrans;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;
    private bool _hasAppliedSafeArea;

    private void OnEnable()
    {
        _panelRectTrans = GetComponent<RectTransform>();
        _hasAppliedSafeArea = false;
        ApplySafeArea();
    }

    private void LateUpdate()
    {
        ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (screenSize.x <= 0 || screenSize.y <= 0)
            return;

        Rect safeArea = Screen.safeArea;
        if (_hasAppliedSafeArea && safeArea == _lastSafeArea && screenSize == _lastScreenSize)
            return;

        _panelRectTrans.anchorMin = new Vector2(safeArea.xMin / screenSize.x, safeArea.yMin / screenSize.y);
        _panelRectTrans.anchorMax = new Vector2(safeArea.xMax / screenSize.x, safeArea.yMax / screenSize.y);
        _panelRectTrans.offsetMin = Vector2.zero;
        _panelRectTrans.offsetMax = Vector2.zero;

        _lastSafeArea = safeArea;
        _lastScreenSize = screenSize;
        _hasAppliedSafeArea = true;
    }
}
