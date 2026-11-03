using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class RoundHealthTests
    {
        GameObject root;
        BoardManager board;
        CombatSimulation simulation;
        [SetUp] public void Setup()
        {
            root = new GameObject("round-health");
            board = root.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void Cleanup()
        {
            simulation?.Stop();
            Object.DestroyImmediate(root);
        }
        BattleUnit Add(string id, BoardSide side, int hp, int x, int y,
            UnitCategory category = UnitCategory.Whelp, bool summoned = false, UnitStats stats = null)
        {
            var go = new GameObject(id); go.transform.SetParent(root.transform);
            BattleUnit unit = category == UnitCategory.Monster ? (BattleUnit)go.AddComponent<MonsterUnit>()
                : category == UnitCategory.Summon ? go.AddComponent<SummonUnit>() : go.AddComponent<WhelpUnit>();
            unit.Initialize(id, side, stats ?? new UnitStats(hp, 0, 99, 100, 100), summoned);
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }
        [Test] public void DeadUnitsAndBothKindsOfSummonContributeNothing()
        {
            var monster = Add("dead-monster", BoardSide.Blue, 500, 0, 8, UnitCategory.Monster);
            var dead = Add("dead-whelp", BoardSide.Blue, 200, 1, 8);
            var flower = Add("flower", BoardSide.Blue, 9999, 2, 8, UnitCategory.Summon);
            var dummy = Add("dummy", BoardSide.Blue, 9999, 3, 8, summoned: true);
            var living = Add("living", BoardSide.Blue, 101, 4, 8);
            var red = Add("red", BoardSide.Red, 30, 0, 1, UnitCategory.Monster);
            monster.ApplyDamage(500); dead.ApplyDamage(200); living.ApplyDamage(44);
            var result = RoundTiebreakers.Timeout(new[] { monster, dead, flower, dummy, living, red, living });
            Assert.AreEqual(2, result.Health.Contributions.Count);
            Assert.AreEqual(29, result.Health.BlueTotal);
            Assert.AreEqual(30, result.Health.RedTotal);
            Assert.AreEqual(BoardSide.Red, result.Winner);
            Assert.AreEqual(57, living.CurrentHealth, "Presentation must not consume the actual HP.");
            Assert.IsNull(RoundTiebreakers.Champion(new[] { flower, dummy }));
        }
        [Test] public void LivingMonsterCountsCurrentRatherThanMaximumHealth()
        {
            var blue = Add("blue", BoardSide.Blue, 200, 0, 8, UnitCategory.Monster);
            var red = Add("red", BoardSide.Red, 100, 0, 1, UnitCategory.Monster);
            blue.ApplyDamage(193); red.ApplyDamage(92);
            var result = RoundTiebreakers.Timeout(new[] { blue, red });
            Assert.AreEqual(7, result.Health.BlueTotal); Assert.AreEqual(8, result.Health.RedTotal);
            Assert.AreEqual(BoardSide.Red, result.Winner);
            // A captured summary does not consult later mutable unit health.
            blue.Heal(100);
            Assert.AreEqual(7, result.Health.BlueTotal);
            Assert.AreEqual(7, result.Health.Contributions.Single(c => c.Unit == blue).CurrentHealth);
        }
        [Test] public void OnlySummonsRemainingDoesNotInventAHealthWinner()
        {
            var one = Add("flower", BoardSide.Blue, 100, 0, 8, UnitCategory.Summon);
            var two = Add("dummy", BoardSide.Red, 1, 0, 1, summoned: true);
            var result = RoundTiebreakers.Timeout(new[] { one, two });
            Assert.IsNull(result.Winner); Assert.IsEmpty(result.Health.Contributions);
            Assert.AreEqual(0, result.Health.Maximum);
            Assert.AreEqual(0, result.Health.PresentedTotal(BoardSide.Blue, 1));
        }
        [Test] public void MonstersTransferTogetherThenWhelpsOneByOneWithStableOrder()
        {
            var blue = Add("blue-monster", BoardSide.Blue, 100, 0, 8, UnitCategory.Monster);
            var red = Add("red-monster", BoardSide.Red, 80, 0, 1, UnitCategory.Monster);
            var b = Add("b-whelp", BoardSide.Blue, 31, 1, 8);
            var a = Add("a-whelp", BoardSide.Blue, 20, 2, 8);
            var r = Add("r-whelp", BoardSide.Red, 40, 1, 1);
            var summary = new RoundHealthSummary(new[] { b, red, r, a, blue });
            Assert.AreEqual(3, summary.SlotCount);
            Assert.AreEqual(100, summary.PresentedTotal(BoardSide.Blue, 1f / 3), .0001f);
            Assert.AreEqual(80, summary.PresentedTotal(BoardSide.Red, 1f / 3), .0001f);
            Assert.AreEqual(110, summary.PresentedTotal(BoardSide.Blue, 2f / 3), .0001f);
            Assert.AreEqual(100, summary.PresentedTotal(BoardSide.Red, 2f / 3), .0001f);
            Assert.AreEqual(126, summary.PresentedTotal(BoardSide.Blue, 1), .0001f);
            float previousBlue = 0, previousRed = 0;
            for (int i = 0; i <= 100; i++)
            {
                float nextBlue = summary.PresentedTotal(BoardSide.Blue, i / 100f);
                float nextRed = summary.PresentedTotal(BoardSide.Red, i / 100f);
                Assert.GreaterOrEqual(nextBlue, previousBlue); Assert.GreaterOrEqual(nextRed, previousRed);
                previousBlue = nextBlue; previousRed = nextRed;
            }
            CollectionAssert.AreEqual(summary.Contributions.Select(c => c.UnitId),
                new RoundHealthSummary(new[] { blue, a, r, red, b }).Contributions.Select(c => c.UnitId));
        }
        [Test] public void TimeoutFreezesEveryUnitIncludingSummonsAndResetsOnNewCombat()
        {
            var blue = Add("blue", BoardSide.Blue, 100, 0, 8, UnitCategory.Monster);
            var ally = Add("ally", BoardSide.Blue, 31, 1, 8);
            var dead = Add("dead", BoardSide.Blue, 10, 2, 8);
            var summoned = Add("dummy", BoardSide.Blue, 900, 3, 8, summoned: true);
            var red = Add("red", BoardSide.Red, 110, 0, 1, UnitCategory.Monster);
            var units = new[] { blue, ally, dead, summoned, red };
            simulation = new CombatSimulation(board); simulation.Begin(units);
            dead.ApplyDamage(10); blue.ApplyDamage(7); simulation.Step(40);
            Assert.IsTrue(simulation.Finished);
            Assert.AreSame(simulation.HealthSummary, simulation.TimeoutResult.Health);
            Assert.AreEqual(109, simulation.HealthSummary.BlueTotal);
            Assert.AreEqual(BoardSide.Red, simulation.Winner);
            var health = units.Select(u => u.CurrentHealth).ToArray();
            foreach (var unit in units)
            {
                Assert.IsTrue(unit.IsRoundFrozen);
                Assert.AreEqual(0, unit.ApplyDamage(100000));
                Assert.AreEqual(0, unit.Heal(100000));
            }
            simulation.Step(100);
            CollectionAssert.AreEqual(health, units.Select(u => u.CurrentHealth));
            simulation.Begin(new[] { blue, ally, summoned, red });
            Assert.IsFalse(blue.IsRoundFrozen); Assert.IsNull(simulation.HealthSummary);
            Assert.AreEqual(3, blue.ApplyDamage(3));
        }
        [Test] public void PendingBurnsAndEnergyCannotChangeHealthAfterTimeout()
        {
            var definition = DocumentedRoster.CreateGochan();
            definition.Damage = 0; definition.MaxHealth = 10000;
            definition.AttackRange = 99; definition.AttackInterval = .5f;
            definition.AttackWindup = .1f; definition.EnergyPerSecond = 0;
            definition.EnergyPerAttack = 0; definition.EnergyOnDamage = 0;
            var owned = new OwnedUnit(definition); owned.Tricks[1] = true;
            var source = Add("gochan", BoardSide.Blue, 10000, 2, 7,
                stats: UnitStats.FromDefinition(definition, owned));
            var target = Add("target", BoardSide.Red, 10000, 2, 4, UnitCategory.Monster);
            simulation = new CombatSimulation(board); simulation.Begin(new[] { source, target });
            simulation.Step(38); source.AddEnergy(100); simulation.Step(1);
            Assert.Less(target.CurrentHealth, 10000, "The burn attack must really have landed.");
            int before = target.CurrentHealth;
            simulation.Step(1);
            Assert.Less(target.CurrentHealth, before, "A pending burn must tick before the timeout.");
            Assert.IsTrue(simulation.Finished);
            int captured = target.CurrentHealth;
            simulation.Step(10);
            Assert.AreEqual(captured, target.CurrentHealth);
            Assert.AreEqual(0, source.AddEnergy(100));
            Assert.AreEqual(0, target.ApplyDamage(2));
            Assert.AreEqual(captured, simulation.HealthSummary.RedTotal);
        }
        [TestCase(1,1)] [TestCase(2,1)] [TestCase(13,7)] [TestCase(101,51)]
        public void EachWhelpRoundsItsCurrentHalfHealthUp(int health,int expected)
        {
            var first=Add("first",BoardSide.Blue,health+4,0,8);
            var second=Add("second",BoardSide.Blue,1,1,8);
            var enemy=Add("enemy",BoardSide.Red,expected,0,1,UnitCategory.Monster);
            first.ApplyDamage(4);
            var result=RoundTiebreakers.Timeout(new[]{first,second,enemy});
            Assert.AreEqual(expected,result.AbsorbedHealth[first]);
            Assert.AreEqual(1,result.AbsorbedHealth[second]);
            Assert.AreEqual(expected+1,result.Health.BlueTotal,"Round each unit, not only the team's combined total.");
            Assert.AreEqual(BoardSide.Blue,result.Winner);
            Assert.AreEqual(health,first.CurrentHealth);
        }
    }
}
