using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class PlayerTrail : MonoBehaviour
{
    private static readonly int INTENSITY_ID = Shader.PropertyToID("_Intensity");

    [SerializeField] private Material _trailMaterial;
    [SerializeField, Min(0.01f)] private float _minimumLifetime = 0.12f;
    [SerializeField, Min(0.01f)] private float _maximumLifetime = 0.22f;
    [SerializeField, Min(0.01f)] private float _minimumWidth = 0.2f;
    [SerializeField, Min(0.01f)] private float _maximumWidth = 0.45f;
    [SerializeField, Min(0f)] private float _rearOffset = 0.16f;
    [SerializeField, Min(0f)] private float _minimumSpeed = 0.08f;
    [SerializeField, Min(0.01f)] private float _vertexDistance = 0.025f;
    [SerializeField, Min(0.01f)] private float _teleportClearDistance = 1f;

    private TrailRenderer _trail;
    private MaterialPropertyBlock _properties;
    private Vector3 _lastPoint;

    private void Awake()
    {
        _properties = new MaterialPropertyBlock();
        if (_trailMaterial == null)
            _trailMaterial = Resources.Load<Material>("PlayerTrailNeon");

        if (_trailMaterial == null)
        {
            Debug.LogError("PlayerTrailNeon 재질을 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        var trailObj = new GameObject("Player Neon Trail", typeof(TrailRenderer));
        trailObj.layer = gameObject.layer;
        trailObj.transform.SetParent(transform, false);
        _trail = trailObj.GetComponent<TrailRenderer>();
        _trail.sharedMaterial = _trailMaterial;
        _trail.emitting = true;
        _trail.autodestruct = false;
        _trail.time = _maximumLifetime;
        _trail.minVertexDistance = _vertexDistance;
        _trail.alignment = LineAlignment.View;
        _trail.textureMode = LineTextureMode.Stretch;
        _trail.numCornerVertices = 4;
        _trail.numCapVertices = 4;
        _trail.shadowCastingMode = ShadowCastingMode.Off;
        _trail.receiveShadows = false;
        _trail.generateLightingData = false;

        SpriteRenderer playerRenderer = GetComponent<SpriteRenderer>();
        if (playerRenderer != null)
        {
            _trail.sortingLayerID = playerRenderer.sortingLayerID;
            _trail.sortingOrder = playerRenderer.sortingOrder - 1;
        }

        _trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.2f, 0.9f), new Keyframe(0.65f, 0.4f), new Keyframe(1f, 0f));
        var gradient = new Gradient();
        gradient.SetKeys(new[]
        {
            new GradientColorKey(new Color(0.25f, 0.95f, 1f), 0f),
            new GradientColorKey(new Color(0.2f, 0.55f, 1f), 0.35f),
            new GradientColorKey(new Color(0.65f, 0.2f, 1f), 0.7f),
            new GradientColorKey(new Color(1f, 0.12f, 0.8f), 1f)
        }, new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.65f, 0.4f), new GradientAlphaKey(0.2f, 0.8f), new GradientAlphaKey(0f, 1f) });
        _trail.colorGradient = gradient;
        ClearTrail();
    }

    public void RecordMovement(Vector3 previousPosition, Vector3 currentPosition, float maximumSpeed, float deltaTime)
    {
        if (!isActiveAndEnabled || _trail == null || deltaTime <= 0f)
            return;

        Vector3 movement = currentPosition - previousPosition;
        float distance = movement.magnitude;
        if (distance > Mathf.Max(_teleportClearDistance, maximumSpeed * deltaTime * 4f))
        {
            ClearTrail();
            return;
        }

        float speed = distance / deltaTime;
        if (speed <= _minimumSpeed)
            return;

        float strength = Mathf.Clamp01(speed / Mathf.Max(0.01f, maximumSpeed));
        _trail.time = Mathf.Lerp(_minimumLifetime, Mathf.Max(_minimumLifetime, _maximumLifetime), strength);
        _trail.widthMultiplier = Mathf.Lerp(_minimumWidth, Mathf.Max(_minimumWidth, _maximumWidth), strength);

        // Nonserialized native helpers may be lost when scripts reload during Play Mode.
        _properties ??= new MaterialPropertyBlock();
        _properties.SetFloat(INTENSITY_ID, Mathf.Lerp(1.2f, 3.2f, strength));
        _trail.SetPropertyBlock(_properties);

        Vector3 offset = movement.normalized * _rearOffset;
        Vector3 point = currentPosition - offset;
        _trail.transform.position = point;
        if (_trail.positionCount == 0)
        {
            _lastPoint = previousPosition - offset;
            _trail.AddPosition(_lastPoint);
        }

        if ((point - _lastPoint).sqrMagnitude < _vertexDistance * _vertexDistance)
            return;

        // Explicit physics positions keep every wall rebound and never emit during a teleport.
        _trail.AddPosition(point);
        _lastPoint = point;
    }

    public void ClearTrail()
    {
        if (_trail != null)
        {
            _trail.Clear();
            _trail.transform.position = transform.position;
        }
        _lastPoint = transform.position;
    }

    private void OnEnable()
    {
        if (_trail == null)
            return;
        _trail.enabled = true;
        _trail.emitting = true;
        ClearTrail();
    }

    private void OnDisable()
    {
        ClearTrail();
        if (_trail != null)
        {
            _trail.emitting = false;
            _trail.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (_trail != null)
            Destroy(_trail.gameObject);
    }
}
