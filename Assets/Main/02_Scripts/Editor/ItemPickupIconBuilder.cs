using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ItemPickupIconBuilder
{
    public const string SHIELD_PATH = "Assets/Main/04_Resource/Items/pickup_shield.png";
    public const string MISSILE_PATH = "Assets/Main/04_Resource/Items/pickup_interceptor_missile.png";

    [MenuItem("RedDot Dodge/Update Pickup Icons")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Main/01_Scene/MainPlay.unity")
            throw new InvalidOperationException("MainPlay를 열고 Play를 종료한 뒤 실행하세요.");
        ItemSystem system = UnityEngine.Object.FindFirstObjectByType<ItemSystem>();
        if (system == null)
            throw new InvalidOperationException("MainPlay의 ItemSystem을 찾지 못했습니다.");
        Sprite shield = ImportSprite(SHIELD_PATH);
        Sprite missile = ImportSprite(MISSILE_PATH);
        var data = new SerializedObject(system);
        SerializedProperty visuals = data.FindProperty("_itemVisuals");
        for (int i = 0; i < visuals.arraySize; i++)
        {
            SerializedProperty visual = visuals.GetArrayElementAtIndex(i);
            ITEM_TYPE type = (ITEM_TYPE)visual.FindPropertyRelative("_type").intValue;
            if (type == ITEM_TYPE.SHIELD || type == ITEM_TYPE.HOMING_MISSILE)
                visual.FindPropertyRelative("_pickupSprite").objectReferenceValue = type == ITEM_TYPE.SHIELD ? shield : missile;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(system.gameObject.scene);
        EditorSceneManager.SaveScene(system.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("PICKUP_ICONS_UPDATED: shield and interceptor missile pickup images connected.");
    }

    public static Sprite ImportSprite(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("아이템 이미지를 찾지 못했습니다.", path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException("Sprite로 불러오지 못했습니다: " + path);
    }
}
