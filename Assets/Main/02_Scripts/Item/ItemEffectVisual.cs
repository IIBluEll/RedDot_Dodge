using UnityEngine;

// 프리팹의 표현 전용 컴포넌트입니다. 전투 판정은 AItemEffect에서 처리합니다.
public class ItemEffectVisual : MonoBehaviour
{
    private bool _initialized;
    private Vector3 _initialScale;
    private Quaternion _initialRotation;
    private ParticleSystem[] _particleSystems;
    private Animator[] _animators;
    private AudioSource[] _audioSources;

    protected AItemEffect Effect { get; private set; }

    public void Begin(AItemEffect effect)
    {
        EnsureInitialized();
        Effect = effect;
        transform.localScale = _initialScale;
        transform.localRotation = _initialRotation;
        transform.position = effect.Position;
        gameObject.SetActive(true);

        foreach (Animator animator in _animators)
        {
            if (!animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
                continue;

            animator.Rebind();
            animator.Update(0f);
        }

        foreach (ParticleSystem particleSystem in _particleSystems)
        {
            particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (particleSystem.gameObject.activeInHierarchy && particleSystem.main.playOnAwake)
                particleSystem.Play(false);
        }

        OnEffectBegan();
        Tick();
    }

    public void Tick()
    {
        if (Effect == null || Effect.IsFinished)
            return;

        transform.position = Effect.Position;
        OnEffectTicked();
    }

    public void Stop()
    {
        EnsureInitialized();
        if (Effect != null)
            OnEffectStopped();
        foreach (ParticleSystem particleSystem in _particleSystems)
            particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        foreach (AudioSource audioSource in _audioSources)
            audioSource.Stop();

        Effect = null;
        transform.localScale = _initialScale;
        transform.localRotation = _initialRotation;
        gameObject.SetActive(false);
    }

    protected virtual void OnEffectBegan() { }
    protected virtual void OnEffectTicked() { }
    protected virtual void OnEffectStopped() { }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialScale = transform.localScale;
        _initialRotation = transform.localRotation;
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        _animators = GetComponentsInChildren<Animator>(true);
        _audioSources = GetComponentsInChildren<AudioSource>(true);
        _initialized = true;
    }
}
