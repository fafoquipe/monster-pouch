using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class HymaySuperLogicTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private BoardManager board;
        private CombatSimulation simulation;

        [SetUp]
        public void SetUp()
        {
            var boardObject = new GameObject("hymay-super-board");
            objects.Add(boardObject);
            board = boardObject.AddComponent<BoardManager>();
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

        [Test]
        public void BasicThatFillsEnergyFinishesBeforeSuperStartsOnTheNextAction()
        {
            UnitDefinition definition = DocumentedRoster.CreateTauris();
            definition.MaxHealth = 1000;
            definition.Damage = 10;
            definition.EnergyPerAttack = definition.EnergyMax;
            definition.AttackInterval = .2f;
            definition.AttackWindup = .1f;
            // Isolate the energy/action boundary from the separately tested visual recovery.
            definition.AttackHold = definition.AttackRecovery = 0;
            BattleUnit tauris = Unit(definition, 2, 5, BoardSide.Blue);
            BattleUnit target = Target(2, 4, BoardSide.Red);
            int attackSignals = 0, superSignals = 0;

            Begin(tauris, target);
            simulation.Attacked += (source, _, __) => { if (source == tauris) attackSignals++; };
            simulation.AbilityUsed += (source, kind) =>
            {
                if (source == tauris && kind == "tauris-super") superSignals++;
            };

            simulation.Step(.1f);
            Assert.AreEqual(1, attackSignals, "The charging action must remain a normal attack.");
            Assert.AreEqual(0, superSignals);
            Assert.AreEqual(definition.EnergyMax, tauris.Energy);
            Assert.AreEqual(1000, target.CurrentHealth);

            simulation.Step(.1f);
            Assert.AreEqual(990, target.CurrentHealth, "The normal hit must resolve normally after filling energy.");
            Assert.AreEqual(0, superSignals);

            simulation.Step(.1f);
            Assert.AreEqual(1, superSignals, "The separate Super signal belongs to the next action.");
            Assert.AreEqual(2, attackSignals, "The Super launch is a new combat action, not the charging basic.");
            Assert.AreEqual(0, tauris.Energy);
            Assert.AreEqual(990, target.CurrentHealth, "The Super impact has its own later resolution.");
        }

        [TestCase(BoardSide.Blue, 5, 2, 7)]
        [TestCase(BoardSide.Red, 4, 7, 2)]
        public void RetreatUsesConfiguredDistanceAndCorrectSideDirection(
            BoardSide side, int startY, int enemyY, int expectedY)
        {
            BattleUnit hymay = Hymay(2, startY, side);
            BattleUnit target = Target(2, enemyY, Opposite(side));
            Begin(hymay, target);
            hymay.AddEnergy(hymay.BaseStats.EnergyMax);

            simulation.Step(1.2f);

            Assert.AreEqual(new Vector2Int(2, expectedY), hymay.CurrentCell.Coordinates);
            Assert.AreSame(hymay, board.GetCell(2, expectedY).OccupiedBy);
            Assert.IsNull(board.GetCell(2, startY).OccupiedBy);
        }

        [TestCase(false, 1)]
        [TestCase(true, 3)]
        public void OccupiedPreferredCentreUsesItsImmediateLateralCells(bool blockLeft, int expectedX)
        {
            BattleUnit hymay = Hymay(2);
            BattleUnit target = Target(2, 2, BoardSide.Red);
            Block(2, 7);
            if (blockLeft) Block(1, 7);
            int hymayMoves = 0;
            BoardCell reportedDestination = null;
            BeginWithBoardUnits(hymay, target);
            simulation.Moved += (unit, _, destination) =>
            {
                if (unit != hymay) return;
                hymayMoves++;
                reportedDestination = destination;
            };
            hymay.AddEnergy(hymay.BaseStats.EnergyMax);

            simulation.Step(1.2f);

            Assert.AreEqual(new Vector2Int(expectedX, 7), hymay.CurrentCell.Coordinates);
            Assert.AreEqual(1, hymayMoves);
            Assert.AreSame(hymay.CurrentCell, reportedDestination,
                "Visual movement receives the already committed logical destination.");
        }

        [Test]
        public void BlockedPreferredRowFallsBackToShorterCentralDistance()
        {
            BattleUnit hymay = Hymay(2);
            BattleUnit target = Target(2, 2, BoardSide.Red);
            BlockRow(7);
            BeginWithBoardUnits(hymay, target);
            hymay.AddEnergy(hymay.BaseStats.EnergyMax);

            simulation.Step(1.2f);

            Assert.AreEqual(new Vector2Int(2, 6), hymay.CurrentCell.Coordinates);
        }

        [Test]
        public void BlockedNormalSpaceExpandsRetreatUntilAFreeCellExists()
        {
            BattleUnit hymay = Hymay(2);
            BattleUnit target = Target(2, 2, BoardSide.Red);
            BlockRow(7);
            BlockRow(6);
            BeginWithBoardUnits(hymay, target);
            hymay.AddEnergy(hymay.BaseStats.EnergyMax);

            simulation.Step(1.2f);

            Assert.AreEqual(new Vector2Int(2, 8), hymay.CurrentCell.Coordinates);
        }

        [Test]
        public void FullyBlockedBoardKeepsHymayInPlaceWithoutBreakingOccupancy()
        {
            BattleUnit hymay = Hymay(2);
            BattleUnit target = Target(2, 2, BoardSide.Red);
            for (int y = 6; y < BoardManager.Height; y++) BlockRow(y);
            BeginWithBoardUnits(hymay, target);
            hymay.AddEnergy(hymay.BaseStats.EnergyMax);

            simulation.Step(1.2f);

            Assert.AreEqual(new Vector2Int(2, 5), hymay.CurrentCell.Coordinates);
            Assert.AreSame(hymay, board.GetCell(2, 5).OccupiedBy);
            Assert.IsFalse(board.GetAllCells().Exists(cell =>
                !ReferenceEquals(cell, hymay.CurrentCell) && ReferenceEquals(cell.OccupiedBy, hymay)));
        }

        private BattleUnit Hymay(int retreatDistance, int y = 5, BoardSide side = BoardSide.Blue)
        {
            UnitDefinition definition = DocumentedRoster.CreateHymay();
            definition.MaxHealth = 1000;
            definition.Tricks[2].Parameters = new[]
            {
                new AbilityParameter { Key = "distance", Value = retreatDistance }
            };
            return Unit(definition, 2, y, side, 2);
        }

        private BattleUnit Target(int x, int y, BoardSide side)
        {
            return Unit(new UnitDefinition
            {
                Id = "target",
                MaxHealth = 1000,
                Damage = 0,
                AttackRange = 9,
                AttackInterval = 100,
                AttackWindup = .1f,
                MoveInterval = 100
            }, x, y, side);
        }

        private void Block(int x, int y)
        {
            Unit(new UnitDefinition
            {
                Id = "blocker",
                MaxHealth = 1000,
                Damage = 0,
                AttackRange = 9,
                AttackInterval = 100,
                AttackWindup = .1f,
                MoveInterval = 100
            }, x, y, BoardSide.Blue);
        }

        private void BlockRow(int y)
        {
            Block(2, y);
            Block(1, y);
            Block(3, y);
        }

        private BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side, params int[] tricks)
        {
            var owned = new OwnedUnit(definition);
            foreach (int index in tricks) owned.Tricks[index] = true;
            var go = new GameObject(side + "-" + definition.Id + "-" + objects.Count);
            objects.Add(go);
            BattleUnit unit = definition.IsMonster
                ? (BattleUnit)go.AddComponent<MonsterUnit>()
                : go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y));
            return unit;
        }

        private void Begin(params BattleUnit[] units)
        {
            simulation = new CombatSimulation(board);
            simulation.Begin(units);
        }

        private void BeginWithBoardUnits(BattleUnit hymay, BattleUnit target)
        {
            var units = new List<BattleUnit> { hymay, target };
            foreach (GameObject item in objects)
            {
                BattleUnit unit = item != null ? item.GetComponent<BattleUnit>() : null;
                if (unit != null && !units.Contains(unit)) units.Add(unit);
            }
            simulation = new CombatSimulation(board);
            simulation.Begin(units);
        }

        private static BoardSide Opposite(BoardSide side)
        {
            return side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue;
        }
    }
}
