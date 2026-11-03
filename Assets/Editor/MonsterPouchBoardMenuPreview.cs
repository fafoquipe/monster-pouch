using System;
using System.Collections;
using System.IO;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MonsterPouchBoardMenuPreview
{
    const string Folder = "Logs/local-validation/board-navigation";

    public static string Capture()
    {
        Directory.CreateDirectory(Folder);
        Render(1080, false, false, "navigation-1080");
        Render(360, false, false, "navigation-360");
        Render(1080, true, false, "navigation-states");
        Render(540, false, true, "navigation-es");
        return "Rendered all five selected states and EN/ES captions at desktop/mobile sizes: " + Path.GetFullPath(Folder);
    }

    static void Render(int width, bool allStates, bool spanish, string filename)
    {
        int rows = allStates ? 5 : 1;
        float height = rows * 124;
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Board navigation preview", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(root, scene);
        var target = new RenderTexture(width, Mathf.RoundToInt(height * width / 540), 24, RenderTextureFormat.ARGB32);
        var output = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active; Camera camera = null;
        try
        {
            var rt = root.GetComponent<RectTransform>(); rt.pivot = Vector2.zero; rt.sizeDelta = new Vector2(540, height);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            root.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = width / 540f;
            var cameraObject = new GameObject("Board menu camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera = cameraObject.AddComponent<Camera>(); camera.scene = scene;
            camera.orthographic = true; camera.orthographicSize = height / 2;
            camera.transform.position = new Vector3(270, height / 2, -100);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = BoardMenuStyle.Ink;
            camera.allowHDR = camera.allowMSAA = false; camera.targetTexture = target; canvas.worldCamera = camera;
            string[] labels = spanish ? new[] { "TIENDA", "BRIEF", "BATALLA", "GUÍA", "DESAFÍOS" }
                : new[] { "SHOP", "BRIEF", "BATTLE", "GUIDE", "CHALLENGES" };
            for (int row = 0; row < rows; row++)
                for (int i = 0; i < 5; i++)
                    BoardMenuStyle.Tab(rt, new Rect(3 + i * 108, 5 + row * 124, 102, 110),
                        (BoardMenuSymbol)i, labels[i], i == (allStates ? row : 2), () => { });
            Canvas.ForceUpdateCanvases();
            foreach (var icon in root.GetComponentsInChildren<Image>())
                if (icon.name=="Board symbol" && icon.sprite==null)
                    throw new InvalidOperationException("Missing menu illustration: " + icon.transform.parent.name);
            foreach (var text in root.GetComponentsInChildren<Text>())
                if (text.font == null || text.preferredWidth > text.rectTransform.rect.width)
                    throw new InvalidOperationException("Navigation caption does not fit: " + text.text);
            camera.Render(); RenderTexture.active = target;
            output.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); output.Apply();
            File.WriteAllBytes(Folder + "/" + filename + ".png", output.EncodeToPNG());
        }
        finally
        {
            if (camera != null) camera.targetTexture = null;
            RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(output);
            Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    public static string CaptureLive()
    {
        var ui = Object.FindFirstObjectByType<LocalGameUI>();
        if (!Application.isPlaying || ui == null || ui.Match.Phase != MatchPhase.Menu) return "No live lobby to capture.";
        ui.RenderPage(); ui.StartCoroutine(LiveFrame()); return "Live menu capture queued.";
    }

    static IEnumerator LiveFrame()
    {
        yield return new WaitForSecondsRealtime(.3f); yield return new WaitForEndOfFrame();
        Directory.CreateDirectory(Folder);
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder + "/navigation-in-game.png"));
    }
}
