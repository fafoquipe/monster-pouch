using System.Collections.Generic;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Progression;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class RedesignIntegralTests
    {
        [Test] public void RuntimeCatalogHasNoUnityAssetReferencesAndDoesNotShareArrays()
        {
            var source=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");var copy=source.CreateRuntimeConfig();
            try
            {
                Assert.IsEmpty(copy.Characters);Assert.IsNotEmpty(source.Characters);
                Assert.That(JsonUtility.ToJson(copy),Does.Not.Contain("instanceID"));
                Assert.AreNotSame(source.RoundIncome,copy.RoundIncome);Assert.AreNotSame(source.BlueDeployment,copy.BlueDeployment);
                Assert.AreEqual(source.Characters.Length,copy.Units.Length);
            }
            finally{Object.DestroyImmediate(copy);}
        }
        [Test] public void RuntimeCharacterIsDetachedAndIdentityCannotChange()
        {
            var c=ScriptableObject.CreateInstance<CharacterDefinition>();var attack=ScriptableObject.CreateInstance<CharacterAttack>();
            try
            {
                c.InitializeIdentity("tsu");c.General=DocumentedRoster.CreateTsu();attack.Import(c.General);c.Attack=attack;
                var runtime=c.CreateRuntimeDefinition();runtime.Damage=999;runtime.BaseAbility.Name="runtime mutation";
                Assert.AreNotEqual(999,attack.Damage);Assert.AreNotEqual("runtime mutation",c.General.BaseAbility.Name);
                Assert.AreEqual("tsu",runtime.Id);Assert.Throws<System.InvalidOperationException>(()=>c.InitializeIdentity("other"));
            }
            finally{Object.DestroyImmediate(c);Object.DestroyImmediate(attack);}
        }
        [Test] public void ProgressAwardsOncePersistsAndUnlocksOnlyConfiguredRewards()
        {
            var config=ScriptableObject.CreateInstance<ProgressionConfig>();
            try
            {
                config.ExperiencePerMatch=80;config.ExperiencePerWin=30;config.ExperiencePerLevel=new[]{100};
                config.Rewards=new[]{new LevelReward{Level=2,UnlockId="lobby.test"}};
                var progress=new PlayerProgress();Assert.IsTrue(progress.Award(config,"match-a",true));
                Assert.IsFalse(progress.Award(config,"match-a",true));Assert.AreEqual(2,progress.Level);Assert.AreEqual(10,progress.Experience);
                var loaded=PlayerProgress.Restore(progress.Serialize());Assert.AreEqual(2,loaded.Level);CollectionAssert.AreEqual(new[]{"lobby.test"},loaded.Unlocks);
                Assert.IsFalse(loaded.Award(config,"match-a",false));Assert.AreEqual(1,PlayerProgress.Restore("broken json").Level);
            }
            finally{Object.DestroyImmediate(config);}
        }
        [Test] public void OldMatchReceiptsSurviveMoreThan128ResultsAndReloads()
        {
            var config=ScriptableObject.CreateInstance<ProgressionConfig>();
            try
            {
                config.ExperiencePerMatch=3;config.ExperiencePerWin=2;config.ExperiencePerLevel=new[]{100};
                config.Rewards=new[]{new LevelReward{Level=2,UnlockId="lobby.test"}};
                var progress=new PlayerProgress();
                for(int i=0;i<300;i++)Assert.IsTrue(progress.Award(config,"match-"+i,i%2==0));
                var loaded=PlayerProgress.Restore(progress.Serialize());
                Assert.AreEqual(300,loaded.CompletedMatches.Count);
                string before=loaded.Serialize();
                // Replays include the oldest receipt, both sides of the former
                // truncation boundary, and a retry with a different win result.
                foreach(int i in new[]{0,127,128,171,172,299})
                    Assert.IsFalse(loaded.Award(config,"match-"+i,i%2!=0),"Repeated receipt: "+i);
                Assert.AreEqual(before,loaded.Serialize(),"A retry must not change XP, levels, unlocks or receipts.");
                Assert.IsTrue(loaded.Award(config,"match-new",true));
                CollectionAssert.AreEqual(new[]{"lobby.test"},loaded.Unlocks);
                var reopened=PlayerProgress.Restore(loaded.Serialize());
                Assert.IsFalse(reopened.Award(config,"match-0",true));
                Assert.IsFalse(reopened.Award(config,"match-new",true));
            }
            finally{Object.DestroyImmediate(config);}
        }
        [Test] public void WhitespaceMatchIdentityCannotAwardProgress()
        {
            var config=ScriptableObject.CreateInstance<ProgressionConfig>();
            try
            {
                var progress=new PlayerProgress();string before=progress.Serialize();
                Assert.IsFalse(progress.Award(config," \t\r\n",true));
                Assert.AreEqual(before,progress.Serialize());
            }
            finally{Object.DestroyImmediate(config);}
        }
        [Test] public void PopowLandsBeforeTheStunEnds()
        {
            Assert.AreEqual(0,ImpactPresentationSettings.Lift(0,.1f,.14f,.32f));
            Assert.AreEqual(.32f,ImpactPresentationSettings.Lift(.1f,.1f,.14f,.32f),.0001f);
            Assert.Greater(ImpactPresentationSettings.Lift(.17f,.1f,.14f,.32f),0);
            Assert.AreEqual(0,ImpactPresentationSettings.Lift(.25f,.1f,.14f,.32f));
            Assert.AreEqual(0,ImpactPresentationSettings.Lift(1.5f,.1f,.14f,.32f));
        }
        [Test] public void SeporaFourthKillAddsNoDamageAndNextCombatResetsOnlyRoundCounter()
        {
            var boardGo=new GameObject("redesign-board");var unitGo=new GameObject("sepora");var enemyGo=new GameObject("enemy");
            CombatSimulation sim=null;
            try
            {
                var board=boardGo.AddComponent<BoardManager>();board.BuildBoard();
                var definition=DocumentedRoster.CreateSepora();var owned=new OwnedUnit(definition);owned.Tricks[2]=true;
                var unit=unitGo.AddComponent<MonsterUnit>();unit.Initialize("sepora",BoardSide.Blue,UnitStats.FromDefinition(definition,owned));board.TryOccupyCell(unit,2,5);
                var enemy=enemyGo.AddComponent<WhelpUnit>();enemy.Initialize("enemy",BoardSide.Red,new UnitStats());board.TryOccupyCell(enemy,2,4);
                sim=new CombatSimulation(board);sim.Begin(new BattleUnit[]{unit,enemy});
                int permanent=0;sim.PermanentDamageEarned+=(u,n)=>{permanent+=n;owned.PersistentDamageBonus+=n;};
                var clockMethod=typeof(CombatSimulation).GetMethod("Clock",BindingFlags.Instance|BindingFlags.NonPublic);
                var onKill=typeof(CombatSimulation).GetMethod("OnKill",BindingFlags.Instance|BindingFlags.NonPublic);
                object clock=clockMethod.Invoke(sim,new object[]{unit});
                for(int i=0;i<5;i++)onKill.Invoke(sim,new[]{clock});
                Assert.AreEqual(3,permanent);Assert.AreEqual(3,owned.PersistentDamageBonus);
                sim.Stop();sim.Begin(new BattleUnit[]{unit,enemy});clock=clockMethod.Invoke(sim,new object[]{unit});
                onKill.Invoke(sim,new[]{clock});Assert.AreEqual(4,permanent);Assert.AreEqual(4,owned.PersistentDamageBonus);
            }
            finally{sim?.Stop();Object.DestroyImmediate(unitGo);Object.DestroyImmediate(enemyGo);Object.DestroyImmediate(boardGo);}
        }
        [Test] public void LegacyStatusPoliciesCannotAddDurationOrShortenTheWinner()
        {
            var def=ScriptableObject.CreateInstance<StatusEffectDefinition>();
            try{Assert.AreEqual(20,def.ResolveExpiry(5,20,3));def.Stacking=StatusStacking.Replace;Assert.AreEqual(20,def.ResolveExpiry(5,20,3));def.Stacking=StatusStacking.AddDuration;Assert.AreEqual(20,def.ResolveExpiry(5,20,3));Assert.AreEqual(25,def.ResolveExpiry(5,20,20));}
            finally{Object.DestroyImmediate(def);}
        }
    }
}
