using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class ExpandedCombatTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<CombatSimulation> simulations = new List<CombatSimulation>();
        private BoardManager board;

        [SetUp] public void SetUp()
        {
            var go = new GameObject("expanded-combat-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
        }

        [TearDown] public void TearDown()
        {
            foreach (CombatSimulation simulation in simulations) simulation.Stop();
            simulations.Clear();
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        private BattleUnit Unit(string id, BoardSide side, int x, int y, UnitStats stats, bool monster = true)
        {
            var go = new GameObject(id); objects.Add(go);
            BattleUnit unit = monster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
            unit.Initialize(id, side, stats); Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }

        private CombatSimulation Begin(params BattleUnit[] units)
        {
            var sim = new CombatSimulation(board); simulations.Add(sim); sim.Begin(units); return sim;
        }

        [Test] public void Revival_HoldsCellAndRoundForEightTicks_ReturnsHalfHealthOnlyOnce()
        {
            BattleUnit anuik = Unit("anuik", BoardSide.Blue, 2, 4, new UnitStats(10, 0, revivesPerCombat: 1));
            BattleUnit enemy = Unit("enemy", BoardSide.Red, 3, 4, new UnitStats(100, 10, attackInterval: 10));
            BoardCell origin = anuik.CurrentCell;
            CombatSimulation sim = Begin(anuik, enemy);
            int starting = 0, returned = 0, terminal = 0;
            sim.Reviving += (unit, duration) => { starting++; Assert.AreEqual(.8f, duration, .0001f); };
            sim.Revived += unit => returned++;
            sim.Died += unit => terminal++;
            sim.Step(.2f);
            Assert.IsTrue(anuik.IsReviving); Assert.IsFalse(anuik.IsAlive); Assert.IsFalse(sim.Finished);
            Assert.AreSame(anuik, origin.OccupiedBy); Assert.AreEqual(0, terminal);
            sim.Step(.7f); Assert.AreEqual(0, anuik.CurrentHealth); Assert.AreEqual(0, returned);
            sim.Step(.1f); Assert.AreEqual(5, anuik.CurrentHealth); Assert.IsFalse(anuik.IsReviving);
            Assert.AreEqual(1, returned); Assert.AreSame(origin, anuik.CurrentCell);
            anuik.ApplyDamage(100); sim.Step(.1f);
            Assert.IsTrue(sim.Finished); Assert.AreEqual(BoardSide.Red, sim.Winner);
            Assert.AreEqual(1, starting); Assert.AreEqual(1, terminal); Assert.IsNull(origin.OccupiedBy);
        }

        [Test] public void MutualWipe_WithOneRevivalWaitsForReturnThenAwardsLivingTeam()
        {
            BattleUnit anuik = Unit("anuik", BoardSide.Blue, 2, 4, new UnitStats(10, 10, revivesPerCombat: 1));
            BattleUnit enemy = Unit("enemy", BoardSide.Red, 3, 4, new UnitStats(10, 10));
            CombatSimulation sim = Begin(anuik, enemy);
            sim.Step(.2f); Assert.IsFalse(sim.Finished); Assert.IsTrue(anuik.IsReviving); Assert.IsFalse(enemy.IsAlive);
            sim.Step(.8f); Assert.IsTrue(sim.Finished); Assert.AreEqual(BoardSide.Blue, sim.Winner); Assert.AreEqual(5, anuik.CurrentHealth);
        }

        [Test] public void SimultaneousRevivals_BothContinueAndSecondMutualWipeIsDraw()
        {
            BattleUnit blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(10, 10, revivesPerCombat: 1));
            BattleUnit red = Unit("red", BoardSide.Red, 3, 4, new UnitStats(10, 10, revivesPerCombat: 1));
            CombatSimulation sim = Begin(red, blue);
            sim.Step(.2f); Assert.IsTrue(blue.IsReviving && red.IsReviving); Assert.IsFalse(sim.Finished);
            sim.Step(.8f); Assert.AreEqual(5, blue.CurrentHealth); Assert.AreEqual(5, red.CurrentHealth); Assert.IsFalse(sim.Finished);
            sim.Step(.2f); Assert.IsTrue(sim.Finished); Assert.IsNull(sim.Winner);
            Assert.IsFalse(blue.IsReviving || red.IsReviving);
        }

        [Test] public void LaunchedHearts_SurviveSourceEnteringRevival()
        {
            BattleUnit anuik = Unit("anuik", BoardSide.Blue, 2, 4, new UnitStats(10, 7, 3, revivesPerCombat: 1));
            BattleUnit enemy = Unit("enemy", BoardSide.Red, 3, 4, new UnitStats(50, 10, attackInterval: 10));
            CombatSimulation sim = Begin(anuik, enemy);
            sim.Step(.2f); Assert.IsTrue(anuik.IsReviving); Assert.AreEqual(50, enemy.CurrentHealth);
            sim.Step(.2f); Assert.AreEqual(43, enemy.CurrentHealth); Assert.IsTrue(anuik.IsReviving);
        }

        [Test] public void ImpactsArrivingDuringRevival_AreDiscardedAndCannotDamageOrHealIt()
        {
            BattleUnit anuik = Unit("anuik", BoardSide.Blue, 2, 4, new UnitStats(10, 0, revivesPerCombat: 1));
            BattleUnit melee = Unit("melee", BoardSide.Red, 3, 4, new UnitStats(100, 10, attackInterval: 10));
            BattleUnit ranged = Unit("ranged", BoardSide.Red, 2, 1, new UnitStats(100, 50, 9, attackInterval: 10));
            CombatSimulation sim = Begin(anuik, melee, ranged); int rangedHits = 0;
            sim.Impacted += (source, target, damage) => { if (source == ranged) rangedHits++; };
            sim.Step(.5f); Assert.IsTrue(anuik.IsReviving); Assert.AreEqual(0, rangedHits); Assert.AreEqual(0, anuik.Heal(100));
            sim.Step(.5f); Assert.AreEqual(5, anuik.CurrentHealth);
        }

        [TestCase(true)] [TestCase(false)]
        public void FourthLandedBite_KillsMonsterOrWhelpRegardlessOfArmor(bool monster)
        {
            BattleUnit tauris = Unit("tauris", BoardSide.Blue, 2, 4, new UnitStats(100, 1, attackInterval: .2f, lethalEveryHits: 4));
            BattleUnit target = Unit("target", BoardSide.Red, 3, 4, new UnitStats(100, 0, armor: 999), monster);
            CombatSimulation sim = Begin(tauris, target); int lethal = 0; var hits = new List<int>();
            sim.LethalImpacted += (source, victim) => { Assert.AreSame(tauris, source); Assert.AreSame(target, victim); lethal++; };
            sim.Impacted += (source, victim, damage) => { if (source == tauris) hits.Add(damage); };
            sim.Step(.6f); Assert.AreEqual(97, target.CurrentHealth); Assert.AreEqual(0, lethal); Assert.IsTrue(sim.IsNextHitLethal(tauris));
            sim.Step(.2f); Assert.AreEqual(0, target.CurrentHealth); Assert.AreEqual(1, lethal);
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 97 }, hits); Assert.AreEqual(BoardSide.Blue, sim.Winner);
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void TaurisDefinition_PursuesUntilAdjacent_UsesOnlyMeleeContactsAndKeepsFourthHitLethal(BoardSide side)
        {
            int startY = side == BoardSide.Blue ? 6 : 3;
            int targetY = side == BoardSide.Blue ? 3 : 6;
            BattleUnit tauris = Unit("tauris", side, 2, startY, UnitStats.FromDefinition(ExpandedRoster.CreateTauris()));
            BattleUnit target = Unit("target", side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue,
                2, targetY, new UnitStats(1000, 0, 9, attackInterval: 100, armor: 999));
            CombatSimulation sim = Begin(target, tauris);
            int moves = 0, attacks = 0, lethal = 0; float firstAttack = -1, firstImpact = -1;
            var hits = new List<int>();
            sim.Moved += (actor, from, to) => { if (actor == tauris) moves++; };
            sim.Attacked += (actor, victim, projectile) =>
            {
                if (actor != tauris) return;
                attacks++; if (firstAttack < 0) firstAttack = sim.Elapsed;
                Assert.Greater(moves, 0, "Tauris must close the initial three-cell gap before biting.");
                Assert.IsTrue(CombatTargetSelector.IsInBasicAttackRange(actor.CurrentCell, victim.CurrentCell));
                Assert.IsFalse(projectile, "A Tauris bite must not start a flying projectile.");
                Assert.AreEqual(0f, CombatSimulation.GetProjectileTravelTime(actor, victim));
                Assert.AreEqual(CombatSimulation.GetAttackWindup(actor), CombatSimulation.GetImpactDelay(actor, victim));
            };
            sim.Impacted += (actor, victim, damage) =>
            {
                if (actor != tauris) return;
                if (firstImpact < 0) firstImpact = sim.Elapsed;
                hits.Add(damage);
            };
            sim.LethalImpacted += (actor, victim) => { if (actor == tauris) lethal++; };
            sim.Step(.1f);
            Assert.AreEqual(1, moves); Assert.AreEqual(0, attacks); Assert.AreEqual(1000, target.CurrentHealth);
            for (int tick = 0; tick < 100 && !sim.Finished; tick++) sim.Step(.1f);
            Assert.IsTrue(sim.Finished); Assert.AreEqual(side, sim.Winner);
            Assert.AreEqual(4, attacks); Assert.AreEqual(1, lethal);
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 997 }, hits);
            Assert.AreEqual(CombatSimulation.GetAttackWindup(tauris), firstImpact - firstAttack, .0001f);
            Assert.AreEqual(100, tauris.CurrentHealth); Assert.AreEqual(6, tauris.BaseStats.Attack);
        }

        [Test] public void SimultaneousLethalBites_ObserveSameLifeSnapshotAndDraw()
        {
            BattleUnit blue = Unit("blue", BoardSide.Blue, 2, 4, new UnitStats(100, 1, armor: 999, lethalEveryHits: 1));
            BattleUnit red = Unit("red", BoardSide.Red, 3, 4, new UnitStats(100, 1, armor: 999, lethalEveryHits: 1));
            CombatSimulation sim = Begin(red, blue); int lethal = 0; sim.LethalImpacted += (a, b) => lethal++;
            sim.Step(.2f); Assert.AreEqual(2, lethal); Assert.IsTrue(sim.Finished); Assert.IsNull(sim.Winner);
        }

        [Test] public void ZeroDamageLethalImpactPending_PreventsPrematureStalemate()
        {
            BattleUnit tauris = Unit("tauris", BoardSide.Blue, 2, 4,
                new UnitStats(100, 0, attackInterval: 100, attackWindup: 11f, lethalEveryHits: 1));
            BattleUnit target = Unit("target", BoardSide.Red, 3, 4, new UnitStats(100, 0, attackInterval: 100));
            CombatSimulation sim = Begin(tauris, target);
            sim.Step(10f); Assert.IsFalse(sim.Finished); Assert.AreEqual(100, target.CurrentHealth);
            sim.Step(1.1f); Assert.IsTrue(sim.Finished); Assert.AreEqual(BoardSide.Blue, sim.Winner); Assert.AreEqual(0, target.CurrentHealth);
        }

        [Test] public void ZeroDamageHits_ChargeLethalAbilityAsProgressBetweenProjectiles()
        {
            BattleUnit tauris = Unit("tauris", BoardSide.Blue, 2, 6,
                new UnitStats(100, 0, 9, attackInterval: 4f, lethalEveryHits: 4));
            BattleUnit target = Unit("target", BoardSide.Red, 2, 2, new UnitStats(100, 0, 9, attackInterval: 100));
            CombatSimulation sim = Begin(tauris, target);
            sim.Step(10f); Assert.IsFalse(sim.Finished); Assert.AreEqual(100, target.CurrentHealth);
            Assert.IsTrue(sim.IsNextHitLethal(tauris));
            sim.Step(2.6f); Assert.IsTrue(sim.Finished); Assert.AreEqual(BoardSide.Blue, sim.Winner);
        }

        [Test] public void LethalBite_AllowsOneConfiguredRevival()
        {
            BattleUnit tauris = Unit("tauris", BoardSide.Red, 3, 4, new UnitStats(100, 1, attackInterval: 10, lethalEveryHits: 1));
            BattleUnit anuik = Unit("anuik", BoardSide.Blue, 2, 4, new UnitStats(100, 0, armor: 999, revivesPerCombat: 1));
            CombatSimulation sim = Begin(tauris, anuik); sim.Step(.2f); Assert.IsTrue(anuik.IsReviving);
            sim.Step(.8f); Assert.AreEqual(50, anuik.CurrentHealth); Assert.IsFalse(sim.Finished);
        }

        [Test] public void Kayon_AtFourCellsFiresWithoutApproaching_AndKeepsItsSixSecondSummons()
        {
            BattleUnit kayon = Unit("kayon-coins", BoardSide.Blue, 2, 8,
                UnitStats.FromDefinition(ExpandedRoster.CreateKayon()), false);
            BattleUnit enemy = Unit("coin-target", BoardSide.Red, 2, 4, new UnitStats(1000, 0, 9, attackInterval: 100));
            BoardCell origin = kayon.CurrentCell;
            CombatSimulation sim = Begin(kayon, enemy);
            int rangedAttacks = 0, moves = 0;
            sim.Attacked += (source, target, projectile) =>
            {
                if (source != kayon) return;
                Assert.AreSame(enemy, target);
                Assert.IsTrue(projectile, "Kayon's gold coins must use ranged travel, not a melee hit.");
                rangedAttacks++;
            };
            sim.Moved += (unit, from, to) => { if (unit == kayon) moves++; };

            Assert.AreEqual(4, kayon.BaseStats.AttackRange);
            sim.Step(.7f);
            Assert.Greater(rangedAttacks, 0);
            Assert.AreEqual(1000, enemy.CurrentHealth, "The coin must still be in flight before its windup plus four-cell travel.");
            sim.Step(.1f);
            Assert.AreEqual(998, enemy.CurrentHealth, "The ranged hit retains Kayon's two damage.");
            Assert.AreSame(origin, kayon.CurrentCell);
            Assert.AreEqual(0, moves, "A target four cells away is already in range.");

            sim.Step(5.1f);
            Assert.AreEqual(0, sim.CombatOnlyUnits.Count, "The first invocation must not arrive before six seconds.");
            sim.Step(.1f);
            Assert.AreEqual(1, sim.CombatOnlyUnits.Count);
            BattleUnit firstDummy = sim.CombatOnlyUnits[0];
            Assert.IsTrue(firstDummy.IsCombatSummon);
            Assert.AreEqual(14, firstDummy.BaseStats.MaxHealth);
            Assert.AreEqual(2, firstDummy.BaseStats.Attack);
            Assert.AreEqual(1, Mathf.Abs(origin.X - firstDummy.CurrentCell.X) + Mathf.Abs(origin.Y - firstDummy.CurrentCell.Y));
            sim.Step(6f);
            Assert.AreEqual(2, sim.CombatOnlyUnits.Count);
            sim.Step(6f);
            Assert.AreEqual(2, sim.CombatOnlyUnits.Count, "Coins must not remove the two-living-Dummy cap.");
            Assert.AreEqual(28, kayon.CurrentHealth);
            Assert.AreSame(origin, kayon.CurrentCell);
            Assert.AreEqual(0, moves);
            Assert.Greater(rangedAttacks, 4);
        }

        [Test] public void Kayon_CreatesWeakCombatOnlyDummysAtSixSeconds_WithTwoLivingMaximum()
        {
            BattleUnit kayon = Unit("kayon", BoardSide.Blue, 2, 8, new UnitStats(999, 0, 9, summonInterval: 6, maxLivingSummons: 2), false);
            BattleUnit enemy = Unit("enemy", BoardSide.Red, 2, 1, new UnitStats(9999, 0, 9));
            CombatSimulation sim = Begin(kayon, enemy); int events = 0;
            sim.Summoned += (unit, definition) =>
            {
                events++; Assert.IsTrue(unit.IsCombatSummon); Assert.AreEqual("dummy", definition.Id);
                Assert.AreEqual(14, unit.CurrentHealth); Assert.AreEqual(2, unit.BaseStats.Attack); Assert.AreEqual(0, unit.BaseStats.Armor);
                Assert.AreEqual(BoardSide.Blue, unit.Side); Assert.AreSame(unit, unit.CurrentCell.OccupiedBy);
                Assert.AreEqual(1, Mathf.Abs(kayon.CurrentCell.X - unit.CurrentCell.X) + Mathf.Abs(kayon.CurrentCell.Y - unit.CurrentCell.Y));
            };
            sim.Step(5.9f); Assert.AreEqual(0, events);
            sim.Step(.1f); Assert.AreEqual(1, events);
            sim.Step(12f); Assert.AreEqual(2, events); Assert.AreEqual(2, sim.CombatOnlyUnits.Count);
            Assert.AreEqual(2, sim.CombatOnlyUnits.Count(unit => unit.IsAlive));
            Assert.AreNotEqual(sim.CombatOnlyUnits[0].UnitId, sim.CombatOnlyUnits[1].UnitId);
            BattleUnit[] temporary = sim.CombatOnlyUnits.ToArray(); sim.Stop();
            Assert.AreEqual(0, sim.CombatOnlyUnits.Count); foreach (BattleUnit unit in temporary) Assert.IsTrue(unit == null);
        }

        [Test] public void DeadSummon_OpensCapAgainAndLastSummonKeepsItsTeamFighting()
        {
            BattleUnit kayon = Unit("kayon", BoardSide.Blue, 2, 8, new UnitStats(999, 0, 9, summonInterval: .2f, maxLivingSummons: 1), false);
            BattleUnit enemy = Unit("enemy", BoardSide.Red, 2, 1, new UnitStats(9999, 0, 9));
            CombatSimulation sim = Begin(kayon, enemy); sim.Step(.2f);
            BattleUnit first = sim.CombatOnlyUnits[0]; first.ApplyDamage(999); sim.Step(.2f);
            Assert.AreEqual(2, sim.CombatOnlyUnits.Count); BattleUnit last = sim.CombatOnlyUnits[1]; Assert.IsTrue(last.IsAlive);
            kayon.ApplyDamage(9999); sim.Step(.1f); Assert.IsFalse(sim.Finished); Assert.IsTrue(last.IsAlive);
            last.ApplyDamage(999); sim.Step(.1f); Assert.IsTrue(sim.Finished); Assert.AreEqual(BoardSide.Red, sim.Winner);
        }

        [Test] public void SurroundedKayon_WaitsForFreeNeighborWithoutOverwritingOccupants()
        {
            BattleUnit kayon = Unit("kayon", BoardSide.Blue, 2, 8, new UnitStats(999, 0, 9, summonInterval: .2f, maxLivingSummons: 1), false);
            BattleUnit enemy = Unit("enemy", BoardSide.Red, 2, 1, new UnitStats(9999, 0, 9));
            BattleUnit front = Unit("front", BoardSide.Blue, 2, 7, new UnitStats(999, 0, 9));
            BattleUnit left = Unit("left", BoardSide.Blue, 1, 8, new UnitStats(999, 0, 9));
            BattleUnit right = Unit("right", BoardSide.Blue, 3, 8, new UnitStats(999, 0, 9));
            BattleUnit back = Unit("back", BoardSide.Blue, 2, 9, new UnitStats(999, 0, 9));
            CombatSimulation sim = Begin(kayon, enemy, front, left, right, back);
            sim.Step(.3f); Assert.AreEqual(0, sim.CombatOnlyUnits.Count); Assert.AreSame(front, board.GetCell(2, 7).OccupiedBy);
            front.ApplyDamage(9999); sim.Step(.1f); Assert.AreEqual(1, sim.CombatOnlyUnits.Count);
            Assert.AreSame(sim.CombatOnlyUnits[0], board.GetCell(2, 7).OccupiedBy);
        }

        [Test] public void Summoning_CannotAddCopiesOrCoinsToItsOwnersPouch()
        {
            var config = ScriptableObject.CreateInstance<MatchConfig>();
            try
            {
                UnitDefinition kayonDefinition = ExpandedRoster.CreateKayon();
                kayonDefinition.AttackRange = 9; kayonDefinition.Damage = 0; kayonDefinition.SummonInterval = .2f;
                config.Units = new[] { ExpandedRoster.CreateAnuik(), kayonDefinition };
                var pouch = new PouchState(config, 123, "anuik", BoardSide.Blue, new[] { "kayon" });
                Assert.IsTrue(pouch.BeginPreparation(1)); Assert.IsTrue(pouch.TryBuy(0, pouch.Offers[0].Token, out _));
                OwnedUnit owned = pouch.Owned["kayon"];
                int coins = pouch.Coins, pool = pouch.PoolCount, count = pouch.Owned.Count, copies = owned.Copies;
                BattleUnit kayon = Unit("kayon", BoardSide.Blue, 2, 8, UnitStats.FromDefinition(owned.Definition), false);
                BattleUnit enemy = Unit("enemy", BoardSide.Red, 2, 1, new UnitStats(9999, 0, 9));
                var sim = new CombatSimulation(board); simulations.Add(sim);
                sim.Begin(new[] { kayon, enemy }, new Dictionary<BattleUnit, OwnedUnit> { [kayon] = owned });
                sim.Step(.4f); Assert.AreEqual(2, sim.CombatOnlyUnits.Count);
                Assert.AreEqual(coins, pouch.Coins); Assert.AreEqual(pool, pouch.PoolCount); Assert.AreEqual(count, pouch.Owned.Count);
                Assert.AreEqual(copies, owned.Copies); Assert.IsFalse(pouch.Owned.ContainsKey("dummy"));
                Assert.IsFalse(pouch.TrySell("dummy", out _)); Assert.AreEqual(coins, pouch.Coins);
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test] public void BlockedFrontCell_SummonsIntoNextLegalNeighborInsteadOfRetryingBlockedCell()
        {
            BattleUnit kayon = Unit("kayon", BoardSide.Blue, 2, 8,
                new UnitStats(999, 0, 9, summonInterval: .2f, maxLivingSummons: 1), false);
            BattleUnit enemy = Unit("enemy", BoardSide.Red, 2, 1, new UnitStats(9999, 0, 9));
            Assert.IsTrue(board.TrySetCellBlocked(2, 7, true));
            CombatSimulation sim = Begin(kayon, enemy); sim.Step(.2f);
            Assert.AreEqual(1, sim.CombatOnlyUnits.Count);
            Assert.AreSame(board.GetCell(1, 8), sim.CombatOnlyUnits[0].CurrentCell);
            Assert.IsTrue(board.GetCell(2, 7).IsBlocked); Assert.IsNull(board.GetCell(2, 7).OccupiedBy);
        }

        [Test] public void AllSummonNeighborsBlocked_DoesNotPretendUsefulProgressIsPending()
        {
            BattleUnit kayon = Unit("kayon", BoardSide.Blue, 2, 8,
                new UnitStats(999, 0, 9, summonInterval: .2f, maxLivingSummons: 1), false);
            BattleUnit enemy = Unit("enemy", BoardSide.Red, 2, 1, new UnitStats(9999, 0, 9));
            Assert.IsTrue(board.TrySetCellBlocked(2, 7, true)); Assert.IsTrue(board.TrySetCellBlocked(1, 8, true));
            Assert.IsTrue(board.TrySetCellBlocked(3, 8, true)); Assert.IsTrue(board.TrySetCellBlocked(2, 9, true));
            CombatSimulation sim = Begin(kayon, enemy); sim.Step(10.1f);
            Assert.AreEqual(0, sim.CombatOnlyUnits.Count); Assert.IsTrue(sim.Finished);
            StringAssert.Contains("Sin progreso", sim.Reason); Assert.AreEqual(10f, sim.Elapsed, .0001f);
        }

        [Test] public void NewFactories_ProduceSelectableDefinitionsAndDetachedCombatValues()
        {
            UnitDefinition[] definitions = ExpandedRoster.CreateAll();
            Assert.AreEqual(4, definitions.Select(d => d.Id).Distinct().Count());
            Assert.IsTrue(definitions[0].IsMonster && definitions[1].IsMonster);
            Assert.IsFalse(definitions[2].IsMonster || definitions[3].IsMonster);
            foreach (UnitDefinition definition in definitions)
            {
                UnitStats stats = UnitStats.FromDefinition(definition);
                if (definition.Id == "tauris") Assert.AreEqual(1, stats.AttackRange);
                else Assert.Greater(stats.AttackRange, 1);
                if (!definition.IsMonster) { Assert.AreEqual(3, definition.Tricks.Length); Assert.IsTrue(definition.Tricks.All(trick => trick != null)); }
            }
            UnitDefinition anuik = ExpandedRoster.CreateAnuik(); UnitStats snapshot = UnitStats.FromDefinition(anuik);
            anuik.RevivesPerCombat = 8; Assert.AreEqual(1, snapshot.RevivesPerCombat);
            Assert.AreEqual(4, UnitStats.FromDefinition(ExpandedRoster.CreateTauris()).LethalEveryHits);
            Assert.AreEqual(2, UnitStats.FromDefinition(ExpandedRoster.CreateKayon()).MaxLivingSummons);
        }

        [Test] public void RevivalAllowance_ResetsWhenANewCombatBegins()
        {
            BattleUnit anuik = Unit("anuik", BoardSide.Blue, 2, 4, new UnitStats(10, 0, revivesPerCombat: 1));
            BattleUnit enemy = Unit("enemy", BoardSide.Red, 3, 4, new UnitStats(100, 0));
            CombatSimulation sim = Begin(anuik, enemy); int starts = 0; sim.Reviving += (u, duration) => starts++;
            anuik.ApplyDamage(10); sim.Step(.9f); Assert.AreEqual(5, anuik.CurrentHealth); Assert.AreEqual(1, starts);
            sim.Begin(new[] { anuik, enemy }); Assert.AreEqual(10, anuik.CurrentHealth);
            anuik.ApplyDamage(10); sim.Step(.1f); Assert.IsTrue(anuik.IsReviving); Assert.AreEqual(2, starts);
        }

        [Test] public void TimeoutDuringRevival_ReleasesDeadOccupantAndEmitsOneFinalDeath()
        {
            BattleUnit anuik = Unit("anuik", BoardSide.Blue, 2, 4,
                new UnitStats(1000, 0, 9, attackInterval: 100, revivesPerCombat: 1));
            BattleUnit enemy = Unit("enemy", BoardSide.Red, 3, 4, new UnitStats(1000, 1, 9));
            BoardCell origin = anuik.CurrentCell; CombatSimulation sim = Begin(anuik, enemy); int finalDeaths = 0;
            sim.Died += unit =>
            {
                Assert.AreSame(anuik, unit); Assert.IsFalse(unit.IsReviving); Assert.IsNull(unit.CurrentCell); finalDeaths++;
            };
            sim.Step(39.7f); Assert.IsFalse(sim.Finished);
            anuik.ApplyDamage(anuik.CurrentHealth); sim.Step(.1f);
            Assert.IsTrue(anuik.IsReviving); Assert.AreSame(anuik, origin.OccupiedBy); Assert.AreEqual(0, finalDeaths);
            sim.Step(.2f); Assert.IsTrue(sim.Finished); Assert.IsNull(sim.Winner);
            StringAssert.Contains("Tiempo", sim.Reason); Assert.AreEqual(40f, sim.Elapsed, .0001f);
            Assert.IsFalse(anuik.IsReviving); Assert.IsNull(anuik.CurrentCell); Assert.IsNull(origin.OccupiedBy);
            Assert.AreEqual(1, finalDeaths); sim.Step(1f); Assert.AreEqual(1, finalDeaths);
        }

        [Test] public void ExpandedCombat_ProducesIdenticalEventsAcrossInputOrderAndFrameDurations()
        {
            List<string> fixedSteps = RecordedBattle(false);
            List<string> unevenSteps = RecordedBattle(true);
            Assert.IsTrue(fixedSteps.Any(item => item.StartsWith("reviving:")));
            Assert.IsTrue(fixedSteps.Any(item => item.StartsWith("lethal:")));
            Assert.IsTrue(fixedSteps.Any(item => item.StartsWith("summon:")));
            CollectionAssert.AreEqual(fixedSteps, unevenSteps);
        }

        private List<string> RecordedBattle(bool uneven)
        {
            var go = new GameObject("recorded-board"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
            BattleUnit anuik = Unit("anuik", BoardSide.Blue, 2, 4, new UnitStats(40, 3, 3, attackInterval: .6f, revivesPerCombat: 1));
            // Keep the first four bites aimed at Anuik before any decoy can spawn or approach.
            // The old .6-second summons could intercept Tauris and never exercise revival.
            BattleUnit kayon = Unit("kayon", BoardSide.Blue, 5, 9,
                new UnitStats(100, 0, 9, summonInterval: 2f, maxLivingSummons: 2, summonHealth: 8, summonDamage: 1), false);
            BattleUnit tauris = Unit("tauris", BoardSide.Red, 2, 3, new UnitStats(500, 1, 1, attackInterval: .2f, lethalEveryHits: 4));
            var sim = new CombatSimulation(board); simulations.Add(sim); var events = new List<string>();
            sim.Reviving += (unit, duration) => events.Add("reviving:" + unit.UnitId + ":" + Mathf.RoundToInt(sim.Elapsed * 10));
            sim.Revived += unit => events.Add("revived:" + unit.UnitId + ":" + unit.CurrentHealth);
            sim.Summoned += (unit, definition) => events.Add("summon:" + unit.UnitId + ":" + unit.CurrentCell.X + "," + unit.CurrentCell.Y);
            sim.LethalImpacted += (source, target) => events.Add("lethal:" + source.UnitId + ":" + target.UnitId);
            sim.Impacted += (source, target, amount) => events.Add("hit:" + source.UnitId + ":" + target.UnitId + ":" + amount);
            sim.Moved += (unit, from, to) => events.Add("move:" + unit.UnitId + ":" + to.X + "," + to.Y);
            sim.Died += unit => events.Add("dead:" + unit.UnitId);
            sim.Begin(uneven ? new[] { tauris, kayon, anuik } : new[] { anuik, kayon, tauris });
            if (uneven) for (int i = 0; i < 10; i++) { sim.Step(.37f); sim.Step(.23f); }
            else for (int i = 0; i < 60; i++) sim.Step(.1f);
            foreach (BattleUnit unit in new[] { anuik, kayon, tauris }.Concat(sim.CombatOnlyUnits).OrderBy(unit => unit.UnitId))
                events.Add("state:" + unit.UnitId + ":" + unit.CurrentHealth + ":" + unit.IsReviving);
            events.Add("result:" + sim.Finished + ":" + sim.Winner + ":" + Mathf.RoundToInt(sim.Elapsed * 10));
            return events;
        }
    }
}
