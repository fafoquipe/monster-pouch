using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class CombatTargetSelectorTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;
        private readonly List<GameObject> unitObjects =
            new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("combat-target-selector-board");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < unitObjects.Count; i++)
                Object.DestroyImmediate(unitObjects[i]);

            unitObjects.Clear();
            Object.DestroyImmediate(boardObject);
        }

        [Test]
        public void SelectTarget_NeverSelectsSelfOrAllies()
        {
            MonsterUnit actor =
                CreateUnit("actor", BoardSide.Blue, 1, 8);
            MonsterUnit ally =
                CreateUnit("ally", BoardSide.Blue, 2, 7);
            var available = new List<BattleUnit>
            {
                actor,
                ally,
                actor
            };

            CombatTargetSelection result =
                CombatTargetSelector.SelectTarget(
                    boardManager,
                    actor,
                    available);

            Assert.AreEqual(
                CombatTargetSelectionStatus.NoTarget,
                result.Status);
            Assert.IsNull(result.Target);
        }

        [Test]
        public void SelectTarget_SelectsAValidEnemy()
        {
            MonsterUnit actor =
                CreateUnit("actor", BoardSide.Blue, 0, 9);
            MonsterUnit enemy =
                CreateUnit("enemy", BoardSide.Red, 5, 0);

            CombatTargetSelection result =
                CombatTargetSelector.SelectTarget(
                    boardManager,
                    actor,
                    new List<BattleUnit> { actor, enemy });

            Assert.AreSame(enemy, result.Target);
            Assert.AreEqual(
                CombatTargetSelectionStatus.MoveRequested,
                result.Status);
            Assert.IsNotNull(result.AttackCell);
            Assert.IsNotNull(result.NextCell);
            Assert.IsFalse(
                ReferenceEquals(enemy.CurrentCell, result.AttackCell));
        }

        [Test]
        public void SelectTarget_PrefersReachableEnemyWithShortestPath()
        {
            MonsterUnit actor =
                CreateUnit("actor", BoardSide.Blue, 0, 9);
            MonsterUnit nearEnemy =
                CreateUnit("near-enemy", BoardSide.Red, 0, 5);
            MonsterUnit farEnemy =
                CreateUnit("far-enemy", BoardSide.Red, 5, 0);

            CombatTargetSelection result =
                CombatTargetSelector.SelectTarget(
                    boardManager,
                    actor,
                    new List<BattleUnit>
                    {
                        farEnemy,
                        actor,
                        nearEnemy
                    });

            Assert.AreSame(nearEnemy, result.Target);
            Assert.AreEqual(3, result.PathLength);
        }

        [Test]
        public void SelectTarget_IgnoresInaccessibleEnemyWhenAnotherIsReachable()
        {
            MonsterUnit actor =
                CreateUnit("actor", BoardSide.Blue, 0, 9);
            MonsterUnit inaccessibleEnemy =
                CreateUnit("inaccessible", BoardSide.Red, 0, 0);
            MonsterUnit reachableEnemy =
                CreateUnit("reachable", BoardSide.Red, 5, 5);

            Assert.IsTrue(boardManager.TrySetCellBlocked(0, 1, true));
            Assert.IsTrue(boardManager.TrySetCellBlocked(1, 0, true));
            Assert.IsTrue(boardManager.TrySetCellBlocked(1, 1, true));

            CombatTargetSelection result =
                CombatTargetSelector.SelectTarget(
                    boardManager,
                    actor,
                    new List<BattleUnit>
                    {
                        inaccessibleEnemy,
                        reachableEnemy
                    });

            Assert.AreSame(reachableEnemy, result.Target);
            Assert.AreEqual(
                CombatTargetSelectionStatus.MoveRequested,
                result.Status);
        }

        [Test]
        public void SelectTarget_DiagonalAdjacencyIsReadyToAttack()
        {
            MonsterUnit actor =
                CreateUnit("actor", BoardSide.Blue, 1, 1);
            MonsterUnit enemy =
                CreateUnit("enemy", BoardSide.Red, 2, 2);

            CombatTargetSelection result =
                CombatTargetSelector.SelectTarget(
                    boardManager,
                    actor,
                    new List<BattleUnit> { enemy });

            Assert.AreSame(enemy, result.Target);
            Assert.AreEqual(
                CombatTargetSelectionStatus.ReadyToAttack,
                result.Status);
            Assert.AreSame(actor.CurrentCell, result.AttackCell);
            Assert.IsNull(result.NextCell);
            Assert.AreEqual(0, result.PathLength);
        }

        [Test]
        public void SelectTarget_UsesPriorityRulesAndIgnoresInputOrderOnTie()
        {
            MonsterUnit actor =
                CreateUnit("actor", BoardSide.Blue, 2, 5);
            MonsterUnit higherValueEnemy =
                CreateUnit("higher-value-enemy", BoardSide.Red, 0, 3);
            MonsterUnit lowerValueEnemy =
                CreateUnit("lower-value-enemy", BoardSide.Red, 4, 3);

            for (int i = 0; i < 20; i++)
            {
                var available =
                    i % 2 == 0
                        ? new List<BattleUnit>
                        {
                            higherValueEnemy,
                            lowerValueEnemy
                        }
                        : new List<BattleUnit>
                        {
                            lowerValueEnemy,
                            higherValueEnemy
                        };

                CombatTargetSelection result =
                    CombatTargetSelector.SelectTarget(
                        boardManager,
                        actor,
                        available);

                Assert.AreSame(higherValueEnemy, result.Target);
                Assert.AreEqual(2, result.PathLength);
            }
        }

        [Test]
        public void SelectTarget_DoesNotModifyBoardOccupancyOrReservations()
        {
            MonsterUnit actor =
                CreateUnit("actor", BoardSide.Blue, 2, 8);
            MonsterUnit enemy =
                CreateUnit("enemy", BoardSide.Red, 3, 1);
            var occupants = new IBoardUnit[
                BoardManager.Width,
                BoardManager.Height];
            var reservations = new IBoardUnit[
                BoardManager.Width,
                BoardManager.Height];

            for (int y = 0; y < BoardManager.Height; y++)
            {
                for (int x = 0; x < BoardManager.Width; x++)
                {
                    BoardCell cell = boardManager.GetCell(x, y);
                    occupants[x, y] = cell.OccupiedBy;
                    reservations[x, y] = cell.ReservedBy;
                }
            }

            BoardCell actorCellBefore = actor.CurrentCell;
            BoardCell enemyCellBefore = enemy.CurrentCell;

            CombatTargetSelector.SelectTarget(
                boardManager,
                actor,
                new List<BattleUnit> { actor, enemy });

            Assert.AreSame(actorCellBefore, actor.CurrentCell);
            Assert.AreSame(enemyCellBefore, enemy.CurrentCell);

            for (int y = 0; y < BoardManager.Height; y++)
            {
                for (int x = 0; x < BoardManager.Width; x++)
                {
                    BoardCell cell = boardManager.GetCell(x, y);
                    Assert.AreSame(occupants[x, y], cell.OccupiedBy);
                    Assert.AreSame(
                        reservations[x, y],
                        cell.ReservedBy);
                }
            }
        }

        [Test]
        public void SelectTarget_FiltersNullInactiveAndUnplacedEnemies()
        {
            MonsterUnit actor =
                CreateUnit("actor", BoardSide.Blue, 0, 9);
            MonsterUnit inactiveEnemy =
                CreateUnit("inactive-enemy", BoardSide.Red, 0, 0);
            inactiveEnemy.gameObject.SetActive(false);

            GameObject unplacedObject =
                new GameObject("unplaced-enemy");
            unitObjects.Add(unplacedObject);
            MonsterUnit unplacedEnemy =
                unplacedObject.AddComponent<MonsterUnit>();
            unplacedEnemy.ConfigureSide(BoardSide.Red);

            MonsterUnit validEnemy =
                CreateUnit("valid-enemy", BoardSide.Red, 5, 5);

            CombatTargetSelection result =
                CombatTargetSelector.SelectTarget(
                    boardManager,
                    actor,
                    new List<BattleUnit>
                    {
                        null,
                        inactiveEnemy,
                        unplacedEnemy,
                        validEnemy
                    });

            Assert.AreSame(validEnemy, result.Target);
        }

        [Test]
        public void SelectTarget_ReturnsNoTargetWithoutEnemies()
        {
            MonsterUnit actor =
                CreateUnit("actor", BoardSide.Red, 2, 2);

            CombatTargetSelection result =
                CombatTargetSelector.SelectTarget(
                    boardManager,
                    actor,
                    new List<BattleUnit>());

            Assert.AreEqual(
                CombatTargetSelectionStatus.NoTarget,
                result.Status);
            Assert.IsNull(result.Target);
            Assert.IsNull(result.AttackCell);
            Assert.IsNull(result.NextCell);
        }

        private MonsterUnit CreateUnit(
            string unitName,
            BoardSide side,
            int x,
            int y)
        {
            var unitObject = new GameObject(unitName);
            unitObjects.Add(unitObject);
            MonsterUnit unit =
                unitObject.AddComponent<MonsterUnit>();
            unit.ConfigureSide(side);
            Assert.IsTrue(
                boardManager.TryOccupyCell(
                    unit,
                    boardManager.GetCell(x, y)));
            return unit;
        }
    }
}
