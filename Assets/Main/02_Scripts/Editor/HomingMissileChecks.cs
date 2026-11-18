using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class HomingMissileChecks
{
    private const string PENDING = "RedDot.MissileChecks.Pending";
    private static readonly BindingFlags PRIVATE = BindingFlags.Instance | BindingFlags.NonPublic;
    private static EdgeChaseWaveHost s_host;
    private static ItemSystem s_items;
    private static HomingMissileEffect s_effect;
    private static float s_elapsed;
    private static float s_accumulator;
    private static int s_frame;
    private static bool s_flightCaptured;
    private static bool s_impactCaptured;

    [MenuItem("RedDot Dodge/Check Homing Missiles")]
    public static void Run()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Main/01_Scene/MainPlay.unity")
            throw new InvalidOperationException("MainPlay를 열고 Play를 종료한 뒤 실행하세요.");
        CheckRules();
        SessionState.SetBool(PENDING, true);
        EditorApplication.EnterPlaymode();
    }

    public static void CheckRules()
    {
        using (var fixture = new Fixture())
        {
            HomingMissileEffect effect = fixture.Begin();
            Require(effect.Missiles.Count == 5, "Expected five missiles.");
            fixture.Step(effect, 0.1f);
            Require(effect.Missiles.All(missile => missile.TargetId == 0 && Mathf.Abs(missile.Position.magnitude - 0.9f) < 0.001f), "Empty-space flight failed.");
            fixture.Add(1, new Vector2(0f, 4f));
            fixture.Step(effect, 0.2f);
            Require(effect.Missiles.Any(missile => missile.TargetId == 1), "New target was not acquired.");
        }
        using (var fixture = new Fixture())
        {
            fixture.Add(1, new Vector2(0f, 4f)); fixture.Add(2, new Vector2(4f, 0f));
            HomingMissileEffect effect = fixture.Begin();
            fixture.Step(effect, 0.02f);
            Require(effect.Missiles.Select(missile => missile.TargetId).Distinct().Count() == 2, "Targets were not distributed.");
            fixture.HOST.KillEnemiesInRadius(new Vector2(0f, 4f), 0.1f);
            fixture.Step(effect, 0.04f);
            Require(effect.Missiles.All(missile => missile.TargetId != 1), "Destroyed target was not replaced.");
        }
        using (var fixture = new Fixture())
        {
            fixture.Add(1, new Vector2(0f, 0.5f), true); fixture.Add(2, new Vector2(0.4f, 0.5f));
            HomingMissileEffect effect = fixture.Begin();
            Require(fixture.Step(effect, 0.1f) == 2, "Swept impact and splash, including frozen enemies, failed.");
            Require(fixture.Step(effect, 0.12f) == 0, "Kills were counted more than once.");
        }
        using (var fixture = new Fixture())
        {
            HomingMissileEffect effect = fixture.Begin();
            fixture.Step(effect, fixture.DEFINITION.LifetimeSeconds);
            Require(effect.Missiles.All(missile => missile.Phase == HOMING_MISSILE_PHASE.EXPLODING), "Timeout did not detonate.");
            fixture.Step(effect, fixture.DEFINITION.LifetimeSeconds + fixture.DEFINITION.BlastDurationSeconds + 0.01f);
            Require(effect.IsFinished, "Expired volley did not finish.");
            Require(!fixture.DEFINITION.ReplaceExisting, "New pickups must keep existing volleys.");
        }
        Debug.Log("MISSILE_RULES_PASSED: five-shot launch, target acquisition/distribution, retargeting, swept splash, frozen kills, no duplicate score and timeout.");
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        EditorApplication.playModeStateChanged -= OnPlayStateChanged;
        EditorApplication.playModeStateChanged += OnPlayStateChanged;
    }

    private static void OnPlayStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PENDING, false))
        {
            s_host = null; s_effect = null; s_elapsed = 0f; s_accumulator = 0f; s_frame = -1;
            s_flightCaptured = false; s_impactCaptured = false;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
            EditorApplication.update -= Tick;
    }

    private static void Tick()
    {
        if (Time.frameCount < 2 || s_frame == Time.frameCount)
            return;
        s_frame = Time.frameCount;
        try
        {
            if (s_host == null) BeginSceneCheck();
            float deltaTime = Mathf.Min(Time.deltaTime, 0.1f);
            s_elapsed += deltaTime;
            s_accumulator += deltaTime;
            while (s_accumulator >= Time.fixedDeltaTime)
            {
                Call(s_host, "FixedUpdate");
                s_accumulator -= Time.fixedDeltaTime;
            }
            if (!s_flightCaptured && s_elapsed >= 0.18f)
            {
                Capture("Missiles_Flying", true);
                s_flightCaptured = true;
            }
            if (!s_impactCaptured && s_effect.Missiles.Any(missile => missile.Phase == HOMING_MISSILE_PHASE.EXPLODING))
            {
                Capture("Missile_Impact", false);
                s_impactCaptured = true;
            }
            if (s_elapsed < 2f) return;
            Require(s_host.KillCount > 0 && s_host.Score > 0 && s_flightCaptured && s_impactCaptured, "MainPlay missile kill/score/render failed.");
            int kills = s_host.KillCount;
            s_host.RestartRound();
            Require(s_items.ActiveEffectCount == 0 && s_items.PickupCount == 0 && s_effect.IsFinished, "Restart left a volley active.");
            File.WriteAllText(Folder() + "/result.json", $"{{\"passed\":true,\"scene\":\"MainPlay\",\"mode\":\"Unity Play Mode\",\"killCount\":{kills},\"fiveMissiles\":true,\"flightRendered\":true,\"impactRendered\":true,\"restartCleared\":true}}");
            Debug.Log("MISSILE_MAINPLAY_PASSED: flight, impact, kills, score, restart.");
            Finish();
        }
        catch (Exception exception)
        {
            File.WriteAllText(Folder() + "/result.json", "{\"passed\":false}");
            Debug.LogException(exception);
            Finish();
        }
    }

    private static void BeginSceneCheck()
    {
        s_host = Object.FindFirstObjectByType<EdgeChaseWaveHost>();
        PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
        s_items = Object.FindFirstObjectByType<ItemSystem>();
        s_host.enabled = false;
        s_host.RestartRound();
        player.enabled = false;
        Set(s_host, "_readyRemainingSeconds", 0f);
        GameBalance balance = Get<GameBalance>(s_host, "_gameBalance");
        Vector2[] positions = { new Vector2(3f, -1f), new Vector2(3.4f, -1f), new Vector2(3f, 1f), new Vector2(3.4f, 1f), new Vector2(4f, 0f), new Vector2(4.3f, 0.1f), new Vector2(1.8f, 2.3f) };
        var request = new WaveRequest(balance.GetPattern(WAVE_PATTERN.EDGE_CHASE), balance.GetStage(0f), positions.Length);
        var plan = new WavePlan(positions.Select(position => new EnemyPlacement(position, position)).ToArray(), Vector2.zero, Vector2.left);
        var wave = new WaveState(request, plan, 0f);
        Require(s_host.TryBeginWave(wave, balance.EnemyCapacity), "Check wave was rejected.");
        Set(s_host, "_survivalSeconds", wave.WarningEndsAtSeconds);
        Call(s_host, "ActivateWarning");
        ItemData data = balance.Items.Single(item => item.Type == ITEM_TYPE.HOMING_MISSILE);
        s_items.SpawnPickup(data, new Vector2(-2f, -1f), s_host.SurvivalSeconds);
        Capture("Missile_Pickup", false);
        s_items.ResetRound(204);
        s_items.SpawnPickup(data, player.transform.position, s_host.SurvivalSeconds);
        Call(s_host, "FixedUpdate");
        foreach (object entry in Get<IList>(s_items, "EFFECTS"))
            if (entry.GetType().GetField("EFFECT").GetValue(entry) is HomingMissileEffect missile) s_effect = missile;
        Require(s_effect != null && s_effect.Missiles.Count == 5, "Pickup did not launch five missiles.");
    }

    private static void Capture(string name, bool requireMissilePixels)
    {
        Texture2D texture = Render();
        if (requireMissilePixels)
        {
            HomingMissileVisual visual = Object.FindFirstObjectByType<HomingMissileVisual>();
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            bool[] enabled = renderers.Select(renderer => renderer.enabled).ToArray();
            foreach (Renderer renderer in renderers) renderer.enabled = false;
            Texture2D baseline = Render();
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = enabled[i];
            Color32[] first = texture.GetPixels32(); Color32[] second = baseline.GetPixels32();
            int changed = 0;
            for (int i = 0; i < first.Length; i++)
                if (Math.Abs(first[i].r - second[i].r) + Math.Abs(first[i].g - second[i].g) + Math.Abs(first[i].b - second[i].b) > 12) changed++;
            Object.Destroy(baseline);
            Require(changed > 30, "Missiles have no visible pixels.");
        }
        File.WriteAllBytes(Folder() + "/" + name + ".png", texture.EncodeToPNG());
        Object.Destroy(texture);
    }

    private static Texture2D Render()
    {
        Camera camera = Camera.main;
        RenderTexture oldTarget = camera.targetTexture; RenderTexture oldActive = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
            return texture;
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void Finish()
    {
        SessionState.SetBool(PENDING, false);
        EditorApplication.update -= Tick;
        EditorApplication.isPaused = false;
        EditorApplication.ExitPlaymode();
    }
    private static string Folder() { string path = Path.GetFullPath(Application.dataPath + "/../ArtExports/MissileQA"); Directory.CreateDirectory(path); return path; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, PRIVATE).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, PRIVATE).SetValue(target, value);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, PRIVATE).Invoke(target, null);

    private sealed class Fixture : IDisposable
    {
        public readonly GameBalance BALANCE = ScriptableObject.CreateInstance<GameBalance>();
        public readonly HomingMissileDefinition DEFINITION = ScriptableObject.CreateInstance<HomingMissileDefinition>();
        public readonly GameObject ROOT_OBJ = new GameObject("Missile Rules Check");
        public readonly EdgeChaseWaveHost HOST;
        private readonly Sprite SPRITE;
        public Fixture()
        {
            HOST = ROOT_OBJ.AddComponent<EdgeChaseWaveHost>();
            Set(HOST, "_gameBalance", BALANCE);
            Set(HOST, "_itemSystem", ROOT_OBJ.AddComponent<ItemSystem>());
            Texture2D texture = Texture2D.whiteTexture;
            SPRITE = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f, texture.width, 0, SpriteMeshType.FullRect);
            Set(HOST, "_enemySprite", SPRITE);
        }
        public void Add(int id, Vector2 position, bool frozen = false)
        {
            SpriteRenderer renderer = new GameObject("Target", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
            renderer.transform.SetParent(ROOT_OBJ.transform);
            renderer.transform.position = position; renderer.sprite = SPRITE;
            Type type = typeof(EdgeChaseWaveHost).GetNestedType("ActiveEnemy", BindingFlags.NonPublic);
            object enemy = Activator.CreateInstance(type);
            type.GetField("Id").SetValue(enemy, id); type.GetField("Renderer").SetValue(enemy, renderer);
            type.GetField("PreviousPosition").SetValue(enemy, position);
            type.GetField("FrozenUntilSeconds").SetValue(enemy, frozen ? 99f : 0f);
            Get<IList>(HOST, "ACTIVE_ENEMIES").Add(enemy);
        }
        public HomingMissileEffect Begin()
        {
            var effect = new HomingMissileEffect(DEFINITION, new ItemEffectContext(HOST, null, Vector2.zero, 0f));
            effect.Begin(); return effect;
        }
        public int Step(HomingMissileEffect effect, float time) { effect.Tick(time); return effect.Apply(); }
        public void Dispose() { Object.DestroyImmediate(ROOT_OBJ); Object.DestroyImmediate(SPRITE); Object.DestroyImmediate(BALANCE); Object.DestroyImmediate(DEFINITION); }
    }
}
