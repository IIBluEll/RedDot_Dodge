using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PlayerTrailRenderChecks
{
    private const string PENDING = "RedDot.PlayerTrail.RenderCheck";
    private static PlayerMovement s_player;
    private static PlayerTrail s_trail;
    private static float s_elapsed;
    private static int s_frame;

    [MenuItem("RedDot Dodge/Check Player Trail Rendering")]
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Play를 종료한 뒤 실행하세요.");
        SessionState.SetBool(PENDING, true);
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
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PENDING, false))
        {
            s_elapsed = 0f;
            s_frame = -1;
            s_player = null;
            s_trail = null;
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(PENDING, false);
        }
    }

    private static void Tick()
    {
        if (Time.frameCount < 2 || s_frame == Time.frameCount)
            return;
        s_frame = Time.frameCount;
        try
        {
            if (s_player == null)
            {
                s_player = Object.FindFirstObjectByType<PlayerMovement>();
                Object.FindFirstObjectByType<EdgeChaseWaveHost>().enabled = false;
                s_player.enabled = false;
                s_trail = s_player.GetComponent<PlayerTrail>();
                s_player.transform.position = new Vector3(-3f, 0f, 0f);
                s_trail.ClearTrail();
            }
            float deltaTime = Mathf.Min(Time.deltaTime, 0.05f);
            Vector3 previous = s_player.transform.position;
            s_elapsed += deltaTime;
            Vector3 current = new Vector3(-3f + s_elapsed * 6f, Mathf.Sin(s_elapsed * 4f) * 0.6f, 0f);
            s_player.transform.SetPositionAndRotation(current, Quaternion.Euler(0f, 0f, Mathf.Atan2(current.y - previous.y, current.x - previous.x) * Mathf.Rad2Deg - 90f));
            s_trail.RecordMovement(previous, current, 6f, deltaTime);
            if (s_elapsed < 0.8f)
                return;

            EditorApplication.update -= Tick;
            EditorApplication.isPaused = true;
            Capture();
        }
        catch (Exception exception)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(PENDING, false);
            Debug.LogException(exception);
        }
    }

    private static void Capture(string name = "PlayerTrail")
    {
        string folder = Path.GetFullPath(Application.dataPath + "/../ArtExports/PlayerTrailQA");
        Directory.CreateDirectory(folder);
        TrailRenderer renderer = s_trail.GetComponentInChildren<TrailRenderer>();
        var mesh = new Mesh();
        renderer.BakeMesh(mesh, Camera.main, true);
        Texture2D withTrail = Render(Camera.main);
        renderer.enabled = false;
        Texture2D withoutTrail = Render(Camera.main);
        renderer.enabled = true;
        Color32[] first = withTrail.GetPixels32();
        Color32[] second = withoutTrail.GetPixels32();
        int changed = 0;
        for (int i = 0; i < first.Length; i++)
            if (Math.Abs(first[i].r - second[i].r) + Math.Abs(first[i].g - second[i].g) + Math.Abs(first[i].b - second[i].b) > 12)
                changed++;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        string report = $"positions={renderer.positionCount}, vertices={mesh.vertexCount}, changedPixels={changed}, width={renderer.widthMultiplier}, lifetime={renderer.time}, intensity={block.GetFloat("_Intensity")}, pipeline={UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline}, shader={renderer.sharedMaterial.shader.name}, supported={renderer.sharedMaterial.shader.isSupported}, bounds={renderer.bounds}, meshBounds={mesh.bounds}";
        if (mesh.vertexCount > 0)
            report += $", uv0={mesh.uv[0]}, color0={mesh.colors32[0]}, vertex0={mesh.vertices[0]}";
        File.WriteAllText(folder + "/" + name + "-check.txt", report);
        File.WriteAllBytes(folder + "/" + name + ".png", withTrail.EncodeToPNG());
        File.WriteAllBytes(folder + "/WithoutTrail.png", withoutTrail.EncodeToPNG());
        Object.Destroy(mesh);
        Object.Destroy(withTrail);
        Object.Destroy(withoutTrail);
        SessionState.SetBool(PENDING, false);
        Debug.Log("PLAYER_TRAIL_RENDER: " + report);
        if (changed < 50)
            throw new InvalidOperationException("꼬리 메시가 실제 화면에 렌더링되지 않았습니다. " + report);
    }

    private static Texture2D Render(Camera camera)
    {
        RenderTexture oldTarget = camera.targetTexture;
        RenderTexture oldActive = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texture.Apply();
            return texture;
        }
        finally
        {
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            RenderTexture.ReleaseTemporary(target);
        }
    }
}
