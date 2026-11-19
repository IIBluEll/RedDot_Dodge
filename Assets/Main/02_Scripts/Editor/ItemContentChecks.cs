using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ItemContentChecks
{
    private const string PENDING_KEY = "RedDot.ThreeItemChecks.Pending";
    private static EdgeChaseWaveHost s_host;
    private static ItemSystem s_items;
    private static PlayerMovement s_player;
    private static GameBalance s_balance;
    private static int s_passed;
    private static readonly BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("RedDot Dodge/Validate Three Items")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Play를 종료한 뒤 검증을 시작하세요.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Main/01_Scene/MainPlay.unity")
            throw new InvalidOperationException("MainPlay 씬에서 검증을 시작하세요.");

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        SessionState.SetBool(PENDING_KEY, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        EditorApplication.playModeStateChanged -= OnPlayStateChanged;
        EditorApplication.playModeStateChanged += OnPlayStateChanged;
    }

    private static void OnPlayStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PENDING_KEY, false))
            EditorApplication.update += WaitForStart;
    }

    private static void WaitForStart()
    {
        if (!Application.isPlaying || Time.frameCount < 2)
            return;

        EditorApplication.update -= WaitForStart;
        EditorApplication.isPaused = true;
        try
        {
            s_host = Object.FindFirstObjectByType<EdgeChaseWaveHost>();
            s_items = Object.FindFirstObjectByType<ItemSystem>();
            s_player = Object.FindFirstObjectByType<PlayerMovement>();
            Require(s_host != null && s_host.enabled && Get<EnemyWaveDirector>(s_host, "_waveDirector") != null, "MainPlay 초기화 실패");
            s_balance = Get<GameBalance>(s_host, "_gameBalance");
            s_passed = 0;
            Check("four configured types and equal weights", () =>
            {
                Require(s_balance.Items.Count == 4 && s_balance.Items.All(item => item.Weight == 1), "4종 가중치 불일치");
                Require(s_balance.Items.Select(item => item.Type).Distinct().Count() == 4, "아이템 종류 중복");
                ItemVisual[] visuals = Get<ItemVisual[]>(s_items, "_itemVisuals");
                Require(visuals.Length == 4 && visuals.All(visual => visual.IsValid() && visual.EffectPrefab != null), "아이템 프리팹 연결 누락");
            });
            Check("bomb consumes pickup, kills and updates actual HUD", () =>
            {
                Spawn(ITEM_TYPE.BOMB, new Vector2(2f, 0f));
                s_player.transform.position = new Vector2(2f, 0f);
                AddEnemy(new Vector2(1f, 0f)); AddEnemy(new Vector2(2.1f, 0f)); AddEnemy(new Vector2(3f, 0f));
                Tick();
                Require(s_host.Score == 6 && Enemies.Count == 0 && Running, "폭탄 처치/피격/점수 오류");
                Require(Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Single(text => text.name == "Score Value").text == "6", "실제 점수 HUD 미갱신");
                for (int i = 0; i < 5; i++) Tick();
                Capture("Bomb_Active");
            });
            Check("freeze stops enemies then allows shattering", () =>
            {
                Spawn(ITEM_TYPE.FREEZE, new Vector2(2f, 0f));
                s_player.transform.position = new Vector2(2f, 0f);
                AddEnemy(new Vector2(2.9f, 0f)); AddEnemy(new Vector2(4.3f, 0f));
                Tick();
                Require(Enemies.Count == 2 && FrozenUntil(0) > 3f && FrozenUntil(1) > 3f, "빙결 적용 오류");
                for (int i = 0; i < 5; i++) Tick();
                Capture("Freeze_Active");
                s_player.transform.position = new Vector2(2.9f, 0f);
                Tick();
                Require(s_host.Score == 1 && Enemies.Count == 1 && Running, "쇄빙 오류");
            });
            Check("bubble shield stays armed beyond sixty seconds", () =>
            {
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); Tick();
                Set(s_host, "_survivalSeconds", 61f); Tick();
                Require(ShieldCount(false) == 1 && s_host.Score == 0 && Running, "보호막이 시간 만료됨");
            });
            Check("larger bubble contact triggers a localized blast", () =>
            {
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); Tick();
                AddEnemy(new Vector2(0.7f, 0f)); AddEnemy(new Vector2(1.6f, 0f)); AddEnemy(new Vector2(3f, 0f));
                Tick();
                Require(Running && s_host.Score == 3 && Enemies.Count == 1 && ShieldCount(true) == 1, "보호막 접촉 폭발 오류");
                for (int i = 0; i < 5; i++) Tick();
                Capture("Shield_Burst");
            });
            Check("frozen contact shatters without spending shield", () =>
            {
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); AddEnemy(Vector2.zero, 10f); Tick();
                Require(Running && s_host.Score == 1 && ShieldCount(false) == 1, "빙결 접촉에서 보호막 소모됨");
            });
            Check("repeated pickups keep one armed bubble", () =>
            {
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); Tick();
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); Tick();
                Require(ShieldCount(false) == 1 && s_items.ActiveEffectCount == 1, "보호막이 중첩됨");
            });
            Check("new shield does not cancel previous burst", () =>
            {
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); Tick();
                AddEnemy(new Vector2(0.7f, 0f)); Tick();
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); Tick();
                Require(ShieldCount(false) == 1 && ShieldCount(true) == 1, "새 보호막이 기존 폭발을 취소함");
            });
            Check("freeze applies before bubble collision", () =>
            {
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); Spawn(ITEM_TYPE.FREEZE, Vector2.zero);
                AddEnemy(new Vector2(0.1f, 0f)); Tick();
                Require(ShieldCount(false) == 1 && s_host.Score == 1 && Running, "동시 획득 처리 순서 오류");
            });
            Check("bubble blast grants no global invulnerability", () =>
            {
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); Tick();
                AddEnemy(new Vector2(0.5f, 0f)); AddEnemy(new Vector2(6f, 0f));
                s_player.transform.position = new Vector2(6f, 0f); Tick();
                Require(!Running && s_host.Score == 1, "범위 밖 피격까지 무효화함");
            });
            Check("expiry disables burst visuals and damage together", () =>
            {
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); AddEnemy(new Vector2(0.7f, 0f)); Tick();
                Set(s_host, "_survivalSeconds", 1f); AddEnemy(new Vector2(1.5f, 0f)); Tick();
                Require(s_items.ActiveEffectCount == 0 && Enemies.Count == 1 && s_host.Score == 1, "종료된 보호막 폭발이 남음");
            });
            Check("reset clears held shield, effects, pickups and combo", () =>
            {
                Spawn(ITEM_TYPE.SHIELD, Vector2.zero); Spawn(ITEM_TYPE.BOMB, Vector2.zero); Tick();
                Spawn(ITEM_TYPE.FREEZE, new Vector2(3f, 2f));
                s_host.RestartRound();
                Require(s_items.ActiveEffectCount == 0 && s_items.PickupCount == 0 && s_host.Score == 0 && s_host.Combo == 0, "재시작 정리 오류");
            });
            Reset();
            Spawn(ITEM_TYPE.SHIELD, Vector2.zero); Tick();
            Spawn(ITEM_TYPE.BOMB, new Vector2(-3f, 1.7f));
            Spawn(ITEM_TYPE.FREEZE, new Vector2(0f, 2.5f));
            Spawn(ITEM_TYPE.SHIELD, new Vector2(3f, 1.7f));
            AddEnemy(new Vector2(-5f, -1f)); AddEnemy(new Vector2(5f, -2f)); AddEnemy(new Vector2(4f, 3f));
            s_host.FreezeEnemiesInRadius(new Vector2(-5f, -1f), 0.5f, 10f);
            Tick(); Capture("Items_Overview");
            string report = "{\"passed\":" + s_passed + ",\"failed\":0,\"scene\":\"MainPlay\",\"mode\":\"Unity Play Mode\"}";
            File.WriteAllText(ReportFolder() + "/validation.json", report);
            Debug.Log("THREE_ITEMS_PLAYMODE_PASSED: " + s_passed);
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory(ReportFolder());
            File.WriteAllText(ReportFolder() + "/validation.json", "{\"passed\":" + s_passed + ",\"failed\":1}");
            Debug.LogException(exception);
        }
        finally
        {
            SessionState.SetBool(PENDING_KEY, false);
            EditorApplication.isPaused = false;
            EditorApplication.ExitPlaymode();
        }
    }

    private static void Check(string name, Action action)
    {
        Reset(); action(); s_passed++; Debug.Log("THREE_ITEMS_PASS: " + name);
    }

    private static void Reset()
    {
        s_host.RestartRound();
        Set(s_host, "_readyRemainingSeconds", 0f);
        s_player.enabled = false;
    }

    private static void Spawn(ITEM_TYPE type, Vector2 position)
    {
        s_items.SpawnPickup(s_balance.Items.Single(data => data.Type == type), position, s_host.SurvivalSeconds);
    }

    private static void Tick() => Call(s_host, "FixedUpdate");
    private static bool Running => Get<bool>(s_host, "_roundInProgress");
    private static IList Enemies => Get<IList>(s_host, "ACTIVE_ENEMIES");
    private static float FrozenUntil(int index) => (float)Enemies[index].GetType().GetField("FrozenUntilSeconds").GetValue(Enemies[index]);

    private static void AddEnemy(Vector2 position, float frozenUntil = 0f)
    {
        var enemyObj = new GameObject("Item QA Enemy", typeof(SpriteRenderer));
        enemyObj.transform.SetParent(s_host.transform, false);
        SpriteRenderer renderer = enemyObj.GetComponent<SpriteRenderer>();
        renderer.sprite = Get<Sprite>(s_host, "_enemySprite");
        renderer.transform.position = position;
        renderer.transform.localScale = Vector3.one * (0.45f / renderer.sprite.bounds.size.x);
        renderer.sortingOrder = 1;
        Type type = typeof(EdgeChaseWaveHost).GetNestedType("ActiveEnemy", BindingFlags.NonPublic);
        object enemy = Activator.CreateInstance(type);
        type.GetField("Id").SetValue(enemy, Enemies.Count + 1);
        type.GetField("Renderer").SetValue(enemy, renderer);
        type.GetField("PreviousPosition").SetValue(enemy, position);
        type.GetField("Speed").SetValue(enemy, 0f);
        type.GetField("FrozenUntilSeconds").SetValue(enemy, frozenUntil);
        Enemies.Add(enemy);
    }

    private static int ShieldCount(bool burst)
    {
        int count = 0;
        foreach (object active in Get<IList>(s_items, "EFFECTS"))
        {
            AItemEffect effect = (AItemEffect)active.GetType().GetField("EFFECT").GetValue(active);
            if (effect is ShieldEffect shield && shield.HasBurst == burst) count++;
        }
        return count;
    }

    private static void Capture(string name)
    {
        ScorePresenter_presenter presenter = Object.FindFirstObjectByType<ScorePresenter_presenter>();
        Call(presenter, "Update");
        Camera camera = Camera.main;
        Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(item => item.isRootCanvas);
        RenderMode oldMode = canvas.renderMode;
        Camera oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, 1920f, 1080f), 0, 0);
            texture.Apply();
            Directory.CreateDirectory(ReportFolder());
            File.WriteAllBytes(ReportFolder() + "/" + name + ".png", texture.EncodeToPNG());
            Object.Destroy(texture);
        }
        finally
        {
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static string ReportFolder() => Path.GetFullPath(Application.dataPath + "/../ArtExports/ItemQA");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static FieldInfo Field(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, PRIVATE_INSTANCE);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Call(object target, string name) => target.GetType().GetMethod(name, PRIVATE_INSTANCE).Invoke(target, null);
}
