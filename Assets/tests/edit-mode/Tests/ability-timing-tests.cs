using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class AbilityTimingTests
    {
        GameObject root;BoardManager board;CombatSimulation sim;
        readonly List<Object> assets=new List<Object>();
        [SetUp] public void Setup(){root=new GameObject("ability-timing");board=root.AddComponent<BoardManager>();board.BuildBoard();}
        [TearDown] public void Cleanup(){sim?.Stop();Object.DestroyImmediate(root);foreach(var asset in assets)Object.DestroyImmediate(asset);assets.Clear();}
        static void Set(TrickDefinition ability,string key,float value)
        {
            ability.Parameters=(ability.Parameters??new AbilityParameter[0]).Where(p=>p.Key!=key)
                .Concat(new[]{new AbilityParameter{Key=key,Value=value}}).ToArray();
        }
        BattleUnit Unit(UnitDefinition def,int x,int y,BoardSide side,params int[] upgrades)
        {
            var owned=new OwnedUnit(def);foreach(int upgrade in upgrades)owned.Tricks[upgrade]=true;
            var go=new GameObject(def.Id);go.transform.SetParent(root.transform);
            var u=go.AddComponent<WhelpUnit>();u.Initialize(def.Id+"-"+x+"-"+y,side,UnitStats.FromDefinition(def,owned));
            Assert.IsTrue(board.TryOccupyCell(u,x,y));return u;
        }
        BattleUnit Enemy(int x=2,int y=4,BoardSide side=BoardSide.Red)=>Unit(new UnitDefinition{Id="dummy",MaxHealth=1000,Damage=0,AttackRange=9,AttackInterval=100,MoveInterval=100},x,y,side);
        UnitDefinition Definition(string id,float cast)
        {
            var def=DocumentedRoster.CreateAll().Single(d=>d.Id==id);def.MaxHealth=1000;def.Damage=3;def.AttackInterval=100;def.MoveInterval=100;
            def.EnergyPerAttack=def.EnergyOnDamage=def.EnergyPerSecond=0;
            Set(def.BaseAbility,"castDuration",cast);Set(def.BaseAbility,"damageOverride",17);
            return def;
        }
        void Begin(BattleUnit actor,params BattleUnit[] others){sim=new CombatSimulation(board);sim.Begin(new[]{actor}.Concat(others).ToArray());actor.AddEnergy(100);}

        [TestCase("tokoro",0f)] [TestCase("tokoro",.8f)]
        [TestCase("stein",0f)] [TestCase("stein",.8f)]
        [TestCase("sepora",0f)] [TestCase("sepora",.8f)]
        public void CastControlsReleaseAndAbilityDamageIsIndependentFromBasic(string id,float duration)
        {
            var def=Definition(id,duration);var actor=Unit(def,2,5,BoardSide.Blue);var enemy=Enemy();Begin(actor,enemy);
            actor.SetAbilityAnimationDuration(8); // The headless simulation ignores this obsolete renderer input.
            sim.Step(.1f);
            if(duration>0){Assert.AreEqual(1000,enemy.CurrentHealth);sim.Step(duration-.1f);Assert.AreEqual(1000,enemy.CurrentHealth);sim.Step(.1f);}
            Assert.AreEqual(983,enemy.CurrentHealth);Assert.AreEqual(3,actor.BaseStats.Attack);
        }
        [Test] public void TsuGumKeepsBasicCadenceAndAppliesItsEffectOnlyOnImpact()
        {
            var def=DocumentedRoster.CreateTsu();def.AttackWindup=.5f;def.AttackInterval=1;def.Damage=3;
            def.BaseAbility.EveryAttacks=2;def.MoveInterval=100;
            Set(def.BaseAbility,"castDuration",9);Set(def.BaseAbility,"holdDuration",9);Set(def.BaseAbility,"recoveryDuration",9);
            Set(def.BaseAbility,"damageOverride",17);
            var actor=Unit(def,2,5,BoardSide.Blue);var enemy=Enemy();Begin(actor,enemy);
            var starts=new List<float>();sim.Attacked+=(a,b,p)=>{if(a==actor)starts.Add(sim.Elapsed);};
            sim.Step(.7f);Assert.AreEqual(997,enemy.CurrentHealth);Assert.IsFalse(enemy.IsRooted);
            sim.Step(.4f);Assert.AreEqual(2,starts.Count);Assert.That(starts[1]-starts[0],Is.EqualTo(1).Within(.001f));
            Assert.AreEqual(.5f,CombatSimulation.GetAttackWindup(actor));Assert.IsNull(actor.ActionAbilityEffect);
            sim.Step(.4f);Assert.AreEqual(997,enemy.CurrentHealth);Assert.IsFalse(enemy.IsRooted);
            sim.Step(.2f);Assert.AreEqual(980,enemy.CurrentHealth);
            sim.Step(.1f);Assert.IsTrue(enemy.IsRooted); // Status flags refresh at the next simulation tick.
            sim.Step(.3f);Assert.AreEqual(3,starts.Count);
            Assert.IsFalse(AbilityTiming.HasPhases(actor.BaseStats,"tsu-root"));
        }
        [Test] public void AttackEffectsDoNotStartASecondCharacterAnimation()
        {
            var def=DocumentedRoster.CreateTsu();var actor=Unit(def,2,5,BoardSide.Blue);
            var texture=new Texture2D(2,1);assets.Add(texture);
            var basic=Sprite.Create(texture,new Rect(0,0,1,1),Vector2.one*.5f,1);assets.Add(basic);
            var special=Sprite.Create(texture,new Rect(1,0,1,1),Vector2.one*.5f,1);assets.Add(special);
            var art=new UnitArt{Id="tsu",Portrait=basic};
            for(int i=0;i<8;i++)art.DirectionalAnimations[i]=new UnitDirectionalAnimation{Idle=new[]{basic},Attack=new[]{basic},Special=new[]{special}};
            var view=actor.gameObject.AddComponent<UnitPresentation>();view.Configure(actor,null,art);
            view.Attack(actor.transform.position+Vector3.up,true,.6f);view.UseAbility("tsu-root");view.AdvancePresentation(.1f);
            Assert.IsFalse(view.IsUsingAbility);Assert.AreSame(basic,view.Renderer.sprite);
            view.UseAbility("jazar-radiation");Assert.IsFalse(view.IsUsingAbility);
        }
        [Test] public void ZeroAbilityDamageDoesNotFallBackToTheBasicAttack()
        {
            var def=Definition("stein",0);Set(def.BaseAbility,"damageOverride",0);
            var actor=Unit(def,2,5,BoardSide.Blue);var enemy=Enemy();Begin(actor,enemy);sim.Step(.1f);
            Assert.AreEqual(1000,enemy.CurrentHealth);Assert.IsTrue(enemy.IsStunned);
        }
        [Test] public void SeporaStormDurationStartsAfterChargeAndCannotAttackDuringCharge()
        {
            var def=Definition("sepora",1);Set(def.BaseAbility,"duration",2);
            var actor=Unit(def,2,5,BoardSide.Blue);var enemy=Enemy();Begin(actor,enemy);
            sim.Step(1);Assert.AreEqual(1000,enemy.CurrentHealth);Assert.IsTrue(actor.IsEnergyLocked);
            sim.Step(.1f);Assert.AreEqual(983,enemy.CurrentHealth);
            sim.Step(1.9f);Assert.IsTrue(actor.IsEnergyLocked);sim.Step(.1f);Assert.IsFalse(actor.IsEnergyLocked);
        }
        [TestCase("reset")] [TestCase("death")] [TestCase("range")]
        public void UnreleasedStormIsCancelledWhenItsCasterOrTargetBecomesInvalid(string change)
        {
            var actor=Unit(Definition("sepora",1),2,5,BoardSide.Blue);var enemy=Enemy();Begin(actor,enemy);sim.Step(.1f);
            if(change=="reset")actor.ResetForCombat();else if(change=="death")actor.ApplyDamage(5000);else Assert.IsTrue(board.TryRepositionUnit(enemy,board.GetCell(5,0)));
            int specialHits=0;sim.Impacted+=(a,b,d)=>{if(a==actor&&d==17)specialHits++;};sim.Step(2);
            Assert.AreEqual(0,specialHits);
        }
        [Test] public void HoldAndReturnPreventNextBasicFromCuttingTheAbilityShort()
        {
            var def=Definition("stein",.2f);def.AttackInterval=.1f;
            Set(def.BaseAbility,"holdDuration",1);Set(def.BaseAbility,"recoveryDuration",.3f);
            var actor=Unit(def,2,5,BoardSide.Blue);var enemy=Enemy();Begin(actor,enemy);
            int basics=0;sim.Attacked+=(a,b,p)=>{if(a==actor)basics++;};sim.Step(1.5f);Assert.AreEqual(0,basics);
            sim.Step(.1f);Assert.AreEqual(1,basics);
        }
        [TestCase(0f)] [TestCase(.4f)]
        public void ConesHealOnlyWhenTheirOwnTravelTimeFinishes(float travel)
        {
            var def=Definition("tokoro",0);Set(def.Tricks[1],"travelDuration",travel);
            var actor=Unit(def,2,5,BoardSide.Blue,1);var ally=Enemy(1,5,BoardSide.Blue);var enemy=Enemy();Begin(actor,enemy,ally);ally.ApplyDamage(50);
            int launches=0;sim.ConesLaunched+=(u,origin,cells,d)=>{launches++;Assert.AreEqual(actor.CurrentCell,origin);Assert.AreEqual(8,cells.Length);Assert.AreEqual(travel,d,.001f);};
            sim.Step(.1f);
            if(travel>0){Assert.AreEqual(950,ally.CurrentHealth);Assert.IsEmpty(sim.GroundEffects);sim.Step(travel-.1f);Assert.AreEqual(950,ally.CurrentHealth);sim.Step(.1f);}
            Assert.AreEqual(960,ally.CurrentHealth);Assert.AreEqual(travel>0?1:0,launches);
            Assert.AreEqual(6,sim.GroundEffects.Count);sim.Step(.1f);Assert.AreEqual(960,ally.CurrentHealth);
        }
        [Test] public void FloCanPlantInstantlyWhenPreparationIsZero()
        {
            var actor=Unit(Definition("flo",0),2,5,BoardSide.Blue);var enemy=Enemy(5,0);Begin(actor,enemy);sim.Step(.1f);
            Assert.AreEqual(1,sim.CombatOnlyUnits.Count);
        }
        [Test] public void HeadbuttKeepsItsContactPoseThroughSelfStunThenReturns()
        {
            var def=Definition("tokoro",.5f);Set(def.BaseAbility,"holdDuration",1);Set(def.BaseAbility,"recoveryDuration",.2f);
            var actor=Unit(def,2,5,BoardSide.Blue);var enemy=Enemy();
            var texture=new Texture2D(4,1);assets.Add(texture);var frames=new Sprite[4];
            for(int i=0;i<4;i++){frames[i]=Sprite.Create(texture,new Rect(i,0,1,1),Vector2.one*.5f,1);assets.Add(frames[i]);}
            var art=new UnitArt{Id="tokoro",Portrait=frames[0],AbilityAnimationDuration=8};
            for(int i=0;i<8;i++)art.DirectionalAnimations[i]=new UnitDirectionalAnimation{Idle=new[]{frames[0]},Special=frames,SpecialReleasePoint=.5f};
            var view=actor.gameObject.AddComponent<UnitPresentation>();view.Configure(actor,null,art);
            Begin(actor,enemy);sim.AbilityUsed+=(a,k)=>{if(a==actor)view.UseAbility(k);};sim.Step(.1f);
            view.AdvancePresentation(.49f);Assert.IsTrue(view.IsUsingAbility);
            sim.Step(.5f);view.AdvancePresentation(.02f);Assert.IsTrue(actor.IsStunned);Assert.IsTrue(view.IsUsingAbility);var contact=view.Renderer.sprite;
            view.AdvancePresentation(.8f);Assert.IsTrue(view.IsUsingAbility);Assert.AreSame(contact,view.Renderer.sprite);
            view.AdvancePresentation(.4f);Assert.IsFalse(view.IsUsingAbility);Assert.AreEqual(983,enemy.CurrentHealth);
        }
    }
}
