using UnityEngine;

// UI로 배치한 경기장 영역에 월드 좌표의 경기장을 맞춥니다.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class InGameArenaCamera : MonoBehaviour
{
    [SerializeField] private Camera _gameCamera;
    [SerializeField] private RectTransform _arenaRectTrans;
    [SerializeField] private GameBalance _gameBalance;
    [SerializeField] private SpriteRenderer _backgroundRenderer;

    private readonly Vector3[] WORLD_CORNERS = new Vector3[4];
    private Canvas _canvas;

    private void OnEnable()
    {
        Canvas.willRenderCanvases += OnCanvasRendered;
        AlignCamera();
    }

    private void OnDisable()
    {
        Canvas.willRenderCanvases -= OnCanvasRendered;
    }

    private void LateUpdate()
    {
        AlignCamera();
    }

    private void OnCanvasRendered()
    {
        AlignCamera();
    }

    public void AlignCamera()
    {
        if (_gameCamera == null || _arenaRectTrans == null || _gameBalance == null || Screen.width <= 0 || Screen.height <= 0)
            return;
        if (_canvas == null)
            _canvas = _arenaRectTrans.GetComponentInParent<Canvas>();
        if (_canvas == null || !_arenaRectTrans.gameObject.activeInHierarchy)
            return;

        Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        _arenaRectTrans.GetWorldCorners(WORLD_CORNERS);
        Vector2 minimum = RectTransformUtility.WorldToScreenPoint(canvasCamera, WORLD_CORNERS[0]);
        Vector2 maximum = RectTransformUtility.WorldToScreenPoint(canvasCamera, WORLD_CORNERS[2]);
        Vector2 pixelSize = maximum - minimum;
        if (pixelSize.x <= 1f || pixelSize.y <= 1f)
            return;

        Vector2 arenaSize = _gameBalance.ArenaSize;
        float unitsPerPixel = Mathf.Max(arenaSize.x / pixelSize.x, arenaSize.y / pixelSize.y);
        Vector2 center = (minimum + maximum) * 0.5f;
        Vector3 cameraPosition = _gameCamera.transform.position;
        cameraPosition.x = -(center.x - Screen.width * 0.5f) * unitsPerPixel;
        cameraPosition.y = -(center.y - Screen.height * 0.5f) * unitsPerPixel;
        if (!_gameCamera.orthographic)
            _gameCamera.orthographic = true;
        Rect fullScreen = new Rect(0f, 0f, 1f, 1f);
        if (_gameCamera.rect != fullScreen)
            _gameCamera.rect = fullScreen;
        float aspect = (float)Screen.width / Screen.height;
        if (!Mathf.Approximately(_gameCamera.aspect, aspect))
            _gameCamera.aspect = aspect;
        float orthographicSize = unitsPerPixel * Screen.height * 0.5f;
        if (!Mathf.Approximately(_gameCamera.orthographicSize, orthographicSize))
            _gameCamera.orthographicSize = orthographicSize;
        if (_gameCamera.transform.position != cameraPosition)
            _gameCamera.transform.position = cameraPosition;

        if (_backgroundRenderer != null && _backgroundRenderer.sprite != null)
        {
            Vector2 spriteSize = _backgroundRenderer.sprite.bounds.size;
            Vector3 backgroundPosition = _backgroundRenderer.transform.position;
            backgroundPosition.x = cameraPosition.x;
            backgroundPosition.y = cameraPosition.y;
            Vector3 backgroundScale = new Vector3(unitsPerPixel * Screen.width / spriteSize.x,
                unitsPerPixel * Screen.height / spriteSize.y, 1f);
            if (_backgroundRenderer.transform.position != backgroundPosition)
                _backgroundRenderer.transform.position = backgroundPosition;
            if (_backgroundRenderer.transform.localScale != backgroundScale)
                _backgroundRenderer.transform.localScale = backgroundScale;
        }
    }
}
