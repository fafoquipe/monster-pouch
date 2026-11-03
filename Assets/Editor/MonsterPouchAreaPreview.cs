using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using UnityEngine;

// Visual QA uses live combat copies; never saves preferences, scenes or balance assets.
public static class MonsterPouchAreaPreview
{
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string Preview(string kind)
    {
        var ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        if(ui==null||!Application.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
        if(!new[]{"gochan","jazzar","anuik","taunt","popow"}.Contains(kind))throw new ArgumentException("gochan, jazzar, anuik, taunt or popow");
        var match=ui.Match;match.enabled=false;ui.CloseModal();
        bool ranged=kind=="gochan"||kind=="jazzar";
        string hero=kind=="taunt"?"bugaloo":ranged?"anuik":kind;
        match.StartMatch(hero,new[]{"gochan","flo","atong","hymay","bugui","tokoro","trimol"},260926);
        typeof(MatchController).GetMethod("ClearActors",Hidden).Invoke(match,null);
        var blue=(Dictionary<string,OwnedUnit>)typeof(PouchState).GetField("owned",Hidden).GetValue(match.Player);
        var red=(Dictionary<string,OwnedUnit>)typeof(PouchState).GetField("owned",Hidden).GetValue(match.Bot);
        blue.Clear();red.Clear();
        string sourceId=kind=="taunt"?"bugaloo":kind=="jazzar"?"jazar":kind;
        var owned=Add(blue,match,sourceId,2,ranged?7:6);
        if(kind=="anuik")for(int i=0;i<owned.Definition.Tricks.Length;i++)
            if(owned.Definition.Tricks[i].EffectId=="anuik-revive-impact")owned.Tricks[i]=true;
        Add(red,match,"tokoro",2,ranged?4:5);
        Add(red,match,"flo",1,ranged?4:6);
        Add(red,match,"atong",3,ranged?4:6);
        typeof(MatchController).GetMethod("SyncActors",Hidden).Invoke(match,null);
        match.Ready();ui.RenderPage();
        foreach(var actor in match.Actors.Values)
        {
            typeof(UnitStats).GetField("maxHealth",Hidden).SetValue(actor.BaseStats,1000);actor.Heal(1000);
            if(actor.Side==BoardSide.Red)
            {
                typeof(UnitStats).GetField("attack",Hidden).SetValue(actor.BaseStats,0);
                typeof(UnitStats).GetField("moveSpeed",Hidden).SetValue(actor.BaseStats,.01f);
                typeof(UnitStats).GetField("attackSpeed",Hidden).SetValue(actor.BaseStats,.01f);
            }
        }
        var source=match.Actors.Values.First(a=>a.Side==BoardSide.Blue);
        if(kind=="anuik")source.ApplyDamage(source.CurrentHealth);
        if(kind=="taunt"||kind=="popow")source.AddEnergy(1000);
        ui.StartCoroutine(Record(ui,kind));return "Recording "+kind;
    }
    static OwnedUnit Add(Dictionary<string,OwnedUnit> roster,MatchController match,string id,int x,int y)
    {
        var unit=new OwnedUnit(match.Config.Get(id));
        typeof(OwnedUnit).GetProperty("Location").SetValue(unit,UnitLocation.Field);
        typeof(OwnedUnit).GetProperty("Deployment").SetValue(unit,new Vector2Int(x,y));roster.Add(id,unit);return unit;
    }
    static IEnumerator Record(LocalGameUI ui,string kind)
    {
        var match=ui.Match;var simulation=match.Simulation;bool captured=false;
        float elapsed=0,next=.14f;int frame=0;bool capturedFlight=false;
        Action<BattleUnit,string,BoardCell[]> onArea=(source,id,cells)=>
        {
            if(captured)return;captured=true;
            MonsterPouchPlayInspection.Capture("area-"+kind+"-impact");
        };
        match.AreaEffect+=onArea;
        while(elapsed<4.2f&&match.Simulation==simulation)
        {
            float dt=Time.deltaTime;match.Advance(dt);elapsed+=dt;
            if(kind=="gochan"&&!capturedFlight&&GameObject.Find("documented-projectile-gochan")!=null)
            {capturedFlight=true;MonsterPouchPlayInspection.Capture("area-gochan-projectile");}
            if(kind=="jazzar"&&!capturedFlight&&GameObject.Find("jazzar-toxic-flight")!=null)
            {capturedFlight=true;MonsterPouchPlayInspection.Capture("area-jazzar-projectile");}
            if(kind=="jazzar"&&!captured&&GameObject.Find("jazzar-toxic-splash")!=null)
            {captured=true;MonsterPouchPlayInspection.Capture("area-jazzar-impact");}
            if(elapsed>=next&&frame<5)
            {MonsterPouchPlayInspection.Capture("area-"+kind+"-"+frame);frame++;next+=.35f;}
            yield return null;
        }
        match.AreaEffect-=onArea;
        Debug.Log("Area preview "+kind+" complete; event="+captured);
    }
    public static string Restore(){return MonsterPouchImpactPreview.Restore();}
}
