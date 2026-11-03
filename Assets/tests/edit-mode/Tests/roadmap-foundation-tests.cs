using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class RoadmapFoundationTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        CombatSimulation simulation;
        BoardManager board;

        [SetUp] public void Setup()
        {
            var go = new GameObject("roadmap-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void Cleanup()
        {
            simulation?.Stop();
            foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear();
        }
        BattleUnit Unit(UnitDefinition definition, string id, BoardSide side, int x, int y, params int[] upgrades)
        {
            var owned = new OwnedUnit(definition);
            foreach (int upgrade in upgrades) owned.Tricks[upgrade] = true;
            var go = new GameObject(id); objects.Add(go);
            var unit = go.AddComponent<WhelpUnit>();
            unit.Initialize(id, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }
        BattleUnit Dummy(BoardSide side) => Unit(new UnitDefinition {
            Id="test-only-target", MaxHealth=10000, Damage=0, AttackRange=99,
            AttackInterval=100, MoveInterval=100, AttackWindup=.1f
        }, "target", side, 5, side == BoardSide.Red ? 0 : 9);
        static UnitDefinition RecoveryDefinition()
        {
            var d = DocumentedRoster.CreateTokoro();
            d.MaxHealth=1000; d.Damage=0; d.AttackRange=99;
            d.AttackInterval=.5f; d.EnergyPerSecond=10; d.EnergyOnDamage=20;
            return d;
        }
        static void Parameter(TrickDefinition ability, string key, float value)
        {
            var parameters=(ability.Parameters??new AbilityParameter[0]).Where(p=>p.Key!=key).ToList();
            parameters.Add(new AbilityParameter{Key=key,Value=value});
            ability.Parameters=parameters.ToArray();
        }
        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void RecoveryHealsExactly180AndBlocksAllPositiveEnergyForThreeSeconds(BoardSide side)
        {
            var tokoro=Unit(RecoveryDefinition(),"tokoro",side,2,5,2);
            var enemy=Dummy(side==BoardSide.Blue?BoardSide.Red:BoardSide.Blue);
            simulation=new CombatSimulation(board,7,1.5f);
            simulation.Begin(new[]{tokoro,enemy});
            tokoro.ApplyDamage(400);
            Assert.AreEqual(0,tokoro.AddEnergy(100),"Ally grants use the same guard.");
            int attacks=0;
            simulation.Attacked+=(actor,_,__)=>{if(actor==tokoro)attacks++;};
            simulation.Step(1.5f);
            Assert.AreEqual(690,tokoro.CurrentHealth);
            Assert.IsTrue(tokoro.IsSleeping);
            simulation.Step(1.4f);
            Assert.AreEqual(774,tokoro.CurrentHealth);
            Assert.AreEqual(0,tokoro.Energy);
            Assert.AreEqual(0,attacks);
            simulation.Step(.1f);
            Assert.AreEqual(780,tokoro.CurrentHealth);
            Assert.IsFalse(tokoro.IsSleeping);
            Assert.IsFalse(tokoro.IsOpeningEnergyBlocked);
            Assert.Greater(tokoro.AddEnergy(1),0);
            simulation.Step(1);
            Assert.Greater(attacks,0);
            simulation.Step(27);
            Assert.IsFalse(tokoro.IsSleeping,"The obsolete end-of-round sleep must never return.");
        }
        [Test]
        public void FractionalRateAccumulatesAndRecoveryStopsAtEditedDuration()
        {
            var d=RecoveryDefinition();
            Parameter(d.Tricks[2],"heal",7); Parameter(d.Tricks[2],"duration",2);
            var unit=Unit(d,"tokoro",BoardSide.Blue,2,5,2); var enemy=Dummy(BoardSide.Red);
            simulation=new CombatSimulation(board);simulation.Begin(new[]{unit,enemy});unit.ApplyDamage(100);
            simulation.Step(2);
            Assert.AreEqual(914,unit.CurrentHealth);Assert.IsFalse(unit.IsSleeping);
            simulation.Step(1);Assert.AreEqual(914,unit.CurrentHealth);
        }
        [Test]
        public void RecoveryClampsHealthAndStopClearsLocks()
        {
            var unit=Unit(RecoveryDefinition(),"tokoro",BoardSide.Blue,2,5,2);var enemy=Dummy(BoardSide.Red);
            simulation=new CombatSimulation(board);simulation.Begin(new[]{unit,enemy});
            unit.ApplyDamage(1);simulation.Step(.2f);
            Assert.AreEqual(1000,unit.CurrentHealth);
            simulation.Stop();Assert.IsFalse(unit.IsOpeningEnergyBlocked);Assert.IsFalse(unit.IsSleeping);
        }
        [Test]
        public void RestartBeginsANewRecoveryWithoutCarryingOldHealing()
        {
            var unit=Unit(RecoveryDefinition(),"tokoro",BoardSide.Blue,2,5,2);var enemy=Dummy(BoardSide.Red);
            simulation=new CombatSimulation(board);simulation.Begin(new[]{unit,enemy});
            unit.ApplyDamage(100);simulation.Step(1);
            simulation.Begin(new[]{unit,enemy});unit.ApplyDamage(100);simulation.Step(1);
            Assert.AreEqual(960,unit.CurrentHealth);Assert.IsTrue(unit.IsOpeningEnergyBlocked);
        }
        [Test]
        public void WindowDelaysNormalAttacksButDoesNotWaitForTokoro()
        {
            var tokoro=Unit(RecoveryDefinition(),"tokoro",BoardSide.Blue,2,5,2);
            var enemy=Dummy(BoardSide.Red);
            var ally=Unit(new UnitDefinition{Id="test-only-ally",MaxHealth=1000,Damage=1,AttackRange=99,
                AttackInterval=.5f,AttackWindup=.1f,MoveInterval=100},"ally",BoardSide.Blue,1,5);
            simulation=new CombatSimulation(board,1,1.5f);simulation.Begin(new[]{tokoro,ally,enemy});
            int attacks=0;simulation.Attacked+=(a,_,__)=>{if(a==ally)attacks++;};
            simulation.Step(1.5f);Assert.AreEqual(0,attacks);
            simulation.Step(.2f);Assert.Greater(attacks,0);Assert.IsTrue(tokoro.IsSleeping);
        }
        [Test]
        public void OpeningShieldResolvesInsideWindowBeforeBasicAttacks()
        {
            var suki=Unit(DocumentedRoster.CreateSuki(),"suki",BoardSide.Blue,2,5,1);
            var enemy=Dummy(BoardSide.Red);
            simulation=new CombatSimulation(board,1,1.5f);simulation.Begin(new[]{suki,enemy});
            int attacks=0;simulation.Attacked+=(_,__,___)=>attacks++;
            simulation.Step(1.5f);
            Assert.Greater(suki.Shield,0);Assert.AreEqual(0,attacks);
        }
        [Test]
        public void ShippedRecoveryAssetAndRuntimeUseApprovedValues()
        {
            var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/tokoro/Character");
            Assert.NotNull(character);
            var runtime=character.CreateRuntimeDefinition();
            var owned=new OwnedUnit(runtime);owned.Tricks[2]=true;
            var stats=UnitStats.FromDefinition(runtime,owned);
            Assert.AreEqual(3,stats.AbilityValue("tokoro-sleep","duration",-1));
            Assert.AreEqual(60,stats.AbilityValue("tokoro-sleep","heal",-1));
            StringAssert.Contains("primeros",AbilityBalance.Description(runtime.Tricks[2],runtime));
            Assert.AreEqual(1.5f,Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").OpeningAbilityWindowSeconds);
        }
    }

    public sealed class RoadmapBankRegressionTests
    {
        MatchConfig config;
        PouchState pouch;
        OwnedUnit unit;
        [SetUp] public void Setup()
        {
            config=MatchConfig.CreateDefault();config.RoundIncome=new[]{100};
            config.Units=new[]{config.Get("bugaloo"),config.Get("dummy")};
            pouch=new PouchState(config,13,"bugaloo",BoardSide.Blue);
            pouch.BeginPreparation(1);
            var offer=pouch.Offers[0];
            Assert.IsTrue(pouch.TryBuyAndPlace(0,offer.Token,UnitLocation.Field,new Vector2Int(2,7),out _));
            var upgrade=pouch.Offers[1];Assert.IsTrue(pouch.TryBuy(1,upgrade.Token,out _));
            unit=pouch.Owned["dummy"];
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(config);}
        [Test]
        public void StoreAndWithdrawAtZeroBalanceKeepIdentityUpgradesAndInvestment()
        {
            pouch.AddCombatCoins(-pouch.Coins);
            int paid=unit.Paid;var tricks=unit.Tricks.ToArray();int copies=unit.Copies;
            for(int i=0;i<8;i++)
            {
                Assert.IsTrue(pouch.TrySetLocation("dummy",UnitLocation.Bench,Vector2Int.zero,out _));
                Assert.IsTrue(pouch.TrySetLocation("dummy",UnitLocation.Bench,Vector2Int.zero,out _));
                Assert.AreEqual(0,pouch.Coins);
                Assert.IsTrue(pouch.Offers.All(o=>o==null||o.UnitId!="dummy"));
                Assert.IsTrue(pouch.TrySetLocation("dummy",UnitLocation.Field,new Vector2Int(2,7),out _));
                Assert.AreSame(unit,pouch.Owned["dummy"]);Assert.AreEqual(0,pouch.Coins);
                Assert.AreEqual(paid,unit.Paid);Assert.AreEqual(copies,unit.Copies);
                CollectionAssert.AreEqual(tricks,unit.Tricks);
            }
            Assert.IsTrue(pouch.TrySell("dummy",out _));Assert.AreEqual(paid,pouch.Coins);
            Assert.IsFalse(pouch.TrySell("dummy",out _));Assert.AreEqual(paid,pouch.Coins);
        }
        [Test]
        public void InvalidWithdrawalAndCombatStorageAreAtomic()
        {
            Assert.IsTrue(pouch.TrySetLocation("dummy",UnitLocation.Bench,Vector2Int.zero,out _));
            int coins=pouch.Coins,paid=unit.Paid;
            Assert.IsFalse(pouch.TrySetLocation("dummy",UnitLocation.Field,pouch.Monster.Deployment,out _));
            Assert.AreEqual(UnitLocation.Bench,unit.Location);
            Assert.IsFalse(pouch.TrySetLocation("dummy",UnitLocation.Field,new Vector2Int(-1,-1),out _));
            pouch.EndPreparation();
            Assert.IsFalse(pouch.TrySetLocation("dummy",UnitLocation.Field,new Vector2Int(2,7),out _));
            Assert.AreEqual(UnitLocation.Bench,unit.Location);Assert.AreEqual(coins,pouch.Coins);Assert.AreEqual(paid,unit.Paid);
        }
    }
}
