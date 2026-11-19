using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class ItemContentBuilder
{
    private const string SCENE_PATH = "Assets/Main/01_Scene/MainPlay.unity";
    private const string CONTENT_PATH = "Assets/Main/06_Content/Items";
    private const string ART_PATH = "Assets/Main/04_Resource/Items/Shield";
    private const string AUDIO_PATH = "Assets/Main/04_Resource/Audio/Items";

    [MenuItem("RedDot Dodge/Configure Three Items")]
    public static void Configure()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != SCENE_PATH)
            EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

        EnsureFolder(CONTENT_PATH);
        EnsureFolder(ART_PATH);
        EnsureFolder(AUDIO_PATH);
        Sprite icon = ItemPickupIconBuilder.ImportSprite(ItemPickupIconBuilder.SHIELD_PATH);
        Sprite membrane = CreateShieldSprite("shield_membrane", true);
        AudioClip bombAudio = CreateSound("BombBlast", 0);
        AudioClip freezeAudio = CreateSound("FreezeWave", 1);
        AudioClip equipAudio = CreateSound("ShieldEquip", 2);
        AudioClip burstAudio = CreateSound("ShieldBurst", 3);
        AudioClip shatterAudio = CreateSound("IceShatter", 4);

        Sprite[] bombFrames = LoadFrames("Assets/Main/04_Resource/VFX/BombBlastLayered_Sheet.png");
        Sprite[] freezeFrames = LoadFrames("Assets/Main/04_Resource/VFX/FreezeWaveLayered_Sheet.png");
        Material bombMaterial = Load<Material>("Assets/Main/04_Resource/VFX/PreviewNeon/BombBlastNeon.mat");
        Material freezeMaterial = Load<Material>("Assets/Main/04_Resource/VFX/PreviewNeon/FreezeWaveNeon.mat");
        Material greenMaterial = CreateGreenMaterial();

        ItemEffectVisual bombPrefab = CreateFramePrefab("BombEffect", bombFrames, bombMaterial, bombAudio);
        ItemEffectVisual freezePrefab = CreateFramePrefab("FreezeEffect", freezeFrames, freezeMaterial, freezeAudio);
        ItemEffectVisual shieldPrefab = CreateShieldPrefab(membrane, bombFrames, greenMaterial, equipAudio, burstAudio);
        ShieldEffectDefinition shield = AssetDatabase.LoadAssetAtPath<ShieldEffectDefinition>(CONTENT_PATH + "/BubbleShield.asset");
        if (shield == null)
        {
            shield = ScriptableObject.CreateInstance<ShieldEffectDefinition>();
            AssetDatabase.CreateAsset(shield, CONTENT_PATH + "/BubbleShield.asset");
        }

        GameBalance balance = Load<GameBalance>("Assets/Main/04_Resource/Balance/GameBalance.asset");
        var balanceData = new SerializedObject(balance);
        SerializedProperty items = balanceData.FindProperty("_items");
        items.arraySize = Mathf.Max(items.arraySize, 3);
        SetItem(items.GetArrayElementAtIndex(0), ITEM_TYPE.BOMB, 2.4f, 0.35f, 0.35f, null);
        SetItem(items.GetArrayElementAtIndex(1), ITEM_TYPE.FREEZE, 3.5f, 3.9f, 0.35f, null);
        SetItem(items.GetArrayElementAtIndex(2), ITEM_TYPE.SHIELD, 0f, 0f, 0f, shield);
        balanceData.ApplyModifiedPropertiesWithoutUndo();
        balance.Validate();
        EditorUtility.SetDirty(balance);

        ItemSystem system = Object.FindFirstObjectByType<ItemSystem>(FindObjectsInactive.Include);
        EdgeChaseWaveHost host = Object.FindFirstObjectByType<EdgeChaseWaveHost>(FindObjectsInactive.Include);
        if (host == null)
            throw new InvalidOperationException("MainPlay의 웨이브 Host를 찾지 못했습니다.");

        if (system == null)
        {
            GameObject itemObj = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(trans => trans.gameObject).FirstOrDefault(obj => obj.name == "Item" || obj.name == "Items");
            if (itemObj == null) itemObj = new GameObject("Items");
            system = itemObj.AddComponent<ItemSystem>();
        }

        system.gameObject.name = "Items";
        system.gameObject.SetActive(true);
        system.enabled = true;
        var systemData = new SerializedObject(system);
        if (systemData.FindProperty("_pickupRenderer").objectReferenceValue == null)
        {
            SpriteRenderer template = Child(system.transform, "Pickup Template").AddComponent<SpriteRenderer>();
            template.sortingOrder = 2;
            systemData.FindProperty("_pickupRenderer").objectReferenceValue = template;
        }
        SerializedProperty visuals = systemData.FindProperty("_itemVisuals");
        visuals.arraySize = Mathf.Max(visuals.arraySize, 3);
        SetVisual(visuals.GetArrayElementAtIndex(0), ITEM_TYPE.BOMB,
            LoadSprite("Assets/Main/04_Resource/Sprites/Gameplay/pickup_bomb.png"), bombPrefab);
        SetVisual(visuals.GetArrayElementAtIndex(1), ITEM_TYPE.FREEZE,
            LoadSprite("Assets/Main/04_Resource/Sprites/Gameplay/pickup_freeze.png"), freezePrefab);
        SetVisual(visuals.GetArrayElementAtIndex(2), ITEM_TYPE.SHIELD, icon, shieldPrefab);
        systemData.FindProperty("_frozenEnemySprite").objectReferenceValue = LoadSprite("Assets/Main/04_Resource/VFX/FrozenEnemy.png");
        systemData.ApplyModifiedPropertiesWithoutUndo();
        ((SpriteRenderer)systemData.FindProperty("_pickupRenderer").objectReferenceValue).enabled = false;
        SpriteRenderer oldEffect = systemData.FindProperty("_effectRenderer").objectReferenceValue as SpriteRenderer;
        if (oldEffect != null) oldEffect.enabled = false;

        AudioSource shatterSource = host.GetComponent<AudioSource>();
        if (shatterSource == null) shatterSource = host.gameObject.AddComponent<AudioSource>();
        ConfigureAudio(shatterSource, shatterAudio, false);
        var hostData = new SerializedObject(host);
        hostData.FindProperty("_itemSystem").objectReferenceValue = system;
        hostData.FindProperty("_gameBalance").objectReferenceValue = balance;
        hostData.FindProperty("_shatterAudio").objectReferenceValue = shatterSource;
        hostData.ApplyModifiedPropertiesWithoutUndo();
        host.enabled = true;
        if (Object.FindFirstObjectByType<AudioListener>() == null && Camera.main != null)
            Camera.main.gameObject.AddComponent<AudioListener>();

        Text scoreTxt = FindText("Score Value");
        ScoreText_view view = scoreTxt.GetComponent<ScoreText_view>();
        if (view == null) view = scoreTxt.gameObject.AddComponent<ScoreText_view>();
        var viewData = new SerializedObject(view);
        viewData.FindProperty("_scoreTxt").objectReferenceValue = scoreTxt;
        viewData.FindProperty("_comboTxt").objectReferenceValue = FindText("Combo Count");
        viewData.FindProperty("_comboRemainingTxt").objectReferenceValue = FindText("Combo Remaining");
        viewData.FindProperty("_elapsedTxt").objectReferenceValue = FindText("Elapsed Time");
        Image comboFill = Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(image => image.name == "Combo Fill");
        comboFill.type = Image.Type.Filled;
        comboFill.fillMethod = Image.FillMethod.Horizontal;
        comboFill.fillOrigin = 0;
        viewData.FindProperty("_comboFillImg").objectReferenceValue = comboFill;
        viewData.ApplyModifiedPropertiesWithoutUndo();
        ScorePresenter_presenter presenter = scoreTxt.GetComponent<ScorePresenter_presenter>();
        if (presenter == null) presenter = scoreTxt.gameObject.AddComponent<ScorePresenter_presenter>();
        var presenterData = new SerializedObject(presenter);
        presenterData.FindProperty("_waveHost").objectReferenceValue = host;
        presenterData.FindProperty("_view").objectReferenceValue = view;
        presenterData.ApplyModifiedPropertiesWithoutUndo();
        presenter.enabled = true;
        scoreTxt.text = "0";
        FindText("Combo Count").text = "×0";
        FindText("Combo Remaining").text = "0.0s";
        FindText("Elapsed Time").text = "00:00";
        comboFill.fillAmount = 0f;

        EditorSceneManager.MarkSceneDirty(host.gameObject.scene);
        EditorSceneManager.SaveScene(host.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("THREE_ITEMS_CONFIGURED: Bomb / Freeze / Bubble Shield, scene and HUD connected.");
    }

    private static ItemEffectVisual CreateFramePrefab(string name, Sprite[] frames, Material material, AudioClip clip)
    {
        string path = CONTENT_PATH + "/" + name + ".prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return LoadEffectPrefab(path);
        var rootObj = new GameObject(name);
        rootObj.SetActive(false);
        FrameItemEffectVisual visual = rootObj.AddComponent<FrameItemEffectVisual>();
        SpriteRenderer renderer = Child(rootObj.transform, "Frames").AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = material;
        renderer.sprite = frames[0];
        renderer.sortingOrder = -1;
        var data = new SerializedObject(visual);
        data.FindProperty("_renderer").objectReferenceValue = renderer;
        SetReferences(data.FindProperty("_frames"), frames);
        data.ApplyModifiedPropertiesWithoutUndo();
        ConfigureAudio(rootObj.AddComponent<AudioSource>(), clip, true);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootObj, path);
        Object.DestroyImmediate(rootObj);
        return LoadEffectPrefab(path);
    }

    private static ItemEffectVisual CreateShieldPrefab(Sprite membrane, Sprite[] burstFrames, Material material, AudioClip equip, AudioClip burst)
    {
        string path = CONTENT_PATH + "/BubbleShieldEffect.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return LoadEffectPrefab(path);
        var rootObj = new GameObject("BubbleShieldEffect");
        rootObj.SetActive(false);
        ShieldItemEffectVisual visual = rootObj.AddComponent<ShieldItemEffectVisual>();
        SpriteRenderer membraneRenderer = Child(rootObj.transform, "Membrane").AddComponent<SpriteRenderer>();
        membraneRenderer.sprite = membrane;
        membraneRenderer.sharedMaterial = material;
        membraneRenderer.sortingOrder = 1;
        var rings = new LineRenderer[3];
        for (int i = 0; i < rings.Length; i++)
        {
            LineRenderer ring = Child(rootObj.transform, "Ring " + i).AddComponent<LineRenderer>();
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 96;
            ring.widthMultiplier = i == 0 ? 0.065f : i == 1 ? 0.02f : 0.012f;
            ring.startColor = ring.endColor = new Color(1f, 1f, 1f, i == 0 ? 0.22f : 0.85f);
            ring.sortingOrder = 2;
            float radius = i == 2 ? 0.91f : 1f;
            for (int p = 0; p < 96; p++)
            {
                float angle = p * Mathf.PI * 2f / 96f;
                ring.SetPosition(p, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
            }
            rings[i] = ring;
        }
        SpriteRenderer burstRenderer = Child(rootObj.transform, "Burst").AddComponent<SpriteRenderer>();
        burstRenderer.sharedMaterial = material;
        burstRenderer.sortingOrder = -1;
        burstRenderer.sprite = burstFrames[0];
        AudioSource burstSource = Child(rootObj.transform, "Burst Audio").AddComponent<AudioSource>();
        ConfigureAudio(burstSource, burst, false);
        ConfigureAudio(rootObj.AddComponent<AudioSource>(), equip, true);
        var data = new SerializedObject(visual);
        SetReferences(data.FindProperty("_rings"), rings);
        data.FindProperty("_membraneRenderer").objectReferenceValue = membraneRenderer;
        data.FindProperty("_burstRenderer").objectReferenceValue = burstRenderer;
        data.FindProperty("_burstAudio").objectReferenceValue = burstSource;
        SetReferences(data.FindProperty("_burstFrames"), burstFrames);
        data.ApplyModifiedPropertiesWithoutUndo();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootObj, path);
        Object.DestroyImmediate(rootObj);
        return LoadEffectPrefab(path);
    }

    private static Material CreateGreenMaterial()
    {
        string path = CONTENT_PATH + "/ShieldNeon.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Load<Shader>(ART_PATH + "/ShieldNeon.shader");
        if (material == null)
        {
            material = new Material(shader) { name = "ShieldNeon" };
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetColor("_Tint", new Color(0.18f, 1f, 0.42f, 1f));
        material.SetFloat("_GlowIntensity", 2.4f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Sprite CreateShieldSprite(string name, bool membrane)
    {
        string path = ART_PATH + "/" + name + ".png";
        if (!File.Exists(path) || name == "pickup_shield")
        {
            const int SIZE = 256;
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);
            var pixels = new Color[SIZE * SIZE];
            Vector2[] crest = { new Vector2(0f, 0.22f), new Vector2(0.17f, 0.14f), new Vector2(0.14f, -0.09f),
                new Vector2(0f, -0.24f), new Vector2(-0.14f, -0.09f), new Vector2(-0.17f, 0.14f) };
            for (int y = 0; y < SIZE; y++)
            for (int x = 0; x < SIZE; x++)
            {
                Vector2 point = new Vector2((x + 0.5f) / SIZE - 0.5f, (y + 0.5f) / SIZE - 0.5f);
                float radius = point.magnitude;
                Color color;
                if (membrane)
                {
                    float alpha = radius < 0.48f ? 0.035f + Mathf.Pow(radius / 0.48f, 5f) * 0.15f : 0f;
                    color = new Color(0.45f, 1f, 0.7f, alpha);
                }
                else
                {
                    float glow = Mathf.Exp(-Mathf.Pow((radius - 0.375f) / 0.055f, 2f));
                    float ring = 1f - SmoothThreshold(0.006f, 0.015f, Mathf.Abs(radius - 0.36f));
                    color = radius < 0.35f ? new Color(0.025f, 0.13f, 0.065f, 1f) : new Color(0.13f, 1f, 0.4f, glow * 0.65f);
                    color = Color.Lerp(color, new Color(0.6f, 1f, 0.7f, 1f), ring);
                    float distance = float.PositiveInfinity;
                    for (int p = 0; p < crest.Length; p++)
                        distance = Mathf.Min(distance, Mathf.Sqrt(ItemGeometry.DistanceToSegmentSquared(point, crest[p], crest[(p + 1) % crest.Length])));
                    float check = Mathf.Min(Mathf.Sqrt(ItemGeometry.DistanceToSegmentSquared(point, new Vector2(-0.085f, 0f), new Vector2(-0.02f, -0.065f))),
                        Mathf.Sqrt(ItemGeometry.DistanceToSegmentSquared(point, new Vector2(-0.02f, -0.065f), new Vector2(0.095f, 0.075f))));
                    float glyph = 1f - SmoothThreshold(0.009f, 0.017f, Mathf.Min(distance, check));
                    color = Color.Lerp(color, new Color(0.86f, 1f, 0.9f, 1f), glyph);
                }
                pixels[y * SIZE + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 256f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return LoadSprite(path);
    }

    private static AudioClip CreateSound(string name, int kind)
    {
        string path = AUDIO_PATH + "/" + name + ".wav";
        if (!File.Exists(path))
        {
            const int RATE = 22050;
            float duration = kind == 0 || kind == 3 ? 0.32f : 0.22f;
            int count = Mathf.CeilToInt(RATE * duration);
            var random = new System.Random(901 + kind);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(RATE); writer.Write(RATE * 2);
                writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / RATE;
                    float progress = t / duration;
                    float envelope = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t / 0.006f) * 0.5f) * Mathf.Pow(1f - progress, 2f);
                    float noise = (float)random.NextDouble() * 2f - 1f;
                    float tone = kind == 0 || kind == 3
                        ? Mathf.Sin(2f * Mathf.PI * (90f * t - 60f * t * t)) * 0.7f + noise * 0.3f
                        : Mathf.Sin(2f * Mathf.PI * (kind == 1 ? 880f : kind == 2 ? 660f : 1900f) * t) * 0.6f
                            + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.15f + noise * (kind == 4 ? 0.4f : 0.08f);
                    writer.Write((short)(Mathf.Clamp(tone * envelope * 0.6f, -1f, 1f) * short.MaxValue));
                }
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return Load<AudioClip>(path);
    }

    private static void SetItem(SerializedProperty item, ITEM_TYPE type, float radius, float duration, float visualDuration, AItemEffectDefinition definition)
    {
        item.FindPropertyRelative("_type").intValue = (int)type;
        item.FindPropertyRelative("_weight").intValue = 1;
        item.FindPropertyRelative("_radius").floatValue = radius;
        item.FindPropertyRelative("_durationSeconds").floatValue = duration;
        item.FindPropertyRelative("_visualDurationSeconds").floatValue = visualDuration;
        item.FindPropertyRelative("_effectDefinition").objectReferenceValue = definition;
    }

    private static void SetVisual(SerializedProperty visual, ITEM_TYPE type, Sprite icon, ItemEffectVisual prefab)
    {
        if (prefab == null || !EditorUtility.IsPersistent(prefab))
            throw new InvalidOperationException($"{type} 효과 프리팹 참조를 저장할 수 없습니다.");
        visual.FindPropertyRelative("_type").intValue = (int)type;
        visual.FindPropertyRelative("_pickupSprite").objectReferenceValue = icon;
        visual.FindPropertyRelative("_effectPrefab").objectReferenceValue = prefab;
    }

    private static float SmoothThreshold(float minimum, float maximum, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(minimum, maximum, value));
    }

    private static ItemEffectVisual LoadEffectPrefab(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return Load<GameObject>(path).GetComponent<ItemEffectVisual>()
            ?? throw new InvalidOperationException(path + "의 효과 컴포넌트를 찾지 못했습니다.");
    }

    private static void SetReferences(SerializedProperty property, Object[] references)
    {
        property.arraySize = references.Length;
        for (int i = 0; i < references.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = references[i];
    }

    private static Sprite[] LoadFrames(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().First();
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException(path);
    private static Text FindText(string name) => Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(text => text.name == name);
    private static GameObject Child(Transform parent, string name)
    {
        var childObj = new GameObject(name);
        childObj.transform.SetParent(parent, false);
        return childObj;
    }

    private static void ConfigureAudio(AudioSource source, AudioClip clip, bool playOnAwake)
    {
        source.clip = clip;
        source.playOnAwake = playOnAwake;
        source.spatialBlend = 0f;
        source.volume = 0.3f;
    }
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
