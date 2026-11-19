using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ResultFlowChecks
{
    private const string PENDING_KEY = "RedDotDodge.ResultQA.Pending";
    private const string SCORE_KEY = "RedDotDodge.ResultQA.BestScore";
    private const BindingFlags PRIVATE_INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> CHECKS = new();
    private static bool s_started;

    [Serializable]
    private sealed class Report
    {
        public bool Passed;
        public string[] Checks;
        public string Error;
    }

    [MenuItem("RedDot Dodge/Check Result Flow")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Play를 종료한 뒤 실행해 주세요.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene("Assets/Main/01_Scene/InGame.unity");
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
        {
            s_started = false;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
            EditorApplication.update -= Tick;
    }

    private static void Tick()
    {
        if (s_started || Time.frameCount < 2)
            return;
        s_started = true;
        EditorApplication.update -= Tick;
        CHECKS.Clear();
        try
        {
            CheckStorage();
            CheckFlow();
            Complete(true, null);
        }
        catch (Exception exception)
        {
            Complete(false, exception.ToString());
        }
        finally
        {
            PlayerPrefs.DeleteKey(SCORE_KEY);
            PlayerPrefs.Save();
        }
    }

    private static void CheckStorage()
    {
        PlayerPrefs.DeleteKey(SCORE_KEY);
        var storage = new PlayerPrefsScoreStorage(SCORE_KEY);
        Require(storage.BestScore == 0L, "첫 실행 최고 점수 0");
        long largeScore = (long)int.MaxValue + 24600L;
        Require(storage.TrySaveBestScore(largeScore), "32비트 범위를 넘는 최고 점수 저장");
        Require(new PlayerPrefsScoreStorage(SCORE_KEY).BestScore == largeScore, "저장소 재생성 후 64비트 점수 복원");
        Require(!storage.TrySaveBestScore(largeScore) && !storage.TrySaveBestScore(1L), "동점·낮은 점수 갱신 제외");
        Require(storage.TrySaveBestScore(long.MaxValue) && new PlayerPrefsScoreStorage(SCORE_KEY).BestScore == long.MaxValue, "long 최대값 손실 없이 저장·복원");
        foreach (string invalid in new[] { "invalid", "-5", "9223372036854775808" })
        {
            PlayerPrefs.SetString(SCORE_KEY, invalid);
            Require(new PlayerPrefsScoreStorage(SCORE_KEY).BestScore == 0L, "손상된 저장값 복구: " + invalid);
        }
        PlayerPrefs.DeleteKey(SCORE_KEY);
    }

    private static void CheckFlow()
    {
        InGameFlowController flow = Object.FindFirstObjectByType<InGameFlowController>();
        ResultPresenter_presenter presenter = Object.FindFirstObjectByType<ResultPresenter_presenter>();
        ResultPanel_view view = Object.FindFirstObjectByType<ResultPanel_view>(FindObjectsInactive.Include);
        EdgeChaseWaveHost host = Get<EdgeChaseWaveHost>(flow, "_waveHost");
        PlayerMovement player = Get<PlayerMovement>(host, "_playerMovement");
        ItemSystem items = Get<ItemSystem>(host, "_itemSystem");
        GameBalance balance = Get<GameBalance>(host, "_gameBalance");
        InGameArenaCamera arenaCamera = Get<InGameArenaCamera>(flow, "_arenaCamera");
        RectTransform hudRectTrans = Get<RectTransform>(flow, "_hudRectTrans");
        RectTransform arenaRectTrans = Get<RectTransform>(flow, "_arenaRectTrans");
        Vector2 hudPosition = hudRectTrans.anchoredPosition;
        Vector2 arenaPosition = arenaRectTrans.anchoredPosition;
        var storage = new PlayerPrefsScoreStorage(SCORE_KEY);
        Set(presenter, "_scoreStorage", storage);
        presenter.Initialize();
        Require(State(flow) == "MENU" && !view.gameObject.activeSelf && !player.gameObject.activeSelf, "메뉴 시작 시 결과·플레이어 숨김");
        Require(!Get<bool>(host, "_allowKeyboardRestart"), "InGame의 R 키 우회 재시작 비활성화");
        flow.OnGameStartActioned();
        DOTween.CompleteAll(true);
        Require(State(flow) == "IN_GAME" && host.ReadyRemainingSeconds > 0f, "메뉴 시작 버튼에서 준비 단계 진입");
        host.enabled = false;
        player.enabled = false;
        int resultCount = 0;
        RoundResult snapshot = default;
        host.RoundEnded += result => { resultCount++; snapshot = result; };

        for (int round = 0; round < 3; round++)
        {
            Set(host, "_readyRemainingSeconds", 0f);
            Set(host, "_survivalSeconds", 157.32f);
            Get<RoundScore>(host, "_roundScore").AddKills(5, host.SurvivalSeconds);
            AddContactEnemy(host, player.transform.position);
            AddContactEnemy(host, new Vector2(3f, 2f));
            AddContactEnemy(host, new Vector2(-3f, -2f));
            var request = new WaveRequest(balance.Patterns.First(weight => weight.Pattern is EdgeChasePatternData).Pattern, balance.GetStage(host.SurvivalSeconds), 2);
            var plan = new WavePlan(new[] { new EnemyPlacement(new Vector2(-6f, 2f), new Vector2(-6f, 2f)), new EnemyPlacement(new Vector2(6f, 2f), new Vector2(6f, 2f)) }, Vector2.zero, Vector2.right);
            Require(host.TryBeginWave(new WaveState(request, plan, host.SurvivalSeconds), balance.EnemyCapacity), "사망 시 예고 상태 정리 검사 구성: " + round);
            if (round == 0)
            {
                while (host.EnemyCount < balance.EnemyCapacity)
                    AddContactEnemy(host, new Vector2(5f, -2f));
                Require(host.EnemyCount == 96, "적 상한 96기 상태에서 사망 연출 검사");
            }
            Vector2 effectPosition = new Vector2(-6f, -2f);
            items.SpawnPickup(balance.Items.Single(item => item.Type == ITEM_TYPE.BOMB), effectPosition, host.SurvivalSeconds);
            items.CollectPickups(effectPosition, effectPosition, balance.PlayerRadius, host.SurvivalSeconds);
            items.SpawnPickup(balance.Items.Single(item => item.Type == ITEM_TYPE.FREEZE), new Vector2(4f, -3f), host.SurvivalSeconds);
            Require(items.ActiveEffectCount > 0 && items.PickupCount > 0, "활성 효과·미획득 아이템 정리 검사 구성: " + round);
            Vector3 cameraPosition = arenaCamera.transform.position;
            Call(host, "FixedUpdate");
            Require(State(flow) == "DEATH_TRANSITION" && !view.gameObject.activeSelf && !arenaCamera.enabled, "사망 연출 중 결과창 대기·카메라 고정: " + round);
            Require(host.EnemyCount == 0 && !host.HasWarning && !host.HasFormation, "활성 적·예고·대형 전체 제거: " + round);
            Require(items.PickupCount == 0 && items.ActiveEffectCount == 0 && !player.gameObject.activeSelf, "아이템·잔류 효과·플레이어 숨김: " + round);
            Require(Get<IList>(Get<EnemyDeathVisuals>(host, "_deathVisuals"), "PARTICLES").Count > 0, "남은 적 파편 연출 발생: " + round);
            Require(storage.BestScore == 15L, "사망 연출 시작 시 최고 점수 즉시 저장: " + round);
            Sequence deathTransition = Get<Sequence>(flow, "_transition");
            deathTransition.Goto(deathTransition.Duration() * 0.9f);
            Canvas.ForceUpdateCanvases();
            Require(hudRectTrans.anchoredPosition.y > hudPosition.y && arenaRectTrans.anchoredPosition.y < arenaPosition.y, "HUD 위쪽·경기장 아래쪽 퇴장: " + round);
            Require(arenaCamera.transform.position == cameraPosition && Get<CanvasGroup>(flow, "_inGameGroup").alpha == 1f, "UI 퇴장 중 카메라·배경 유지: " + round);
            DOTween.CompleteAll(true);
            DOTween.CompleteAll(true);
            Require(State(flow) == "RESULT" && view.gameObject.activeSelf && !Get<bool>(host, "_roundInProgress"), "실제 적 접촉 후 결과 화면: " + round);
            Require(!hudRectTrans.gameObject.activeSelf && !arenaRectTrans.gameObject.activeSelf
                && Get<GameObject>(flow, "_inGameCanvasObj").transform.Find("BackGround").gameObject.activeInHierarchy, "결과에는 배경과 결과창만 표시: " + round);
            Require(Get<IList>(Get<EnemyDeathVisuals>(host, "_deathVisuals"), "PARTICLES").Count == 0, "결과창 진입 시 파편 정리 완료: " + round);
            Require(snapshot.Score == 15L && snapshot.MaximumCombo == 5 && snapshot.KillCount == 5, "사망 결과 통계 확정: " + round);
            Require(resultCount == round + 1, "한 판의 사망 이벤트 한 번만 발생: " + round);
            Call(host, "FinishRound");
            Call(host, "FixedUpdate");
            Require(resultCount == round + 1 && host.Score == snapshot.Score && host.SurvivalSeconds == snapshot.SurvivalSeconds, "사망 후 점수·시간·결과 고정: " + round);
            Require(Get<TMP_Text>(view, "_recordLabelTxt").text == (round == 0 ? "최고 기록!" : "최고 점수"), "신기록과 동점 표시: " + round);
            Require(storage.BestScore == 15L && Get<TMP_Text>(presenter, "_hudBestScoreTxt").text == "15", "결과·HUD 최고 점수 연결: " + round);

            if (round == 2)
            {
                view.Show(new RoundResult(24600L, 18, 156, 157.34f), 24600L, true);
                DOTween.CompleteAll(true);
                Capture("ResultPreview");
                view.Show(new RoundResult(long.MaxValue, int.MaxValue, int.MaxValue, 157.34f), long.MaxValue, false);
                DOTween.CompleteAll(true);
                Canvas.ForceUpdateCanvases();
                Require(!Get<TMP_Text>(view, "_scoreTxt").isTextOverflowing && !Get<TMP_Text>(view, "_bestScoreTxt").isTextOverflowing, "64비트 최대 점수 자동 축소 표시");
            }

            Get<Button>(view, "_retryBtn").onClick.Invoke();
            Require(State(flow) == "START_TRANSITION" && !view.gameObject.activeSelf, "Retry 후 게임 UI 복귀 전환: " + round);
            DOTween.CompleteAll(true);
            Require(State(flow) == "IN_GAME" && !view.gameObject.activeSelf && host.Score == 0L && host.Combo == 0 && host.KillCount == 0 && host.EnemyCount == 0, "Retry 버튼 후 이전 판 데이터 정리: " + round);
            Require(host.SurvivalSeconds == 0f && host.ReadyRemainingSeconds > 0f, "Retry 후 3초 준비 재시작: " + round);
            Require(hudRectTrans.gameObject.activeSelf && arenaRectTrans.gameObject.activeSelf && arenaCamera.enabled
                && hudRectTrans.anchoredPosition == hudPosition && arenaRectTrans.anchoredPosition == arenaPosition, "Retry 후 UI 위치·카메라 복원: " + round);
        }

        Set(host, "_readyRemainingSeconds", 0f);
        AddContactEnemy(host, player.transform.position);
        Call(host, "FixedUpdate");
        DOTween.CompleteAll(true);
        DOTween.CompleteAll(true);
        view.GetComponentsInChildren<Button>().Single(button => button.name == "Menu Button").onClick.Invoke();
        DOTween.CompleteAll(true);
        Require(State(flow) == "MENU" && !view.gameObject.activeSelf && storage.BestScore == 15L, "결과의 메뉴 버튼 복귀와 결과 숨김");
        flow.OnGameStartActioned();
        DOTween.CompleteAll(true);
        Get<RoundScore>(host, "_roundScore").AddKills(100, 0f);
        flow.ReturnToMenu();
        DOTween.CompleteAll(true);
        Require(storage.BestScore == 15L, "진행 중 메뉴 종료는 최고 점수 저장 제외");

        flow.OnGameStartActioned();
        DOTween.CompleteAll(true);
        Set(host, "_readyRemainingSeconds", 0f);
        AddContactEnemy(host, player.transform.position);
        Call(host, "FixedUpdate");
        Require(State(flow) == "DEATH_TRANSITION", "사망 연출 중 메뉴 복귀 검사 구성");
        flow.ReturnToMenu();
        DOTween.CompleteAll(true);
        Require(State(flow) == "MENU" && !view.gameObject.activeSelf && arenaCamera.enabled
            && hudRectTrans.anchoredPosition == hudPosition && arenaRectTrans.anchoredPosition == arenaPosition, "사망 연출 중단 후 결과 재등장 없이 메뉴 복귀");
    }

    private static void AddContactEnemy(EdgeChaseWaveHost host, Vector2 position)
    {
        var enemyObj = new GameObject("Result QA Enemy", typeof(SpriteRenderer));
        enemyObj.transform.SetParent(host.transform, false);
        SpriteRenderer renderer = enemyObj.GetComponent<SpriteRenderer>();
        renderer.sprite = Get<Sprite>(host, "_enemySprite");
        renderer.transform.position = position;
        Type type = typeof(EdgeChaseWaveHost).GetNestedType("ActiveEnemy", BindingFlags.NonPublic);
        object enemy = Activator.CreateInstance(type);
        type.GetField("Id").SetValue(enemy, 1);
        type.GetField("Renderer").SetValue(enemy, renderer);
        type.GetField("PreviousPosition").SetValue(enemy, position);
        Get<IList>(host, "ACTIVE_ENEMIES").Add(enemy);
    }

    private static void Capture(string name)
    {
        Camera camera = Camera.main;
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(canvas => canvas.isRootCanvas).ToArray();
        RenderMode[] modes = canvases.Select(canvas => canvas.renderMode).ToArray();
        Camera[] cameras = canvases.Select(canvas => canvas.worldCamera).ToArray();
        float[] distances = canvases.Select(canvas => canvas.planeDistance).ToArray();
        RenderTexture oldTarget = camera.targetTexture;
        int oldCullingMask = camera.cullingMask;
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
        var uiCameraObj = new GameObject("Result QA UI Camera", typeof(Camera));
        Camera uiCamera = uiCameraObj.GetComponent<Camera>();
        uiCamera.transform.position = new Vector3(0f, 0f, -10f);
        uiCamera.orthographic = true;
        uiCamera.orthographicSize = 5f;
        uiCamera.clearFlags = CameraClearFlags.Depth;
        uiCamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
        uiCamera.targetTexture = target;
        try
        {
            foreach (Canvas canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = uiCamera;
                canvas.planeDistance = 1f;
            }
            camera.targetTexture = target;
            camera.cullingMask &= ~uiCamera.cullingMask;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            uiCamera.Render();
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
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = modes[i];
                canvases[i].worldCamera = cameras[i];
                canvases[i].planeDistance = distances[i];
            }
            camera.targetTexture = oldTarget;
            camera.cullingMask = oldCullingMask;
            RenderTexture.active = oldActive;
            Object.Destroy(uiCameraObj);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static void Complete(bool passed, string error)
    {
        SessionState.SetBool(PENDING_KEY, false);
        Directory.CreateDirectory(ReportFolder());
        File.WriteAllText(ReportFolder() + "/result.json", JsonUtility.ToJson(new Report { Passed = passed, Checks = CHECKS.ToArray(), Error = error }, true));
        if (passed)
            Debug.Log("RESULT_FLOW_CHECKS_PASSED: " + CHECKS.Count);
        else
            Debug.LogError("RESULT_FLOW_CHECKS_FAILED: " + error);
        EditorApplication.ExitPlaymode();
        if (Application.isBatchMode)
            EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
    }

    private static string ReportFolder() => Path.GetFullPath(Application.dataPath + "/../ArtExports/ResultQA");
    private static string State(object flow) => Get<object>(flow, "_state").ToString();
    private static FieldInfo Field(object target, string name) => target.GetType().GetField(name, PRIVATE_INSTANCE) ?? throw new MissingFieldException(name);
    private static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Call(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, PRIVATE_INSTANCE).Invoke(target, arguments);
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        CHECKS.Add(message);
    }
}
