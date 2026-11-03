using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TrainingLessonsTests
    {
        GameObject root;
        MatchConfig config,asset;
        BoardManager board;
        MatchController match;
        string assetBefore,configBefore;

        [SetUp]
        public void SetUp()
        {
            asset=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
            Assert.IsNotNull(asset);
            assetBefore=JsonUtility.ToJson(asset);
            config=asset.CreateRuntimeConfig();configBefore=JsonUtility.ToJson(config);
            root=new GameObject("training-lessons-tests");
            board=root.AddComponent<BoardManager>();board.BuildBoard();
            var mapper=root.AddComponent<BoardWorldMapper>();
            mapper.Configure(board,root.transform,new Vector2(.26f,-.18f),new Vector2(-.65f,.81f));
            match=root.AddComponent<MatchController>();match.Configure(config,board,mapper);
        }

        [TearDown]
        public void TearDown()
        {
            Assert.AreEqual(assetBefore,JsonUtility.ToJson(asset),"The shared catalogue must never be changed by training.");
            Assert.AreEqual(configBefore,JsonUtility.ToJson(config),"The player's normal runtime stats must remain intact.");
            Object.DestroyImmediate(root);Object.DestroyImmediate(config);Time.timeScale=1;
        }

        [Test]
        public void PositioningKeepsNormalExchangeAndDeploymentRulesWithAPassiveDummy()
        {
            Start("bugaloo",TrainingLesson.Positioning);
            var monster=match.Player.Monster;var whelp=match.Player.Owned[match.TrainingWhelpId];
            var oldMonster=monster.Deployment;var oldWhelp=whelp.Deployment;
            Assert.IsTrue(match.Place(monster.Definition.Id,board.GetCell(oldWhelp.x,oldWhelp.y)));
            Assert.AreEqual(oldWhelp,monster.Deployment);Assert.AreEqual(oldMonster,whelp.Deployment);
            Assert.AreEqual(2,match.Actors.Values.Count(actor=>actor.Side==BoardSide.Blue));
            Assert.IsNotNull(match.TrainingTarget);Assert.IsTrue(match.TrainingTarget.IsPassiveTrainingTarget);
            var targetPosition=match.TrainingTarget.CurrentCell;
            Assert.IsFalse(match.Place(monster.Definition.Id,targetPosition));
            match.Advance(120);
            Assert.AreSame(targetPosition,match.TrainingTarget.CurrentCell);
            Assert.AreEqual(0,match.TrainingTarget.BaseStats.Attack);
            Assert.IsFalse(match.BeginTrainingCombat());
            Assert.IsFalse(match.Player.Owned.ContainsKey("dummy"));
            Assert.IsFalse(match.Bot.Owned.ContainsKey("dummy"));
        }

        [TestCase("bugaloo")]
        [TestCase("popow")]
        public void AttacksLessonUsesRealDamageAndEnergyAgainstAnImmobileHarmlessTarget(string monsterId)
        {
            Start(monsterId,TrainingLesson.AttacksHealth);
            var stats=UnitStats.FromDefinition(match.Player.Monster.Definition);
            int energyHits=Mathf.CeilToInt(stats.EnergyMax/stats.EnergyPerAttack);
            int expectedHealth=Mathf.Max(ExpandedRoster.CreateSummonedDummy().MaxHealth,(energyHits+2)*Mathf.Max(1,stats.Attack));
            Assert.AreEqual(expectedHealth,match.TrainingTarget.CurrentHealth,"Target survives one actual energy cycle plus two hits.");
            var targetCell=match.TrainingTarget.CurrentCell;
            int enemyAttacks=0;match.Attacked+=(actor,target,projectile)=>{if(actor.Side==BoardSide.Red)enemyAttacks++;};
            var farthest=board.GetAllCells().Where(c=>config.IsDeploymentLegal(BoardSide.Blue,c.Coordinates))
                .OrderByDescending(c=>Mathf.Abs(c.X-targetCell.X)+Mathf.Abs(c.Y-targetCell.Y)).First();
            Assert.IsTrue(match.Place(monsterId,farthest));
            Assert.IsTrue(match.BeginTrainingCombat());
            for(int i=0;i<1200&&match.TrainingAbilityCount==0&&!match.TrainingCombatFinished;i++)match.Advance(.1f);
            Assert.Greater(match.TrainingAttackCount,0);Assert.Greater(match.TrainingDamageDealt,0);
            Assert.Greater(match.TrainingAbilityCount,0,"No artificial energy: a real automatic ability must occur.");
            Assert.Less(match.TrainingTarget.CurrentHealth,expectedHealth);
            Assert.AreSame(targetCell,match.TrainingTarget.CurrentCell);
            Assert.AreEqual(0,enemyAttacks);
            Assert.AreEqual(stats.MaxHealth,match.Actors[match.Player.Monster].CurrentHealth);
            Assert.IsNull(match.SessionId);Assert.AreEqual(0,match.PlayerWins);
        }

        [TestCase("bugaloo","popow")]
        [TestCase("popow","bugaloo")]
        public void ApprovedWeakenedStarterDuelIsWinnableWithoutChangingNormalStatsOrBO5(string selected,string rival)
        {
            Start(selected,TrainingLesson.SimpleCombat);
            Assert.AreEqual(rival,match.Bot.Monster.Definition.Id);
            var normal=config.Get(rival);var weak=match.Bot.Monster.Definition;
            Assert.AreNotSame(normal,weak);
            Assert.AreEqual(Mathf.CeilToInt(normal.MaxHealth*.5f),weak.MaxHealth);
            Assert.AreEqual(Mathf.CeilToInt(normal.Damage*.5f),weak.Damage);
            Assert.AreEqual(normal.AttackInterval*2,weak.AttackInterval);
            Assert.AreEqual(normal.MoveInterval*2,weak.MoveInterval);
            Assert.AreEqual(normal.EnergyMax*2,weak.EnergyMax);
            Assert.AreEqual(normal.EnergyPerAttack*.5f,weak.EnergyPerAttack);
            PlaceMonster();Assert.IsTrue(match.BeginTrainingCombat());
            for(int i=0;i<500&&!match.TrainingCombatFinished;i++)match.Advance(.1f);
            Assert.IsTrue(match.TrainingCombatFinished);Assert.IsTrue(match.TrainingCombatWon);
            Assert.AreEqual(MatchPhase.Combat,match.Phase,"Practice must never enter round or BO5 result phases.");
            Assert.AreEqual(0,match.PlayerWins);Assert.AreEqual(0,match.BotWins);Assert.IsNull(match.SessionId);
            Assert.IsFalse(match.BeginTrainingCombat());
            Assert.IsTrue(match.RestartTraining());
            Assert.AreEqual(TrainingLesson.SimpleCombat,match.CurrentTrainingLesson);
            Assert.IsFalse(match.TrainingCombatFinished);Assert.AreEqual(0,match.TrainingAttackCount);
            Assert.AreEqual(weak.MaxHealth,match.Bot.Monster.Definition.MaxHealth,"Restart must not halve an already halved enemy.");
        }

        [Test]
        public void DefeatCanRestartTheSamePracticeWithoutRecordingAMatch()
        {
            Start("bugaloo",TrainingLesson.SimpleCombat);PlaceMonster();Assert.IsTrue(match.BeginTrainingCombat());
            var player=match.Actors[match.Player.Monster];player.ApplyDamage(player.CurrentHealth);
            match.Advance(.2f);
            Assert.IsTrue(match.TrainingCombatFinished);Assert.IsFalse(match.TrainingCombatWon);
            Assert.IsNull(match.SessionId);Assert.AreEqual(0,match.BotWins);Assert.AreEqual(0,match.PlayerWins);
            Assert.IsTrue(match.RestartTraining());Assert.AreEqual(MatchPhase.Preparation,match.Phase);
            Assert.IsNull(match.Simulation);Assert.IsFalse(match.TrainingCombatFinished);
            Assert.AreEqual(UnitLocation.Brief,match.Player.Monster.Location);
        }

        [Test]
        public void EconomyUsesRealCostsFiniteCopiesRerollsAndOneOwnedUnitPerType()
        {
            Start("bugaloo",TrainingLesson.PreparationEconomy);
            var id=match.TrainingWhelpId;var definition=config.Get(id);
            int budget=definition.BaseCost+definition.Tricks.Sum(trick=>trick.Cost);
            Assert.AreEqual(budget,match.Player.Coins);
            Assert.IsTrue(match.Player.Offers.All(offer=>offer!=null&&offer.UnitId==id));
            var offer=match.Player.Offers[0];Assert.IsTrue(match.Buy(0,offer.Token));
            Assert.IsFalse(match.Buy(0,offer.Token));
            var unit=match.Player.Owned[id];Assert.IsTrue(match.Place(id,FreeCell()));
            var copy=match.Player.Offers[1];int upgradeCost=match.Player.GetOfferCost(1);
            Assert.IsTrue(match.Buy(1,copy.Token));
            Assert.AreEqual(2,unit.Copies);Assert.AreEqual(1,unit.TrickCount);
            Assert.AreEqual(budget-definition.BaseCost-upgradeCost,match.Player.Coins);
            Assert.AreEqual(1,match.Player.Owned.Values.Count(owned=>owned.Definition.Id==id));
            Assert.AreEqual(2,match.Player.RemainingCopies(id));
            int rerolls=match.Player.Rerolls;Assert.IsTrue(match.Reroll(match.Player.OfferVersion));
            Assert.AreEqual(rerolls-1,match.Player.Rerolls);
            Assert.IsTrue(match.Player.Offers.Where(item=>item!=null).All(item=>item.UnitId==id));
            Assert.IsFalse(match.BeginTrainingCombat());match.Advance(1000);
            Assert.AreEqual(MatchPhase.Preparation,match.Phase);Assert.IsNull(match.SessionId);
        }

        [Test]
        public void BankStoresAndRedeploysForFreeSuppressingCopiesWithoutCreatingDuplicates()
        {
            Start("popow",TrainingLesson.TacticalBank);
            var id=match.TrainingWhelpId;var unit=match.Player.Owned[id];var old=unit.Deployment;
            var offer=match.Player.Offers[0];Assert.IsNotNull(offer);
            Assert.AreEqual(0,unit.Paid);Assert.AreEqual(0,match.Player.Coins);
            Assert.IsTrue(match.Store(id,true));
            Assert.AreEqual(UnitLocation.Bench,unit.Location);Assert.AreEqual(0,match.Player.Coins);
            Assert.IsTrue(match.Player.Offers.All(item=>item==null||item.UnitId!=id));
            Assert.IsFalse(match.BuyAndPlace(0,offer.Token,board.GetCell(old.x,old.y)));
            Assert.AreEqual(1,match.Player.Owned.Values.Count(owned=>owned.Definition.Id==id));
            Assert.IsTrue(match.Place(id,board.GetCell(old.x,old.y)));
            Assert.AreEqual(0,match.Player.Coins);Assert.AreEqual(1,unit.Copies);
            Assert.AreEqual(UnitLocation.Field,unit.Location);
            Assert.AreEqual(MatchConfig.CopiesPerWhelp-1,match.Player.RemainingCopies(id));
        }

        [TestCase("bugaloo")]
        [TestCase("popow")]
        public void WeakeningIncludesAllEffectiveStarterAbilityParametersAndPreservesIdsAndCosts(string id)
        {
            var source=config.Get(id);string before=JsonUtility.ToJson(source);
            var weak=TrainingStatScaling.Weaken(source,.5f);
            Assert.AreEqual(before,JsonUtility.ToJson(source));Assert.AreEqual(source.Id,weak.Id);
            Assert.AreEqual(source.DisplayName,weak.DisplayName);Assert.AreEqual(source.BaseCost,weak.BaseCost);
            Assert.AreEqual(source.AttackKind,weak.AttackKind);Assert.AreEqual(source.TargetPolicy,weak.TargetPolicy);
            Assert.AreEqual(source.EnergyOnDamage*.5f,weak.EnergyOnDamage);
            Assert.AreEqual(source.ProjectileSpeed*.5f,weak.ProjectileSpeed);
            Assert.AreEqual(Mathf.Max(1,Mathf.CeilToInt(source.IQSpeed*.5f)),weak.IQSpeed);
            var original=AbilityBalance.Resolve(source.BaseAbility);
            var scaled=AbilityBalance.Resolve(weak.BaseAbility);
            foreach(var parameter in original)
            {
                float expected=parameter.Value;
                if(expected>0)
                    expected=parameter.Key=="castDuration"||parameter.Key=="holdDuration"||parameter.Key=="recoveryDuration"||parameter.Key=="travelDuration"?expected*2:
                        parameter.Key=="radius"?Mathf.Ceil(expected*.5f):expected*.5f;
                Assert.AreEqual(expected,scaled.Single(p=>p.Key==parameter.Key).Value,.0001f,parameter.Key);
            }
            for(int i=0;i<3;i++)
            {
                Assert.AreEqual(source.Tricks[i].Id,weak.Tricks[i].Id);
                Assert.AreEqual(source.Tricks[i].Cost,weak.Tricks[i].Cost);
            }
        }

        [Test]
        public void PracticeDashUsesHalfTheNormalEffectiveWidthOnlyOnce()
        {
            Start("bugaloo",TrainingLesson.SimpleCombat);PlaceMonster();Assert.IsTrue(match.BeginTrainingCombat());
            float width=-1;
            match.Simulation.DashStarted+=(actor,origin,target,landing,actualWidth,duration)=>
            {if(actor==match.TrainingTarget)width=actualWidth;};
            // Direct energy isolates the transformation; the lesson itself uses
            // only genuine attack/damage energy, covered by the full duel tests.
            match.TrainingTarget.AddEnergy(match.TrainingTarget.BaseStats.EnergyMax);
            for(int i=0;i<100&&width<0;i++)match.Advance(.1f);
            float normal=UnitStats.FromDefinition(config.Get("popow")).AbilityValue("popow-dash","width",1f);
            Assert.AreEqual(normal*.5f,width,.0001f);
        }

        [Test]
        public void NegativeInheritanceSentinelsAndZeroValuesRemainValid()
        {
            var source=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(config.Get("popow")));
            source.AttackHold=source.AttackRecovery=-1;source.EnergyPerSecond=0;source.RevivesPerCombat=0;
            source.BaseAbility.Parameters=source.BaseAbility.Parameters.Concat(new[]{new AbilityParameter{Key="damageOverride",Value=-1}}).ToArray();
            var weak=TrainingStatScaling.Weaken(source,.5f);
            Assert.AreEqual(-1,source.AttackHold);Assert.AreEqual(-1,source.AttackRecovery);
            Assert.GreaterOrEqual(weak.AttackHold,0);Assert.GreaterOrEqual(weak.AttackRecovery,0);
            Assert.AreEqual(0,weak.EnergyPerSecond);Assert.AreEqual(0,weak.RevivesPerCombat);
            Assert.IsFalse(weak.BaseAbility.Parameters.Any(p=>float.IsNaN(p.Value)||float.IsInfinity(p.Value)));
            Assert.Throws<System.ArgumentException>(()=>TrainingStatScaling.Weaken(source,float.NaN));
            Assert.Throws<System.ArgumentException>(()=>TrainingStatScaling.Weaken(source,0));
        }

        [Test]
        public void LeavingTrainingRestoresNormalCombatAndDoesNotKeepPracticeTargets()
        {
            Start("bugaloo",TrainingLesson.Positioning);match.Menu();
            Assert.IsFalse(match.IsTraining);Assert.IsNull(match.TrainingTarget);Assert.IsEmpty(match.Actors);
            match.StartMatch("popow",StarterRoster.Whelps,881);
            Assert.IsFalse(match.IsTraining);Assert.AreEqual(40,match.Remaining);Assert.IsNotNull(match.SessionId);
            Assert.IsFalse(match.Actors.Values.Any(actor=>actor.IsPassiveTrainingTarget));
            Assert.AreEqual(config.Get("popow").MaxHealth,match.Player.Monster.Definition.MaxHealth);
            match.Ready();Assert.AreEqual(MatchPhase.Combat,match.Phase);
        }

        void Start(string monster,TrainingLesson lesson)=>Assert.IsTrue(match.StartTraining(monster,StarterRoster.Whelps,lesson));
        void PlaceMonster()
        {
            var point=config.FormationCells(match.Player.Monster.Definition,BoardSide.Blue).First();
            Assert.IsTrue(match.Place(match.Player.Monster.Definition.Id,board.GetCell(point.x,point.y)));
        }
        BoardCell FreeCell()=>board.GetAllCells().First(c=>c.OccupiedBy==null&&!c.IsBlocked&&config.IsDeploymentLegal(BoardSide.Blue,c.Coordinates));
    }
}
