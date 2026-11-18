using UnityEngine;

// 기존 SpriteRenderer/프레임 연결을 위한 표현입니다.
[AddComponentMenu("")]
public sealed class SpriteItemEffectVisual : ItemEffectVisual
{
    private SpriteRenderer _renderer;
    private Sprite[] _frames;
    private float _width;

    public void Configure(SpriteRenderer renderer, Sprite[] frames, Material material, float width)
    {
        _renderer = renderer;
        _frames = frames;
        _width = width;
        _renderer.sharedMaterial = material;
        _renderer.color = Color.white;
    }

    protected override void OnEffectBegan()
    {
        _renderer.enabled = true;
    }

    protected override void OnEffectTicked()
    {
        int frame = Mathf.Min((int)(Effect.Progress * _frames.Length), _frames.Length - 1);
        _renderer.sprite = _frames[frame];
        _renderer.transform.localScale = Vector3.one * (_width / _frames[frame].bounds.size.x);
    }

    protected override void OnEffectStopped()
    {
        if (_renderer != null)
        {
            _renderer.enabled = false;
            _renderer.color = Color.white;
        }
    }
}
