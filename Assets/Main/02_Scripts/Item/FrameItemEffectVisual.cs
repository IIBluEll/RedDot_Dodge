using UnityEngine;

public sealed class FrameItemEffectVisual : ItemEffectVisual
{
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private Sprite[] _frames;

    protected override void OnEffectBegan()
    {
        _renderer.color = Color.white;
        _renderer.enabled = true;
    }

    protected override void OnEffectTicked()
    {
        int frame = Mathf.Min((int)(Effect.Progress * _frames.Length), _frames.Length - 1);
        _renderer.sprite = _frames[frame];
        if (Effect is IRadialItemEffect radial)
            _renderer.transform.localScale = Vector3.one * (radial.Radius * 2f / _frames[frame].bounds.size.x);
    }

    protected override void OnEffectStopped()
    {
        _renderer.enabled = false;
    }
}
