using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MissileContentBuilder
{
    private const string CONTENT_PATH = "Assets/Main/06_Content/Items";
    private const string ART_PATH = "Assets/Main/04_Resource/Items/Missile";
    private const string AUDIO_PATH = "Assets/Main/04_Resource/Audio/Items";

    [MenuItem("RedDot Dodge/Add Homing Missiles")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Main/01_Scene/MainPlay.unity")
            throw new InvalidOperationException("MainPlay를 열고 Play를 종료한 뒤 실행하세요.");
        Directory.CreateDirectory(ART_PATH);
        Directory.CreateDirectory(CONTENT_PATH);
        Directory.CreateDirectory(AUDIO_PATH);
        AssetDatabase.Refresh();
        Sprite icon = CreateSprite("pickup_missile", true);
        Sprite missile = CreateSprite("missile_body", false);
        Material bodyMaterial = CreateMaterial("MissileNeon", "Assets/Main/04_Resource/Items/Shield/ShieldNeon.shader", true);
        Material trailMaterial = CreateMaterial("MissileTrailNeon", "Assets/Main/04_Resource/VFX/PlayerTrailNeon.shader", false);
        AudioClip launch = CreateAudio("MissileLaunch", false);
        AudioClip impact = CreateAudio("MissileImpact", true);
        HomingMissileDefinition definition = AssetDatabase.LoadAssetAtPath<HomingMissileDefinition>(CONTENT_PATH + "/HomingMissiles.asset");
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<HomingMissileDefinition>();
            AssetDatabase.CreateAsset(definition, CONTENT_PATH + "/HomingMissiles.asset");
        }
        HomingMissileVisual prefab = CreatePrefab(missile, bodyMaterial, trailMaterial, launch, impact);
        GameBalance balance = Load<GameBalance>("Assets/Main/04_Resource/Balance/GameBalance.asset");
        var balanceData = new SerializedObject(balance);
        SerializedProperty items = balanceData.FindProperty("_items");
        SerializedProperty item = FindOrAppend(items);
        item.FindPropertyRelative("_type").intValue = (int)ITEM_TYPE.HOMING_MISSILE;
        item.FindPropertyRelative("_effectDefinition").objectReferenceValue = definition;
        item.FindPropertyRelative("_radius").floatValue = 0f;
        item.FindPropertyRelative("_durationSeconds").floatValue = 0f;
        item.FindPropertyRelative("_visualDurationSeconds").floatValue = 0f;
        balanceData.ApplyModifiedPropertiesWithoutUndo();
        balance.Validate();
        EditorUtility.SetDirty(balance);

        ItemSystem system = Object.FindFirstObjectByType<ItemSystem>();
        if (system == null)
            throw new InvalidOperationException("MainPlay의 ItemSystem을 찾지 못했습니다.");
        var systemData = new SerializedObject(system);
        SerializedProperty visual = FindOrAppend(systemData.FindProperty("_itemVisuals"));
        visual.FindPropertyRelative("_type").intValue = (int)ITEM_TYPE.HOMING_MISSILE;
        visual.FindPropertyRelative("_pickupSprite").objectReferenceValue = icon;
        visual.FindPropertyRelative("_effectPrefab").objectReferenceValue = prefab;
        visual.FindPropertyRelative("_effectSprites").arraySize = 0;
        visual.FindPropertyRelative("_effectMaterial").objectReferenceValue = null;
        systemData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(system.gameObject.scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(system.gameObject.scene);
        Debug.Log("HOMING_MISSILES_CONFIGURED: five missiles, pickup, prefab, audio and MainPlay connected. Existing tuning preserved.");
    }

    private static SerializedProperty FindOrAppend(SerializedProperty array)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("_type").intValue == (int)ITEM_TYPE.HOMING_MISSILE)
                return entry;
        }
        int index = array.arraySize++;
        SerializedProperty appended = array.GetArrayElementAtIndex(index);
        SerializedProperty weight = appended.FindPropertyRelative("_weight");
        if (weight != null) weight.intValue = 1;
        return appended;
    }

    private static HomingMissileVisual CreatePrefab(Sprite missile, Material body, Material trail, AudioClip launch, AudioClip impact)
    {
        string path = CONTENT_PATH + "/HomingMissileEffect.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing.GetComponent<HomingMissileVisual>();
        var rootObj = new GameObject("HomingMissileEffect");
        rootObj.SetActive(false);
        AudioSource audio = rootObj.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;
        audio.volume = 0.6f;
        HomingMissileVisual visual = rootObj.AddComponent<HomingMissileVisual>();
        var data = new SerializedObject(visual);
        data.FindProperty("_missileSprite").objectReferenceValue = missile;
        data.FindProperty("_bodyMaterial").objectReferenceValue = body;
        data.FindProperty("_trailMaterial").objectReferenceValue = trail;
        data.FindProperty("_audio").objectReferenceValue = audio;
        data.FindProperty("_launchClip").objectReferenceValue = launch;
        data.FindProperty("_impactClip").objectReferenceValue = impact;
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath("Assets/Main/04_Resource/VFX/BombBlastLayered_Sheet.png").OfType<Sprite>().OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
        SerializedProperty frameData = data.FindProperty("_explosionFrames");
        frameData.arraySize = frames.Length;
        for (int i = 0; i < frames.Length; i++) frameData.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(rootObj, path);
        Object.DestroyImmediate(rootObj);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return Load<GameObject>(path).GetComponent<HomingMissileVisual>();
    }

    private static Material CreateMaterial(string name, string shaderPath, bool tinted)
    {
        string path = CONTENT_PATH + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Load<Shader>(shaderPath)) { name = name };
        if (tinted)
        {
            material.SetColor("_Tint", new Color(1f, 0.82f, 0.16f, 1f));
            material.SetFloat("_GlowIntensity", 1.8f);
        }
        else material.SetFloat("_Intensity", 2.6f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Sprite CreateSprite(string name, bool icon)
    {
        string path = ART_PATH + "/" + name + ".png";
        if (!File.Exists(path))
        {
            int width = icon ? 256 : 64;
            int height = icon ? 256 : 128;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            Vector2[] rocket = { new Vector2(0f, 0.47f), new Vector2(0.13f, 0.15f), new Vector2(0.13f, -0.2f), new Vector2(0.3f, -0.43f),
                new Vector2(0.1f, -0.34f), new Vector2(0f, -0.47f), new Vector2(-0.1f, -0.34f), new Vector2(-0.3f, -0.43f), new Vector2(-0.13f, -0.2f), new Vector2(-0.13f, 0.15f) };
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector2 point = new Vector2((x + 0.5f) / width - 0.5f, (y + 0.5f) / height - 0.5f);
                Vector2 shapePoint = icon ? point * 1.9f : point;
                bool inside = false;
                float edgeDistance = float.PositiveInfinity;
                for (int i = 0, j = rocket.Length - 1; i < rocket.Length; j = i++)
                {
                    Vector2 first = rocket[i]; Vector2 second = rocket[j];
                    if ((first.y > shapePoint.y) != (second.y > shapePoint.y)
                        && shapePoint.x < (second.x - first.x) * (shapePoint.y - first.y) / (second.y - first.y) + first.x)
                        inside = !inside;
                    edgeDistance = Mathf.Min(edgeDistance, Mathf.Sqrt(ItemGeometry.DistanceToSegmentSquared(shapePoint, first, second)));
                }
                Color color = new Color(1f, 1f, 1f, inside ? 1f : Mathf.Exp(-edgeDistance * edgeDistance * 2400f) * 0.45f);
                if (icon)
                {
                    float radius = point.magnitude;
                    float ring = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.004f, 0.014f, Mathf.Abs(radius - 0.36f)));
                    float glow = Mathf.Exp(-Mathf.Pow((radius - 0.36f) / 0.045f, 2f));
                    Color background = radius < 0.35f ? new Color(0.17f, 0.11f, 0.015f, 1f) : new Color(1f, 0.76f, 0.1f, glow * 0.7f);
                    background = Color.Lerp(background, new Color(1f, 0.93f, 0.4f), ring);
                    color = Color.Lerp(background, Color.white, color.a);
                }
                pixels[y * width + x] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return Load<Sprite>(path);
    }

    private static AudioClip CreateAudio(string name, bool impact)
    {
        string path = AUDIO_PATH + "/" + name + ".wav";
        if (!File.Exists(path))
        {
            const int RATE = 22050;
            int count = RATE / 5;
            var random = new System.Random(impact ? 162 : 139);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(RATE); writer.Write(RATE * 2);
                writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
                for (int i = 0; i < count; i++)
                {
                    float progress = (float)i / count;
                    float t = (float)i / RATE;
                    float noise = (float)random.NextDouble() * 2f - 1f;
                    float tone = Mathf.Sin(2f * Mathf.PI * (impact ? 110f * t - 180f * t * t : 1000f * t - 1800f * t * t));
                    float sample = (tone * (impact ? 0.6f : 0.3f) + noise * (impact ? 0.4f : 0.7f)) * Mathf.Pow(1f - progress, 2f) * Mathf.Min(1f, t / 0.005f);
                    writer.Write((short)(sample * short.MaxValue * 0.65f));
                }
            }
        }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return Load<AudioClip>(path);
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException(path);
}
