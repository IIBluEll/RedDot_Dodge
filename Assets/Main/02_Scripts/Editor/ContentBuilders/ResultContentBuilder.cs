using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ResultContentBuilder
{
    private const string SCENE_PATH = "Assets/Main/01_Scene/InGame.unity";
    private const string SPRITE_PATH = "Assets/Main/04_Resource/Sprites/";
    private static readonly Color TEXT_COLOR = new Color32(237, 247, 255, 255);
    private static readonly Color GOLD_COLOR = new Color32(255, 225, 60, 255);

    [MenuItem("RedDot Dodge/Create Result Screen")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Play를 종료한 뒤 실행해 주세요.");
        if (EditorSceneManager.GetActiveScene().path != SCENE_PATH)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(SCENE_PATH);
        }
        InGameFlowController flow = Object.FindFirstObjectByType<InGameFlowController>(FindObjectsInactive.Include);
        if (flow == null)
            throw new InvalidOperationException("InGame의 Flow를 찾지 못했습니다.");
        var cameraData = new SerializedObject(flow);
        Set(cameraData, "_arenaCamera", Object.FindFirstObjectByType<InGameArenaCamera>());
        cameraData.ApplyModifiedPropertiesWithoutUndo();
        ResultPanel_view existingView = Object.FindFirstObjectByType<ResultPanel_view>(FindObjectsInactive.Include);
        if (existingView != null)
        {
            BindHudBestScore(flow.GetComponent<ResultPresenter_presenter>());
            ConfigureTitleGlow(existingView);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("기존 결과 화면 배치를 유지하고 최고 기록 표시를 연결했습니다.");
            return;
        }

        TMP_FontAsset koreanFont = Load<TMP_FontAsset>("Assets/Main/04_Resource/Font/DoHyeon-Regular SDF.asset");
        TMP_FontAsset numberFont = Load<TMP_FontAsset>("Assets/Main/04_Resource/Font/Oxanium-Bold SDF.asset");
        RectTransform canvasRectTrans = Rect("Result_Canvas", null, Vector2.zero, Vector2.zero);
        Canvas canvas = canvasRectTrans.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30;
        CanvasScaler scaler = canvasRectTrans.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRectTrans.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform overlayRectTrans = Rect("Result Overlay", canvasRectTrans, Vector2.zero, Vector2.zero);
        Stretch(overlayRectTrans);
        Image dimmerImg = overlayRectTrans.gameObject.AddComponent<Image>();
        dimmerImg.color = new Color(0.005f, 0.01f, 0.03f, 0.82f);
        CanvasGroup group = overlayRectTrans.gameObject.AddComponent<CanvasGroup>();
        ResultPanel_view view = overlayRectTrans.gameObject.AddComponent<ResultPanel_view>();

        RectTransform safeAreaRectTrans = Rect("SafeArea", overlayRectTrans, Vector2.zero, Vector2.zero);
        Stretch(safeAreaRectTrans);
        safeAreaRectTrans.gameObject.AddComponent<SafeAreaPanel>();
        RectTransform panelRectTrans = Rect("Result Panel", safeAreaRectTrans, Vector2.zero, new Vector2(1040f, 720f));
        Image panelImg = panelRectTrans.gameObject.AddComponent<Image>();
        panelImg.sprite = Sprite("UI/panel_dialog.png");
        panelImg.raycastTarget = false;

        Text("Section Label", panelRectTrans, new Vector2(-245f, 288f), new Vector2(390f, 42f), "게임 오버 / 결과", koreanFont, 27f, TEXT_COLOR, TextAlignmentOptions.Left);
        TMP_Text titleTxt = Text("Game Over", panelRectTrans, new Vector2(0f, 199f), new Vector2(870f, 120f), "GAME OVER", numberFont, 78f, Color.white);
        titleTxt.fontStyle = FontStyles.Bold | FontStyles.Italic;
        Shadow glow = titleTxt.gameObject.AddComponent<Shadow>();
        glow.effectColor = new Color(0.9f, 0.08f, 1f, 0.8f);
        glow.effectDistance = new Vector2(2.5f, -2.5f);

        TMP_Text scoreTxt = Stat("Final Score", panelRectTrans, 90f, "최종 점수", koreanFont, numberFont, GOLD_COLOR);
        TMP_Text comboTxt = Stat("Maximum Combo", panelRectTrans, 20f, "최대 콤보", koreanFont, numberFont, TEXT_COLOR);
        TMP_Text survivalTxt = Stat("Survival Time", panelRectTrans, -50f, "생존 시간", koreanFont, numberFont, TEXT_COLOR);
        TMP_Text killsTxt = Stat("Kill Count", panelRectTrans, -120f, "처치 수", koreanFont, numberFont, TEXT_COLOR);

        RectTransform recordRectTrans = Rect("Best Record Card", panelRectTrans, new Vector2(306f, -13f), new Vector2(248f, 264f));
        Image recordImg = recordRectTrans.gameObject.AddComponent<Image>();
        recordImg.color = new Color(0.015f, 0.027f, 0.065f, 1f);
        recordImg.raycastTarget = false;
        Outline border = recordRectTrans.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(0.32f, 0.47f, 0.66f, 1f);
        border.effectDistance = new Vector2(1.5f, -1.5f);
        TMP_Text recordLabelTxt = Text("Record Label", recordRectTrans, new Vector2(0f, 52f), new Vector2(224f, 46f), "최고 기록!", koreanFont, 32f, GOLD_COLOR);
        TMP_Text bestScoreTxt = Text("Best Score", recordRectTrans, new Vector2(0f, -16f), new Vector2(224f, 74f), "0", numberFont, 44f, GOLD_COLOR);

        Button retryBtn = ActionButton("Retry Button", panelRectTrans, new Vector2(-226f, -247f), "다시 하기", "Icons/icon_retry.png", koreanFont, true, flow.OnRetryActioned);
        Button menuBtn = ActionButton("Menu Button", panelRectTrans, new Vector2(226f, -247f), "메인 메뉴", "Icons/icon_home.png", koreanFont, false, flow.ReturnToMenu);
        Navigation retryNavigation = retryBtn.navigation;
        retryNavigation.mode = Navigation.Mode.Explicit;
        retryNavigation.selectOnRight = menuBtn;
        retryBtn.navigation = retryNavigation;
        Navigation menuNavigation = menuBtn.navigation;
        menuNavigation.mode = Navigation.Mode.Explicit;
        menuNavigation.selectOnLeft = retryBtn;
        menuBtn.navigation = menuNavigation;

        var viewData = new SerializedObject(view);
        Set(viewData, "_canvasGroup", group);
        Set(viewData, "_panelRectTrans", panelRectTrans);
        Set(viewData, "_scoreTxt", scoreTxt);
        Set(viewData, "_maximumComboTxt", comboTxt);
        Set(viewData, "_survivalTxt", survivalTxt);
        Set(viewData, "_killCountTxt", killsTxt);
        Set(viewData, "_bestScoreTxt", bestScoreTxt);
        Set(viewData, "_recordLabelTxt", recordLabelTxt);
        Set(viewData, "_retryBtn", retryBtn);
        viewData.ApplyModifiedPropertiesWithoutUndo();

        ResultPresenter_presenter presenter = flow.gameObject.AddComponent<ResultPresenter_presenter>();
        var presenterData = new SerializedObject(presenter);
        Set(presenterData, "_view", view);
        presenterData.ApplyModifiedPropertiesWithoutUndo();
        BindHudBestScore(presenter);
        ConfigureTitleGlow(view);
        var flowData = new SerializedObject(flow);
        Set(flowData, "_resultPresenter", presenter);
        flowData.ApplyModifiedPropertiesWithoutUndo();
        var hostData = new SerializedObject(flowData.FindProperty("_waveHost").objectReferenceValue);
        hostData.FindProperty("_allowKeyboardRestart").boolValue = false;
        hostData.ApplyModifiedPropertiesWithoutUndo();

        overlayRectTrans.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("RESULT_SCREEN_CREATED: InGame 결과 화면·버튼·최고 점수 표시 연결 완료");
    }

    private static void BindHudBestScore(ResultPresenter_presenter presenter)
    {
        var presenterData = new SerializedObject(presenter);
        Set(presenterData, "_hudBestScoreTxt", Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(txt => txt.name == "Best Score Value"));
        presenterData.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureTitleGlow(ResultPanel_view view)
    {
        const string folder = "Assets/Main/06_Content/UI";
        const string path = folder + "/ResultTitleGlow.mat";
        TMP_Text titleTxt = view.GetComponentsInChildren<TMP_Text>(true).Single(txt => txt.name == "Game Over");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Main/06_Content", "UI");
            material = new Material(titleTxt.font.material) { name = "ResultTitleGlow" };
            material.EnableKeyword("UNDERLAY_ON");
            material.SetColor("_UnderlayColor", new Color(1f, 0.04f, 1f, 0.9f));
            material.SetFloat("_UnderlayDilate", 0.35f);
            material.SetFloat("_UnderlaySoftness", 0.75f);
            AssetDatabase.CreateAsset(material, path);
        }
        titleTxt.fontSharedMaterial = material;
        AssetDatabase.SaveAssets();
    }

    private static TMP_Text Stat(string name, RectTransform parent, float y, string label, TMP_FontAsset koreanFont, TMP_FontAsset numberFont, Color valueColor)
    {
        Text(name + " Label", parent, new Vector2(-293f, y), new Vector2(240f, 54f), label, koreanFont, 31f, TEXT_COLOR, TextAlignmentOptions.Left);
        TMP_Text valueTxt = Text(name + " Value", parent, new Vector2(-35f, y), new Vector2(245f, 54f), "0", numberFont, 36f, valueColor, TextAlignmentOptions.Right);
        RectTransform lineRectTrans = Rect(name + " Divider", parent, new Vector2(-157f, y - 34f), new Vector2(515f, 1f));
        Image lineImg = lineRectTrans.gameObject.AddComponent<Image>();
        lineImg.color = new Color(0.3f, 0.58f, 0.8f, 0.18f);
        lineImg.raycastTarget = false;
        return valueTxt;
    }

    private static Button ActionButton(string name, RectTransform parent, Vector2 position, string label, string iconPath, TMP_FontAsset font, bool primary, UnityAction action)
    {
        RectTransform buttonRectTrans = Rect(name, parent, position, new Vector2(414f, 102f));
        Image buttonImg = buttonRectTrans.gameObject.AddComponent<Image>();
        buttonImg.sprite = Sprite(primary ? "UI/button_focus.png" : "UI/button_normal.png");
        Button button = buttonRectTrans.gameObject.AddComponent<Button>();
        button.targetGraphic = buttonImg;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState { highlightedSprite = Sprite("UI/button_focus.png"), selectedSprite = Sprite("UI/button_focus.png"), pressedSprite = Sprite("UI/button_pressed.png") };
        UnityEventTools.AddPersistentListener(button.onClick, action);
        RectTransform iconRectTrans = Rect("Icon", buttonRectTrans, new Vector2(-125f, 0f), new Vector2(48f, 48f));
        Image iconImg = iconRectTrans.gameObject.AddComponent<Image>();
        iconImg.sprite = Sprite(iconPath);
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        Text("Label", buttonRectTrans, new Vector2(34f, 0f), new Vector2(230f, 65f), label, font, 35f, TEXT_COLOR);
        return button;
    }

    private static TMP_Text Text(string name, RectTransform parent, Vector2 position, Vector2 size, string value, TMP_FontAsset font, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform textRectTrans = Rect(name, parent, position, size);
        TextMeshProUGUI text = textRectTrans.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 18f;
        text.fontSizeMax = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rectObj = new GameObject(name, typeof(RectTransform));
        rectObj.layer = LayerMask.NameToLayer("UI");
        var rectTrans = (RectTransform)rectObj.transform;
        rectTrans.SetParent(parent, false);
        rectTrans.anchorMin = Vector2.one * 0.5f;
        rectTrans.anchorMax = Vector2.one * 0.5f;
        rectTrans.anchoredPosition = position;
        rectTrans.sizeDelta = size;
        return rectTrans;
    }

    private static void Stretch(RectTransform rectTrans)
    {
        rectTrans.anchorMin = Vector2.zero;
        rectTrans.anchorMax = Vector2.one;
        rectTrans.offsetMin = Vector2.zero;
        rectTrans.offsetMax = Vector2.zero;
    }

    private static Sprite Sprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(SPRITE_PATH + path).OfType<Sprite>().FirstOrDefault();
        return sprite != null ? sprite : throw new InvalidOperationException("Sprite를 찾지 못했습니다: " + path);
    }

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset != null ? asset : throw new InvalidOperationException("에셋을 찾지 못했습니다: " + path);
    }

    private static void Set(SerializedObject data, string propertyName, Object value)
    {
        data.FindProperty(propertyName).objectReferenceValue = value;
    }
}
