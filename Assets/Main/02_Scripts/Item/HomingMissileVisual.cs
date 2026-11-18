using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class HomingMissileVisual : ItemEffectVisual
{
    [SerializeField] private Sprite _missileSprite;
    [SerializeField] private Material _bodyMaterial;
    [SerializeField] private Material _trailMaterial;
    [SerializeField] private Sprite[] _explosionFrames;
    [SerializeField] private AudioSource _audio;
    [SerializeField] private AudioClip _launchClip;
    [SerializeField] private AudioClip _impactClip;

    private readonly List<MissileView> VIEWS = new();

    private sealed class MissileView
    {
        public Transform RootTrans;
        public SpriteRenderer Body;
        public TrailRenderer Trail;
        public SpriteRenderer Explosion;
        public bool PlayedBurst;
    }

    protected override void OnEffectBegan()
    {
        HomingMissileEffect effect = (HomingMissileEffect)Effect;
        while (VIEWS.Count < effect.Missiles.Count)
            VIEWS.Add(CreateView());
        foreach (MissileView view in VIEWS)
        {
            view.RootTrans.gameObject.SetActive(false);
            view.Trail.Clear();
            view.Trail.enabled = true;
            view.Trail.emitting = true;
            view.PlayedBurst = false;
        }
        if (_audio != null && _launchClip != null)
            _audio.PlayOneShot(_launchClip, 0.3f);
    }

    protected override void OnEffectTicked()
    {
        HomingMissileEffect effect = (HomingMissileEffect)Effect;
        for (int i = 0; i < effect.Missiles.Count; i++)
        {
            MissileView view = VIEWS[i];
            HomingMissileState missile = effect.Missiles[i];
            bool visible = missile.Phase != HOMING_MISSILE_PHASE.FINISHED;
            view.RootTrans.gameObject.SetActive(visible);
            if (!visible)
                continue;
            view.RootTrans.SetPositionAndRotation(missile.Position, Quaternion.Euler(0f, 0f, Mathf.Atan2(missile.Direction.y, missile.Direction.x) * Mathf.Rad2Deg - 90f));
            bool exploding = missile.Phase == HOMING_MISSILE_PHASE.EXPLODING;
            view.Body.enabled = !exploding;
            view.Explosion.enabled = exploding;
            if (!exploding)
                continue;
            if (!view.PlayedBurst)
            {
                view.PlayedBurst = true;
                if (_audio != null && _impactClip != null)
                    _audio.PlayOneShot(_impactClip, 0.22f);
            }
            float progress = Mathf.Clamp01((effect.ElapsedSeconds - missile.ExplosionStartedAtSeconds) / effect.BlastDurationSeconds);
            int frame = Mathf.Min((int)(progress * _explosionFrames.Length), _explosionFrames.Length - 1);
            view.Explosion.sprite = _explosionFrames[frame];
            view.Explosion.transform.localScale = Vector3.one * (effect.BlastRadius * 2f / _explosionFrames[frame].bounds.size.x);
        }
    }

    protected override void OnEffectStopped()
    {
        foreach (MissileView view in VIEWS)
        {
            view.Trail.Clear();
            view.Trail.emitting = false;
            view.RootTrans.gameObject.SetActive(false);
        }
    }

    private MissileView CreateView()
    {
        Transform rootTrans = Child(transform, "Homing Missile");
        SpriteRenderer body = Child(rootTrans, "Body").gameObject.AddComponent<SpriteRenderer>();
        body.sprite = _missileSprite;
        body.sharedMaterial = _bodyMaterial;
        body.sortingOrder = 3;
        body.transform.localScale = Vector3.one * (0.42f / _missileSprite.bounds.size.y);

        TrailRenderer trail = Child(rootTrans, "Exhaust").gameObject.AddComponent<TrailRenderer>();
        trail.transform.localPosition = Vector3.down * 0.14f;
        trail.sharedMaterial = _trailMaterial;
        trail.sortingOrder = 2;
        trail.time = 0.18f;
        trail.minVertexDistance = 0.025f;
        trail.widthMultiplier = 0.14f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.numCapVertices = 3;
        trail.numCornerVertices = 3;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.autodestruct = false;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.95f, 0.35f), 0f), new GradientColorKey(new Color(1f, 0.25f, 0.03f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;

        SpriteRenderer explosion = Child(rootTrans, "Explosion").gameObject.AddComponent<SpriteRenderer>();
        explosion.sharedMaterial = _bodyMaterial;
        explosion.sortingOrder = 1;
        explosion.enabled = false;
        return new MissileView { RootTrans = rootTrans, Body = body, Trail = trail, Explosion = explosion };
    }

    private static Transform Child(Transform parentTrans, string name)
    {
        var childObj = new GameObject(name);
        childObj.transform.SetParent(parentTrans, false);
        return childObj.transform;
    }
}
