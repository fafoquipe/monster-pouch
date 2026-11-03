using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class TokoroIceCreamTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private BoardManager board;
        private CombatSimulation simulation;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("tokoro-cone-board");
            objects.Add(go);
            board = go.AddComponent<BoardManager>();
            board.BuildBoard();
        }

        [TearDown]
        public void TearDown()
        {
            simulation?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
        }

        [TestCase(BoardSide.Blue)]
        [TestCase(BoardSide.Red)]
        public void ExistingAllyConsumesConeInTheSameTickAsStun(BoardSide side)
        {
            BattleUnit tokoro = Tokoro(side);
            BattleUnit ally = Stationary("ally", 1, 5, side);
            BattleUnit enemy = Stationary("enemy", 2, 4, Opposite(side));
            Begin(tokoro, ally, enemy);
            ally.ApplyDamage(50);
            int healed = 0;
            simulation.Healed += (unit, amount) => { if (unit == ally) healed += amount; };

            simulation.Step(.2f);

            Assert.IsTrue(tokoro.IsStunned);
            Assert.AreEqual(60, ally.CurrentHealth);
            Assert.AreEqual(10, healed);
            Assert.IsFalse(HasCone(1, 5), "The occupied tile never retains the collected cone.");
        }

        [TestCase(BoardSide.Blue)]
        [TestCase(BoardSide.Red)]
        public void ExistingEnemyConsumesConeAndTakesIncreasedDamageWithoutMoving(BoardSide side)
        {
            BattleUnit tokoro = Tokoro(side);
            BattleUnit enemy = Stationary("enemy", 2, 4, Opposite(side));
            BattleUnit attacker = Unit(new UnitDefinition
            {
                Id = "attacker", MaxHealth = 100, Damage = 10, AttackRange = 1,
                AttackInterval = 100, AttackWindup = .3f, MoveInterval = 100
            }, 1, 4, side);
            Begin(tokoro, enemy, attacker);

            simulation.Step(.2f);
            Assert.IsFalse(HasCone(2, 4));
            Assert.AreEqual(100, enemy.CurrentHealth);
            simulation.Step(.2f);

            Assert.AreEqual(88, enemy.CurrentHealth, "The next 10 damage hit receives the 20% weakness bonus.");
            Assert.AreEqual(new Vector2Int(2, 4), enemy.CurrentCell.Coordinates);
        }

        [Test]
        public void EmptyTileKeepsConeUntilAUnitEntersIt()
        {
            BattleUnit tokoro = Tokoro(BoardSide.Blue);
            BattleUnit ally = Stationary("ally", 0, 8, BoardSide.Blue);
            BattleUnit enemy = Stationary("enemy", 2, 4, BoardSide.Red);
            Begin(tokoro, ally, enemy);
            ally.ApplyDamage(50);

            simulation.Step(.2f);
            Assert.IsTrue(HasCone(1, 5));
            Assert.AreEqual(50, ally.CurrentHealth);
            Assert.IsTrue(board.TryRepositionUnit(ally, board.GetCell(1, 5)));
            simulation.Step(.1f);

            Assert.AreEqual(60, ally.CurrentHealth);
            Assert.IsFalse(HasCone(1, 5));
        }

        [Test]
        public void CollectedConeCannotHealAgainOnLaterTicksOrReentry()
        {
            BattleUnit tokoro = Tokoro(BoardSide.Blue);
            BattleUnit ally = Stationary("ally", 1, 5, BoardSide.Blue);
            BattleUnit enemy = Stationary("enemy", 2, 4, BoardSide.Red);
            Begin(tokoro, ally, enemy);
            ally.ApplyDamage(50);
            int collections = 0;
            simulation.Healed += (unit, amount) => { if (unit == ally) collections++; };

            simulation.Step(.2f);
            simulation.Step(.2f);
            Assert.IsTrue(board.TryRepositionUnit(ally, board.GetCell(0, 8)));
            simulation.Step(.1f);
            Assert.IsTrue(board.TryRepositionUnit(ally, board.GetCell(1, 5)));
            simulation.Step(.1f);

            Assert.AreEqual(60, ally.CurrentHealth);
            Assert.AreEqual(1, collections);
            Assert.IsFalse(HasCone(1, 5));
        }

        [Test]
        public void OneStunCreatesOneBatchAndNeverRepeatsWhileStunned()
        {
            BattleUnit tokoro = Tokoro(BoardSide.Blue);
            BattleUnit ally = Stationary("ally", 1, 5, BoardSide.Blue);
            BattleUnit enemy = Stationary("enemy", 2, 4, BoardSide.Red);
            Begin(tokoro, ally, enemy);
            ally.ApplyDamage(50);

            simulation.Step(.2f);
            Assert.AreEqual(60, ally.CurrentHealth);
            simulation.Step(2f);
            Assert.IsTrue(tokoro.IsStunned);
            Assert.AreEqual(60, ally.CurrentHealth, "Remaining stunned must not create another batch.");
            Assert.IsFalse(HasCone(1, 5));
            var cones = simulation.GroundEffects.Where(effect => effect.Kind == "cone").ToList();
            Assert.AreEqual(6, cones.Count);
            Assert.AreEqual(cones.Count, cones.Select(effect => effect.Cell).Distinct().Count());
        }

        private bool HasCone(int x, int y) => simulation.GroundEffects.Any(effect =>
            effect.Kind == "cone" && effect.Cell == board.GetCell(x, y));

        private BattleUnit Tokoro(BoardSide side)
        {
            UnitDefinition definition = DocumentedRoster.CreateTokoro();
            // These tests isolate collection; delayed flight has separate coverage.
            definition.BaseAbility.Parameters=new[]{new AbilityParameter{Key="castDuration",Value=.1f},new AbilityParameter{Key="damageOverride",Value=0}};
            definition.Tricks[1].Parameters=new[]{new AbilityParameter{Key="travelDuration",Value=0}};
            definition.Damage = 0;
            definition.AttackWindup = .1f;
            definition.AttackInterval = 100;
            definition.MoveInterval = 100;
            return Unit(definition, 2, 5, side, 1);
        }

        private BattleUnit Stationary(string id, int x, int y, BoardSide side) => Unit(new UnitDefinition
        {
            Id = id, MaxHealth = 100, Damage = 0, AttackRange = 9,
            AttackInterval = 100, AttackWindup = .1f, MoveInterval = 100, ProjectileSpeed = 100
        }, x, y, side);

        private BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side, params int[] tricks)
        {
            var owned = new OwnedUnit(definition);
            foreach (int trick in tricks) owned.Tricks[trick] = true;
            var go = new GameObject(side + "-" + definition.Id + "-" + objects.Count);
            objects.Add(go);
            var unit = go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }

        private void Begin(BattleUnit tokoro, params BattleUnit[] others)
        {
            simulation = new CombatSimulation(board);
            simulation.Begin(new[] { tokoro }.Concat(others).ToArray());
            tokoro.AddEnergy(tokoro.BaseStats.EnergyMax);
        }

        private static BoardSide Opposite(BoardSide side) =>
            side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue;
    }
}
