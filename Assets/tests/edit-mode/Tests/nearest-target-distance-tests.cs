using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class NearestTargetDistanceTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board;
        CombatSimulation sim;
        static int Y(BoardSide side, int y) => side == BoardSide.Blue ? y : 9 - y;
        static BoardSide Other(BoardSide side) => side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue;

        [SetUp] public void Setup()
        {
            var go = new GameObject("nearest-target-distance"); objects.Add(go);
            board = go.AddComponent<BoardManager>(); board.BuildBoard();
        }

        [TearDown] public void Cleanup()
        {
            sim?.Stop(); sim = null;
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        BattleUnit Unit(string id, BoardSide side, int x, int y, UnitDefinition definition = null, params int[] tricks)
        {
            definition = definition ?? new UnitDefinition { Id = "target", MaxHealth = 1000, Damage = 0,
                AttackRange = 9, AttackInterval = 100, MoveInterval = 100 };
            var owned = new OwnedUnit(definition);
            foreach (int trick in tricks) owned.Tricks[trick] = true;
            var go = new GameObject(id); objects.Add(go);
            var unit = go.AddComponent<MonsterUnit>();
            unit.Initialize(id, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }

        void Begin(BattleUnit actor, params BattleUnit[] passiveEnemies)
        {
            var units = new List<BattleUnit> { actor }; units.AddRange(passiveEnemies);
            sim = new CombatSimulation(board); sim.Begin(units);
            foreach (var enemy in passiveEnemies) enemy.IsPassiveTrainingTarget = true;
        }

        static UnitDefinition Basic(int range, bool documented = true) => new UnitDefinition
        {
            Id = "distance-fixture", UsesDocumentedRules = documented, MaxHealth = 1000, Damage = 10,
            AttackRange = range, AttackInterval = .8f, AttackWindup = .1f,
            AttackHold = 0, AttackRecovery = 0, MoveInterval = .2f, IQSpeed = 10
        };

        [TestCase(BoardSide.Blue, 1, false)] [TestCase(BoardSide.Red, 1, false)]
        [TestCase(BoardSide.Blue, 6, false)] [TestCase(BoardSide.Red, 6, false)]
        [TestCase(BoardSide.Blue, 1, true)] [TestCase(BoardSide.Red, 1, true)]
        [TestCase(BoardSide.Blue, 6, true)] [TestCase(BoardSide.Red, 6, true)]
        public void CombatAcquisitionPrefersNearLateralOverFarFrontal(BoardSide side, int range, bool documented)
        {
            var actor = Unit("actor", side, 2, Y(side, 8), Basic(range, documented));
            var lateral = Unit("z-lateral", Other(side), 4, Y(side, 6)); // sqrt(8)
            var frontal = Unit("a-frontal", Other(side), 2, Y(side, 5)); // 3; Manhattan wrongly wins
            foreach (var order in new[] { new[] { frontal, lateral }, new[] { lateral, frontal } })
                Assert.AreSame(lateral, CombatTargetSelector.SelectTarget(board, actor, order).Target);
            Begin(actor, frontal, lateral);
            BattleUnit attacked = null;
            sim.Attacked += (source, target, _) => { if (source == actor) attacked = target; };
            sim.Step(.1f);
            Assert.AreSame(lateral, sim.CurrentTarget(actor), "Must reach the real combat acquisition path.");
            if (range == 1)
            { Assert.IsNull(attacked); Assert.IsNotNull(actor.ReservedCell, "Melee starts its approach."); }
            else
            {
                Assert.AreSame(lateral, attacked);
                sim.Step(.7f);
                Assert.Less(lateral.CurrentHealth, 1000);
                Assert.AreEqual(1000, frontal.CurrentHealth);
            }
        }

        [TestCase(BoardSide.Blue, "sepora")] [TestCase(BoardSide.Red, "sepora")]
        [TestCase(BoardSide.Blue, "stein")] [TestCase(BoardSide.Red, "stein")]
        [TestCase(BoardSide.Blue, "tauris")] [TestCase(BoardSide.Red, "tauris")]
        public void CurrentCharacterAssetsUseDistanceInRealCombat(BoardSide side, string id)
        {
            var asset = Resources.Load<CharacterDefinition>("MonsterPouch/Characters/" + id + "/Character");
            Assert.IsNotNull(asset, "Read current character assets, not historical display names.");
            var definition = asset.CreateRuntimeDefinition();
            Assert.AreEqual(id, definition.Id);
            if (id == "sepora") Assert.AreEqual("Selora", definition.DisplayName);
            var actor = Unit(id, side, 2, Y(side, 8), definition);
            var lateral = Unit("z-lateral", Other(side), 4, Y(side, 6));
            var frontal = Unit("a-frontal", Other(side), 2, Y(side, 5));
            Begin(actor, frontal, lateral);
            BattleUnit attacked = null;
            sim.Attacked += (source, target, _) => { if (source == actor && attacked == null) attacked = target; };
            sim.Step(.1f);
            Assert.AreSame(lateral, sim.CurrentTarget(actor));
            if (definition.AttackRange > 1)
            {
                Assert.AreSame(lateral, attacked);
                for (int i = 0; i < 20 && lateral.CurrentHealth == 1000; i++) sim.Step(.1f);
                Assert.Less(lateral.CurrentHealth, 1000);
                Assert.AreEqual(1000, frontal.CurrentHealth);
            }
            else Assert.IsNotNull(actor.ReservedCell);
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void SameManhattanRingStillChoosesGeometricallyCloserEnemy(BoardSide side)
        {
            var actor = Unit("actor", side, 2, Y(side, 8), Basic(6));
            var lateral = Unit("z-nearer", Other(side), 3, Y(side, 7)); // sqrt(2)
            var frontal = Unit("a-farther", Other(side), 2, Y(side, 6)); // 2
            Begin(actor, frontal, lateral); sim.Step(.1f);
            Assert.AreSame(lateral, sim.CurrentTarget(actor));
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void LeavingRangeReacquiresNearestOccupiedEnemyAfterReactionDelay(BoardSide side)
        {
            var actor = Unit("actor", side, 2, Y(side, 8), Basic(3));
            var first = Unit("first", Other(side), 2, Y(side, 7));
            var lateral = Unit("z-lateral", Other(side), 4, Y(side, 6));
            var frontal = Unit("a-frontal", Other(side), 2, Y(side, 5));
            Begin(actor, first, frontal, lateral); sim.Step(.1f);
            Assert.AreSame(first, sim.CurrentTarget(actor));
            Assert.IsTrue(board.TryRepositionUnit(first, board.GetCell(5, Y(side, 0))));
            sim.Step(.3f);
            Assert.AreSame(lateral, sim.CurrentTarget(actor));
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void ReservationsDoNotChangeDistanceAndArrivalDoesNotStealValidLock(BoardSide side)
        {
            var actor = Unit("actor", side, 2, Y(side, 8), Basic(6));
            var lateral = Unit("z-lateral", Other(side), 4, Y(side, 6)); // sqrt(8)
            var moving = Unit("a-moving", Other(side), 2, Y(side, 5)); // 3
            Begin(actor, moving, lateral);
            var origin = moving.CurrentCell; var destination = board.GetCell(2, Y(side, 6));
            Assert.IsTrue(board.TryReserveCell(moving, destination));
            var step = new CombatMovementStep(moving, 4);
            sim.Step(.1f);
            Assert.AreSame(lateral, sim.CurrentTarget(actor));
            Assert.IsFalse(step.Advance(board, 3));
            Assert.AreSame(moving, origin.OccupiedBy);
            Assert.AreSame(moving, destination.ReservedBy); Assert.IsNull(destination.OccupiedBy);
            Assert.IsTrue(step.Advance(board, 4)); Assert.IsTrue(step.Arrived);
            Assert.AreSame(moving, CombatTargetSelector.SelectTarget(board, actor, new[] { lateral, moving }).Target);
            sim.Step(.8f);
            Assert.AreSame(lateral, sim.CurrentTarget(actor), "Arrival must preserve the approved in-range lock.");
        }

        [TestCase(BoardSide.Blue, 1)] [TestCase(BoardSide.Red, 1)]
        [TestCase(BoardSide.Blue, 3)] [TestCase(BoardSide.Red, 3)]
        public void ReservedCellInsideRangeCannotBeAttackedUntilOccupied(BoardSide side, int range)
        {
            var actor = Unit("actor", side, 2, Y(side, 8), Basic(range));
            var target = Unit("moving", Other(side), 2, Y(side, 7 - range));
            Begin(actor, target);
            var origin = target.CurrentCell; var destination = board.GetCell(2, Y(side, 8 - range));
            Assert.IsTrue(board.TryReserveCell(target, destination));
            var step = new CombatMovementStep(target, 5);
            var before = CombatTargetSelector.SelectTarget(board, actor, new[] { target });
            Assert.AreEqual(CombatTargetSelectionStatus.MoveRequested, before.Status);
            int attacks = 0; sim.Attacked += (source, _, __) => { if (source == actor) attacks++; };
            sim.Step(.1f); Assert.AreEqual(0, attacks);
            Assert.AreSame(origin, target.CurrentCell); Assert.IsNull(destination.OccupiedBy);
            Assert.IsFalse(step.Advance(board, 4));
            Assert.IsTrue(step.Advance(board, 5));
            Assert.AreEqual(CombatTargetSelectionStatus.ReadyToAttack,
                CombatTargetSelector.SelectTarget(board, actor, new[] { target }).Status);
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void LowestHealthAndItsExistingLockRemainExplicitExceptions(BoardSide side)
        {
            var definition = Basic(6); definition.TargetPolicy = TargetPolicy.LowestHealth;
            var actor = Unit("hunter", side, 2, Y(side, 8), definition);
            var nearby = Unit("nearby", Other(side), 3, Y(side, 8));
            var weak = Unit("weak", Other(side), 2, Y(side, 5));
            Begin(actor, nearby, weak); weak.ApplyDamage(500);
            sim.Step(.1f); Assert.AreSame(weak, sim.CurrentTarget(actor));
            nearby.ApplyDamage(990); sim.Step(.8f);
            Assert.AreSame(weak, sim.CurrentTarget(actor));
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void FartherTaunterOverridesNearerLateralEnemy(BoardSide side)
        {
            var actor = Unit("10-actor", side, 2, Y(side, 7), Basic(6));
            var near = Unit("near", Other(side), 3, Y(side, 7));
            var definition = DocumentedRoster.CreateBugaloo();
            definition.MaxHealth = 1000; definition.Damage = 0; definition.AttackRange = 9;
            var taunter = Unit("00-taunter", Other(side), 2, Y(side, 5), definition);
            sim = new CombatSimulation(board); sim.Begin(new[] { actor, near, taunter });
            near.IsPassiveTrainingTarget = true;
            taunter.AddEnergy(taunter.BaseStats.EnergyMax);
            sim.Step(.1f);
            Assert.IsTrue(actor.IsTaunted); Assert.AreSame(taunter, sim.CurrentTarget(actor));
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void HymalPullFocusOverridesNearerEnemyAndCanForcePursuit(BoardSide side)
        {
            int actorX = side == BoardSide.Blue ? 3 : 1;
            int nearX = side == BoardSide.Blue ? 4 : 0;
            var definition = DocumentedRoster.CreateHymay();
            definition.MaxHealth = 1000; definition.AttackWindup = .1f;
            var hook = Unit("00-hymay", side, 2, Y(side, 7), definition, 0);
            Assert.IsTrue(hook.BaseStats.HasEffect("hymay-focus"));
            var actor = Unit("10-ally", side, actorX, Y(side, 6), Basic(1));
            var chosen = Unit("chosen", Other(side), 2, Y(side, 5));
            var near = Unit("near", Other(side), nearX, Y(side, 6));
            sim = new CombatSimulation(board); sim.Begin(new[] { hook, actor, chosen, near });
            chosen.IsPassiveTrainingTarget = near.IsPassiveTrainingTarget = true;
            hook.AddEnergy(hook.BaseStats.EnergyMax);
            sim.Step(.1f);
            Assert.AreSame(near, sim.CurrentTarget(actor), "Before pull: " + sim.CurrentTarget(actor)?.UnitId);
            for (int i = 0; i < 20 && !chosen.IsBeingPulled; i++) sim.Step(.1f);
            Assert.IsTrue(chosen.IsBeingPulled, "Exercise Hymal's actual pull and focus ability.");
            Assert.AreSame(chosen, sim.CurrentTarget(actor), "During pull: " + sim.CurrentTarget(actor)?.UnitId);
            // An explicit focus remains valid even if its target subsequently leaves range.
            // Finish the ability's own motion before an ordinary reposition.
            for (int i = 0; i < 20 && chosen.IsBeingPulled; i++) sim.Step(.1f);
            Assert.IsFalse(chosen.IsBeingPulled);
            Assert.IsTrue(board.TryRepositionUnit(chosen, board.GetCell(5, Y(side, 0))));
            sim.Step(.8f); Assert.AreSame(chosen, sim.CurrentTarget(actor), "Forced pursuit: " + sim.CurrentTarget(actor)?.UnitId);
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void NearFlyingOrInvisibleEnemyCannotWinAcquisition(BoardSide side)
        {
            var actor = Unit("actor", side, 2, Y(side, 8), Basic(6));
            var flying = Unit("flying", Other(side), 3, Y(side, 8));
            var hidden = Unit("hidden", Other(side), 1, Y(side, 8));
            var visible = Unit("visible", Other(side), 2, Y(side, 5));
            Begin(actor, flying, hidden, visible);
            flying.BeginFlight(3);
            // Exercise the actual timed stealth filter; IsInvisible is a presentation flag.
            var clock = typeof(CombatSimulation).GetMethod("Clock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var hiddenClock = clock.Invoke(sim, new object[] { hidden });
            hiddenClock.GetType().GetField("StealthUntil").SetValue(hiddenClock, 30);
            sim.Step(.1f); Assert.AreSame(visible, sim.CurrentTarget(actor));
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void SeloraSecondaryVolleyUsesDistanceWithoutReplacingPrimary(BoardSide side)
        {
            var definition = DocumentedRoster.CreateSepora();
            definition.AttackRange = 6; definition.AttackWindup = .1f;
            var actor = Unit("sepora", side, 2, Y(side, 8), definition, 1);
            var primary = Unit("primary", Other(side), 2, Y(side, 7));
            var lateral = Unit("z-lateral", Other(side), 4, Y(side, 6));
            var frontal = Unit("a-frontal", Other(side), 2, Y(side, 5));
            Begin(actor, frontal, lateral, primary);
            var hits = new List<BattleUnit>();
            sim.Impacted += (source, target, _) => { if (source == actor) hits.Add(target); };
            sim.Step(.5f);
            CollectionAssert.AreEqual(new[] { primary, lateral }, hits);
            Assert.AreSame(primary, sim.CurrentTarget(actor));
            Assert.AreEqual(1000, frontal.CurrentHealth);
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void AnuikBounceUsesDistanceFromHitEnemy(BoardSide side)
        {
            var definition = DocumentedRoster.CreateAnuik();
            definition.AttackRange = 6; definition.AttackWindup = .1f; definition.ProjectileSpeed = 100;
            definition.Tricks[2].Parameters = new[] { new AbilityParameter { Key = "bounceRange", Value = 4 } };
            var actor = Unit("anuik", side, 2, Y(side, 9), definition, 2);
            var primary = Unit("primary", Other(side), 2, Y(side, 8));
            var lateral = Unit("z-lateral", Other(side), 4, Y(side, 6));
            var frontal = Unit("a-frontal", Other(side), 2, Y(side, 5));
            Begin(actor, frontal, lateral, primary);
            BattleUnit bounce = null;
            sim.ProjectileBounced += (_, first, next, __) => { Assert.AreSame(primary, first); bounce = next; };
            sim.Step(.6f); Assert.AreSame(lateral, bounce);
            Assert.AreEqual(1000, frontal.CurrentHealth);
        }

        [TestCase(BoardSide.Blue)] [TestCase(BoardSide.Red)]
        public void EqualEuclideanDistanceRetainsBoardPriorityIndependentlyOfInputOrder(BoardSide side)
        {
            var actor = Unit("actor", side, 2, Y(side, 5), Basic(6));
            var left = Unit("left", Other(side), 0, Y(side, 3));
            var right = Unit("right", Other(side), 4, Y(side, 3));
            var expected = BoardPriorityResolver.CompareInRoundPosition(left, right) == BoardPriorityResult.First ? left : right;
            foreach (var order in new[] { new[] { left, right }, new[] { right, left } })
                Assert.AreSame(expected, CombatTargetSelector.SelectTarget(board, actor, order).Target);
        }
    }
}
