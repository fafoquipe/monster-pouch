using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    public sealed class CombatTickControllerTests
    {
        private GameObject boardObject;
        private BoardManager boardManager;
        private GameObject mapperObject;
        private BoardWorldMapper worldMapper;
        private CombatTickController controller;
        private readonly List<GameObject> createdObjects =
            new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            boardObject = new GameObject("combat-tick-board");
            boardManager = boardObject.AddComponent<BoardManager>();
            boardManager.BuildBoard();

            mapperObject = new GameObject("combat-tick-mapper");
            worldMapper = mapperObject.AddComponent<BoardWorldMapper>();
            worldMapper.Configure(
                boardManager,
                null,
                Vector2.one,
                Vector2.zero);

            controller =
                new CombatTickController(boardManager, worldMapper);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdObjects.Count; i++)
                Object.DestroyImmediate(createdObjects[i]);

            createdObjects.Clear();
            Object.DestroyImmediate(mapperObject);
            Object.DestroyImmediate(boardObject);
        }

        [Test]
        public void ExecuteTick_UnitInRangeStaysReadyToAttack()
        {
            MonsterUnit actor =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "actor",
                    BoardSide.Blue,
                    1,
                    1);
            MonsterUnit enemy =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "enemy",
                    BoardSide.Red,
                    2,
                    2);
            BoardCell originalCell = actor.CurrentCell;

            List<CombatTickResult> results =
                controller.ExecuteTick(
                    new List<BattleUnit> { actor, enemy });
            CombatTickResult result = FindResult(results, actor);

            Assert.AreEqual(
                CombatTickStatus.ReadyToAttack,
                result.Status);
            Assert.AreSame(enemy, result.Target);
            Assert.AreSame(originalCell, actor.CurrentCell);
            Assert.IsFalse(
                actor.GetComponent<BoardUnitView>().IsMoving);
        }

        [Test]
        public void ExecuteTick_DistantUnitMovesExactlyOneOrthogonalCell()
        {
            MonsterUnit actor =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "actor",
                    BoardSide.Blue,
                    0,
                    9);
            MonsterUnit enemy =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "enemy",
                    BoardSide.Red,
                    5,
                    0);
            BoardCell origin = actor.CurrentCell;

            List<CombatTickResult> results =
                controller.ExecuteTick(
                    new List<BattleUnit> { enemy, actor });
            CombatTickResult result = FindResult(results, actor);
            BoardCell destination = actor.CurrentCell;
            int deltaX = Abs(destination.X - origin.X);
            int deltaY = Abs(destination.Y - origin.Y);

            Assert.AreEqual(CombatTickStatus.Moved, result.Status);
            Assert.AreEqual(1, deltaX + deltaY);
            Assert.AreEqual(0, deltaX * deltaY);
            Assert.AreSame(destination, result.DestinationCell);
        }

        [Test]
        public void ExecuteTick_ApprovedMovementStartsVisualAnimation()
        {
            MonsterUnit actor =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "actor",
                    BoardSide.Blue,
                    0,
                    9);
            MonsterUnit enemy =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "enemy",
                    BoardSide.Red,
                    5,
                    0);
            BoardUnitView view =
                actor.GetComponent<BoardUnitView>();
            Vector3 initialPosition = actor.transform.position;

            CombatTickResult result = FindResult(
                controller.ExecuteTick(
                    new List<BattleUnit> { actor, enemy }),
                actor);

            Assert.AreEqual(CombatTickStatus.Moved, result.Status);
            Assert.IsTrue(view.IsMoving);
            Assert.AreEqual(initialPosition, actor.transform.position);
        }

        [Test]
        public void ExecuteTick_VisuallyMovingUnitIsBusy()
        {
            MonsterUnit actor =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "actor",
                    BoardSide.Blue,
                    0,
                    9);
            MonsterUnit enemy =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "enemy",
                    BoardSide.Red,
                    5,
                    0);
            BoardUnitView view =
                actor.GetComponent<BoardUnitView>();
            BoardCell originalCell = actor.CurrentCell;
            Assert.IsTrue(
                view.TryMoveTo(
                    actor.transform.position + Vector3.right,
                    1f));

            CombatTickResult result = FindResult(
                controller.ExecuteTick(
                    new List<BattleUnit> { enemy, actor }),
                actor);

            Assert.AreEqual(CombatTickStatus.Busy, result.Status);
            Assert.AreSame(originalCell, actor.CurrentCell);
            Assert.IsTrue(view.IsMoving);
        }

        [Test]
        public void ExecuteTick_ConflictUsesExistingPriorityAndBlocksLoser()
        {
            MonsterUnit higherValueActor =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "higher-value-actor",
                    BoardSide.Blue,
                    1,
                    3);
            MonsterUnit lowerValueActor =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "lower-value-actor",
                    BoardSide.Blue,
                    3,
                    3);
            MonsterUnit enemy =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "enemy",
                    BoardSide.Red,
                    2,
                    0);
            int higherOriginValue =
                higherValueActor.CurrentCell.Value;
            int lowerOriginValue =
                lowerValueActor.CurrentCell.Value;

            for (int x = 0; x < BoardManager.Width; x++)
            {
                if (x != 2)
                    Assert.IsTrue(
                        boardManager.TrySetCellBlocked(x, 2, true));
            }

            BoardUnitView enemyView =
                enemy.GetComponent<BoardUnitView>();
            Assert.IsTrue(
                enemyView.TryMoveTo(enemy.transform.position, 1f));

            List<CombatTickResult> results =
                controller.ExecuteTick(
                    new List<BattleUnit>
                    {
                        lowerValueActor,
                        enemy,
                        higherValueActor
                    });
            CombatTickResult winnerResult =
                FindResult(results, higherValueActor);
            CombatTickResult loserResult =
                FindResult(results, lowerValueActor);
            BoardCell contestedCell = boardManager.GetCell(2, 3);

            Assert.Greater(higherOriginValue, lowerOriginValue);
            Assert.AreEqual(
                CombatTickStatus.Moved,
                winnerResult.Status);
            Assert.AreEqual(
                CombatTickStatus.Blocked,
                loserResult.Status);
            Assert.AreSame(
                contestedCell,
                higherValueActor.CurrentCell);
            Assert.IsTrue(
                lowerValueActor.CurrentCell.MatchesCoordinates(3, 3));
            Assert.AreSame(
                higherValueActor,
                contestedCell.OccupiedBy);
            Assert.IsTrue(
                higherValueActor.GetComponent<BoardUnitView>().IsMoving);
            Assert.IsFalse(
                lowerValueActor.GetComponent<BoardUnitView>().IsMoving);
        }

        [Test]
        public void ExecuteTick_NeverMovesOntoEnemyCell()
        {
            MonsterUnit actor =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "actor",
                    BoardSide.Blue,
                    0,
                    3);
            MonsterUnit enemy =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "enemy",
                    BoardSide.Red,
                    0,
                    0);
            BoardCell enemyCell = enemy.CurrentCell;
            BoardUnitView enemyView =
                enemy.GetComponent<BoardUnitView>();
            Assert.IsTrue(
                enemyView.TryMoveTo(enemy.transform.position, 1f));

            CombatTickResult result = FindResult(
                controller.ExecuteTick(
                    new List<BattleUnit> { actor, enemy }),
                actor);

            Assert.AreEqual(CombatTickStatus.Moved, result.Status);
            Assert.AreNotSame(enemyCell, result.AttackCell);
            Assert.AreNotSame(enemyCell, actor.CurrentCell);
            Assert.AreSame(enemy, enemyCell.OccupiedBy);
            Assert.IsTrue(
                CombatTargetSelector.IsInBasicAttackRange(
                    result.AttackCell,
                    enemyCell));
        }

        [Test]
        public void ExecuteTick_CompletedVisualMovementMatchesLogicalCell()
        {
            MonsterUnit actor =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "actor",
                    BoardSide.Blue,
                    0,
                    9);
            MonsterUnit enemy =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "enemy",
                    BoardSide.Red,
                    5,
                    0);
            BoardUnitView view =
                actor.GetComponent<BoardUnitView>();

            CombatTickResult result = FindResult(
                controller.ExecuteTick(
                    new List<BattleUnit> { actor, enemy }),
                actor);
            Vector3 expectedPosition =
                worldMapper.GetWorldPosition(actor.CurrentCell);
            view.AdvanceMovement(view.MovementDuration);

            Assert.AreEqual(CombatTickStatus.Moved, result.Status);
            Assert.IsFalse(view.IsMoving);
            Assert.AreEqual(expectedPosition, actor.transform.position);
            Assert.AreSame(
                actor,
                actor.CurrentCell.OccupiedBy);
        }

        [Test]
        public void ExecuteTick_DoesNotChangeUnitStateOrApplyDamage()
        {
            MonsterUnit actor =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "actor",
                    BoardSide.Blue,
                    1,
                    1);
            MonsterUnit enemy =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "enemy",
                    BoardSide.Red,
                    2,
                    2);
            int actorMaxHealth = actor.BaseStats.MaxHealth;
            int enemyMaxHealth = enemy.BaseStats.MaxHealth;

            controller.ExecuteTick(
                new List<BattleUnit> { actor, enemy });

            Assert.AreEqual(UnitState.Idle, actor.State);
            Assert.AreEqual(UnitState.Idle, enemy.State);
            Assert.AreEqual(actorMaxHealth, actor.BaseStats.MaxHealth);
            Assert.AreEqual(enemyMaxHealth, enemy.BaseStats.MaxHealth);
        }

        [Test]
        public void ExecuteTick_IsDeterministicForReversedInputOrder()
        {
            string forward = RunDeterministicScenario(false);
            string reversed = RunDeterministicScenario(true);

            Assert.AreEqual(forward, reversed);
        }

        [Test]
        public void ExecuteTick_MissingViewBlocksLogicAndAnimation()
        {
            GameObject actorObject =
                new GameObject("actor-without-view");
            createdObjects.Add(actorObject);
            MonsterUnit actor =
                actorObject.AddComponent<MonsterUnit>();
            actor.ConfigureSide(BoardSide.Blue);
            Assert.IsTrue(
                boardManager.TryOccupyCell(
                    actor,
                    boardManager.GetCell(0, 9)));
            MonsterUnit enemy =
                CreateUnit(
                    boardManager,
                    worldMapper,
                    "enemy",
                    BoardSide.Red,
                    5,
                    0);
            BoardCell originalCell = actor.CurrentCell;

            CombatTickResult result = FindResult(
                controller.ExecuteTick(
                    new List<BattleUnit> { actor, enemy }),
                actor);

            Assert.AreEqual(CombatTickStatus.Blocked, result.Status);
            Assert.AreSame(originalCell, actor.CurrentCell);
        }

        private MonsterUnit CreateUnit(
            BoardManager targetBoard,
            BoardWorldMapper targetMapper,
            string unitName,
            BoardSide side,
            int x,
            int y)
        {
            var unitObject = new GameObject(unitName);
            createdObjects.Add(unitObject);
            MonsterUnit unit =
                unitObject.AddComponent<MonsterUnit>();
            unit.ConfigureSide(side);
            BoardUnitView view =
                unitObject.AddComponent<BoardUnitView>();
            view.Configure(unit, targetMapper);
            Assert.IsTrue(
                targetBoard.TryOccupyCell(
                    unit,
                    targetBoard.GetCell(x, y)));
            view.SnapToCurrentCell();
            return unit;
        }

        private static CombatTickResult FindResult(
            IReadOnlyList<CombatTickResult> results,
            BattleUnit actor)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (ReferenceEquals(results[i].Actor, actor))
                    return results[i];
            }

            Assert.Fail(
                $"No combat tick result was returned for {actor.name}.");
            return default;
        }

        private string RunDeterministicScenario(bool reverseInput)
        {
            var scenarioBoardObject =
                new GameObject(
                    reverseInput
                        ? "determinism-board-reversed"
                        : "determinism-board-forward");
            createdObjects.Add(scenarioBoardObject);
            BoardManager scenarioBoard =
                scenarioBoardObject.AddComponent<BoardManager>();
            scenarioBoard.BuildBoard();

            var scenarioMapperObject =
                new GameObject(
                    reverseInput
                        ? "determinism-mapper-reversed"
                        : "determinism-mapper-forward");
            createdObjects.Add(scenarioMapperObject);
            BoardWorldMapper scenarioMapper =
                scenarioMapperObject.AddComponent<BoardWorldMapper>();
            scenarioMapper.Configure(
                scenarioBoard,
                null,
                Vector2.one,
                Vector2.zero);

            MonsterUnit blue =
                CreateUnit(
                    scenarioBoard,
                    scenarioMapper,
                    "blue",
                    BoardSide.Blue,
                    2,
                    8);
            MonsterUnit red =
                CreateUnit(
                    scenarioBoard,
                    scenarioMapper,
                    "red",
                    BoardSide.Red,
                    3,
                    1);
            WhelpUnit blueWhelp =
                CreateWhelp(
                    scenarioBoard,
                    scenarioMapper,
                    "blue-whelp",
                    BoardSide.Blue,
                    1,
                    7);
            WhelpUnit redWhelp =
                CreateWhelp(
                    scenarioBoard,
                    scenarioMapper,
                    "red-whelp",
                    BoardSide.Red,
                    4,
                    2);
            var units = new List<BattleUnit>
            {
                blue,
                red,
                blueWhelp,
                redWhelp
            };

            if (reverseInput)
                units.Reverse();

            var scenarioController =
                new CombatTickController(
                    scenarioBoard,
                    scenarioMapper);
            List<CombatTickResult> results =
                scenarioController.ExecuteTick(units);
            string signature = string.Empty;

            for (int i = 0; i < results.Count; i++)
            {
                CombatTickResult result = results[i];
                string targetName =
                    result.Target != null
                        ? result.Target.gameObject.name
                        : "none";
                signature +=
                    result.Actor.gameObject.name + ":" +
                    result.Status + ":" +
                    result.Actor.CurrentCell.X + "," +
                    result.Actor.CurrentCell.Y + ":" +
                    targetName + "|";
            }

            return signature;
        }

        private WhelpUnit CreateWhelp(
            BoardManager targetBoard,
            BoardWorldMapper targetMapper,
            string unitName,
            BoardSide side,
            int x,
            int y)
        {
            var unitObject = new GameObject(unitName);
            createdObjects.Add(unitObject);
            WhelpUnit unit =
                unitObject.AddComponent<WhelpUnit>();
            unit.ConfigureSide(side);
            BoardUnitView view =
                unitObject.AddComponent<BoardUnitView>();
            view.Configure(unit, targetMapper);
            Assert.IsTrue(
                targetBoard.TryOccupyCell(
                    unit,
                    targetBoard.GetCell(x, y)));
            view.SnapToCurrentCell();
            return unit;
        }

        private static int Abs(int value)
        {
            return value < 0 ? -value : value;
        }
    }
}
