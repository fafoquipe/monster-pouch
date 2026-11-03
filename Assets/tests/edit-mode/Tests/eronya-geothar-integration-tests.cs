using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    // Actual BoardManager/BattleUnit/CombatSimulation; requires Unity's test runner.
    // Compiling this file is deliberately not reported as executing its cases.
    public sealed class EronyaGeotharIntegrationTests
    {
        GameObject root;BoardManager board;CombatSimulation simulation;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [SetUp]public void Setup(){root=new GameObject("eronya-core-fixture");board=root.AddComponent<BoardManager>();board.BuildBoard();}
        [TearDown]public void Cleanup(){simulation?.Stop();Object.DestroyImmediate(root);}
        BattleUnit Unit(UnitDefinition definition,BoardSide side,int x,int y,int stars=0)
        {
            var owned=new OwnedUnit(definition);for(int i=0;i<stars;i++)owned.Tricks[i]=true;
            var go=new GameObject(side+"/"+definition.Id);go.transform.SetParent(root.transform);
            BattleUnit unit=definition.IsMonster?(BattleUnit)go.AddComponent<MonsterUnit>():go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name,side,UnitStats.FromDefinition(definition,owned));unit.CombatTeamSlot=definition.IsMonster?0:1;
            Assert.That(board.TryOccupyCell(unit,x,y),Is.True);return unit;
        }
        BattleUnit Dummy(BoardSide side,int x,int y)
        {
            var unit=Unit(new UnitDefinition {Id="fixture-target-"+x,MaxHealth=10000,Damage=0,AttackRange=1,
                AttackInterval=1000,MoveInterval=1000},side,x,y);
            unit.IsPassiveTrainingTarget=true;return unit;
        }
        UnitDefinition Definition(string id)
        {
            string path="Assets/resources/MonsterPouch/Characters/"+id+"/Character.asset";
            var character=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            Assert.That(character,Is.Not.Null,"Installed character asset required: "+path);
            Assert.That(character.Id,Is.EqualTo(id),path);
            Assert.That(character.Validate(),Is.Empty,"Installed character references must be valid: "+path);
            // Same saved asset -> runtime copy path as the game; no factory fallback.
            // Fixture-only edits below affect this copy, never the serialized asset.
            var definition=character.CreateRuntimeDefinition();
            Assert.That(definition,Is.Not.Null,"Runtime copy required: "+path);
            return definition;
        }
        void Begin(params BattleUnit[] units){simulation=new CombatSimulation(board,123,0);simulation.Begin(units);}
        object Clock(BattleUnit unit)=>((IEnumerable)typeof(CombatSimulation).GetField("actors",Private).GetValue(simulation)).Cast<object>()
            .Single(a=>(BattleUnit)a.GetType().GetField("Unit").GetValue(a)==unit);
        object Kind(string type,string value)=>Enum.Parse(typeof(CombatSimulation).GetNestedType(type,BindingFlags.NonPublic),value);
        object Call(string method,params object[] values)
        {
            var info=typeof(CombatSimulation).GetMethod(method,Private);Assert.NotNull(info,method);
            var parameters=info.GetParameters();var args=new object[parameters.Length];
            for(int i=0;i<args.Length;i++)args[i]=i<values.Length?values[i]:parameters[i].DefaultValue;
            return info.Invoke(simulation,args);
        }
        void Damage(BattleUnit source,BattleUnit target,float amount)=>Call("EronyaDealDamage",Clock(source),Clock(target),amount,
            "fixture-damage",Kind("EronyaDamageOrigin","Ability"));
        void Status(BattleUnit source,BattleUnit target,string kind,int ticks)=>Call("EronyaApplyStatus",Clock(source),Clock(target),
            Kind("EronyaStatusKind",kind),"fixture-"+kind,1f,ticks);
        public static IEnumerable<TestCaseData> AllStars()
        {foreach(var d in EronyaRoster.CreateAll())for(int stars=0;stars<=3;stars++)yield return new TestCaseData(d.Id,stars).SetName("Eronya_cast_"+d.Id+"_"+stars);}
        [TestCaseSource(nameof(AllStars))]
        public void EveryActiveRunsThroughRealSchedulerAtEveryStar(string id,int stars)
        {
            var definition=Definition(id);definition.AttackInterval=1000;
            var actor=Unit(definition,BoardSide.Blue,2,6,stars);var enemy=Dummy(BoardSide.Red,2,5);var ally=Dummy(BoardSide.Blue,3,6);
            Begin(actor,enemy,ally);
            // A real opening tunnel may now outlast the global opening window.
            // Finish that independent action before testing a prepared active cast.
            actor.IsPassiveTrainingTarget=true;
            while(actor.IsFlying)simulation.Step(.1f);
            actor.IsPassiveTrainingTarget=false;
            var origin=actor.CurrentCell;
            Assert.That(board.TryRepositionUnit(enemy,board.GetCell(origin.X,origin.Y-1)),Is.True);
            Assert.That(board.TryRepositionUnit(ally,board.GetCell(origin.X+1,origin.Y)),Is.True);
            ally.ApplyDamage(100);
            var emitted=new List<string>();simulation.AbilityUsed+=(u,e)=>{if(u==actor)emitted.Add(e);};
            actor.AddEnergy(actor.BaseStats.EnergyMax);simulation.Step(.4f);
            Assert.That(emitted,Does.Not.Contain(definition.BaseAbility.EffectId),"No release before four preparation ticks finish.");
            simulation.Step(.1f);Assert.That(emitted,Does.Contain(definition.BaseAbility.EffectId));
        }
        public static IEnumerable<TestCaseData> MixedLegacyCases()
        {foreach(var d in EronyaRoster.CreateAll())yield return new TestCaseData(d.Id).SetName("Eronya_mixed_Martio_"+d.Id);}
        [TestCaseSource(nameof(MixedLegacyCases))]
        public void EachDeliveredCharacterCompletesARealMixedRoundAgainstMartio(string id)
        {
            var actor=Unit(Definition(id),BoardSide.Blue,2,6,3);
            var legacy=Unit(MartioDefinition.Create(),BoardSide.Red,2,5,3);
            Begin(actor,legacy);
            Assert.IsTrue(simulation.UsesEronyaCombat);
            for(int i=0;i<310&&!simulation.Finished;i++)simulation.Step(.1f);
            Assert.IsTrue(simulation.Finished,"A mixed round must settle within its 30-second limit.");
            Assert.IsTrue(simulation.Winner.HasValue);
            Assert.GreaterOrEqual(actor.PreciseHealth,0m);Assert.GreaterOrEqual(legacy.PreciseHealth,0m);
            Assert.IsTrue(actor.IsRoundFrozen&&legacy.IsRoundFrozen);
        }
        public static IEnumerable<TestCaseData> ContactOrigins()
        {
            foreach(var id in new[]{"alveyr","zephalontropo","alboguera","palista","cael","guarda-veterano","arboreo","antidota"})
            foreach(var origin in new[]{"Basic","Ability","Periodic","Reflection"})
                yield return new TestCaseData(id,origin).SetName("Eronya_contact_"+id+"_"+origin);
        }
        [TestCaseSource(nameof(ContactOrigins))]
        public void AuthoredBasicContactDoesNotReplayForAbilityDotOrReflection(string id,string origin)
        {
            var definition=Definition(id);definition.AttackInterval=1000;
            var actor=Unit(definition,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,2,5);
            Begin(actor,enemy);
            int contacts=0;
            simulation.AreaEffect+=(source,effect,cells)=>{
                if(source==actor&&(effect=="aireola-basic-contact"||effect=="naturalis-basic-contact"))
                {contacts++;Assert.That(cells,Has.Length.EqualTo(1));Assert.AreSame(enemy.CurrentCell,cells[0]);}
            };
            Call("EronyaDealDamage",Clock(actor),Clock(enemy),1f,"fixture-contact-origin",Kind("EronyaDamageOrigin",origin));
            Call("EronyaFlushPackets");
            Assert.That(contacts,Is.EqualTo(origin=="Basic"?1:0));
            Assert.Less(enemy.PreciseHealth,10000m,"Presentation filtering must not suppress the original damage packet.");
        }
        [TestCase(10f,5f)]
        [TestCase(3.25f,1.5f)]
        [TestCase(0f,0f)]
        public void SavedCharacterEnergyConfigurationReachesRealScheduler(float perAttack,float perSecond)
        {
            var definition=Definition("alveyr");definition.EnergyMax=20;
            definition.EnergyPerAttack=perAttack;definition.EnergyPerSecond=perSecond;definition.AttackInterval=1000;
            var actor=Unit(definition,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,2,5);
            Begin(actor,enemy);simulation.Step(.4f);
            Assert.That(actor.Energy,Is.EqualTo(perSecond*.4f).Within(.0001f));
            Call("EronyaDealDamage",Clock(actor),Clock(enemy),1f,"fixture-energy-basic",Kind("EronyaDamageOrigin","Basic"));
            Call("EronyaFlushPackets");
            Assert.That(actor.Energy,Is.EqualTo(perSecond*.4f+perAttack).Within(.0001f));
            Assert.That(actor.BaseStats.EnergyMax,Is.EqualTo(20f));
        }
        [TestCase(0,0f)] [TestCase(1,0f)] [TestCase(0,.75f)] [TestCase(1,.75f)]
        public void SavedDamageTypeDoesNotChangeGeneralIncomingDamage(int savedType,float externalReduction)
        {
            var definition=Definition("alveyr");definition.BasicDamageType=(CombatDamageType)savedType;
            definition.EnergyPerSecond=0;definition.EnergyPerAttack=0;definition.AttackInterval=1000;
            var victim=Definition("zephalontropo");victim.BaseArmor=5;victim.EnergyPerSecond=0;victim.AttackInterval=1000;
            var actor=Unit(definition,BoardSide.Blue,2,6);var enemy=Unit(victim,BoardSide.Red,2,5);
            Begin(actor,enemy);decimal before=enemy.PreciseHealth;
            var clock=Clock(enemy);
            clock.GetType().GetField("DamageReduction").SetValue(clock,externalReduction);
            clock.GetType().GetField("DamageReductionUntil").SetValue(clock,100);
            int expected=(int)Call("Incoming",Clock(enemy),20);
            Damage(actor,enemy,20);Call("EronyaFlushPackets");
            Assert.AreEqual(externalReduction==0?20:5,expected);Assert.AreEqual(expected,before-enemy.PreciseHealth);
        }
        [Test] public void TitanicaGeneralBlocksDeduplicatePacketsAndExcludePeriodicReflection()
        {
            var definition=Definition("titanica");definition.BaseArmor=0;definition.MaxHealth=10000;
            definition.EnergyPerSecond=0;definition.AttackInterval=1000;
            var defender=Unit(definition,BoardSide.Blue,2,6,2);var source=Dummy(BoardSide.Red,2,5);
            Begin(defender,source);
            for(int attack=1;attack<=20;attack++)
            {
                decimal before=defender.PreciseHealth;
                Call("EronyaWithAttack",Clock(source),1000L+attack,(Action)(()=>{
                    for(int packet=0;packet<2;packet++)Call("EronyaDealDamage",Clock(source),Clock(defender),10f,
                        "fixture-general-block",Kind("EronyaDamageOrigin",attack%2==0?"Basic":"Ability"));
                }));
                Call("EronyaFlushPackets");
                Assert.AreEqual(attack%4==0||attack%5==0?0m:20m,before-defender.PreciseHealth,"Attack "+attack);
                foreach(var origin in new[]{"Periodic","Reflection"})
                {
                    before=defender.PreciseHealth;
                    var hit=Call("EronyaDealDamage",Clock(source),Clock(defender),1f,"fixture-origin",Kind("EronyaDamageOrigin",origin));
                    Call("EronyaFlushPackets");Assert.AreEqual(1m,before-defender.PreciseHealth);
                    if(origin=="Reflection")Assert.IsFalse((bool)hit.GetType().GetField("CanReflect").GetValue(hit));
                }
            }
        }
        [TestCase("alveyr")] [TestCase("bugaloo")]
        public void ReceivedDamageEnergyUsesExistingBugrumHealthAndShieldRules(string id)
        {
            var definition=Definition(id);definition.EnergyMax=20;definition.EnergyPerAttack=1;
            definition.EnergyOnDamage=2;definition.EnergyPerSecond=0;definition.AttackInterval=1000;definition.BaseArmor=999;
            // Keep this energy test outside Alveyr's opening damage-suppression column.
            var actor=Unit(definition,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,3,5);
            Begin(actor,enemy);decimal before=actor.PreciseHealth;
            Call("DirectDamage",Clock(enemy),Clock(actor),5);Call("EronyaFlushPackets");
            Assert.AreEqual(5m,before-actor.PreciseHealth);Assert.AreEqual(2f,actor.Energy);
            actor.ClearEnergy();actor.AddShield(10);
            Call("DirectDamage",Clock(enemy),Clock(actor),5);Call("EronyaFlushPackets");
            Assert.AreEqual(0f,actor.Energy,"Shield-only contact does not grant Bugrum-style energy.");
            Call("DirectDamage",Clock(enemy),Clock(actor),10);Call("EronyaFlushPackets");
            Assert.AreEqual(2f,actor.Energy,"Only the health-damaging application grants energy.");
            actor.ClearEnergy();
            Call("DirectDamage",Clock(enemy),Clock(actor),1);Call("DirectDamage",Clock(enemy),Clock(actor),1);
            Call("EronyaFlushPackets");Assert.AreEqual(4f,actor.Energy,"Separate direct applications retain separate grants.");
        }
        [Test] public void AlveyrAttackBatchGetsOneDamageCreditAndBasicKeepsIt()
        {
            var definition=Definition("alveyr");definition.EnergyMax=20;definition.EnergyPerAttack=1;
            definition.EnergyOnDamage=2;definition.EnergyPerSecond=0;definition.AttackInterval=1000;
            var actor=Unit(definition,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,3,5);
            Begin(actor,enemy);
            for(int i=0;i<2;i++)Call("EronyaDealDamage",Clock(enemy),Clock(actor),3f,"fixture-received-batch");
            Call("EronyaFlushPackets");Assert.AreEqual(2f,actor.Energy);
            Call("EronyaDealDamage",Clock(actor),Clock(enemy),1f,"fixture-basic",Kind("EronyaDamageOrigin","Basic"));
            Call("EronyaFlushPackets");Assert.AreEqual(3f,actor.Energy,"Ledger synchronization cannot erase or duplicate incoming credit.");
        }
        [Test] public void BattleStatReadsPreserveRoundBaselineAndNeverAdvanceCombat()
        {
            var definition=Definition("alveyr");definition.MaxHealth=200;definition.Damage=7;definition.AttackInterval=2;
            definition.EnergyPerSecond=0;
            var actor=Unit(definition,BoardSide.Blue,2,6);var enemy=Dummy(BoardSide.Red,2,5);Begin(actor,enemy);
            var clock=Clock(actor);clock.GetType().GetField("DamageBonus").SetValue(clock,3);
            clock.GetType().GetField("SpeedBonus").SetValue(clock,.2f);
            Call("EronyaApplyStatus",clock,clock,Kind("EronyaStatusKind","AttackSpeed"),"fixture-speed",.3f,20);
            Call("EronyaApplyStatus",clock,clock,Kind("EronyaStatusKind","Slow"),"fixture-slow",.2f,20);
            Assert.IsFalse((bool)Call("EronyaApplyStatus",clock,clock,Kind("EronyaStatusKind","Armor"),"fixture-retired",100f,20));
            actor.BaseStats.AddMaxHealth(50);actor.ApplyDamage(9);
            float elapsed=simulation.Elapsed,energy=actor.Energy;
            for(int i=0;i<30;i++)
            {
                var view=simulation.CaptureBattleStats(actor);
                Assert.AreEqual(200,view.BaseHealth);Assert.AreEqual(191m,view.CurrentHealth);
                Assert.AreEqual(7,view.BaseDamage);Assert.AreEqual(10m,view.CurrentDamage);
                // Shared legacy multiplier: base0.5 * (1 + own0.2 + received0.3) * (1 - slow0.2).
                Assert.AreEqual(.5f,view.BaseAttacksPerSecond);Assert.That(view.CurrentAttacksPerSecond,Is.EqualTo(.6m).Within(.00001m));
            }
            Assert.AreEqual(elapsed,simulation.Elapsed);Assert.AreEqual(energy,actor.Energy);
            actor.ResetForCombat();var reset=simulation.CaptureBattleStats(actor);
            Assert.AreEqual(250,reset.BaseHealth);Assert.AreEqual(7m,reset.CurrentDamage);
        }
        [Test]public void MixedTimeoutPublishesFrozenScoresForUi()
        {
            var definition=Definition("korran");definition.Damage=0;definition.AttackInterval=1000;definition.MoveInterval=1000;
            var old=MartioDefinition.Create();old.Damage=0;old.AttackInterval=1000;old.MoveInterval=1000;
            var actor=Unit(definition,BoardSide.Blue,2,6);var legacy=Unit(old,BoardSide.Red,2,5);
            Begin(actor,legacy);
            for(int i=0;i<305&&!simulation.Finished;i++)simulation.Step(.1f);
            Assert.IsTrue(simulation.EronyaRoundCompared);Assert.IsTrue(simulation.EronyaRoundTimedOut);
            Assert.AreEqual(actor.PreciseHealth,simulation.EronyaBlueScore.Health);
            Assert.AreEqual(legacy.PreciseHealth,simulation.EronyaRedScore.Health);
            Assert.Greater(simulation.EronyaBlueScore.InitialHealth,0m);
            Assert.Greater(simulation.EronyaRedScore.InitialHealth,0m);
            if(simulation.EronyaRoundCriterion==EronyaRoundCriterion.Token)Assert.AreEqual(simulation.EronyaTiebreakSide,simulation.Winner);
        }
    }

    // Pure arithmetic/description checks can also run offline without Unity lifecycle.
    public sealed class EronyaEnergyConfigurationTests
    {
        [Test] public void FallbackDefaultsUseOnePerContactAndNoPassiveEnergy()
        {
            var ledger=new EronyaManaLedger(20m);
            for(int i=0;i<10;i++)ledger.PassiveTick();
            Assert.AreEqual(0m,ledger.Value);Assert.IsTrue(ledger.CreditBasic(1,true,true));
            Assert.AreEqual(1m,ledger.Value);
        }
        [Test] public void EditedFractionalRatesStayIndependentAndRespectCapacity()
        {
            var first=new EronyaManaLedger(20m,3.25m,1.5m);
            var other=new EronyaManaLedger(20m,0m,0m);
            for(int i=0;i<10;i++){first.PassiveTick();other.PassiveTick();}
            first.CreditBasic(1,true,true);other.CreditBasic(1,true,true);
            Assert.AreEqual(4.75m,first.Value);Assert.AreEqual(0m,other.Value);
            Assert.IsFalse(first.Spend());first.Add(100m);Assert.AreEqual(20m,first.Value);
            Assert.IsTrue(first.Spend());Assert.AreEqual(0m,first.Value);
            Assert.IsFalse(first.CreditBasic(1,true,true));first.Reset();
            Assert.IsTrue(first.CreditBasic(1,true,true));Assert.AreEqual(3.25m,first.Value);
        }
        [Test] public void OnlyOneLandedBasicCreditPerAttack()
        {
            var ledger=new EronyaManaLedger(20m,2.75m,0m);
            Assert.IsFalse(ledger.CreditBasic(1,false,true));
            Assert.IsFalse(ledger.CreditBasic(1,true,false));
            Assert.IsTrue(ledger.CreditBasic(1,true,true));
            Assert.IsFalse(ledger.CreditBasic(1,true,true));
            Assert.AreEqual(2.75m,ledger.Value);
        }
        [Test] public void NegativeRatesAreRejectedAndZeroIsValid()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>new EronyaManaLedger(20m,-1m,0m));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new EronyaManaLedger(20m,0m,-1m));
            Assert.DoesNotThrow(()=>new EronyaManaLedger(20m,0m,0m));
        }
        [Test] public void DescriptionsReadEditedOwnerEnergyForBothLanguagesAndRulesets()
        {
            var owner=new UnitDefinition {Ruleset=CombatRuleset.Eronya,EnergyMax=20,EnergyPerAttack=3.25f,EnergyPerSecond=1.5f};
            var effect=new TrickDefinition {EffectId="fixture-energy-description"};
            foreach(bool spanish in new[]{true,false})
            {
                Assert.AreEqual("20/3.25/1.5/20",AbilityBalance.FormatDescription(effect,owner,
                    "{energyMax}/{energyPerAttack}/{energyPerSecond}/{manaCost}",spanish));
                owner.EnergyPerAttack=0;
                Assert.AreEqual("0",AbilityBalance.FormatDescription(effect,owner,"{energyPerAttack}",spanish));
                owner.EnergyPerAttack=3.25f;
            }
            owner.Ruleset=CombatRuleset.Legacy;owner.EnergyPerSecond=-2;
            Assert.AreEqual("20/3.25/0",AbilityBalance.FormatDescription(effect,owner,
                "{energyMax}/{energyPerAttack}/{energyPerSecond}"));
        }
    }

    public sealed class EronyaGeneralDamageTests
    {
        [Test] public void CompatibilityFlagsAndRetiredArmorProduceIdenticalDamage()
        {
            foreach(var kind in new[]{CombatDamageType.Physical,CombatDamageType.Magical})
            {
                Assert.AreEqual(20m,EronyaMechanics.Damage(20m,kind,5m));
                Assert.AreEqual(10m,EronyaMechanics.Damage(20m,kind,5m,0m,new[]{.5m}));
                Assert.AreEqual(2m,EronyaMechanics.Damage(2m,kind,50m));
                Assert.AreEqual(0m,EronyaMechanics.Damage(0m,kind,50m));
                Assert.AreEqual(20m,EronyaMechanics.Damage(20m,kind,5m,.2m));
                Assert.AreEqual(10m,EronyaMechanics.Damage(20m,kind,5m,0m,new[]{.2m,.5m}));
            }
        }
    }

    public sealed class CoreEnergyArmorStatsTests
    {
        [Test] public void SavedArmorAndUpgradeArmorHaveNoStatOrDamageEffect()
        {
            Assert.AreEqual(0,new UnitStats(100,5,armor:900).Armor);
            var definition=new UnitDefinition {Id="fixture-no-armor",MaxHealth=100,Damage=5,BaseArmor=900,
                BaseAbility=new TrickDefinition {Id="padding",Armor=700}};
            Assert.AreEqual(0,UnitStats.FromDefinition(definition).Armor);
            foreach(var kind in new[]{CombatDamageType.Physical,CombatDamageType.Magical})
                Assert.AreEqual(10m,EronyaMechanics.Damage(20m,kind,900m,1m,new[]{.5m}));
        }
        [Test] public void RetiredArmorParametersStaySavedButDoNotResolveAsActive()
        {
            var effect=new TrickDefinition {EffectId="la-minera-fractura",Parameters=new[]{
                new AbilityParameter{Key="armorReduction",Value=99},new AbilityParameter{Key="duration",Value=9},
                new AbilityParameter{Key="damage",Value=12}}};
            var resolved=AbilityBalance.Resolve(effect);
            Assert.IsFalse(resolved.Any(p=>p.Key=="armorReduction"||p.Key=="duration"));
            Assert.AreEqual(12,resolved.Single(p=>p.Key=="damage").Value);
            Assert.AreEqual(99,effect.Parameters[0].Value);
            var pressure=AbilityBalance.Resolve(new TrickDefinition{EffectId="zephalontropo-presion-calibrada"});
            Assert.IsFalse(pressure.Any(p=>p.Key=="armorPenetration"));
            Assert.AreEqual(4,pressure.Single(p=>p.Key=="shieldBonusDamage").Value);
            Assert.IsFalse(AbilityBalance.IsRetiredArmorEffect("alveyr-guardian-de-los-vientos"));
        }
        [Test] public void ZeroCapacityDisablesSpendingWithoutCrashing()
        {
            var ledger=new EronyaManaLedger(0,10,5);ledger.PassiveTick();ledger.CreditBasic(1,true,true);
            Assert.AreEqual(0,ledger.Value);Assert.IsFalse(ledger.Spend());
        }
        [Test] public void BothRostersUseEffectiveLegacyPhasesForTrainingBudget()
        {
            foreach(var definition in EronyaRoster.CreateAll())
            {
                var initial=UnitStats.FromDefinition(definition);
                float before=BasicAttackTiming.TrainingCycleSeconds(initial);
                definition.AttackHold=2;definition.AttackRecovery=3;
                var edited=UnitStats.FromDefinition(definition);
                Assert.AreEqual(initial.AttackInterval,edited.AttackInterval,definition.Id);
                Assert.AreEqual(Mathf.Max(edited.AttackInterval,edited.AttackWindup+5),BasicAttackTiming.TrainingCycleSeconds(edited),definition.Id);
            }
            var legacy=new UnitDefinition{Id="fixture-legacy-tail",MaxHealth=100,Damage=1,AttackInterval=1,
                AttackWindup=.5f,AttackHold=.6f,AttackRecovery=.7f};
            Assert.That(BasicAttackTiming.TrainingCycleSeconds(UnitStats.FromDefinition(legacy)),Is.EqualTo(1.8f).Within(.00001f));
            Assert.AreEqual(new Vector2(.12f,.20f),BasicAttackTiming.DefaultTail("alveyr"));
        }
        [Test] public void DamageEnergyDescriptionUsesEditedValueInBothLanguages()
        {
            var owner=new UnitDefinition {EnergyOnDamage=2.25f};var effect=new TrickDefinition{EffectId="fixture-energy"};
            foreach(bool es in new[]{true,false})Assert.AreEqual("2.25",AbilityBalance.FormatDescription(effect,owner,"{energyOnDamage}",es));
        }
    }
}
