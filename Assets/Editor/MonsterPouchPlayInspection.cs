using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class MonsterPouchPlayInspection
{
    public static string Snapshot()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(ui==null||ui.Match==null)return "No active local game.";
        var m=ui.Match;
        return "phase="+m.Phase+"; round="+m.Round+"; score="+m.PlayerWins+":"+m.BotWins+"; paused="+m.Paused+"; remaining="+m.Remaining+"; selected="+ui.SelectedId+"\n"+
            "coins="+m.Player?.Coins+"; rerolls="+m.Player?.Rerolls+"; result="+m.Result+"\n"+
            string.Join("\n",m.Actors.Select(p=>p.Value.UnitId+" "+p.Key.Location+" cell="+p.Value.CurrentCell?.Coordinates+" hp="+p.Value.CurrentHealth+"/"+p.Value.BaseStats.MaxHealth+" tricks="+p.Key.TrickCount));
    }

    // Diagnostic only: never forces a Canvas rebuild, changes selection or sends input.
    // In particular, preserve depth=-1 so an unfinished Canvas build remains observable.
    public static string HitTargets()
    {
        var ui = Object.FindFirstObjectByType<LocalGameUI>();
        if (ui == null || ui.Match == null) return "No active local game.";
        var match = ui.Match;
        var output = new StringBuilder();
        output.AppendLine("playing=" + Application.isPlaying + "; editorPaused=" + EditorApplication.isPaused +
            "; frame=" + Time.frameCount + "; focused=" + Application.isFocused +
            "; phase=" + match.Phase + "; matchPaused=" + match.Paused);
        var mouse = Mouse.current;
        output.AppendLine(mouse == null ? "Mouse.current=none" :
            "Mouse.current name=" + mouse.name + "; deviceId=" + mouse.deviceId +
            "; position=" + mouse.position.ReadValue() + "; pressed=" + mouse.leftButton.isPressed +
            "; downThisFrame=" + mouse.leftButton.wasPressedThisFrame +
            "; upThisFrame=" + mouse.leftButton.wasReleasedThisFrame);
        var touch = Touchscreen.current;
        output.AppendLine(touch == null ? "Touchscreen.current=none" :
            "Touchscreen.current name=" + touch.name + "; deviceId=" + touch.deviceId +
            "; position=" + touch.primaryTouch.position.ReadValue() +
            "; pressed=" + touch.primaryTouch.press.isPressed +
            "; downThisFrame=" + touch.primaryTouch.press.wasPressedThisFrame +
            "; upThisFrame=" + touch.primaryTouch.press.wasReleasedThisFrame);
        output.AppendLine("InputSystem.devices:");
        foreach (var device in InputSystem.devices)
            output.AppendLine("  id=" + device.deviceId + "; name=" + device.name +
                "; layout=" + device.layout + "; enabled=" + device.enabled);
        output.AppendLine("Player.Owned:");
        if (match.Player == null) output.AppendLine("  (no player)");
        else foreach (var pair in match.Player.Owned)
            output.AppendLine("  " + pair.Key + " location=" + pair.Value.Location +
                " deployment=" + pair.Value.Deployment + " copies=" + pair.Value.Copies);

        const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var gesture = typeof(LocalGameUI).GetField("boardGesture", privateInstance)?.GetValue(ui) as PointerGesture;
        var pressedUnit = typeof(LocalGameUI).GetField("pressedUnit", privateInstance)?.GetValue(ui) as OwnedUnit;
        object lastPointer = typeof(LocalGameUI).GetField("lastPointer", privateInstance)?.GetValue(ui);
        object pressedEnemy = typeof(LocalGameUI).GetField("pressedEnemy", privateInstance)?.GetValue(ui);
        output.AppendLine(gesture == null ? "boardGesture=none" :
            "boardGesture Active=" + gesture.Active + "; Held=" + gesture.Held + "; Dragging=" + gesture.Dragging);
        output.AppendLine("pressedUnit=" + (pressedUnit != null ? pressedUnit.Definition.Id : "none") +
            "; pressedEnemy=" + pressedEnemy + "; lastPointer=" + lastPointer);
        Camera gameCamera = ui.GameCamera;
        output.AppendLine("Actors; GameCamera=" + (gameCamera != null ? HierarchyPath(gameCamera.transform) : "none"));
        foreach (var pair in match.Actors)
        {
            var actor = pair.Value;
            if (actor == null) continue;
            Transform worldTransform = actor.transform;
            output.AppendLine("  " + actor.UnitId + "; side=" + actor.Side + "; location=" + pair.Key.Location +
                "; world=" + worldTransform.position + "; rotation=" + worldTransform.eulerAngles +
                "; scale=" + worldTransform.lossyScale + "; hitScreen=" +
                (gameCamera != null ? gameCamera.WorldToScreenPoint(worldTransform.position + Vector3.up * .45f).ToString() : "none"));
            var presentation = actor.GetComponent<UnitPresentation>();
            var visualRenderer = presentation != null ? presentation.Renderer : null;
            if (visualRenderer != null)
                output.AppendLine("    visualRenderer=" + HierarchyPath(visualRenderer.transform) +
                    "; enabled=" + visualRenderer.enabled + "; bounds=" + visualRenderer.bounds +
                    "; boundsCenterScreen=" + (gameCamera != null ?
                        gameCamera.WorldToScreenPoint(visualRenderer.bounds.center).ToString() : "none"));
            else output.AppendLine("    visualRenderer=none");
        }

        var graphics = ui.GetComponentsInChildren<Graphic>(true)
            .Where(graphic => graphic != null && graphic.isActiveAndEnabled &&
                graphic.gameObject.activeInHierarchy && IsBriefOrBench(graphic.transform))
            .OrderBy(graphic => HierarchyPath(graphic.transform), System.StringComparer.Ordinal)
            .ToArray();
        var eventSystem = EventSystem.current;
        output.AppendLine("Active target Graphics=" + graphics.Length + "; EventSystem=" +
            (eventSystem != null ? HierarchyPath(eventSystem.transform) : "none"));
        var inputModule = eventSystem != null ? eventSystem.currentInputModule : null;
        output.AppendLine(inputModule == null ? "currentInputModule=none" :
            "currentInputModule=" + inputModule.GetType().FullName + "; enabled=" + inputModule.enabled +
            "; activeAndEnabled=" + inputModule.isActiveAndEnabled);
        if (inputModule is InputSystemUIInputModule inputSystemModule)
        {
            output.AppendLine("  point: " + InputActionStatus(inputSystemModule.point));
            output.AppendLine("  leftClick: " + InputActionStatus(inputSystemModule.leftClick));
        }
        foreach (var graphic in graphics)
        {
            RectTransform rect = graphic.rectTransform;
            Canvas canvas = graphic.canvas;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            output.AppendLine("GRAPHIC " + HierarchyPath(graphic.transform) + " type=" + graphic.GetType().Name +
                " depth=" + graphic.depth + " cull=" + graphic.canvasRenderer.cull +
                " raycast=" + graphic.raycastTarget + " rect=" + rect.rect + " centerScreen=" + center);
            output.AppendLine("  " + Handlers(graphic.gameObject));
            if (eventSystem == null) continue;
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = center }, hits);
            output.AppendLine("  RaycastAll count=" + hits.Count);
            for (int index = 0; index < hits.Count; index++)
            {
                RaycastResult hit = hits[index];
                output.AppendLine("    [" + index + "] " + HierarchyPath(hit.gameObject.transform) +
                    " depth=" + hit.depth + " sortingOrder=" + hit.sortingOrder +
                    " module=" + (hit.module != null ? hit.module.GetType().Name : "none"));
                output.AppendLine("      " + Handlers(hit.gameObject));
            }
        }
        return output.ToString();
    }

    static string InputActionStatus(InputActionReference reference)
    {
        InputAction action = reference != null ? reference.action : null;
        if (action == null) return "none";
        InputControl active = action.activeControl;
        return "name=" + action.name + "; enabled=" + action.enabled +
            "; activeControl=" + (active != null ? active.path + " deviceId=" + active.device.deviceId : "none") +
            "; resolvedControls=[" + string.Join(", ", action.controls.Select(control =>
                control.path + " deviceId=" + control.device.deviceId)) + "]";
    }

    static bool IsBriefOrBench(Transform target)
    {
        for (Transform current = target; current != null; current = current.parent)
            if (current.name.StartsWith("brief-", System.StringComparison.Ordinal) || current.name == "Bench drop") return true;
        return false;
    }

    static string HierarchyPath(Transform target)
    {
        if (target == null) return "none";
        var names = new List<string>();
        for (Transform current = target; current != null; current = current.parent) names.Add(current.name);
        names.Reverse();
        return string.Join("/", names);
    }

    static string Handlers(GameObject target)
    {
        return "handlers down=" + Handler<IPointerDownHandler>(target) +
            "; up=" + Handler<IPointerUpHandler>(target) +
            "; click=" + Handler<IPointerClickHandler>(target) +
            "; drag=" + Handler<IDragHandler>(target) +
            "; endDrag=" + Handler<IEndDragHandler>(target);
    }

    static string Handler<T>(GameObject target) where T : IEventSystemHandler
    {
        GameObject receiver = ExecuteEvents.GetEventHandler<T>(target);
        if (receiver == null) return "none";
        return HierarchyPath(receiver.transform) + "[" +
            string.Join(",", receiver.GetComponents<Component>().Where(component => component is T)
                .Select(component => component.GetType().Name)) + "]";
    }

    public static void Capture(string name)
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        ui.StartCoroutine(CaptureAfterFrame(name));
    }
    static IEnumerator CaptureAfterFrame(string name)
    {
        yield return new WaitForEndOfFrame();
        string directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/local-validation/screenshots"));
        Directory.CreateDirectory(directory);
        ScreenCapture.CaptureScreenshot(Path.Combine(directory,Path.GetFileName(name)+".png"));
    }
}
