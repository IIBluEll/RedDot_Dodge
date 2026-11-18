using UnityEngine;

public sealed class ShieldItemEffectVisual : ItemEffectVisual
{
    [SerializeField] private LineRenderer[] _rings;
    [SerializeField] private SpriteRenderer _membraneRenderer;
    [SerializeField] private SpriteRenderer _burstRenderer;
    [SerializeField] private Sprite[] _burstFrames;
    [SerializeField] private AudioSource _burstAudio;
    private bool _playedBurst;

    protected override void OnEffectBegan()
    {
        _playedBurst = false;
        _burstRenderer.enabled = false;
        _membraneRenderer.enabled = true;
        foreach (LineRenderer ring in _rings)
            ring.enabled = true;
    }

    protected override void OnEffectTicked()
    {
        ShieldEffect shield = (ShieldEffect)Effect;
        _membraneRenderer.enabled = !shield.HasBurst;
        foreach (LineRenderer ring in _rings)
        {
            ring.enabled = !shield.HasBurst;
            ring.transform.localScale = Vector3.one * shield.ShieldRadius;
        }

        if (!shield.HasBurst)
        {
            float pulse = 0.92f + Mathf.Sin(shield.ElapsedSeconds * 4f) * 0.08f;
            _membraneRenderer.color = new Color(1f, 1f, 1f, pulse);
            _membraneRenderer.transform.localScale = Vector3.one * (shield.ShieldRadius * 2f / _membraneRenderer.sprite.bounds.size.x);
            return;
        }

        if (!_playedBurst)
        {
            if (_burstAudio != null)
                _burstAudio.Play();
            _playedBurst = true;
        }

        int frame = Mathf.Min((int)(shield.BurstProgress * _burstFrames.Length), _burstFrames.Length - 1);
        _burstRenderer.sprite = _burstFrames[frame];
        _burstRenderer.transform.localScale = Vector3.one * (shield.BurstRadius * 2f / _burstFrames[frame].bounds.size.x);
        _burstRenderer.enabled = true;
    }

    protected override void OnEffectStopped()
    {
        _membraneRenderer.enabled = false;
        _burstRenderer.enabled = false;
        foreach (LineRenderer ring in _rings)
            ring.enabled = false;
        if (_burstAudio != null)
            _burstAudio.Stop();
        _playedBurst = false;
    }
}
