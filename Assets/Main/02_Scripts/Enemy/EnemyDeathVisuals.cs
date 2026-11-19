using System.Collections.Generic;
using UnityEngine;

// Cosmetic particles only: no collision, enemy state or score is kept here.
public sealed class EnemyDeathVisuals
{
    private const int MAX_PARTICLES = 864;
    private readonly Transform ROOT_TRANS;
    private readonly Sprite SHARD_SPRITE;
    private readonly Texture2D SHARD_TEXTURE;
    private readonly System.Random RANDOM = new System.Random();
    private readonly List<Particle> PARTICLES = new();
    private readonly Stack<SpriteRenderer> FREE_RENDERERS = new();

    private struct Particle
    {
        public SpriteRenderer Renderer;
        public Vector2 Velocity;
        public Vector3 InitialScale;
        public Color Color;
        public float Age;
        public float Lifetime;
        public float AngularSpeed;
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
            particle.Renderer.transform.localScale = particle.InitialScale * Mathf.Lerp(1f, 0.25f, progress);
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
    }

    private void AddParticle(Vector2 position, Sprite sprite, Vector2 velocity, Vector3 scale, Color color, float lifetime, float angularSpeed)
    {
        SpriteRenderer renderer;
        if (FREE_RENDERERS.Count > 0)
            renderer = FREE_RENDERERS.Pop();
        else
        {
            var shardObj = new GameObject("Death Particle", typeof(SpriteRenderer));
            shardObj.transform.SetParent(ROOT_TRANS, false);
            renderer = shardObj.GetComponent<SpriteRenderer>();
            renderer.sortingOrder = 5;
        }

        renderer.sprite = sprite;
        renderer.color = color;
        renderer.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, Range(0f, 360f)));
        renderer.transform.localScale = scale;
        renderer.enabled = true;
        PARTICLES.Add(new Particle { Renderer = renderer, Velocity = velocity, InitialScale = scale, Color = color, Lifetime = lifetime, AngularSpeed = angularSpeed });
    }

    private void ReturnRenderer(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;
        renderer.enabled = false;
        FREE_RENDERERS.Push(renderer);
    }

    private float Range(float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, (float)RANDOM.NextDouble());
    }
}
