using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class KazuruBurstAndTaurisGrowthTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board;
        CombatSimulation sim;
        MatchController match;
        MatchConfig config;

        [SetUp] public void Setup()
        {
            var root = new GameObject("burst-growth-board"); objects.Add(root);
            board = root.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void Cleanup()
        {
            sim?.Stop();
            foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear(); if (config != null) Object.DestroyImmediate(config);
        }
        static AbilityParameter P(string key, float value) => new AbilityParameter { Key=key, Value=value };
        BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side, int trick=-1)
        {
            var go = new GameObject(definition.Id + objects.Count); objects.Add(go);
            var owned = new OwnedUnit(definition); if (trick >= 0) owned.Tricks[trick]=true;
            var unit=go.AddComponent<WhelpUnit>(); unit.Initialize(go.name,side,UnitStats.FromDefinition(definition,owned));
            Assert.IsTrue(board.TryOccupyCell(unit,x,y)); return unit;
        }
        BattleUnit Enemy(int x=2,int y=6) => Unit(new UnitDefinition { Id="test-target", MaxHealth=1000,
            Damage=0,AttackRange=1,AttackInterval=100,MoveInterval=100 },x,y,BoardSide.Red);
        object Actor(BattleUnit unit) => ((IEnumerable)typeof(CombatSimulation).GetField("actors",Private).GetValue(sim))
            .Cast<object>().Single(a=>a.GetType().GetField("Unit").GetValue(a)==(object)unit);
        object Call(string name,params object[] args) => typeof(CombatSimulation).GetMethod(name,Private).Invoke(sim,args);
        void Set(BattleUnit unit,string name,object value) => Actor(unit).GetType().GetField(name).SetValue(Actor(unit),value);
        int Get(BattleUnit unit,string name) => (int)Actor(unit).GetType().GetField(name).GetValue(Actor(unit));
        void Begin(params BattleUnit[] units)
        {
            sim=new CombatSimulation(board);sim.Begin(units);
            foreach(var unit in units){Set(unit,"NextMove",99999);Set(unit,"NextAttack",99999);}
        }
        BattleUnit Kazuru(bool upgraded=true)
        {
            var definition=DocumentedRoster.CreateKazuru();definition.Damage=20;definition.ProjectileSpeed=10;
            definition.EnergyMax=1000;definition.EnergyPerAttack=10;
            return Unit(definition,2,7,BoardSide.Blue,upgraded?0:-1);
        }

        [Test] public void TwoRingsHaveSeparateReleaseAndDamageButOnlyOneAttackAndEnergyGain()
        {
            var k=Kazuru();var enemy=Enemy();Begin(k,enemy);
            var damage=new List<int>();var times=new List<float>();int attacks=0,followups=0;
            sim.Impacted+=(source,target,amount)=>{if(source==k){damage.Add(amount);times.Add(sim.Elapsed);}};
            sim.Attacked+=(source,target,projectile)=>{if(source==k)attacks++;};
            sim.KazuruRingLaunched+=(source,target,travel)=>{followups++;Assert.AreSame(enemy,target);Assert.That(sim.Elapsed,Is.EqualTo(.4f).Within(.001f));};
            Call("QueueBasicAttack",Actor(k),enemy);
            sim.Step(.3f);CollectionAssert.AreEqual(new[]{20},damage);Assert.AreEqual(0,followups);
            sim.Step(.2f);CollectionAssert.AreEqual(new[]{20,14},damage);
            Assert.That(times[1]-times[0],Is.EqualTo(.2f).Within(.001f));
            Assert.AreEqual(1,attacks);Assert.AreEqual(1,followups);Assert.AreEqual(1,Get(k,"Attacks"));Assert.AreEqual(10,k.Energy);
            Assert.AreEqual(9,Get(k,"NextAttack"),"A 0.2s burst extends the ordinary 0.7s attack cycle once.");
        }

        [Test] public void NoUpgradeKeepsOneMeleeHit()
        {
            var k=Kazuru(false);var enemy=Enemy();Begin(k,enemy);int hits=0;
            sim.Impacted+=(source,target,damage)=>{if(source==k)hits++;};
            Call("QueueBasicAttack",Actor(k),enemy);sim.Step(.6f);
            Assert.AreEqual(1,hits);Assert.AreEqual(1,k.BaseStats.AttackRange);Assert.AreEqual(7,Get(k,"NextAttack"));
        }

        [Test] public void BurstCapturesBuffedFirstDamageAndUsesEditableSecondRatio()
        {
            var d=DocumentedRoster.CreateKazuru();d.Damage=20;d.EnergyMax=1000;d.ProjectileSpeed=10;
            d.Tricks[0].Parameters=new[]{P("secondRingPercent",35),P("burstInterval",.3f)};
            var k=Unit(d,2,7,BoardSide.Blue,0);var enemy=Enemy();Begin(k,enemy);
            Call("ApplyDamageBoost",Actor(k),20,1f,2);
            var damage=new List<int>();sim.Impacted+=(source,target,n)=>{if(source==k)damage.Add(n);};
            Call("QueueBasicAttack",Actor(k),enemy);sim.Step(.6f);
            CollectionAssert.AreEqual(new[]{40,14},damage,"The buff expires before either hit, but both use the same attack snapshot.");
        }

        [TestCase("stun")][TestCase("death")][TestCase("reset")][TestCase("stop")]
        public void AnInterruptedBurstDoesNotLaunchTheUnreleasedSecondRing(string interruption)
        {
            var k=Kazuru();var enemy=Enemy();Begin(k,enemy);int followups=0;
            sim.KazuruRingLaunched+=(a,b,t)=>followups++;
            Call("QueueBasicAttack",Actor(k),enemy);sim.Step(.3f);
            if(interruption=="stun")Call("Stun",Actor(k),20,"impact");
            else if(interruption=="death")k.ApplyDamage(9999);
            else if(interruption=="reset"){k.ResetForCombat();k.IsPassiveTrainingTarget=true;}
            else sim.Stop();
            sim.Step(.3f);Assert.AreEqual(0,followups);Assert.AreEqual(980,enemy.CurrentHealth);
        }

        [Test] public void SecondRingChecksRangeAgainAndDoesNotRetargetAfterFirstRelease()
        {
            var k=Kazuru();var enemy=Enemy();var other=Enemy(3,6);Begin(k,enemy,other);int launches=0;
            sim.KazuruRingLaunched+=(a,b,t)=>launches++;
            Call("QueueBasicAttack",Actor(k),enemy);sim.Step(.3f);
            board.ReleaseUnit(enemy);Assert.IsTrue(board.TryOccupyCell(enemy,5,0));sim.Step(.2f);
            Assert.AreEqual(0,launches);Assert.AreEqual(980,enemy.CurrentHealth);Assert.AreEqual(1000,other.CurrentHealth);
        }

        [Test] public void NewlyChargedSuperWaitsUntilSecondRingHasBeenReleased()
        {
            var d=DocumentedRoster.CreateKazuru();d.EnergyPerAttack=100;d.ProjectileSpeed=10;
            var k=Unit(d,2,7,BoardSide.Blue,0);var enemy=Enemy();Begin(k,enemy);
            int followups=0;sim.KazuruRingLaunched+=(a,b,t)=>followups++;
            Call("QueueBasicAttack",Actor(k),enemy);sim.Step(.3f);Assert.AreEqual(100,k.Energy);
            sim.Step(.1f);Assert.AreEqual(1,followups);Assert.AreEqual(100,k.Energy);
            sim.Step(.1f);Assert.AreEqual(0,k.Energy,"The super can start after the completed burst release.");
        }

        [TestCase(20,40,120,60)][TestCase(35,40,135,75)][TestCase(20,100,120,120)][TestCase(0,40,100,40)]
        public void HungerAddsEditableMaximumHealthThenHealsOnlyThatAmount(int bonus,int start,int maximum,int health)
        {
            var d=DocumentedRoster.CreateTauris();d.Tricks[2].Parameters=new[]{P("healthPerKill",bonus)};
            var t=Unit(d,2,7,BoardSide.Blue,2);var enemy=Enemy();Begin(t,enemy);t.ApplyDamage(100-start);
            int earned=0;sim.PermanentHealthEarned+=(unit,value)=>earned+=value;
            Call("OnKill",Actor(t));Assert.AreEqual(maximum,t.BaseStats.MaxHealth);Assert.AreEqual(health,t.CurrentHealth);
            Assert.AreEqual(bonus,earned);Assert.AreEqual(100,d.MaxHealth,"The shared catalogue remains unchanged.");
        }

        [Test] public void AntiHealReducesRecoveryButNotPermanentGrowthAndDeathDoesNotResurrect()
        {
            var t=Unit(DocumentedRoster.CreateTauris(),2,7,BoardSide.Blue,2);var enemy=Enemy();Begin(t,enemy);
            t.ApplyDamage(60);Set(t,"AntiHealUntil",100);Set(t,"AntiHealMultiplier",.4f);
            Call("OnKill",Actor(t));Assert.AreEqual(120,t.BaseStats.MaxHealth);Assert.AreEqual(48,t.CurrentHealth);
            t.ApplyDamage(1000);Call("OnKill",Actor(t));Assert.AreEqual(140,t.BaseStats.MaxHealth);Assert.AreEqual(0,t.CurrentHealth);
        }

        [Test] public void RealDeathsGrantGrowthExactlyOncePerElimination()
        {
            var t=Unit(DocumentedRoster.CreateTauris(),2,7,BoardSide.Blue,2);var a=Enemy();var b=Enemy(3,6);var survivor=Enemy(5,0);
            Begin(t,a,b,survivor);int rewards=0;sim.PermanentHealthEarned+=(unit,value)=>rewards++;
            Call("DirectDamage",Actor(t),Actor(a),2000);Call("DirectDamage",Actor(t),Actor(b),2000);
            sim.Step(.1f);Assert.AreEqual(140,t.BaseStats.MaxHealth);Assert.AreEqual(2,rewards);
            sim.Step(.3f);Assert.AreEqual(140,t.BaseStats.MaxHealth);Assert.AreEqual(2,rewards);
        }

        [Test] public void ControllerPreservesGrowthAcrossRoundsAndOpponentSnapshotsButResetsOnNewMatch()
        {
            config=MatchConfig.CreateDocumented();config.OpeningAbilityWindowSeconds=0;
            var root=board.gameObject;var mapper=root.AddComponent<BoardWorldMapper>();mapper.Configure(board,root.transform,Vector2.one,Vector2.zero);
            match=root.AddComponent<MatchController>();match.Configure(config,board,mapper);
            match.StartMatch("tauris",41);Assert.AreEqual(MatchPhase.Preparation,match.Phase);
            var owned=match.Player.Monster;owned.Tricks[2]=true;
            match.Ready();sim=match.Simulation;var t=match.Actors[owned];int basis=t.BaseStats.MaxHealth;
            Call("OnKill",Actor(t));Call("OnKill",Actor(t));
            Assert.AreEqual(40,owned.PersistentHealthBonus);Assert.AreEqual(basis+40,t.BaseStats.MaxHealth);
            var echo=new MatchController.OpponentEcho(owned,match.Round);
            Assert.AreEqual(40,echo.Inspection.PersistentHealthBonus);
            foreach(var enemy in match.Actors.Values.Where(u=>u.Side!=t.Side))enemy.ApplyDamage(99999);
            match.Advance(.1f);Assert.AreEqual(MatchPhase.RoundResult,match.Phase);
            match.Advance(match.RoundTransitionDuration+.1f);Assert.AreEqual(2,match.Round);
            match.Ready();sim=match.Simulation;t=match.Actors[owned];
            Assert.AreEqual(basis+40,t.BaseStats.MaxHealth);Assert.AreEqual(basis+40,t.CurrentHealth);
            Call("OnKill",Actor(t));Assert.AreEqual(60,owned.PersistentHealthBonus);
            Assert.AreEqual(basis+60,UnitStats.FromDefinition(owned.Definition,owned).MaxHealth);
            Assert.AreEqual(40,echo.Inspection.PersistentHealthBonus,"An inspection snapshot must stay frozen.");
            match.StartMatch("tauris",42);sim=null;Assert.AreEqual(0,match.Player.Monster.PersistentHealthBonus);
            Assert.AreEqual(basis,UnitStats.FromDefinition(match.Player.Monster.Definition,match.Player.Monster).MaxHealth);
        }

        [Test] public void SavedAssetsAndBothLanguagesUseTheNewEditableRules()
        {
            var k=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/kazuru/Character").CreateRuntimeDefinition();
            var t=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/tauris/Character").CreateRuntimeDefinition();
            Assert.AreEqual(0,k.Tricks[0].DamageBonus);Assert.AreEqual(2,k.Tricks[0].RangeBonus);
            Assert.AreEqual(10,AbilityBalance.Resolve(t.Tricks[2]).Single(p=>p.Key=="healthPerKill").Value);
            Assert.AreEqual(90,AbilityBalance.Resolve(t.Tricks[2]).Single(p=>p.Key=="healthPercent").Value);
            Assert.AreEqual(100,AbilityBalance.Resolve(t.Tricks[2]).Single(p=>p.Key=="damagePercent").Value);
            StringAssert.Contains("70%",AbilityBalance.Description(k.Tricks[0],k));
            StringAssert.Contains("10",AbilityBalance.Description(t.Tricks[2],t));
            Assert.That(AbilityBalance.Description(t.Tricks[2],t),Does.Not.Contain("toda su vida").And.Not.Contain("{"));
            StringAssert.Contains("eliminación",AbilityBalance.ParameterLabel("tauris-hunger","healthPerKill"));
        }
    }
}
