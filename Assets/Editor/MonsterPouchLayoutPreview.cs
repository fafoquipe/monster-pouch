using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Editor-only reproduction: changes the actual Game view render size, never scene assets.
public static class MonsterPouchLayoutPreview
{
    const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static LocalGameUI UI=>Object.FindFirstObjectByType<LocalGameUI>();
    public static void Tests()
    {
        SessionState.SetString("MonsterPouch.Validation.PendingRun","layout-play-mode");
        var runner=ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"OpponentPreviewPlayTests","ViewportLayoutPlayTests","PlacementExchangePlayTests"}}));
    }
    public static void FocusedTests(string names)
    {
        SessionState.SetString("MonsterPouch.Validation.PendingRun","focused-play");
        var runner=ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=names.Split('|')}));
    }
    public static string Resize(string dimensions)
    {
        var parts=dimensions.Split('x');int width=int.Parse(parts[0]),height=int.Parse(parts[1]);
        var assembly=typeof(Editor).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",Any).GetValue(null);
        var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group=sizesType.GetMethod("GetGroup",Any).Invoke(sizes,new[]{Enum.Parse(groupType,EditorUserBuildSettings.activeBuildTarget==BuildTarget.Android?"Android":"Standalone")});
        var type=group.GetType();
        int count=(int)type.GetMethod("GetTotalCount",Any).Invoke(group,null),index=-1;
        for(int i=0;i<count;i++)
        {
            var size=type.GetMethod("GetGameViewSize",Any).Invoke(group,new object[]{i});
            if((int)size.GetType().GetProperty("width",Any).GetValue(size)==width &&
               (int)size.GetType().GetProperty("height",Any).GetValue(size)==height){index=i;break;}
        }
        if(index<0)
        {
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");
            var kind=assembly.GetType("UnityEditor.GameViewSizeType");
            var size=Activator.CreateInstance(sizeType,Any,null,new object[]{Enum.Parse(kind,"FixedResolution"),width,height,"Layout review "+dimensions},null);
            type.GetMethod("AddCustomSize",Any).Invoke(group,new[]{size});index=count;
        }
        var viewType=assembly.GetType("UnityEditor.GameView");
        var view=EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex",Any).SetValue(view,index);
        view.Focus();view.Repaint();
        return "Game view requested "+dimensions;
    }
    public static string Capture(string name)
    {
        if(UI==null||!Application.isPlaying)return "Enter Play Mode first.";
        UI.StartCoroutine(Shot(name));return "Capture queued: "+name;
    }
    static IEnumerator Shot(string name)
    {
        yield return new WaitForSecondsRealtime(.5f);
        MonsterPouchLocalBridge.Reply(name,Measure());
        MonsterPouchPlayInspection.Capture(name);
    }
    public static string Preparation()
    {
        UI.CloseModal();UI.Match.enabled=false;
        UI.Match.StartMatch("bugaloo",new[]{"stein"},1905);
        return "Bugaloo preparation; "+Measure();
    }
    public static string Pair(string pair)
    {
        var ids=pair.Split(',');var config=UI.Match.Config;
        UI.CloseModal();UI.Match.Menu();UI.Match.enabled=false;
        var saved=config.Units;
        try
        {
            // A same-hero matchup is a visual fixture, not a change to the bot roster.
            if(ids[0]==ids[1])config.Units=saved.Where(u=>!u.IsMonster||u.Id==ids[0]).ToArray();
            int seed=Enumerable.Range(1,1024).First(s=>BotPouchGenerator.Create(config,s^0x45fa1,ids[0]).MonsterId==ids[1]);
            if(!UI.Match.StartMatch(ids[0],new[]{"stein"},seed))throw new InvalidOperationException(UI.Match.Notice);
        }
        finally{config.Units=saved;}
        return pair+"; "+Measure();
    }
    public static string Exchange()
    {
        UI.StartCoroutine(ExchangeSequence());return "Recording exchange.";
    }
    static IEnumerator ExchangeSequence()
    {
        Preparation();UI.Match.Player.AddCombatCoins(30);
        var offer=UI.Match.Player.Offers[0];
        if(!UI.Match.BuyAndPlace(0,offer.Token,UI.Match.Board.GetCell(3,5)))throw new InvalidOperationException(UI.Match.Notice);
        yield return new WaitForSecondsRealtime(.5f);
        UI.Match.Place("stein",UI.Match.Board.GetCell(2,5));
        for(int frame=0;frame<12;frame++)
        {
            yield return new WaitForSecondsRealtime(.033f);
            yield return new WaitForEndOfFrame();
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/local-validation/screenshots"));
            Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir,"exchange-"+frame.ToString("D2")+".png"));
        }
        MonsterPouchLocalBridge.Reply("exchange-sequence",Measure());
    }
    public static string Home(){UI.CloseModal();UI.Match.Menu();UI.OpenHome();return "Home";}
    public static string Decks(){UI.CloseModal();UI.Match.Menu();UI.OpenDecks();return "Brief";}
    public static string NextRound()
    {
        UI.CloseModal();UI.Match.Ready();
        foreach(var actor in UI.Match.Actors.Values.Where(a=>a.Side==BoardSide.Red))actor.ApplyDamage(actor.CurrentHealth);
        UI.Match.Advance(.2f);UI.Match.NextRound();return "Round "+UI.Match.Round;
    }
    public static string Inspect()
    {
        var id=UI.Match.Round==1?UI.Match.InitialOpponent.UnitId:UI.Match.PreviousOpponent.First().UnitId;
        var card=UI.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="echo-"+id);
        var screen=RectTransformUtility.WorldToScreenPoint(null,card.TransformPoint(card.rect.center));
        bool started=UI.BeginBoardPointer(screen);UI.StartCoroutine(Release(screen));return "Long press started="+started;
    }
    static IEnumerator Release(Vector2 screen){yield return new WaitForSecondsRealtime(.8f);UI.EndBoardPointer(screen);}
    static Rect ScreenRect(RectTransform r)
    {
        var c=new Vector3[4];r.GetWorldCorners(c);
        return Rect.MinMaxRect(c[0].x,c[0].y,c[2].x,c[2].y);
    }
    public static string Measure()
    {
        var ui=UI;var b=new StringBuilder();
        b.AppendLine($"screen={Screen.width}x{Screen.height}; safe={Screen.safeArea}; root={ScreenRect(ui.CanvasRoot)}; scale={ui.CanvasRoot.lossyScale}; phase={ui.Match.Phase}; round={ui.Match.Round}");
        foreach(var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            b.AppendLine($"camera {camera.name}: rect={camera.rect}; pixels={camera.pixelRect}; position={camera.transform.position}; ortho={camera.orthographicSize}; clear={camera.clearFlags}; enabled={camera.enabled}");
        foreach(var actor in Object.FindObjectsByType<MonsterPouch.Gameplay.Units.BattleUnit>(FindObjectsSortMode.None))
        {
            var renderer=actor.GetComponent<UnitPresentation>()?.Renderer;
            b.AppendLine($"actor {actor.UnitId}: {actor.CurrentCell?.Coordinates}; foot={ui.GameCamera.WorldToScreenPoint(actor.transform.position)}; sprite={renderer?.bounds}");
        }
        foreach(var r in ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Where(r=>r.name.StartsWith("echo-")||r.name=="Menu background"))
            b.AppendLine($"UI {r.name}: screen={ScreenRect(r)}; local={r.anchoredPosition}; size={r.sizeDelta}");
        var echoes=ui.Match.Round==1&&ui.Match.InitialOpponent!=null?new[]{ui.Match.InitialOpponent}:ui.Match.PreviousOpponent.ToArray();
        foreach(var echo in echoes)
            b.AppendLine($"revealed {echo.UnitId} {echo.Position}; expectedFoot={ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(echo.Position.x,echo.Position.y)))}");
        return b.ToString();
    }
}
