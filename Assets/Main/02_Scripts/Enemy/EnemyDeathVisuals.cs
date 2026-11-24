using System.Collections.Generic;
using UnityEngine;

// Cosmetic particles only: no collision, enemy state or score is kept here.
public sealed class EnemyDeathVisuals
{
    private const int MAX_PARTICLES = 864;
    private readonly Transform ROOT_TRANS;
    private readonly Sprite SHARD_SPRITE;
    private readonly Texture2D SHARD_TEXTURE;
    private readonly Sprite IMPACT_RING_SPRITE;
    private readonly Texture2D IMPACT_RING_TEXTURE;
    private readonly System.Random RANDOM = new System.Random();
    private readonly List<Particle> PARTICLES = new();
    private readonly Stack<SpriteRenderer> FREE_RENDERERS = new();
    private readonly Stack<SpriteRenderer> FREE_IMPACT_RING_RENDERERS = new(2);
    private Material _impactRingMaterial;

    private struct Particle
    {
        public SpriteRenderer Renderer;
        public Vector2 Velocity;
        public Vector3 InitialScale;
        public Color Color;
        public float Age;
        public float Lifetime;
        public float AngularSpeed;
        public float EndScale;
    }

    public EnemyDeathVisuals(Transform parentTrans)
    {
        var rootObj = new GameObject("Enemy Death Effects");
        ROOT_TRANS = rootObj.transform;
        ROOT_TRANS.SetParent(parentTrans, false);

        const int SIZE = 16;
        SHARD_TEXTURE = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
        SHARD_TEXTURE.name = "Enemy Shard Texture";
        SHARD_TEXTURE.filterMode = FilterMode.Bilinear;
        SHARD_TEXTURE.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[SIZE * SIZE];
        for (int y = 0; y < SIZE; y++)
        {
            float halfWidth = (1f - (y + 0.5f) / SIZE) * 0.9f;
            for (int x = 0; x < SIZE; x++)
            {
                float distance = Mathf.Abs((x + 0.5f) / SIZE * 2f - 1f);
                pixels[y * SIZE + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((halfWidth - distance) * SIZE));
            }
        }
        SHARD_TEXTURE.SetPixels(pixels);
        SHARD_TEXTURE.Apply(false, true);
        SHARD_SPRITE = Sprite.Create(SHARD_TEXTURE, new Rect(0f, 0f, SIZE, SIZE), Vector2.one * 0.5f, SIZE, 0, SpriteMeshType.FullRect);
        SHARD_SPRITE.name = "Enemy Shard";

        const int RING_SIZE = 64;
        IMPACT_RING_TEXTURE = new Texture2D(RING_SIZE, RING_SIZE, TextureFormat.RGBA32, false);
        IMPACT_RING_TEXTURE.name = "Player Impact Ring Texture";
        IMPACT_RING_TEXTURE.filterMode = FilterMode.Bilinear;
        IMPACT_RING_TEXTURE.wrapMode = TextureWrapMode.Clamp;
        var ringPixels = new Color[RING_SIZE * RING_SIZE];
        for (int y = 0; y < RING_SIZE; y++)
        {
            for (int x = 0; x < RING_SIZE; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f) * (2f / RING_SIZE) - Vector2.one;
                float distance = Mathf.Abs(point.magnitude - 0.83f);
                float edge = Mathf.Clamp01(1f - distance / 0.045f);
                float glow = Mathf.Clamp01(1f - distance / 0.15f) * 0.35f;
                ringPixels[y * RING_SIZE + x] = new Color(1f, 1f, 1f, Mathf.Max(edge, glow));
            }
        }
        IMPACT_RING_TEXTURE.SetPixels(ringPixels);
        IMPACT_RING_TEXTURE.Apply(false, true);
        IMPACT_RING_SPRITE = Sprite.Create(IMPACT_RING_TEXTURE, new Rect(0f, 0f, RING_SIZE, RING_SIZE), Vector2.one * 0.5f, RING_SIZE, 0, SpriteMeshType.FullRect);
        IMPACT_RING_SPRITE.name = "Player Impact Ring";
    }

    public void PlayPlayerImpact(SpriteRenderer playerRenderer)
    {
        if (playerRenderer == null || playerRenderer.sprite == null)
        {
            return;
        }

        Transform playerTrans = playerRenderer.transform;
        Vector2 position = playerTrans.position;
        Vector3 scale = playerTrans.lossyScale;
        float width = playerRenderer.sprite.bounds.size.x * Mathf.Abs(scale.x);
        var magenta = new Color(1f, 0.23f, 0.76f);
        AddParticle(position, playerRenderer.sprite, Vector2.zero, scale, new Color(1f, 0.86f, 0.98f), 0.22f, 0f,
            endScale: 1.45f, sortingOrder: 8, angle: playerTrans.eulerAngles.z);
        AddParticle(position, IMPACT_RING_SPRITE, Vector2.zero, Vector3.one * (width * 0.9f), magenta, 0.68f, 0f,
            endScale: 4.05f, sortingOrder: 6, angle: 0f);
        AddParticle(position, IMPACT_RING_SPRITE, Vector2.zero, Vector3.one * (width * 1.18f), new Color(0.94f, 0.42f, 1f, 0.32f), 0.86f, 0f,
            endScale: 4.4f, sortingOrder: 6, angle: 0f);

        const int SHARD_COUNT = 14;
        float baseAngle = Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < SHARD_COUNT; i++)
        {
            float angle = baseAngle + (i + Range(-0.24f, 0.24f)) * Mathf.PI * 2f / SHARD_COUNT;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float size = width * Range(0.15f, 0.30f);
            var shardScale = new Vector3(size * 0.5f, size * 1.5f, 1f);
            Color color = Color.Lerp(magenta, Color.white, Range(0.25f, 0.9f));
            AddParticle(position + direction * width * 0.2f, SHARD_SPRITE, direction * width * Range(7f, 13f), shardScale,
                color, Range(0.44f, 0.78f), Range(-200f, 200f), sortingOrder: 7, angle: angle * Mathf.Rad2Deg - 90f);
        }
    }

    public void Play(Vector2 position, Sprite enemySprite, float enemyWidth, bool frozen, int shardCount, float durationSeconds, float shardSpeed)
    {
        // Drop excess presentation rather than change gameplay or grow the pool without a limit.
        if (PARTICLES.Count + shardCount + 1 > MAX_PARTICLES)
            return;

        Color color = frozen ? new Color(0.4f, 0.9f, 1f) : new Color(1f, 0.18f, 0.24f);
        AddParticle(position, enemySprite, Vector2.zero, Vector3.one * (enemyWidth / enemySprite.bounds.size.x), Color.white, 0.065f, 0f);
        float baseAngle = Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < shardCount; i++)
        {
            float angle = baseAngle + (i + Range(-0.3f, 0.3f)) * Mathf.PI * 2f / shardCount;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float width = enemyWidth * Range(0.2f, 0.38f);
            Vector3 scale = new Vector3(width * Range(0.65f, 1f), width * Range(1f, 1.7f), 1f);
            Color shardColor = Color.Lerp(color, Color.white, Range(0f, 0.45f));
            AddParticle(position + direction * enemyWidth * 0.1f, SHARD_SPRITE, direction * shardSpeed * Range(0.6f, 1.2f), scale,
                shardColor, durationSeconds * Range(0.85f, 1.15f), Range(-540f, 540f));
        }
    }

    public void Tick(float deltaTime)
    {
        for (int i = PARTICLES.Count - 1; i >= 0; i--)
        {
            Particle particle = PARTICLES[i];
            particle.Age += deltaTime;
            if (particle.Age >= particle.Lifetime)
            {
                ReturnRenderer(particle.Renderer);
                int lastIndex = PARTICLES.Count - 1;
                PARTICLES[i] = PARTICLES[lastIndex];
                PARTICLES.RemoveAt(lastIndex);
                continue;
            }

            float progress = particle.Age / particle.Lifetime;
            float damping = Mathf.Exp(-7f * deltaTime);
            particle.Renderer.transform.position += (Vector3)(particle.Velocity * ((1f - damping) / 7f));
            particle.Velocity *= damping;
            particle.Renderer.transform.Rotate(0f, 0f, particle.AngularSpeed * deltaTime);
            particle.Renderer.transform.localScale = particle.InitialScale * Mathf.Lerp(1f, particle.EndScale, progress);
            Color color = particle.Color;
            color.a *= 1f - progress * progress;
            particle.Renderer.color = color;
            PARTICLES[i] = particle;
        }
    }

    public void Reset()
    {
        foreach (Particle particle in PARTICLES)
            ReturnRenderer(particle.Renderer);

        PARTICLES.Clear();
    }

    public void Dispose()
    {
        Reset();
        if (ROOT_TRANS != null)
            Object.Destroy(ROOT_TRANS.gameObject);
        Object.Destroy(SHARD_SPRITE);
        Object.Destroy(SHARD_TEXTURE);
        Object.Destroy(IMPACT_RING_SPRITE);
        Object.Destroy(IMPACT_RING_TEXTURE);
        Object.Destroy(_impactRingMaterial);
    }

    private void AddParticle(Vector2 position, Sprite sprite, Vector2 velocity, Vector3 scale, Color color, float lifetime, float angularSpeed,
        float endScale = 0.25f, int sortingOrder = 5, float? angle = null)
    {
        Stack<SpriteRenderer> freeRenderers = sprite == IMPACT_RING_SPRITE ? FREE_IMPACT_RING_RENDERERS : FREE_RENDERERS;
        SpriteRenderer renderer;
        if (freeRenderers.Count > 0)
        {
            renderer = freeRenderers.Pop();
        }
        else
        {
            var shardObj = new GameObject("Death Particle", typeof(SpriteRenderer));
            shardObj.transform.SetParent(ROOT_TRANS, false);
            renderer = shardObj.GetComponent<SpriteRenderer>();
            if (sprite == IMPACT_RING_SPRITE)
            {
                if (_impactRingMaterial == null)
                {
                    _impactRingMaterial = new Material(renderer.sharedMaterial) { name = "Player Impact Ring Material", mainTexture = IMPACT_RING_TEXTURE };
                }
                renderer.sharedMaterial = _impactRingMaterial;
            }
        }

        if (renderer.sprite != sprite)
        {
            renderer.sprite = sprite;
        }
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        renderer.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, angle ?? Range(0f, 360f)));
        renderer.transform.localScale = scale;
        renderer.enabled = true;
        PARTICLES.Add(new Particle { Renderer = renderer, Velocity = velocity, InitialScale = scale, Color = color, Lifetime = lifetime, AngularSpeed = angularSpeed, EndScale = endScale });
    }

    private void ReturnRenderer(SpriteRenderer renderer)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.enabled = false;
        Stack<SpriteRenderer> freeRenderers = renderer.sprite == IMPACT_RING_SPRITE ? FREE_IMPACT_RING_RENDERERS : FREE_RENDERERS;
        freeRenderers.Push(renderer);
    }

    private float Range(float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, (float)RANDOM.NextDouble());
    }
}
