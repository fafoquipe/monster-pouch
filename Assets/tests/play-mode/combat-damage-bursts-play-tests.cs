using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Isolated native UI/event fixture. No scene load, profiles, PlayerPrefs, saves or GameView changes.
public sealed class CombatDamageBurstsPlayTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new List<GameObject>();
    MatchController match; CombatSimulation simulation; CombatDamageNumbers numbers;
    RectTransform canvas; BoardManager board; BattleUnit source, target;
    GameObject Make(string name, params Type[] types)
    { var go = new GameObject(name, types); objects.Add(go); return go; }
    void Set(string property, object value) => typeof(MatchController).GetProperty(property).SetValue(match, value);
    void Event(string name, params object[] args) => (typeof(MatchController).GetField(name, Hidden).GetValue(match) as Delegate)?.DynamicInvoke(args);
    void Tick(float delta) => typeof(CombatDamageNumbers).GetMethod("AdvancePresentation", Hidden).Invoke(numbers, new object[]{delta});
    void TimeAt(int tick) => typeof(CombatSimulation).GetField("tick", Hidden).SetValue(simulation, tick);
    void Hit(BattleUnit victim, int amount=1, BattleUnit attacker=null) => Event("Impacted", attacker??source, victim, amount);
    Text[] Labels() => canvas.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("damage-number-")).ToArray();
    BattleUnit Unit(BoardSide side, int health=100, int damage=0)
    {
        var unit=Make("isolated-popup-unit-"+objects.Count).AddComponent<WhelpUnit>();
        unit.Initialize(unit.name,side,new UnitStats(health,damage,range:9,attackInterval:100,moveInterval:100));
        return unit;
    }
    [SetUp] public void Setup()
    {
        board=Make("isolated-popup-board").AddComponent<BoardManager>();board.BuildBoard();
        simulation=new CombatSimulation(board);
        match=Make("isolated-popup-match").AddComponent<MatchController>();match.enabled=false;
        Set("Phase",MatchPhase.Combat);Set("Simulation",simulation);
        canvas=Make("isolated-popup-canvas",typeof(RectTransform)).GetComponent<RectTransform>();canvas.sizeDelta=new Vector2(540,960);
        var camera=Make("isolated-popup-camera").AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(0,0,-10);
        numbers=Make("isolated-popup-presenter").AddComponent<CombatDamageNumbers>();numbers.Configure(match,canvas,camera);
        source=Unit(BoardSide.Blue);target=Unit(BoardSide.Red);
    }
    [TearDown] public void Cleanup()
    {
        simulation?.Stop();
        for(int i=objects.Count-1;i>=0;i--)if(objects[i]!=null)Object.DestroyImmediate(objects[i]);
        objects.Clear();
    }
    [Test] public void FiveSimultaneousOnesShowOneFiveAndReuseTheSameLabel()
    {
        for(int i=0;i<5;i++)Hit(target);
        Assert.AreEqual(0,numbers.ActiveNumberCount);Assert.IsEmpty(Labels());
        Tick(.18f);var label=Labels().Single();Assert.AreEqual("5",label.text);
        TimeAt(3);Hit(target,2);Tick(.01f);
        Assert.AreSame(label,Labels().Single());Assert.AreEqual("7",label.text);
        Assert.AreEqual(100,target.CurrentHealth,"The event consumer cannot apply damage.");
    }
    [Test] public void TargetsStaySeparateAndHealAndIncomingEventsDoNotAddDamage()
    {
        var other=Unit(BoardSide.Red);Hit(target,2);Hit(other,3);
        Event("Healed",target,40);Hit(source,9,other);Hit(target,0);Hit(target,-1);Tick(.18f);
        CollectionAssert.AreEquivalent(new[]{"2","3"},Labels().Select(x=>x.text));
    }
    [Test] public void SimulatedTickGapsAndOneSecondSegmentsSplitEvenWithinOneRenderedFrame()
    {
        Hit(target);TimeAt(5);Hit(target);Tick(.18f);Assert.AreEqual("2",Labels().Single().text);
        TimeAt(10);Hit(target);Tick(.18f);
        CollectionAssert.AreEquivalent(new[]{"2","1"},Labels().Select(x=>x.text));
        TimeAt(16);Hit(target,3);Tick(.18f);
        CollectionAssert.AreEquivalent(new[]{"2","1","3"},Labels().Select(x=>x.text));
    }
    [Test] public void OneCombinedHealthAndShieldReceiptIsCountedOnceAndHealingStaysSeparate()
    {
        // Settle a 5-point packet: 3 shield + 2 HP. This mirrors the existing event boundary,
        // where Eronya already supplies HP+shield; the UI must not read/add either delta again.
        typeof(BattleUnit).GetMethod("AddShield",Hidden).Invoke(target,new object[]{3,int.MaxValue});
        int remainder=(int)typeof(BattleUnit).GetMethod("AbsorbShield",Hidden).Invoke(target,new object[]{5,null});
        Assert.AreEqual(2,remainder);target.ApplyDamage(remainder);Hit(target,5);
        int healed=target.Heal(1);Event("Healed",target,healed);Tick(.18f);
        Assert.AreEqual("5",Labels().Single().text);Assert.AreEqual(99,target.CurrentHealth);Assert.AreEqual(0,target.Shield);
    }
    [Test] public void CritAnnotatesTheSameAggregateWithoutDoubleCountingAndKeepsSizeLimits()
    {
        Hit(target,2);Event("CriticalImpacted",source,target);Hit(target,3);Tick(.18f);
        var label=Labels().Single();Assert.AreEqual("5",label.text);Assert.AreEqual("damage-number-critical",label.name);
        Assert.AreEqual(.16f,label.color.g,.001f);
        Assert.That(label.fontSize*label.rectTransform.localScale.x,Is.InRange(numbers.Settings.MinimumSize-.001f,numbers.Settings.MaximumSize+.001f));
        Hit(source,5,target);Event("CriticalImpacted",target,source);Assert.AreEqual("5",label.text);
    }
    [Test] public void ResetDespawnDisableAndSimulationReplacementDiscardPendingBatches()
    {
        Hit(target,8);target.ResetForCombat();Hit(target,1);Tick(.18f);Assert.AreEqual("1",Labels().Single().text);
        target.gameObject.SetActive(false);Tick(.01f);Assert.AreEqual(0,numbers.ActiveNumberCount);
        target.gameObject.SetActive(true);Hit(target,7);numbers.enabled=false;Assert.IsEmpty(Labels());
        numbers.enabled=true;Hit(target,9);Set("Simulation",new CombatSimulation(board));Tick(.2f);Assert.IsEmpty(Labels());
        Set("Simulation",simulation);Hit(target,4);Set("Phase",MatchPhase.Preparation);Event("Changed");Assert.IsEmpty(Labels());
        Set("Phase",MatchPhase.Combat);Hit(target,1);Tick(.18f);Assert.AreEqual("1",Labels().Single().text);
    }
    [Test] public void PauseDoesNotSpendTheInitialBufferOrTheVisibleLifetime()
    {
        Hit(target);Set("Paused",true);Tick(90);Assert.IsEmpty(Labels());
        Set("Paused",false);Tick(.18f);var label=Labels().Single();
        Set("Paused",true);Tick(90);Assert.AreSame(label,Labels().Single());
        Set("Paused",false);Tick(.96f);Assert.IsEmpty(Labels());
    }
    [Test] public void LethalPacketsFlushAtFinalPositionAndSurviveDespawnForTheirTail()
    {
        Hit(target,2);target.transform.position=new Vector3(2,3,0);target.ApplyDamage(100);
        Hit(target,3);Assert.AreEqual("5",Labels().Single().text);
        Hit(target,1);Event("CriticalImpacted",source,target);Event("Died",target);
        var label=Labels().Single();Assert.AreEqual("6",label.text);Assert.AreEqual("damage-number-critical",label.name);
        var active=(System.Collections.IList)typeof(CombatDamageNumbers).GetField("active",Hidden).GetValue(numbers);
        Assert.AreEqual(new Vector3(2,3.5f,0),active[0].GetType().GetField("Anchor").GetValue(active[0]));
        Object.DestroyImmediate(target.gameObject);Tick(.1f);Assert.AreSame(label,Labels().Single());
        Tick(1.3f);Assert.IsEmpty(Labels());
    }
    [Test] public void PoolRemainsBoundedAndReusesTextAfterExpiration()
    {
        var pooled=canvas.GetComponentsInChildren<Text>(true);Assert.AreEqual(64,pooled.Length);
        Hit(target);Tick(.18f);Tick(1);Assert.IsEmpty(Labels());Hit(target,3);Tick(.18f);
        Assert.Contains(Labels().Single(),pooled);Assert.AreEqual(64,canvas.GetComponentsInChildren<Text>(true).Length);
        for(int i=1;i<=80;i++){TimeAt(i*10);Hit(target);}
        Tick(.18f);Assert.LessOrEqual(numbers.ActiveNumberCount,64);Assert.AreEqual(64,canvas.GetComponentsInChildren<Text>(true).Length);
    }
    [Test] public void RealFiveHitSimulationProducesIdenticalHealthEnergyAndEventsWithPresenterEnabledOrDisabled()
    {
        var actors=new List<BattleUnit>();
        for(int i=0;i<5;i++){var unit=Unit(BoardSide.Blue,100,1);Assert.IsTrue(board.TryOccupyCell(unit,i+1,5));actors.Add(unit);}
        Assert.IsTrue(board.TryOccupyCell(target,2,4));actors.Add(target);
        var snapshots=new List<string>();
        for(int pass=0;pass<2;pass++)
        {
            numbers.enabled=pass==1;
            foreach(var actor in actors)actor.ResetForCombat();
            simulation=new CombatSimulation(board);Set("Simulation",simulation);
            int impacts=0,total=0;simulation.Impacted+=(a,b,amount)=>{if(amount>0){impacts++;total+=amount;}Event("Impacted",a,b,amount);};
            simulation.Begin(actors);
            for(int i=0;i<100&&total<5;i++)simulation.Step(.1f);
            Assert.AreEqual(5,total);Assert.AreEqual(5,impacts);Assert.AreEqual(95,target.CurrentHealth);
            snapshots.Add(string.Join("|",actors.Select(a=>a.CurrentHealth+":"+a.Energy))+":"+impacts+":"+total);
            if(pass==1){Tick(.18f);Assert.AreEqual("5",Labels().Single().text);}
            simulation.Stop();
        }
        Assert.AreEqual(snapshots[0],snapshots[1]);
    }
}
