using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEngine;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

// An isolated visual harness; no saved decks, balance assets or player progress are changed.
public static class MonsterPouchLunarPreview
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Folder="Logs/local-validation/lobby-polish-20261015";
    public static string TestState()
    {
        bool running=(bool)typeof(TestRunnerApi).GetMethod("IsRunActive",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        return "testsRunning="+running+"; playing="+EditorApplication.isPlaying+"; compiling="+EditorApplication.isCompiling;
    }
    public static string RunChecks()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||TestState().Contains("testsRunning=True"))
            throw new System.InvalidOperationException("Wait for the existing test run to finish first.");
        SessionState.SetString("MonsterPouch.Validation.PendingRun","lunar-final-play");
        var runner=ScriptableObject.CreateInstance<TestRunnerApi>();
        return runner.Execute(new ExecutionSettings(new Filter {testMode=TestMode.PlayMode,
            testNames=new[]{"LunarFeedbackPlayTests","BriefFeedbackPlayTests","LobbyThemeSelectionPlayTests","RedesignPlayTests.CompactSheetHasExplicitPurchaseAndAllAbilitiesHaveDedicatedArt"}}));
    }
    public static string Capture()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(!Application.isPlaying||ui==null)throw new System.InvalidOperationException("Enter Play Mode first.");
        ui.StartCoroutine(Run(ui));return "Capturing actual game views of lunar effects and blue-eyed Stein.";
    }
    static IEnumerator Run(LocalGameUI ui)
    {
        var match=ui.Match;bool wasEnabled=match.enabled;match.enabled=false;
        Directory.CreateDirectory(Folder);
        try
        {
            MonsterPouchLayoutPreview.Resize("1080x2340");
            match.StartMatch("bugaloo",12);
            typeof(MatchController).GetMethod("ClearActors",Private).Invoke(match,null);
            match.Board.BuildBoard();
            var player=(Dictionary<string,OwnedUnit>)typeof(PouchState).GetField("owned",Private).GetValue(match.Player);
            var bot=(Dictionary<string,OwnedUnit>)typeof(PouchState).GetField("owned",Private).GetValue(match.Bot);
            player.Clear();bot.Clear();
            void Place(OwnedUnit unit,Vector2Int cell)
            {
                typeof(OwnedUnit).GetProperty("Location").SetValue(unit,UnitLocation.Field);
                typeof(OwnedUnit).GetProperty("Deployment").SetValue(unit,cell);
            }
            var hero=match.Player.Monster;Place(hero,new Vector2Int(2,5));player.Add(hero.Definition.Id,hero);
            foreach(var pair in new[]{("blotan",new Vector2Int(4,6)),("stein",new Vector2Int(4,7))})
            {
                var unit=new OwnedUnit(match.Config.Get(pair.Item1));Place(unit,pair.Item2);player.Add(pair.Item1,unit);
            }
            var enemy=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(match.Config.Get("aky")));
            enemy.MaxHealth=10000;enemy.Damage=20;enemy.AttackRange=1;enemy.AttackInterval=2;enemy.AttackWindup=.1f;
            enemy.EnergyPerSecond=0;enemy.BaseAbility=null;
            var rival=new OwnedUnit(enemy);Place(rival,new Vector2Int(2,4));bot.Add(enemy.Id,rival);
            match.Ready();ui.RenderPage();yield return null;yield return null;
            var bugaloo=match.Actors.Values.First(a=>a.Side==match.LocalSide&&a.BaseStats.DefinitionId=="bugaloo");
            var blotan=match.Actors.Values.First(a=>a.Side==match.LocalSide&&a.BaseStats.DefinitionId=="blotan");
            int before=match.Player.Coins;bugaloo.AddEnergy(100);
            typeof(MatchController).GetMethod("QueueMoonkenDelivery",Private).Invoke(match,new object[]{blotan,1});
            blotan.GetComponent<UnitPresentation>().UseAbility("blotan-income");
            float elapsed=0;
            foreach(var shot in new[]{(.05f,"lunar-01-start"),(.35f,"lunar-02-rise"),(.55f,"lunar-03-reflection"),(.9f,"lunar-04-flight"),(1.45f,"lunar-05-paid")})
            {
                while(elapsed<shot.Item1){float dt=Mathf.Min(Time.unscaledDeltaTime,shot.Item1-elapsed);match.Advance(dt);elapsed+=dt;yield return null;}
                yield return null;yield return null;
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(Folder+"/"+shot.Item2+".png"));yield return null;
            }
            File.WriteAllText(Folder+"/lunar-captures.txt","Visual harness: income rose and flew to HUD; initial balance="+before+", balance after arrival="+match.Player.Coins+". Bugaloo reflected moon and Stein shown in actual game scene.");
        }
        finally{match.Menu();ui.RenderPage();match.enabled=wasEnabled;}
    }
}
