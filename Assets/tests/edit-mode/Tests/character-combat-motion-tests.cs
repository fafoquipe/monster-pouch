using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class CharacterCombatMotionTests
    {
        public static readonly string[] Ids={"aky","anuik","atong","atori","blotan","bugaloo","bugui","flo","gochan","hymay","jazar","kayon","popow","sepora","stein","tauris","tokoro","trimol","tsu"};
        public static readonly string[] CastIds={"bugaloo","gochan","hymay","kayon","popow","sepora","stein","tauris","tokoro","trimol"};
        readonly List<GameObject> objects=new List<GameObject>();
        [TearDown] public void Cleanup(){foreach(var go in objects)Object.DestroyImmediate(go);objects.Clear();}
        UnitPresentation Create(string id, out MonsterUnit actor, out UnitArt art, out UnitDefinition definition)
        {
            var character=AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/resources/MonsterPouch/Characters/"+id+"/Character.asset");
            Assert.IsNotNull(character,id);
            definition=character.CreateRuntimeDefinition();art=character.Visuals;
            Assert.IsTrue(art.AuthoredBodyMotion,id+" must exercise the live reference animation branch");
            var go=new GameObject("combat-motion-"+id);objects.Add(go);
            actor=go.AddComponent<MonsterUnit>();actor.Initialize(id,BoardSide.Blue,UnitStats.FromDefinition(definition,new OwnedUnit(definition)));
            var view=go.AddComponent<UnitPresentation>();view.Configure(actor,null,art);return view;
        }

        [TestCaseSource(nameof(Ids))]
        public void EveryDirectionKeepsRealContactClockAndGroundAnchor(string id)
        {
            for(int d=0;d<8;d++)
            {
                var view=Create(id,out var actor,out var art,out var definition);
                string before=JsonUtility.ToJson(art),statsBefore=JsonUtility.ToJson(actor.BaseStats);
                float angle=d*Mathf.PI/4;var direction=new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
                float windup=CombatSimulation.GetAttackWindup(actor);
                view.Attack(direction*4,definition.AttackKind==AttackKind.Ranged,windup);
                Assert.That(view.AttackContactDelay,Is.EqualTo(windup).Within(.00001f),id);
                var clip=art.Animation((UnitFacing)d);
                Vector3 preparation=Vector3.zero;
                if(windup>0)
                {
                    view.AdvancePresentation(windup*.75f);preparation=view.VisualRoot.localPosition;
                    Assert.IsFalse(view.AttackContactReached,id);
                    Assert.AreSame(clip.Attack[0],view.Renderer.sprite,id);
                    view.AdvancePresentation(windup*.25f-.001f);
                    Assert.IsFalse(view.AttackContactReached,id+" shows contact early");
                    Assert.AreSame(clip.Attack[0],view.Renderer.sprite,id);
                    view.AdvancePresentation(.001f);
                }
                else view.AdvancePresentation(0); // Instant rays must never acquire a cosmetic windup.
                Assert.IsTrue(view.AttackContactReached,id);
                Assert.AreSame(clip.Attack[clip.AttackContactFrame],view.Renderer.sprite,id);
                if(d==2)Assert.Greater(Vector3.Distance(preparation,view.VisualRoot.localPosition),.10f,id+" needs a visible release");
                Assert.Less(view.VisualRoot.localPosition.magnitude,.85f,id+" leaves its tile visually");
                Assert.That(view.VisualRoot.localScale.x/UnitPresentation.PresentationScale(art),Is.InRange(.75f,1.3f));
                Assert.That(view.VisualRoot.localScale.y/UnitPresentation.PresentationScale(art),Is.InRange(.75f,1.3f));
                view.AdvancePresentation(actor.BaseStats.AttackHold+actor.BaseStats.AttackRecovery*.17f);
                Assert.AreSame(clip.Attack[clip.AttackContactFrame],view.Renderer.sprite,id+" lost the readable impact hold");
                view.AdvancePresentation(actor.BaseStats.AttackRecovery*.83f+.002f);
                Assert.IsFalse(view.IsAttacking,id+" extended the action clock");
                view.AdvancePresentation(0);
                Assert.That(view.VisualRoot.localPosition.x,Is.EqualTo(0).Within(.001f),id);
                Assert.AreEqual(Vector3.zero,actor.transform.position,id+" moved the simulation anchor");
                Assert.AreEqual(statsBefore,JsonUtility.ToJson(actor.BaseStats),id+" modified gameplay stats");
                Assert.AreEqual(before,JsonUtility.ToJson(art),id+" modified the reference art");
            }
        }

        [TestCaseSource(nameof(CastIds))]
        public void SpecialReleaseDrawingWaitsForAuthoritativeCastInAllDirections(string id)
        {
            for(int d=0;d<8;d++)
            {
                var view=Create(id,out var actor,out var art,out _);
                string kind=id=="trimol"?"trimol-boulder":id+"-super";
                float angle=d*Mathf.PI/4;
                view.Face(Mathf.RoundToInt(Mathf.Sin(angle)), -Mathf.RoundToInt(Mathf.Cos(angle)));
                var clip=art.Animation(view.Facing);
                float cast=AbilityTiming.Cast(actor.BaseStats,kind),hold=AbilityTiming.Hold(actor.BaseStats,kind),back=AbilityTiming.Recovery(actor.BaseStats,kind);
                view.UseAbility(kind);
                if(cast>0)
                {
                    view.AdvancePresentation(cast*.72f);
                    Assert.AreSame(clip.Special[0],view.Renderer.sprite,id+" released before the ability");
                    view.AdvancePresentation(cast*.28f-.001f);
                    Assert.AreSame(clip.Special[0],view.Renderer.sprite,id+" released just before the ability");
                    view.AdvancePresentation(.001f);
                }
                else view.AdvancePresentation(0);
                Sprite contact=id=="hymay"?art.Attack(view.Facing):id=="tokoro"?clip.Idle[1]:clip.Special[Mathf.Min(1,clip.Special.Length-1)];
                Assert.AreSame(contact,view.Renderer.sprite,id);
                Assert.AreEqual(id=="hymay"||id=="tokoro"?clip.FlipX:clip.SpecialFlipX,view.Renderer.flipX,id);
                view.AdvancePresentation(hold+back*.15f);
                Assert.AreSame(contact,view.Renderer.sprite,id+" dropped release during hold");
                view.AdvancePresentation(back*.85f+.002f);
                Assert.IsFalse(view.IsUsingAbility,id+" extended its ability");
                Assert.AreEqual(Vector3.zero,actor.transform.position,id);
            }
        }

        [TestCase("tokoro","tokoro-sleep")]
        [TestCase("sepora","sepora-stealth")]
        [TestCase("atori","atori-kill")]
        [TestCase("tauris","tauris-kill")]
        public void PassiveReactionsDoNotStartAnUnrelatedAttack(string id,string kind)
        {
            var view=Create(id,out _,out _,out _);view.UseAbility(kind);
            Assert.IsFalse(view.IsUsingAbility,"A reward/sleep/stealth event should not look like another offensive cast.");
        }

        [Test] public void AtongCriticalPreservesTheHeavyImpactPose()
        {
            var view=Create("atong",out var actor,out var art,out _);
            float windup=CombatSimulation.GetAttackWindup(actor);
            view.Attack(Vector3.right,false,windup);view.AdvancePresentation(windup);
            view.UseAbility("atong-critical");view.AdvancePresentation(.01f);
            Assert.IsFalse(view.IsUsingAbility);Assert.IsTrue(view.AttackContactReached);
            Assert.AreSame(art.Animation(UnitFacing.East).Attack[1],view.Renderer.sprite);
            Assert.Greater(view.VisualRoot.localPosition.x,.4f);
        }

        [Test] public void FlowerPullAndPlantRespectSimulationCast()
        {
            var view=Create("flo",out var actor,out _,out _);
            float duration=AbilityTiming.Cast(actor.BaseStats,"flo-super");
            view.PrepareFlower(duration);view.AdvancePresentation(duration*.45f);
            Assert.Greater(view.VisualRoot.localPosition.y,.10f,"Flower must visibly lift out of its pot.");
            view.AdvancePresentation(duration*.55f+.001f);Assert.IsFalse(view.IsUsingAbility);
            Assert.AreEqual(Vector3.zero,actor.transform.position);
        }

        [TestCaseSource(nameof(Ids))]
        public void ResetCancelsTheStrongerPoseWithoutLeavingAnOffset(string id)
        {
            var view=Create(id,out var actor,out _,out var def);
            float windup=CombatSimulation.GetAttackWindup(actor);
            view.Attack(Vector3.right,def.AttackKind==AttackKind.Ranged,windup);view.AdvancePresentation(windup*.75f);
            actor.ResetForCombat();view.AdvancePresentation(0);
            Assert.IsFalse(view.IsAttacking);Assert.IsFalse(view.IsUsingAbility);
            Assert.That(view.VisualRoot.localPosition.x,Is.EqualTo(0).Within(.001f));
            Assert.That(Quaternion.Angle(view.VisualRoot.localRotation,Quaternion.identity),Is.LessThan(.1f));
        }
    }
}
