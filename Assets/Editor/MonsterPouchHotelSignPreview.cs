using System;
using System.IO;
using MonsterPouch.Local;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Isolated visual check: does not change the live scene, theme, deck or Game view.
public static class MonsterPouchHotelSignPreview
{
    public static string Capture()
    {
        const string folder = "Logs/local-validation/hotel-sign";
        Directory.CreateDirectory(folder);
        foreach (int scale in new[] { 1, 2 }) CaptureAtScale(folder, scale);
        return "Rendered hotel sign at 540 and 1080 pixels. " + Path.GetFullPath(folder);
    }

    static void CaptureAtScale(string folder, int scale)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Hotel sign preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(root, scene);
        var target = new RenderTexture(540 * scale, 180 * scale, 24, RenderTextureFormat.ARGB32);
        var result = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        Camera camera = null;
        try
        {
            var rt = root.GetComponent<RectTransform>();
            rt.pivot = Vector2.zero; rt.sizeDelta = new Vector2(540, 180);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = scale;
            var cameraObject = new GameObject("Hotel preview camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.AddComponent<Camera>(); camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = 90;
            camera.transform.position = new Vector3(270, 90, -100);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(78, 59, 52, 255);
            camera.allowHDR = camera.allowMSAA = false; camera.targetTexture = target;
            canvas.worldCamera = camera;
            var sign = new GameObject("Cabinet crest", typeof(RectTransform), typeof(CanvasRenderer), typeof(LobbyCabinetCrest));
            var signRect = sign.GetComponent<RectTransform>(); signRect.SetParent(rt, false);
            LocalGameUI.SetRect(signRect, new Rect(68, 26, 404, 128));
            sign.GetComponent<LobbyCabinetCrest>().AddLettering();
            foreach (var label in sign.GetComponentsInChildren<Text>())
                if (label.font == null) throw new InvalidOperationException("Missing hotel sign font: " + label.name);
            Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = target;
            result.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); result.Apply();
            File.WriteAllBytes(folder + "/hotel-sign-" + target.width + ".png", result.EncodeToPNG());
        }
        finally
        {
            if (camera != null) camera.targetTexture = null;
            RenderTexture.active = previous; target.Release();
            Object.DestroyImmediate(target); Object.DestroyImmediate(result);
            Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
