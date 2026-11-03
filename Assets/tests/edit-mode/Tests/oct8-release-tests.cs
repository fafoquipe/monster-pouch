using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class October8ReleaseTests
{
    GameObject root;MatchConfig config;MatchController match;
    [SetUp] public void Setup()
    {
        root=new GameObject("release tests");var board=root.AddComponent<BoardManager>();board.BuildBoard();
        var mapper=root.AddComponent<BoardWorldMapper>();mapper.Configure(board,root.transform,new Vector2(1,-1),Vector2.zero);
        config=ScriptableObject.CreateInstance<MatchConfig>();config.Units=DocumentedRoster.CreateAll();
        match=root.AddComponent<MatchController>();match.Configure(config,board,mapper);
    }
    [TearDown] public void Cleanup(){match.Menu();Object.DestroyImmediate(root);Object.DestroyImmediate(config);}
    [Test] public void ReadyCanBeCancelledAndPreparationResumesUntilCommit()
    {
        match.StartOnlineMatch(BoardSide.Blue,"popow",new[]{"aky"},"bugaloo",new[]{"tsu"},42);
        int sent=0;match.OnlineReadyRequested+=()=>sent++;
        Assert.IsTrue(match.ToggleReady());Assert.IsTrue(match.OnlineReady);
        Assert.IsTrue(match.ToggleReady());Assert.IsFalse(match.OnlineReady);
        Assert.AreEqual(2,sent);Assert.IsTrue(match.Reroll(match.Player.OfferVersion));
        match.Ready();match.SetOpponentReady();match.LockPreparation(true);
        Assert.IsFalse(match.ToggleReady());Assert.IsTrue(match.OnlineReady);
        match.LockPreparation(false);Assert.IsTrue(match.ToggleReady());
    }
    [Test] public void TimerReadiesOnceAndCannotBeCancelledAfterExpiry()
    {
        match.StartOnlineMatch(BoardSide.Blue,"popow",new[]{"aky"},"bugaloo",new[]{"tsu"},42);
        int sent=0;match.OnlineReadyRequested+=()=>sent++;
        match.Advance(100);match.Advance(1);Assert.IsTrue(match.OnlineReady);Assert.AreEqual(1,sent);Assert.IsFalse(match.ToggleReady());
    }
    [Test] public void BotReadyHasCancellableCountdown()
    {
        match.StartMatch("popow",new[]{"aky"},42);match.ToggleReady();match.Advance(.3f);
        Assert.AreEqual(MatchPhase.Preparation,match.Phase);match.ToggleReady();match.Advance(1);
        Assert.AreEqual(MatchPhase.Preparation,match.Phase);match.ToggleReady();match.Advance(1.3f);Assert.AreEqual(MatchPhase.Combat,match.Phase);
    }
    [TestCase(" ab12cd ","AB12CD")][TestCase("xy-z!9?01","XYZ901")][TestCase(null,"")]
    public void CodesNormalizeForTypingAndPasting(string value,string expected)=>Assert.AreEqual(expected,LocalGameUI.NormalizeRoomCode(value));
    [TestCase(3,2f)][TestCase(2,.7f)]
    public void HookOnlySweepsAndStunsOnConfiguredAttack(int every,float duration)
    {
        var def=DocumentedRoster.CreatePopow();def.AttackInterval=duration+1;def.AttackWindup=.1f;def.AttackHold=def.AttackRecovery=0;
        def.EnergyMax=100000;def.EnergyPerAttack=def.EnergyOnDamage=def.EnergyPerSecond=0;
        var trick=def.Tricks[1];trick.Parameters=new[]{new AbilityParameter{Key="every",Value=every},new AbilityParameter{Key="duration",Value=duration}};
        var own=new OwnedUnit(def);own.Tricks[1]=true;
        var a=new GameObject("popow");a.transform.SetParent(root.transform);var popow=a.AddComponent<MonsterUnit>();popow.Initialize("p",BoardSide.Blue,UnitStats.FromDefinition(def,own));
        var b=new GameObject("target");b.transform.SetParent(root.transform);var target=b.AddComponent<WhelpUnit>();
        target.Initialize("t",BoardSide.Red,UnitStats.FromDefinition(new UnitDefinition{Id="dummy",MaxHealth=10000,Damage=0,AttackInterval=100,AttackWindup=.1f,MoveInterval=100}));
        match.Board.TryOccupyCell(popow,2,5);match.Board.TryOccupyCell(target,2,4);
        var sim=new CombatSimulation(match.Board);sim.Begin(new BattleUnit[]{popow,target});int hits=0,hooks=0;bool stunned=false;
        sim.AreaEffect+=(actor,effect,cells)=>{if(effect=="popow-sweep")hooks++;};
        sim.Impacted+=(actor,victim,damage)=>{if(actor==popow&&victim==target)hits++;};
        for(int i=0;i<100&&hits<every;i++)
        {
            sim.Step(.1f);
            if(hits<every){Assert.IsFalse(target.IsStunned);Assert.AreEqual(0,hooks);}
            else stunned=target.IsStunned;
        }
        Assert.AreEqual(every,hits);Assert.AreEqual(1,hooks);Assert.IsTrue(stunned);
        sim.Step(duration-.2f);Assert.IsTrue(target.IsStunned);
        sim.Step(.3f);Assert.IsFalse(target.IsStunned);sim.Stop();
    }
}

