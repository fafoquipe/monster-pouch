using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// Test-only GameView preparation. ScreenCapture needs an actual rendering view in Editor batch runs.
internal sealed class InteractionCaptureGameView
{
    const int FrameLimit = 120;
    Vector2Int originalPixels;
    bool savedDimensions, restoreRequested;
#if UNITY_EDITOR
    object gameView;
    PropertyInfo selectedSize;
    MethodInfo repaint;
    int originalSizeIndex;
    bool savedSelection, originalFixedResolution;
#else
    FullScreenMode originalMode;
#endif

    public IEnumerator Open()
    {
        Assert.IsFalse(savedDimensions, "Use one capture view scope per test.");
#if UNITY_EDITOR
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        Type viewType = assemblies.Select(a => a.GetType("UnityEditor.GameView")).FirstOrDefault(t => t != null);
        Type windowType = assemblies.Select(a => a.GetType("UnityEditor.EditorWindow")).FirstOrDefault(t => t != null);
        Type previewType = assemblies.Select(a => a.GetType("MonsterPouchLayoutPreview")).FirstOrDefault(t => t != null);
        Assert.IsNotNull(viewType); Assert.IsNotNull(windowType); Assert.IsNotNull(previewType);
        gameView = windowType.GetMethod("GetWindow", new[] { typeof(Type) }).Invoke(null, new object[] { viewType });
        selectedSize = viewType.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        repaint = viewType.GetMethod("Repaint", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(selectedSize); Assert.IsNotNull(repaint);
        originalSizeIndex = (int)selectedSize.GetValue(gameView);
        savedSelection = true;
        var currentSize = viewType.GetProperty("currentGameViewSize", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(currentSize);
        object size = currentSize.GetValue(gameView);
        Assert.IsNotNull(size);
        var sizeType = size.GetType().GetProperty("sizeType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(sizeType);
        originalFixedResolution = string.Equals(sizeType.GetValue(size).ToString(), "FixedResolution", StringComparison.Ordinal);
        // In batch mode Screen may still describe a residual view until GetWindow has rendered.
        repaint.Invoke(gameView, null);
        yield return null; yield return null;
        originalPixels = new Vector2Int(Screen.width, Screen.height);
        savedDimensions = true;
        var resize = previewType.GetMethod("Resize", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(resize);
        resize.Invoke(null, new object[] { "540x960" });
#else
        originalPixels = new Vector2Int(Screen.width, Screen.height);
        originalMode = Screen.fullScreenMode;
        savedDimensions = true;
        Screen.SetResolution(540, 960, FullScreenMode.Windowed);
#endif
        for (int frame = 0; frame < FrameLimit && (Screen.width != 540 || Screen.height != 960); frame++) yield return null;
        Assert.AreEqual(new Vector2Int(540, 960), new Vector2Int(Screen.width, Screen.height),
            "Render interaction captures in the actual 540x960 GameView before loading the scene.");
        yield return null; yield return null;
    }

    // Synchronous so every fixture can restore the selection from finally even after a failed assertion.
    public void RestoreSelection()
    {
        if (restoreRequested) return;
        restoreRequested = true;
#if UNITY_EDITOR
        if (!savedSelection) return;
        selectedSize.SetValue(gameView, originalSizeIndex);
        repaint?.Invoke(gameView, null);
#else
        if (savedDimensions) Screen.SetResolution(originalPixels.x, originalPixels.y, originalMode);
#endif
    }

    public IEnumerator WaitForRestoration()
    {
        Assert.IsTrue(restoreRequested, "Restore the selection before waiting for its rendered size.");
#if UNITY_EDITOR
        if (savedSelection)
            Assert.AreEqual(originalSizeIndex, (int)selectedSize.GetValue(gameView), "Restore the previously selected GameView size.");
#endif
        if (!savedDimensions) yield break;
#if UNITY_EDITOR
        // Free/aspect-ratio presets derive their pixels from the current window.
        // A resize during PlayMode does not change the selected preset, and its
        // old pixel dimensions are not a valid restoration requirement.
        if (!originalFixedResolution)
        {
            yield return null; yield return null;
            Assert.AreEqual(originalSizeIndex, (int)selectedSize.GetValue(gameView), "Keep the restored free/aspect-ratio GameView selection.");
            yield break;
        }
#endif
        for (int frame = 0; frame < FrameLimit && (Screen.width != originalPixels.x || Screen.height != originalPixels.y); frame++) yield return null;
#if UNITY_EDITOR
        Assert.AreEqual(originalSizeIndex, (int)selectedSize.GetValue(gameView), "Restore the previously selected GameView size.");
#else
        Assert.AreEqual(originalMode, Screen.fullScreenMode);
#endif
        Assert.AreEqual(originalPixels, new Vector2Int(Screen.width, Screen.height), "Restore the previous GameView render dimensions.");
    }
}
