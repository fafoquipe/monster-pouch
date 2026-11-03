using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class EronyaLegacyParityPureTests
    {
        [Test] public void LegacyRangeIncludesAllCornersWithoutExpandingTheConfiguredRadius()
        {
            var origin=new BoardCell(3,4,1,BoardQuadrant.A,BoardSide.Blue);
            foreach(int range in new[]{1,2})
            {
                int included=0;
                for(int x=-3;x<=3;x++)for(int y=-3;y<=3;y++)
                {
                    var cell=new BoardCell(3+x,4+y,1,BoardQuadrant.A,BoardSide.Red);
                    bool expected=(x!=0||y!=0)&&Math.Abs(x)<=range&&Math.Abs(y)<=range;
                    Assert.AreEqual(expected,CombatTargetSelector.IsInAttackRange(origin,cell,range),$"r{range} ({x},{y})");
                    if(expected)included++;
                }
                Assert.AreEqual(range==1?8:24,included);
            }
            Assert.IsFalse(CombatTargetSelector.IsInAttackRange(origin,null,1));
        }
        [Test] public void EachPurchasedSlotActivatesOnlyItsOwnEffectForAllTwentyCharacters()
        {
            foreach(var definition in EronyaRoster.CreateAll())for(int chosen=0;chosen<3;chosen++)
            {
                var owned=new OwnedUnit(definition);owned.Tricks[chosen]=true;
                var stats=UnitStats.FromDefinition(definition,owned);
                for(int slot=0;slot<3;slot++)
                {
                    Assert.AreEqual(slot==chosen,stats.HasTrick(slot),definition.Id+"/"+slot);
                    Assert.AreEqual(slot==chosen,stats.HasEffect(definition.Tricks[slot].EffectId),definition.Id+"/"+slot);
                }
                Assert.IsFalse(stats.HasTrick(-1));Assert.IsFalse(stats.HasTrick(3));
            }
        }
        [Test] public void ExistingMultipleOwnedFlagsArePreservedWithoutCalendarProgression()
        {
            var definition=EronyaRoster.CreateAll().First(d=>d.Id=="alveyr");
            var owned=new OwnedUnit(definition);owned.Tricks[0]=owned.Tricks[2]=true;
            var stats=UnitStats.FromDefinition(definition,owned);
            Assert.IsTrue(stats.HasTrick(0));Assert.IsFalse(stats.HasTrick(1));Assert.IsTrue(stats.HasTrick(2));
            for(int round=0;round<10;round++)Assert.AreEqual(0,EronyaMechanics.HeroStars(round));
            CollectionAssert.AreEqual(new[]{true,false,true},owned.Tricks);
            Assert.IsFalse(owned.IsPositionLocked(5),"Old free unlocks must not manufacture a purchase date or position lock.");
        }
        [Test] public void EnergyAbilitiesResolveTheSameEditablePhaseKeysAsLegacy()
        {
            foreach(var definition in EronyaRoster.CreateAll())
            {
                var ability=definition.BaseAbility;
                var saved=ability.Parameters;
                var resolved=EronyaAbilityParameters.Resolve(ability);
                Assert.AreEqual(definition.Id=="dagata"?.3f:.4f,resolved.Single(p=>p.Key=="castDuration").Value);
                Assert.AreSame(saved,ability.Parameters);
                ability.Parameters=saved.Concat(new[]{new AbilityParameter{Key="castDuration",Value=1.11f},
                    new AbilityParameter{Key="holdDuration",Value=.2f},new AbilityParameter{Key="recoveryDuration",Value=.3f}}).ToArray();
                var stats=UnitStats.FromDefinition(definition);
                Assert.That(AbilityTiming.Cast(stats,ability.EffectId),Is.EqualTo(1.2f).Within(.0001));
                Assert.That(AbilityTiming.Hold(stats,ability.EffectId),Is.EqualTo(.2f).Within(.0001));
                Assert.That(AbilityTiming.Recovery(stats,ability.EffectId),Is.EqualTo(.3f).Within(.0001));
            }
        }
        [Test] public void EditedCastClockSupportsZeroCustomDurationAndOneRefund()
        {
            var mana=new EronyaManaLedger(20,1,0);mana.Add(20);
            var cast=new EronyaCastClock();Assert.IsTrue(cast.Begin(mana,0,1,true,false,0));
            Assert.IsTrue(cast.Ready(0,1));Assert.IsTrue(cast.Preparing);cast.Released();Assert.IsFalse(cast.Preparing);
            mana.Add(20);Assert.IsTrue(cast.Begin(mana,1,1,true,false,11));
            Assert.AreEqual(12,cast.DueTick);Assert.IsFalse(cast.Ready(11,1));Assert.IsTrue(cast.Ready(12,1));
            cast.Interrupt(mana,2,true,3);Assert.AreEqual(20,mana.Value);
            mana.Add(-5);cast.Interrupt(mana,2,true,3);Assert.AreEqual(15,mana.Value);
        }
        [Test] public void MineraDurationDefaultsToInstantAndConsumesEditedTenths()
        {
            var effect=new TrickDefinition {EffectId="la-minera-tunel-subterraneo",Parameters=new[]{new AbilityParameter{Key="distance",Value=2}}};
            Assert.AreEqual(0,AbilityBalance.Value(effect,"duration"));
            effect.Parameters=new[]{new AbilityParameter{Key="distance",Value=2},new AbilityParameter{Key="duration",Value=.74f}};
            Assert.That(AbilityBalance.Value(effect,"duration"),Is.EqualTo(.7f).Within(.0001));
            Assert.AreEqual(2,AbilityBalance.Value(effect,"distance"));
        }
        [Test] public void EditedPoisonAntiHealIsClampedAndBelongsToOneWinningEffect()
        {
            const string id="jabalinero-punta-toxica";
            Assert.IsFalse(EronyaAbilityParameters.IsReadOnlyParameter(id,"antiHealPercent"));
            Assert.AreEqual(0,EronyaAbilityParameters.Normalize(id,"antiHealPercent",-10));
            Assert.AreEqual(100,EronyaAbilityParameters.Normalize(id,"antiHealPercent",200));
            Assert.AreEqual(60,EronyaAbilityParameters.Normalize(id,"antiHealPercent",60));
            var ledger=new EronyaPeriodicLedger();
            ledger.Apply("a",1,"victim",1,id,EronyaPeriodicKind.Poison,3,0,30,poisonAntiHeal:.6m);
            ledger.Apply("b",1,"victim",1,id,EronyaPeriodicKind.Poison,2,1,40,poisonAntiHeal:.8m);
            Assert.AreEqual(1,ledger.Effects.Count);Assert.AreEqual(.6m,ledger.PoisonAntiHeal("victim",1,1));
            Assert.AreEqual(40,EronyaMechanics.HealAmount(100,ledger.PoisonAntiHeal("victim",1,1)));
            ledger.Apply("b",1,"victim",1,id,EronyaPeriodicKind.Poison,4,2,40,poisonAntiHeal:0);
            Assert.AreEqual(0,ledger.PoisonAntiHeal("victim",1,2));Assert.AreEqual(1,ledger.Effects.Count);
            ledger.Cleanse("victim",1);Assert.AreEqual(0,ledger.PoisonAntiHeal("victim",1,2));
        }
        [Test] public void SharedProjectileTravelUsesTheSameFixedTicksForPreviewAndCombat()
        {
            Assert.That(CombatSimulation.GetProjectileTravelTime(2,8),Is.EqualTo(.3f).Within(.0001));
            Assert.That(CombatSimulation.GetProjectileTravelTime(2,2),Is.EqualTo(1f).Within(.0001));
            Assert.That(CombatSimulation.GetProjectileTravelTime(3,6),Is.EqualTo(.5f).Within(.0001));
            Assert.That(CombatSimulation.GetProjectileTravelTime(1,0),Is.EqualTo(10f).Within(.0001));
        }
        [Test] public void ApprovedReplacementParametersPreserveExplicitZeroAndRetireOnlyArmorKeys()
        {
            foreach(var pair in new[]{("zephalontropo-presion-calibrada",4f),("zephalontropo-camara-de-precision",6f),("la-minera-fractura",9f)})
            {
                var effect=new TrickDefinition{EffectId=pair.Item1};
                Assert.AreEqual(pair.Item2,AbilityBalance.Resolve(effect).Single(p=>p.Key=="shieldBonusDamage").Value);
                effect.Parameters=new[]{new AbilityParameter{Key="shieldBonusDamage",Value=0}};
                Assert.AreEqual(0,AbilityBalance.Resolve(effect).Single(p=>p.Key=="shieldBonusDamage").Value);
                Assert.IsFalse(AbilityBalance.IsRetiredArmorEffect(pair.Item1));
            }
            foreach(string id in new[]{"guarda-veterano-dialogo-con-la-raiz","protectora-del-mausoleo-memoria-de-la-roca","protectora-del-mausoleo-memoria-compartida"})
            {Assert.IsFalse(AbilityBalance.IsRetiredArmorEffect(id));Assert.IsTrue(AbilityBalance.IsRetiredArmorParameter(id,"armor"));}
            var guard=AbilityBalance.Resolve(new TrickDefinition{EffectId="guarda-veterano-dialogo-con-la-raiz"});
            Assert.AreEqual(10,guard.Single(p=>p.Key=="reduction").Value);Assert.AreEqual(2,guard.Single(p=>p.Key=="stationaryDuration").Value);
        }
        [Test] public void FreshAuthoringNeverRestoresPdfEnergyArmorOrFreeMonsterPrices()
        {
            foreach(var unit in EronyaRoster.CreateAll())
            {
                Assert.AreEqual(0,unit.BaseArmor,unit.Id);Assert.AreEqual(0,unit.EnergyPerSecond,unit.Id);
                Assert.That(unit.EnergyMax,Is.EqualTo(20).Or.EqualTo(25),unit.Id);
                Assert.That(unit.EnergyPerAttack,Is.InRange(1,2),unit.Id);
                if(unit.IsMonster)Assert.IsTrue(unit.Tricks.All(t=>t.Cost>0),unit.Id);
            }
        }
        [Test] public void MineraFirstHitTauntDefaultsToTwoSecondsAndPreservesEditedZero()
        {
            const string effect="la-minera-tunel-subterraneo";
            Assert.AreEqual(2,AbilityBalance.Resolve(new TrickDefinition{EffectId=effect}).Single(p=>p.Key=="tauntDuration").Value);
            foreach(float value in new[]{0f,.5f,3f})
            {
                var edited=new TrickDefinition{EffectId=effect,Parameters=new[]{new AbilityParameter{Key="tauntDuration",Value=value}}};
                Assert.AreEqual(value,AbilityBalance.Resolve(edited).Single(p=>p.Key=="tauntDuration").Value);
            }
        }
    }

    // Real BoardManager/BattleUnit/scheduler tests. Compile outside Unity; execute only in native runner.
    public sealed class EronyaLegacyParityNativeTests
    {
        GameObject root;BoardManager board;CombatSimulation simulation;MatchConfig config;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [SetUp]public void Setup(){root=new GameObject("legacy-parity");board=root.AddComponent<BoardManager>();board.BuildBoard();}
        [TearDown]public void Cleanup(){simulation?.Stop();Object.DestroyImmediate(root);if(config!=null)Object.DestroyImmediate(config);}
        UnitDefinition Definition(string id="alveyr",int range=1)
        {
            var d=EronyaRoster.CreateAll().First(x=>x.Id==id);d.MaxHealth=1000;d.Damage=7;d.AttackRange=range;
            d.AttackInterval=.8f;d.AttackWindup=.2f;d.AttackHold=.1f;d.AttackRecovery=.1f;d.MoveInterval=.2f;
            d.EnergyMax=25;d.EnergyPerAttack=1;d.EnergyOnDamage=2;d.EnergyPerSecond=0;return d;
        }
        BattleUnit Unit(UnitDefinition d,BoardSide side,int x,int y,int chosen=-1)
        {
            var go=new GameObject(side+"/"+d.Id+"/"+x+"/"+y);go.transform.SetParent(root.transform);
            var owned=new OwnedUnit(d);if(chosen>=0)owned.Tricks[chosen]=true;
            var unit=go.AddComponent<MonsterUnit>();unit.Initialize(go.name,side,UnitStats.FromDefinition(d,owned));
            Assert.IsTrue(board.TryOccupyCell(unit,x,y));return unit;
        }
        BattleUnit Dummy(BoardSide side,int x,int y)=>Unit(new UnitDefinition{Id="dummy-fixture",MaxHealth=1000,
            Damage=0,AttackRange=1,AttackInterval=1000,MoveInterval=1000},side,x,y);
        void Begin(BattleUnit actor,params BattleUnit[] others)
        {simulation=new CombatSimulation(board,1,0);simulation.Begin(new[]{actor}.Concat(others).ToArray());foreach(var u in others)u.IsPassiveTrainingTarget=true;}
        object Clock(BattleUnit unit)=>typeof(CombatSimulation).GetMethod("Clock",Private).Invoke(simulation,new object[]{unit});
        CombatTargetSelection Select(BattleUnit unit)=>(CombatTargetSelection)typeof(CombatSimulation).GetMethod("EronyaSelectBasicAction",Private).Invoke(simulation,new[]{Clock(unit)});
        void Field(BattleUnit unit,string name,object value)=>Clock(unit).GetType().GetField(name).SetValue(Clock(unit),value);
        bool ApplyTaunt(BattleUnit source,BattleUnit target)
        {
            var method=typeof(CombatSimulation).GetMethod("EronyaApplyStatus",Private);
            var parameters=method.GetParameters();
            return (bool)method.Invoke(simulation,new object[]{Clock(source),Clock(target),
                Enum.Parse(parameters[2].ParameterType,"Taunt"),"fixture-taunt",1f,100,
                Enum.Parse(parameters[6].ParameterType,"RefreshStrongest")});
        }
        object InvokeCore(string name,params object[] supplied)
        {
            var method=typeof(CombatSimulation).GetMethod(name,Private);var p=method.GetParameters();
            var args=p.Select(x=>x.DefaultValue).ToArray();Array.Copy(supplied,args,supplied.Length);return method.Invoke(simulation,args);
        }
        decimal GrantShield(BattleUnit target,string kind,int until=100)
        {
            decimal amount=0;
            if(kind=="legacy"||kind=="both"||kind=="fraction")
            {typeof(BattleUnit).GetMethod("AddShield",Private).Invoke(target,new object[]{3,until});amount=3;}
            if(kind=="fish")
            {typeof(BattleUnit).GetMethod("RefreshFishShield",Private).Invoke(target,new object[]{3,target,until});amount=3;}
            if(kind=="eronya"||kind=="both")
            {InvokeCore("EronyaShield",Clock(target),Clock(target),"fixture-shield",3,until);amount+=3;}
            if(kind=="fraction")
            {typeof(BattleUnit).GetMethod("AbsorbPreciseLegacyShield",Private).Invoke(target,new object[]{2.75m,null});amount=.25m;}
            return amount;
        }
        void Damage(BattleUnit source,BattleUnit target,float amount)
        {InvokeCore("EronyaDealDamage",Clock(source),Clock(target),amount,"fixture-damage");InvokeCore("EronyaFlushPackets");}

        [TestCase(1,1,1)][TestCase(1,-1,1)][TestCase(1,1,-1)][TestCase(1,-1,-1)]
        [TestCase(2,1,1)][TestCase(2,-1,1)][TestCase(2,1,-1)][TestCase(2,-1,-1)]
        public void DiagonalBasicsHitOneTargetUsingEditedRange(int range,int sx,int sy)
        {
            var actor=Unit(Definition(range:range),BoardSide.Blue,2,4);
            var enemy=Dummy(BoardSide.Red,2+range*sx,4+range*sy);var other=Dummy(BoardSide.Red,2,9);
            Begin(actor,enemy,other);Assert.AreSame(enemy,Select(actor).Target);
            var origin=actor.CurrentCell;simulation.Step(.1f);
            Assert.AreSame(actor,simulation.CaptureActionPreparation(actor).Source);
            simulation.Step(.5f);Assert.Less(enemy.CurrentHealth,1000);Assert.AreEqual(1000,other.CurrentHealth);
            Assert.AreSame(origin,actor.CurrentCell);Assert.AreEqual(1,actor.Energy);
        }
        [Test] public void BlockedCornerStillAttacksItsDiagonalNeighbour()
        {
            var actor=Unit(Definition(),BoardSide.Blue,0,0);var enemy=Dummy(BoardSide.Red,1,1);
            var a=Dummy(BoardSide.Blue,1,0);var b=Dummy(BoardSide.Blue,0,1);
            Begin(actor,enemy,a,b);Assert.AreEqual(CombatTargetSelectionStatus.ReadyToAttack,Select(actor).Status);
            simulation.Step(.4f);Assert.Less(enemy.CurrentHealth,1000);Assert.IsNull(actor.ReservedCell);
        }
        [TestCase(TargetPolicy.NearestReachable)][TestCase(TargetPolicy.LowestHealth)]
        public void AcquisitionAndMovementMatchLegacyPolicy(TargetPolicy policy)
        {
            var d=Definition();d.TargetPolicy=policy;var actor=Unit(d,BoardSide.Blue,0,0);
            var near=Dummy(BoardSide.Red,1,1);var weak=Dummy(BoardSide.Red,4,4);Begin(actor,near,weak);weak.ApplyDamage(700);
            var expected=CombatTargetSelector.SelectTarget(board,actor,new[]{near,weak});var actual=Select(actor);
            Assert.AreSame(expected.Target,actual.Target);Assert.AreSame(expected.AttackCell,actual.AttackCell);
            Assert.AreSame(expected.NextCell,actual.NextCell);Assert.AreEqual(expected.Status,actual.Status);
            simulation.Step(.1f);
            if(policy==TargetPolicy.LowestHealth)Assert.AreSame(expected.NextCell,actor.ReservedCell);
        }
        [Test] public void OutOfRangeAndInvisibleLocksYieldToAnAttackableTargetButTauntPersists()
        {
            var actor=Unit(Definition(),BoardSide.Blue,2,4);var near=Dummy(BoardSide.Red,3,5);var far=Dummy(BoardSide.Red,4,8);
            Begin(actor,near,far);Field(actor,"LockedTarget",far);Field(actor,"TargetResetVersion",far.CombatResetVersion);
            Assert.AreSame(near,Select(actor).Target);
            Field(actor,"TauntTarget",far);Field(actor,"TaunterResetVersion",far.CombatResetVersion);Field(actor,"TauntUntil",20);
            Assert.AreSame(far,Select(actor).Target);
            Field(far,"StealthUntil",20);Assert.AreSame(near,Select(actor).Target);
        }
        [Test] public void RootStopsMovementAndTargetEscapePreventsPreparedRemoteDamage()
        {
            var actor=Unit(Definition(),BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,3,5);
            Begin(actor,enemy);simulation.Step(.1f);Field(actor,"RootUntil",100);
            Assert.IsTrue(board.TryRepositionUnit(enemy,board.GetCell(4,0)));simulation.Step(.8f);
            Assert.AreEqual(1000,enemy.CurrentHealth);Assert.IsNull(actor.ReservedCell);Assert.AreEqual(0,actor.Energy);
        }
        [TestCase(2f)][TestCase(8f)]
        public void ProjectileSpeedDelaysImpactAndHoldRecoveryDelayTheNextAction(float speed)
        {
            var d=Definition(range:2);d.ProjectileSpeed=speed;d.AttackHold=.5f;d.AttackRecovery=.5f;
            var actor=Unit(d,BoardSide.Blue,2,4);var enemy=Dummy(BoardSide.Red,4,6);Begin(actor,enemy);
            int launches=0;simulation.Attacked+=(a,t,p)=>{if(a==actor)launches++;};
            int impactTick=1+AbilityTiming.Ticks(d.AttackWindup)+AbilityTiming.Ticks(CombatSimulation.GetProjectileTravelTime(actor,enemy));
            simulation.Step((impactTick-1)*.1f);Assert.AreEqual(1000,enemy.CurrentHealth);Assert.AreEqual(1,launches);
            simulation.Step(.1f);Assert.Less(enemy.CurrentHealth,1000);Assert.AreEqual(1,actor.Energy);
        }
        [Test] public void AcceptedTauntRestartsAnUnreleasedBasicAgainstTheTaunter()
        {
            var d=Definition(range:2);d.AttackWindup=1;d.ProjectileSpeed=100;
            var actor=Unit(d,BoardSide.Blue,2,6);var original=Dummy(BoardSide.Red,2,4);var taunter=Dummy(BoardSide.Red,3,4);
            Begin(actor,original,taunter);var targets=new List<BattleUnit>();
            simulation.Attacked+=(a,t,p)=>{if(a==actor)targets.Add(t);};
            simulation.Step(.1f);Assert.AreSame(original,targets.Single());Assert.IsTrue(ApplyTaunt(taunter,actor));
            simulation.Step(.1f);Assert.AreSame(taunter,targets.Last());Assert.AreEqual(2,targets.Count);
            simulation.Step(1.1f);Assert.AreEqual(1000,original.CurrentHealth);Assert.AreEqual(993,taunter.CurrentHealth);
            Assert.AreEqual(1,actor.Energy,"Only the redirected landed basic grants energy.");
        }
        [Test] public void AcceptedTauntDoesNotRecallAnAlreadyReleasedBasicProjectile()
        {
            var d=Definition(range:2);d.ProjectileSpeed=2;d.AttackInterval=100;
            var actor=Unit(d,BoardSide.Blue,2,6);var original=Dummy(BoardSide.Red,2,4);var taunter=Dummy(BoardSide.Red,3,4);
            Begin(actor,original,taunter);simulation.Step(.3f);Assert.IsTrue(ApplyTaunt(taunter,actor));
            actor.IsPassiveTrainingTarget=true;simulation.Step(1);
            Assert.AreEqual(993,original.CurrentHealth);Assert.AreEqual(1000,taunter.CurrentHealth);Assert.AreEqual(1,actor.Energy);
        }
        [TestCase(true,false)][TestCase(false,false)][TestCase(true,true)]
        public void IqCancellationReleasesOnlyTheCancelledBasicLock(bool replacementInRange,bool committedAbilityRecovery)
        {
            var d=Definition();d.AttackWindup=2;d.AttackHold=.6f;d.AttackRecovery=.3f;d.IQSpeed=10;
            var actor=Unit(d,BoardSide.Blue,2,6);var original=Dummy(BoardSide.Red,2,5);var replacement=Dummy(BoardSide.Red,4,0);
            Begin(actor,original,replacement);var targets=new List<BattleUnit>();
            simulation.Attacked+=(a,t,p)=>{if(a==actor)targets.Add(t);};
            simulation.Step(.1f);Assert.AreSame(original,targets.Single());
            // Model an independent already-committed ability deadline while the basic is pending.
            if(committedAbilityRecovery)Field(actor,"AbilityMotionUntil",50);
            Assert.IsTrue(board.TryRepositionUnit(original,board.GetCell(0,0)));
            if(replacementInRange)Assert.IsTrue(board.TryRepositionUnit(replacement,board.GetCell(3,6)));
            simulation.Step(.2f);Assert.AreEqual(1000,original.CurrentHealth);Assert.AreEqual(0,actor.Energy);
            if(committedAbilityRecovery)
            {
                Assert.AreEqual(1,targets.Count);Assert.IsNull(actor.ReservedCell);
                Assert.AreEqual(50,Clock(actor).GetType().GetField("AbilityMotionUntil").GetValue(Clock(actor)));
                Assert.GreaterOrEqual((int)Clock(actor).GetType().GetField("NextAttack").GetValue(Clock(actor)),50);
            }
            else if(replacementInRange){Assert.AreEqual(2,targets.Count);Assert.AreSame(replacement,targets.Last());}
            else Assert.NotNull(actor.ReservedCell,"IQ cancellation must permit moving before the old windup/tails finish.");
        }
        [Test] public void SelectedThirdTrickRunsWithoutGrantingTheFirstTwo()
        {
            var d=Definition("cael");d.AttackInterval=100;
            // This checks slot ownership, independently of the current roster balance.
            d.Tricks[2].Parameters.Single(p=>p.Key=="damage").Value=25;
            var actor=Unit(d,BoardSide.Blue,2,6,2);var enemy=Dummy(BoardSide.Red,2,5);Begin(actor,enemy);
            actor.ApplyDamage(100);actor.AddEnergy(25);simulation.Step(.5f);
            Assert.AreEqual(975,enemy.CurrentHealth);Assert.AreEqual(900,actor.CurrentHealth);
            Assert.IsTrue(enemy.IsRooted);Assert.IsFalse(enemy.IsSlowed);
        }
        [TestCase(false)][TestCase(true)] public void PalistaBasicAndSuperShareTheEditedDiagonalReach(bool super)
        {
            var d=Definition("palista");var actor=Unit(d,BoardSide.Blue,2,4);var enemy=Dummy(BoardSide.Red,3,5);
            Begin(actor,enemy);if(super)actor.AddEnergy(25);simulation.Step(.5f);
            Assert.AreEqual(super?988:993,enemy.CurrentHealth);
        }
        [Test] public void EditedSuperPreparationAndRecoveryControlRealReleaseAndNextAction()
        {
            var d=Definition("palista");d.BaseAbility.Parameters.First(p=>p.Key=="pushCells").Value=0;
            d.BaseAbility.Parameters=d.BaseAbility.Parameters.Concat(new[]{new AbilityParameter{Key="castDuration",Value=.7f},
                new AbilityParameter{Key="holdDuration",Value=.3f},new AbilityParameter{Key="recoveryDuration",Value=.4f}}).ToArray();
            var actor=Unit(d,BoardSide.Blue,2,4);var enemy=Dummy(BoardSide.Red,3,5);Begin(actor,enemy);actor.AddEnergy(25);
            int basics=0;simulation.Attacked+=(a,t,p)=>{if(a==actor)basics++;};
            simulation.Step(.7f);Assert.AreEqual(1000,enemy.CurrentHealth);
            Assert.That(simulation.CaptureActionPreparation(actor).Progress,Is.GreaterThan(.8f));
            simulation.Step(.1f);Assert.AreEqual(988,enemy.CurrentHealth);
            simulation.Step(.6f);Assert.AreEqual(0,basics);
            simulation.Step(.1f);Assert.AreEqual(1,basics);
        }
        [Test] public void MixedMovementContentionUsesLegacyIqReservationAndOrthogonalSteps()
        {
            var d=Definition();d.IQSpeed=1;var actor=Unit(d,BoardSide.Blue,0,2);
            var legacy=Unit(new UnitDefinition{Id="legacy-reservation",MaxHealth=1000,Damage=1,AttackRange=1,
                AttackInterval=100,MoveInterval=.2f,IQSpeed=100},BoardSide.Red,2,2);
            simulation=new CombatSimulation(board,1,0);simulation.Begin(new[]{actor,legacy});
            var first=CombatTargetSelector.SelectTarget(board,actor,new[]{legacy});
            var second=CombatTargetSelector.SelectTarget(board,legacy,new[]{actor});
            Assert.AreSame(first.NextCell,second.NextCell);
            var reference=BoardMovementResolver.ReserveMovement(board,new List<BoardMovementIntent>{
                new BoardMovementIntent(actor,first.AttackCell),new BoardMovementIntent(legacy,second.AttackCell)});
            var expected=reference.Single(r=>r.Status==BoardMovementStatus.Reserved).Unit;
            board.CancelReservation(actor);board.CancelReservation(legacy);simulation.Step(.1f);
            Assert.AreSame(expected,first.NextCell.ReservedBy);Assert.AreSame(legacy,expected);
            Assert.AreEqual(1,CombatTargetSelector.ManhattanDistance(legacy.CurrentCell,legacy.ReservedCell));
        }
        [Test] public void MineraUsesEditedUndergroundTimeAndResetClearsIt()
        {
            var d=Definition("la-minera");d.PassiveAbility.Parameters=new[]{new AbilityParameter{Key="distance",Value=2},new AbilityParameter{Key="duration",Value=.7f}};
            var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,3,4);
            simulation=new CombatSimulation(board,1,0);CombatDisplacementSnapshot move=default;simulation.Displaced+=s=>move=s;
            simulation.Begin(new[]{actor,enemy});enemy.IsPassiveTrainingTarget=true;
            Assert.AreEqual(CombatDisplacementKind.Tunnel,move.Kind);Assert.AreEqual(7,move.EndTick-move.StartTick);Assert.IsFalse(move.IsAuthoritativeInstant);
            Assert.IsTrue(actor.IsFlying);Assert.AreEqual(4,actor.CurrentCell.Y);
            simulation.Step(.6f);Assert.IsTrue(actor.IsFlying);Assert.AreEqual(1000,enemy.CurrentHealth);
            simulation.Step(.1f);Assert.IsFalse(actor.IsFlying);
            simulation.Stop();Assert.IsFalse(actor.IsFlying);actor.ResetForCombat();Assert.AreEqual(0,actor.FlightRemaining);
        }
        [Test] public void MineraWithoutDurationRetainsInstantRelocation()
        {
            var d=Definition("la-minera");d.PassiveAbility.Parameters=d.PassiveAbility.Parameters.Where(p=>p.Key!="duration").ToArray();
            var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,4,9);
            Begin(actor,enemy);Assert.IsFalse(actor.IsFlying);Assert.AreEqual(1,actor.CurrentCell.Y);
        }
        [Test] public void JabalineroEditedAntiHealChangesActualHealingAndStatusDisplay()
        {
            var d=Definition("jabalinero");
            d.PassiveAbility.Parameters.Single(p=>p.Key=="everyHits").Value=1;
            d.PassiveAbility.Parameters.Single(p=>p.Key=="antiHealPercent").Value=60;
            var actor=Unit(d,BoardSide.Blue,2,4);var enemy=Dummy(BoardSide.Red,3,5);Begin(actor,enemy);enemy.ApplyDamage(200);
            simulation.Step(.5f);decimal before=enemy.PreciseHealth;
            Assert.That(simulation.CaptureStatusFrame(enemy).Statuses.Single(s=>s.Id=="anti-heal").Magnitude,Is.EqualTo(.6f).Within(.0001));
            typeof(CombatSimulation).GetMethod("EronyaHeal",Private).Invoke(simulation,new[]{Clock(actor),Clock(enemy),(object)100,"fixture-heal"});
            typeof(CombatSimulation).GetMethod("EronyaFlushPackets",Private).Invoke(simulation,null);
            Assert.AreEqual(before+40,enemy.PreciseHealth);
        }
        [TestCase("zephalontropo",2f,18,9)][TestCase("zephalontropo",8f,18,9)]
        [TestCase("canonero-del-orvek",2f,15,15)][TestCase("canonero-del-orvek",8f,15,15)]
        [TestCase("jabalinero",2f,7,0)][TestCase("jabalinero",8f,7,0)]
        public void ProjectileSupersDeliverDamageControlAndAreaOnlyAtArrival(string id,float speed,int damage,int secondaryDamage)
        {
            var d=Definition(id);d.ProjectileSpeed=speed;d.AttackInterval=100;
            var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,2,4);var secondary=Dummy(BoardSide.Red,3,4);
            Begin(actor,enemy,secondary);CombatAttackPresentationSnapshot launch=null;int areas=0;
            simulation.Attacked+=(a,t,p)=>{if(a==actor&&simulation.CurrentAttackPresentation.IsAbility)launch=simulation.CurrentAttackPresentation;};
            simulation.AreaEffect+=(a,e,c)=>{if(a==actor&&e==d.BaseAbility.EffectId)areas++;};
            actor.AddEnergy(25);simulation.Step(.5f);actor.IsPassiveTrainingTarget=true;
            Assert.NotNull(launch);Assert.AreEqual(0,launch.PreparationSeconds);
            Assert.That(launch.TravelSeconds,Is.EqualTo(CombatSimulation.GetProjectileTravelTime(2,speed)).Within(.0001));
            Assert.AreEqual(0,areas);Assert.AreEqual(1000,enemy.CurrentHealth);Assert.IsFalse(enemy.IsRooted);
            simulation.Step(launch.TravelSeconds-.1f);Assert.AreEqual(1000,enemy.CurrentHealth);Assert.AreEqual(0,areas);
            simulation.Step(.1f);Assert.AreEqual(1000-damage,enemy.CurrentHealth);Assert.AreEqual(1000-secondaryDamage,secondary.CurrentHealth);
            Assert.AreEqual(1,areas);if(id=="jabalinero")Assert.IsTrue(enemy.IsRooted);
        }
        [Test] public void ReleasedNetFollowsItsBoundTargetWithoutASecondRangeCheck()
        {
            var d=Definition("jabalinero");d.ProjectileSpeed=2;
            var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,2,4);Begin(actor,enemy);
            BoardCell impactCell=null;simulation.AreaEffect+=(a,e,c)=>{if(e==d.BaseAbility.EffectId)impactCell=c[0];};
            actor.AddEnergy(25);simulation.Step(.5f);actor.IsPassiveTrainingTarget=true;
            Assert.IsTrue(board.TryRepositionUnit(enemy,board.GetCell(4,0)));simulation.Step(1);
            Assert.AreEqual(993,enemy.CurrentHealth);Assert.IsTrue(enemy.IsRooted);Assert.AreSame(enemy.CurrentCell,impactCell);
        }
        [Test] public void ResetInvalidatesAnAlreadyReleasedProjectile()
        {
            var d=Definition("jabalinero");d.ProjectileSpeed=2;
            var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,2,4);Begin(actor,enemy);
            int areas=0;simulation.AreaEffect+=(a,e,c)=>{if(e==d.BaseAbility.EffectId)areas++;};
            actor.AddEnergy(25);simulation.Step(.5f);actor.IsPassiveTrainingTarget=true;enemy.ResetForCombat();simulation.Step(1);
            Assert.AreEqual(1000,enemy.CurrentHealth);Assert.IsFalse(enemy.IsRooted);Assert.AreEqual(0,areas);
        }
        [TestCase("zephalontropo",18)][TestCase("canonero-del-orvek",15)][TestCase("jabalinero",7)]
        public void SourceDeathDoesNotRecallAReleasedProjectile(string id,int damage)
        {
            var d=Definition(id);d.ProjectileSpeed=2;
            var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,2,4);var survivor=Dummy(BoardSide.Blue,0,9);
            Begin(actor,enemy,survivor);actor.AddEnergy(25);simulation.Step(.5f);actor.ApplyDamage(1000);simulation.Step(1);
            Assert.AreEqual(1000-damage,enemy.CurrentHealth);
        }
        [TestCase("legacy",-1,-1,4)][TestCase("fish",-1,-1,4)][TestCase("eronya",-1,-1,4)]
        [TestCase("both",-1,-1,4)][TestCase("fraction",-1,-1,4)][TestCase("none",-1,-1,0)]
        [TestCase("legacy",0,-1,6)][TestCase("legacy",0,0,0)]
        public void ZephalontropoAddsOneFlatBonusBeforeAnyExistingShieldIsConsumed(string kind,int slot,int edited,int bonus)
        {
            var d=Definition("zephalontropo");d.Damage=8;d.AttackInterval=100;
            if(edited>=0)d.Tricks[0].Parameters.Single(p=>p.Key=="shieldBonusDamage").Value=edited;
            var actor=Unit(d,BoardSide.Blue,2,6,slot);
            // Decimal health is an Eronya contract; legacy targets intentionally round health damage.
            var enemy=kind=="fraction"?Unit(Definition("zephalontropo"),BoardSide.Red,3,6):Dummy(BoardSide.Red,3,6);
            Begin(actor,enemy);
            decimal shield=GrantShield(enemy,kind);simulation.Step(.3f);
            Assert.AreEqual(1000m-(8+bonus-shield),enemy.PreciseHealth);Assert.AreEqual(0,enemy.Shield);
            Assert.AreEqual(1,actor.Energy,"Bonus stays in the same basic packet.");
        }
        [TestCase(true)][TestCase(false)]
        public void ZephalontropoChecksShieldsAtArrivalAfterTheyAppearOrExpireInFlight(bool gained)
        {
            var d=Definition("zephalontropo",2);d.Damage=8;d.ProjectileSpeed=2;d.AttackInterval=100;
            var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,2,4);Begin(actor,enemy);
            if(!gained)GrantShield(enemy,"legacy",5);simulation.Step(.3f);
            if(gained)GrantShield(enemy,"legacy");simulation.Step(1);
            Assert.AreEqual(gained?991:992,enemy.CurrentHealth);Assert.AreEqual(1,actor.Energy);
        }
        [Test] public void ZephalontropoBonusNeverAppliesToAbilityPeriodicOrReflectionPackets()
        {
            var actor=Unit(Definition("zephalontropo"),BoardSide.Blue,2,6,0);var enemy=Dummy(BoardSide.Red,3,6);Begin(actor,enemy);
            typeof(BattleUnit).GetMethod("AddShield",Private).Invoke(enemy,new object[]{100,100});
            var origin=typeof(CombatSimulation).GetMethod("EronyaDealDamage",Private).GetParameters()[4].ParameterType;
            foreach(string kind in new[]{"Ability","Periodic","Reflection"})
            {InvokeCore("EronyaDealDamage",Clock(actor),Clock(enemy),8f,"fixture-damage",Enum.Parse(origin,kind));InvokeCore("EronyaFlushPackets");}
            Assert.AreEqual(76,enemy.Shield);Assert.AreEqual(1000,enemy.CurrentHealth);Assert.AreEqual(0,actor.Energy);
        }
        [TestCase("legacy",9)][TestCase("fish",9)][TestCase("eronya",9)][TestCase("none",9)]
        [TestCase("legacy",0)][TestCase("legacy",5)]
        public void FractureAddsItsEditedShieldBonusOnlyToThePrimaryPacket(string kind,int bonus)
        {
            var d=Definition("la-minera");d.PassiveAbility.Parameters.Single(p=>p.Key=="distance").Value=0;
            d.PassiveAbility.Parameters.Single(p=>p.Key=="duration").Value=0;
            d.BaseAbility.Parameters.Single(p=>p.Key=="shieldBonusDamage").Value=bonus;
            int collapse=Array.FindIndex(d.Tricks,t=>t.EffectId=="la-minera-derrumbe");Assert.GreaterOrEqual(collapse,0);
            var actor=Unit(d,BoardSide.Blue,2,6,collapse);var enemy=Dummy(BoardSide.Red,3,6);var secondary=Dummy(BoardSide.Red,4,6);
            Begin(actor,enemy,secondary);decimal shield=GrantShield(enemy,kind);GrantShield(secondary,"legacy");
            Assert.AreSame(board.GetCell(2,5),actor.CurrentCell,"Keep the original tunnel setup: its actual exit leaves a legal diagonal target.");
            Assert.AreEqual(CombatTargetSelectionStatus.ReadyToAttack,Select(actor).Status);
            int fractureCasts=0;simulation.AbilityUsed+=(a,e)=>{if(a==actor&&e=="la-minera-fractura")fractureCasts++;};
            actor.AddEnergy(25);simulation.Step(.5f);
            Assert.AreEqual(1,fractureCasts,"This test must exercise Fracture, not a fallback basic.");
            Assert.AreEqual(1000m-(17+(shield>0?bonus:0)-shield),enemy.PreciseHealth);
            Assert.AreEqual(995,secondary.CurrentHealth,"Derrumbe does not inherit Fracture's bonus.");Assert.AreEqual(0,actor.Energy);
        }
        [TestCase("la-minera",1,1)][TestCase("la-minera",1,-1)]
        [TestCase("la-minera",-1,1)][TestCase("la-minera",-1,-1)]
        [TestCase("golem-tectonico",1,1)][TestCase("golem-tectonico",1,-1)]
        [TestCase("golem-tectonico",-1,1)][TestCase("golem-tectonico",-1,-1)]
        public void GeotharMeleeSupersCastOnEveryDiagonalAfterEnergyIsGrantedDuringCombat(string id,int dx,int dy)
        {
            var d=id=="la-minera"?MineraAtOneCellExit():Definition(id);d.AttackInterval=10;
            var actor=Unit(d,BoardSide.Blue,2,id=="la-minera"?7:6);var enemy=Dummy(BoardSide.Red,2+dx,6+dy);
            Begin(actor,enemy);var origin=actor.CurrentCell;
            Assert.AreEqual(CombatTargetSelectionStatus.ReadyToAttack,Select(actor).Status);
            simulation.Step(3);Assert.Less(enemy.PreciseHealth,1000m,"The diagonal basic target must already be active.");
            decimal healthBefore=enemy.PreciseHealth;int casts=0;
            simulation.AbilityUsed+=(a,e)=>{if(a==actor&&e==d.BaseAbility.EffectId){casts++;actor.IsPassiveTrainingTarget=true;}};
            actor.AddEnergy(25);simulation.Step(1);
            Assert.AreEqual(1,casts);Assert.AreEqual(0,actor.Energy);Assert.AreSame(origin,actor.CurrentCell);
            if(id=="la-minera")Assert.AreEqual(healthBefore-17,enemy.PreciseHealth);
            else
            {
                Assert.AreEqual(22,actor.Shield);Assert.AreEqual(healthBefore,enemy.PreciseHealth);
                Assert.AreEqual(actor.UnitId,simulation.CaptureStatusFrame(enemy).Statuses.Single(s=>s.Id=="taunt").SourceUnitId);
            }
        }
        [TestCase("la-minera",0,false)][TestCase("la-minera",2,true)]
        [TestCase("golem-tectonico",0,false)][TestCase("golem-tectonico",2,true)]
        public void GeotharMeleeSupersPreserveTheirEditedRangeIndependentlyOfBasicRange(string id,int range,bool casts)
        {
            var d=id=="la-minera"?MineraAtOneCellExit():Definition(id);d.AttackRange=2;d.AttackInterval=100;
            d.BaseAbility.Parameters.Single(p=>p.Key=="range").Value=range;
            var actor=Unit(d,BoardSide.Blue,2,id=="la-minera"?7:6);var enemy=Dummy(BoardSide.Red,4,4);
            Begin(actor,enemy);int count=0;simulation.AbilityUsed+=(a,e)=>{if(a==actor&&e==d.BaseAbility.EffectId){count++;actor.IsPassiveTrainingTarget=true;}};
            actor.AddEnergy(25);simulation.Step(1);
            Assert.AreEqual(casts?1:0,count);Assert.AreEqual(casts?0:25,actor.Energy);
            if(casts)Assert.AreEqual(id=="la-minera"?983m:1000m,enemy.PreciseHealth);
        }
        [TestCase("la-minera")][TestCase("golem-tectonico")]
        public void GeotharMeleeSupersRefundWhenTheirTargetLeavesRangeDuringPreparation(string id)
        {
            var d=id=="la-minera"?MineraAtOneCellExit():Definition(id);
            var actor=Unit(d,BoardSide.Blue,2,id=="la-minera"?7:6);var enemy=Dummy(BoardSide.Red,3,5);
            Begin(actor,enemy);int count=0;simulation.AbilityUsed+=(a,e)=>{if(a==actor&&e==d.BaseAbility.EffectId)count++;};
            actor.AddEnergy(25);simulation.Step(.1f);Assert.AreEqual(0,actor.Energy,"The legal diagonal starts preparation.");
            Assert.IsTrue(board.TryRepositionUnit(enemy,board.GetCell(4,0)));Field(actor,"RootUntil",100);
            simulation.Step(.4f);Assert.AreEqual(0,count);Assert.AreEqual(25,actor.Energy);
            Assert.AreEqual(1000,enemy.CurrentHealth);Assert.AreEqual(0,actor.Shield);
        }
        [Test] public void FractureDiagonalReachDoesNotExpandDerrumbeArea()
        {
            var d=MineraAtOneCellExit();var actor=Unit(d,BoardSide.Blue,2,7,2);
            var primary=Dummy(BoardSide.Red,3,5);var orthogonal=Dummy(BoardSide.Red,4,5);var diagonal=Dummy(BoardSide.Red,4,4);
            Begin(actor,primary,orthogonal,diagonal);actor.AddEnergy(25);simulation.Step(.5f);
            Assert.AreEqual(983,primary.CurrentHealth);Assert.AreEqual(992,orthogonal.CurrentHealth);
            Assert.AreEqual(1000,diagonal.CurrentHealth,"Derrumbe retains its existing Manhattan radius.");
        }
        UnitDefinition SeryaWithEditedPrimaryDamage(int range=1)
        {
            var d=Definition("serya",range);d.AttackInterval=10;
            d.BaseAbility.Parameters.Single(p=>p.Key=="range").Value=range;
            d.BaseAbility.Parameters.Single(p=>p.Key=="damage").Value=18;
            d.BaseAbility.Parameters.Single(p=>p.Key=="secondaryDamage").Value=9;
            return d;
        }
        [TestCase(1,1)][TestCase(1,-1)][TestCase(-1,1)][TestCase(-1,-1)]
        public void SeryaSuperAcceptsEveryDiagonalBeforeHerFirstBasic(int dx,int dy)
        {
            var d=SeryaWithEditedPrimaryDamage();var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,2+dx,6+dy);
            Begin(actor,enemy);int casts=0;simulation.AbilityUsed+=(a,e)=>{if(a==actor&&e==d.BaseAbility.EffectId)casts++;};
            actor.AddEnergy(25);simulation.Step(.5f);
            Assert.AreEqual(1,casts);Assert.AreEqual(0,actor.Energy);Assert.AreEqual(982,enemy.CurrentHealth);
            Assert.IsTrue(simulation.CaptureStatusFrame(enemy).Statuses.Any(s=>s.Id=="burn"));
        }
        [Test] public void SeryaSuperStartsAfterAMidCombatEnergyGrantAgainstHerDiagonalBasicTarget()
        {
            var d=SeryaWithEditedPrimaryDamage();var actor=Unit(d,BoardSide.Blue,2,3);var enemy=Dummy(BoardSide.Red,3,2);
            Begin(actor,enemy);simulation.Step(3);Assert.Less(enemy.CurrentHealth,1000);
            int casts=0;simulation.AbilityUsed+=(a,e)=>{if(a==actor&&e==d.BaseAbility.EffectId){casts++;actor.IsPassiveTrainingTarget=true;}};
            actor.AddEnergy(25);simulation.Step(1);
            Assert.AreEqual(1,casts);Assert.AreEqual(0,actor.Energy);Assert.AreSame(board.GetCell(2,3),actor.CurrentCell);
        }
        [TestCase(0,false)][TestCase(2,true)]
        public void SeryaSuperHonorsItsEditedRangeWithoutChangingHerBasicRange(int range,bool casts)
        {
            var d=SeryaWithEditedPrimaryDamage(2);d.BaseAbility.Parameters.Single(p=>p.Key=="range").Value=range;
            var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,4,4);Begin(actor,enemy);
            int count=0;simulation.AbilityUsed+=(a,e)=>{if(a==actor&&e==d.BaseAbility.EffectId)count++;};
            actor.AddEnergy(25);simulation.Step(.5f);
            Assert.AreEqual(casts?1:0,count);Assert.AreEqual(casts?0:25,actor.Energy);
            if(casts)Assert.AreEqual(982,enemy.CurrentHealth);
        }
        [Test] public void SeryaDiagonalPrimaryDoesNotExpandHerSecondaryAreaOrChangeBurn()
        {
            var d=SeryaWithEditedPrimaryDamage();var actor=Unit(d,BoardSide.Blue,2,6);
            var primary=Dummy(BoardSide.Red,3,5);var inRadius=Dummy(BoardSide.Red,2,4);var outside=Dummy(BoardSide.Red,4,4);
            Begin(actor,primary,inRadius,outside);actor.AddEnergy(25);simulation.Step(.5f);actor.IsPassiveTrainingTarget=true;
            Assert.AreEqual(982,primary.CurrentHealth);Assert.AreEqual(991,inRadius.CurrentHealth);Assert.AreEqual(1000,outside.CurrentHealth);
            simulation.Step(1);Assert.AreEqual(980,primary.CurrentHealth);Assert.AreEqual(989,inRadius.CurrentHealth);
            Assert.AreEqual(1000,outside.CurrentHealth);Assert.IsFalse(simulation.CaptureStatusFrame(outside).Statuses.Any(s=>s.Id=="burn"));
        }
        [Test] public void BattleCardBaselineUsesFinalPreparationLoadoutAndStaysFrozenDuringCombat()
        {
            var d=new UnitDefinition{Id="fixture-preparation-stats",MaxHealth=100,Damage=5,AttackRange=1,AttackInterval=2,
                MoveInterval=1,EnergyMax=20,Tricks=new[]{new TrickDefinition{Id="fixture-prep-upgrade",EffectId="fixture-prep-upgrade",
                    HealthBonus=20,DamageBonus=3,AttackIntervalMultiplier=.5f}}};
            var owned=new OwnedUnit(d);var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,3,5);
            Assert.AreEqual(100,actor.RoundBaseHealth,"The preparation preview predates the final purchases.");
            owned.PersistentHealthBonus=15;owned.PersistentDamageBonus=2;owned.Tricks[0]=true;
            simulation=new CombatSimulation(board,1,0);simulation.Begin(new[]{actor,enemy},new Dictionary<BattleUnit,OwnedUnit>{{actor,owned}});
            actor.IsPassiveTrainingTarget=enemy.IsPassiveTrainingTarget=true;
            var before=simulation.CaptureBattleStats(actor);
            Assert.AreEqual(135,before.BaseHealth);Assert.AreEqual(10,before.BaseDamage);Assert.AreEqual(1,before.BaseAttacksPerSecond);
            actor.ApplyDamage(9);actor.AddEnergy(7);actor.BaseStats.AddMaxHealth(50);
            Field(actor,"DamageBonus",4);Field(actor,"SpeedBonus",.25f);
            var after=simulation.CaptureBattleStats(actor);
            Assert.AreEqual(135,after.BaseHealth);Assert.AreEqual(10,after.BaseDamage);Assert.AreEqual(1,after.BaseAttacksPerSecond);
            Assert.AreEqual(126,after.CurrentHealth);Assert.AreEqual(14,after.CurrentDamage);Assert.AreEqual(1.25m,after.CurrentAttacksPerSecond);
            Assert.AreEqual(7,after.Energy);Assert.AreEqual(20,after.EnergyMax);
            simulation.Stop();owned.PersistentHealthBonus=25;owned.PersistentDamageBonus=4;
            simulation.Begin(new[]{actor,enemy},new Dictionary<BattleUnit,OwnedUnit>{{actor,owned}});
            var next=simulation.CaptureBattleStats(actor);Assert.AreEqual(145,next.BaseHealth);Assert.AreEqual(12,next.BaseDamage);
            Assert.AreEqual(1,next.BaseAttacksPerSecond);Assert.AreEqual(0,next.Energy);
        }
        [Test] public void BattleCardBaselineExcludesOpeningCombatSpeedBuffs()
        {
            var d=Definition("legionario");d.AttackInterval=2;
            var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,3,5);Begin(actor,enemy);
            actor.IsPassiveTrainingTarget=true;var view=simulation.CaptureBattleStats(actor);
            Assert.AreEqual(.5f,view.BaseAttacksPerSecond);Assert.That(view.CurrentAttacksPerSecond,Is.EqualTo(.575m).Within(.00001m));
            simulation.Step(5);var expired=simulation.CaptureBattleStats(actor);
            Assert.AreEqual(.5f,expired.BaseAttacksPerSecond);Assert.That(expired.CurrentAttacksPerSecond,Is.EqualTo(.5m).Within(.00001m));
        }
        [TestCase(-1,1)][TestCase(1,2)]
        public void OpeningMemoryGrantsOncePerSelectedAllyAndSurvivesCasterDeathUntilExpiry(int slot,int count)
        {
            var d=Definition("protectora-del-mausoleo");d.PassiveAbility.Parameters.Single(p=>p.Key=="duration").Value=.5f;
            var actor=Unit(d,BoardSide.Blue,2,6,slot);var first=Dummy(BoardSide.Blue,2,5);var second=Dummy(BoardSide.Blue,3,5);
            var far=Dummy(BoardSide.Blue,0,0);var enemy=Dummy(BoardSide.Red,4,9);Begin(actor,first,second,far,enemy);
            Assert.AreEqual(10,first.Shield);Assert.AreEqual(count==2?10:0,second.Shield);Assert.AreEqual(0,far.Shield);Assert.AreEqual(0,actor.Shield);
            actor.ApplyDamage(1000);simulation.Step(.4f);Assert.AreEqual(10,first.Shield);Assert.AreEqual(count==2?10:0,second.Shield);
            simulation.Step(.1f);Assert.AreEqual(0,first.Shield);Assert.AreEqual(0,second.Shield);
        }
        [Test] public void ExplicitZeroMemoryShieldDoesNotRestoreTheDefault()
        {
            var d=Definition("protectora-del-mausoleo");d.PassiveAbility.Parameters.Single(p=>p.Key=="shield").Value=0;
            var actor=Unit(d,BoardSide.Blue,2,6);var ally=Dummy(BoardSide.Blue,2,5);var enemy=Dummy(BoardSide.Red,4,9);
            Begin(actor,ally,enemy);Assert.AreEqual(0,ally.Shield);
        }
        [TestCase(false)][TestCase(true)]
        public void GuardaUsesEditedStationaryReductionAndMovementClearsOnlyItsOwnSource(bool strongerSource)
        {
            var d=Definition("guarda-veterano");d.PassiveAbility.Parameters.Single(p=>p.Key=="stationaryDuration").Value=.2f;
            d.PassiveAbility.Parameters.Single(p=>p.Key=="reduction").Value=25;
            var actor=Unit(d,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,3,6);Begin(actor,enemy);actor.IsPassiveTrainingTarget=true;
            simulation.Step(.1f);Damage(enemy,actor,100);Assert.AreEqual(900,actor.CurrentHealth);
            simulation.Step(.1f);
            if(strongerSource)
            {
                var p=typeof(CombatSimulation).GetMethod("EronyaApplyStatus",Private).GetParameters();
                InvokeCore("EronyaApplyStatus",Clock(enemy),Clock(actor),Enum.Parse(p[2].ParameterType,"DamageReduction"),"fixture-stronger",.4f,100);
            }
            decimal before=actor.PreciseHealth;Damage(enemy,actor,100);Assert.AreEqual(strongerSource?60m:75m,before-actor.PreciseHealth);
            var cell=actor.CurrentCell;InvokeCore("EronyaPush",Clock(enemy),Clock(actor),1);Assert.AreNotSame(cell,actor.CurrentCell);
            before=actor.PreciseHealth;Damage(enemy,actor,100);Assert.AreEqual(strongerSource?60m:100m,before-actor.PreciseHealth);
        }
        UnitDefinition MineraAtOneCellExit(float tauntSeconds=2)
        {
            var d=Definition("la-minera");d.PassiveAbility.Parameters.Single(p=>p.Key=="distance").Value=1;
            d.PassiveAbility.Parameters.Single(p=>p.Key=="duration").Value=0;
            d.PassiveAbility.Parameters.Single(p=>p.Key=="tauntDuration").Value=tauntSeconds;return d;
        }
        [TestCase(2f)][TestCase(.5f)][TestCase(0f)]
        public void MineraFirstDamagingShieldContactTauntsOneEnemyForItsEditedDuration(float seconds)
        {
            var actor=Unit(MineraAtOneCellExit(seconds),BoardSide.Blue,2,7);
            var first=Dummy(BoardSide.Red,3,6);var second=Dummy(BoardSide.Red,2,5);Begin(actor,first,second);
            actor.IsPassiveTrainingTarget=true;GrantShield(first,"legacy");Damage(actor,first,1);Damage(actor,second,1);
            Assert.IsFalse(second.IsTaunted,"Only the first damaged enemy is affected.");Assert.AreEqual(1000,first.CurrentHealth);
            if(seconds==0){Assert.IsFalse(first.IsTaunted);Assert.IsFalse(simulation.CaptureStatusFrame(first).Statuses.Any(s=>s.Id=="taunt"));return;}
            Assert.IsTrue(first.IsTaunted);Assert.AreSame(actor,Clock(first).GetType().GetField("TauntTarget").GetValue(Clock(first)));
            var status=simulation.CaptureStatusFrame(first).Statuses.Single(s=>s.Id=="taunt");
            Assert.AreEqual("la-minera-tunel-subterraneo",status.EffectId);Assert.AreEqual(actor.UnitId,status.SourceUnitId);
            Assert.AreEqual(CombatStatusTimingKind.Timed,status.TimingKind);Assert.That(status.RemainingSeconds,Is.EqualTo(seconds).Within(.001));
            simulation.Step(seconds-.1f);Assert.IsTrue(first.IsTaunted);simulation.Step(.1f);Assert.IsFalse(first.IsTaunted);
            Assert.IsFalse(simulation.CaptureStatusFrame(first).Statuses.Any(s=>s.Id=="taunt"));
        }
        [Test] public void MineraZeroDamageDoesNotSpendHerSingleEnemyTaunt()
        {
            var actor=Unit(MineraAtOneCellExit(),BoardSide.Blue,2,7);var first=Dummy(BoardSide.Red,3,6);var second=Dummy(BoardSide.Red,2,5);
            Begin(actor,first,second);actor.IsPassiveTrainingTarget=true;Damage(actor,first,0);Damage(actor,second,1);
            Assert.IsFalse(first.IsTaunted);Assert.IsTrue(second.IsTaunted);
        }
        [Test] public void MineraTauntDoesNotStackWithOrReplaceALongerExistingTaunt()
        {
            var actor=Unit(MineraAtOneCellExit(),BoardSide.Blue,2,7);var enemy=Dummy(BoardSide.Red,3,6);var previous=Dummy(BoardSide.Blue,4,6);
            Begin(actor,enemy,previous);actor.IsPassiveTrainingTarget=true;
            var kind=typeof(CombatSimulation).GetMethod("EronyaApplyStatus",Private).GetParameters()[2].ParameterType;
            Assert.IsTrue((bool)InvokeCore("EronyaApplyStatus",Clock(previous),Clock(enemy),Enum.Parse(kind,"Taunt"),"fixture-long-taunt",1f,50));
            Damage(actor,enemy,1);Assert.AreSame(previous,Clock(enemy).GetType().GetField("TauntTarget").GetValue(Clock(enemy)));
            var status=simulation.CaptureStatusFrame(enemy).Statuses.Single(s=>s.Id=="taunt");Assert.AreEqual("fixture-long-taunt",status.EffectId);
            Assert.That(status.RemainingSeconds,Is.EqualTo(5).Within(.001));
        }
        [TestCase("zephalontropo",990)][TestCase("la-minera",979)]
        public void PokrakProtectiveBubblesQualifyForTheExistingFlatShieldBonus(string id,int expectedHealth)
        {
            var d=id=="la-minera"?MineraAtOneCellExit():Definition(id);d.Damage=8;d.AttackInterval=100;
            var actor=Unit(d,BoardSide.Blue,2,id=="la-minera"?7:6);
            var pokrak=DocumentedRoster.CreatePokrak();pokrak.MaxHealth=1000;
            var enemy=Unit(pokrak,BoardSide.Red,3,6);Begin(actor,enemy);InvokeCore("CastPokrakBubbles",Clock(enemy));
            Assert.AreEqual(0,enemy.Shield);Assert.That(simulation.PokrakBubbleReduction(enemy),Is.EqualTo(.2f).Within(.001));
            if(id=="la-minera")actor.AddEnergy(25);simulation.Step(.5f);
            Assert.AreEqual(expectedHealth,enemy.CurrentHealth,"Flat bonus is applied before the bubble's20% reduction.");
            Assert.AreEqual(id=="la-minera"?0:1,actor.Energy);
        }
        [Test] public void ExpiredPokrakBubbleDoesNotQualifyWhenTheProjectileArrives()
        {
            var d=Definition("zephalontropo",2);d.Damage=8;d.ProjectileSpeed=2;d.AttackInterval=100;
            var actor=Unit(d,BoardSide.Blue,2,6);var pokrak=DocumentedRoster.CreatePokrak();pokrak.MaxHealth=1000;
            // Legacy factories keep sparse parameter overrides; duration otherwise resolves from AbilityBalance.
            pokrak.BaseAbility.Parameters=new[]{new AbilityParameter{Key="duration",Value=.2f}};
            var enemy=Unit(pokrak,BoardSide.Red,2,4);Begin(actor,enemy);InvokeCore("CastPokrakBubbles",Clock(enemy));
            Assert.Greater(simulation.PokrakBubbleReduction(enemy),0);simulation.Step(1.3f);
            Assert.AreEqual(0,simulation.PokrakBubbleReduction(enemy));Assert.AreEqual(992,enemy.CurrentHealth);Assert.AreEqual(1,actor.Energy);
        }
        [Test] public void PlainDamageReductionIsNotAutomaticallyTreatedAsAShield()
        {
            var actor=Unit(Definition("zephalontropo"),BoardSide.Blue,2,6);
            var enemy=Unit(Definition("zephalontropo"),BoardSide.Red,3,6);Begin(actor,enemy);
            Assert.IsTrue(actor.BaseStats.UsesEronyaRules);Assert.IsTrue(enemy.BaseStats.UsesEronyaRules);
            var kind=typeof(CombatSimulation).GetMethod("EronyaApplyStatus",Private).GetParameters()[2].ParameterType;
            InvokeCore("EronyaApplyStatus",Clock(enemy),Clock(enemy),Enum.Parse(kind,"DamageReduction"),"fixture-reduction",.2f,100);
            Assert.IsFalse((bool)InvokeCore("EronyaHasTemporaryShield",Clock(enemy)));
            var origin=typeof(CombatSimulation).GetMethod("EronyaDealDamage",Private).GetParameters()[4].ParameterType;
            InvokeCore("EronyaDealDamage",Clock(actor),Clock(enemy),8f,"fixture-basic",Enum.Parse(origin,"Basic"));InvokeCore("EronyaFlushPackets");
            // Both rosters share pre-shield reduction rounding: round(8 * .8) = 6.
            Assert.AreEqual(994m,enemy.PreciseHealth);
        }
        [Test] public void PaidHeroChoiceUsesLegacyMoonkensAndDoesNotChangeOnLaterRounds()
        {
            config=MatchConfig.CreateDocumented();config.RoundIncome=new[]{100,0,0,0,0};
            foreach(string id in new[]{"alveyr","korran","drayven","cael","serya"})for(int slot=0;slot<3;slot++)
            {
                Assert.NotNull(config.Get(id),"The purchase fixture must include the Eronya catalog.");
                config.Get(id).Tricks[slot].Cost=3;
                var pouch=new PouchState(config,1,id,BoardSide.Blue);Assert.IsTrue(pouch.BeginPreparation(1));
                Assert.AreEqual(0,pouch.Monster.TrickCount);Assert.IsTrue(pouch.CanUpgradeMonster(slot));
                Assert.IsTrue(pouch.TryUpgradeMonster(slot,out var reason),reason);Assert.AreEqual(97,pouch.Coins);
                Assert.AreEqual(1,pouch.Monster.UpgradePurchasedRound);Assert.AreEqual(1,pouch.Monster.TrickCount);
                Assert.IsFalse(pouch.TryUpgradeMonster((slot+1)%3,out _));
                for(int round=2;round<=5;round++){pouch.EndPreparation();Assert.IsTrue(pouch.BeginPreparation(round));}
                for(int i=0;i<3;i++)Assert.AreEqual(i==slot,pouch.Monster.Tricks[i]);
                Assert.AreEqual(97,pouch.Coins);Assert.IsTrue(pouch.Monster.IsPositionLocked(5));
            }
        }
    }
}
