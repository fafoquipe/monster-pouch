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
    public sealed class TaurisPositionMarkTests
    {
        readonly List<GameObject> objects = new List<GameObject>();
        BoardManager board;
        CombatSimulation simulation;
        MatchConfig config;

        [SetUp] public void SetUp()
        {
            var root = new GameObject("tauris-mark-board"); objects.Add(root);
            board = root.AddComponent<BoardManager>(); board.BuildBoard();
        }
        [TearDown] public void TearDown()
        {
            simulation?.Stop();
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear(); if (config != null) Object.DestroyImmediate(config);
        }
        BattleUnit Unit(UnitDefinition definition, int x, int y, BoardSide side, bool mark = false)
        {
            var owned = new OwnedUnit(definition); owned.Tricks[0] = mark;
            var go = new GameObject(definition.Id + objects.Count); objects.Add(go);
            BattleUnit unit = definition.IsMonster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
            unit.Initialize(go.name, side, UnitStats.FromDefinition(definition, owned));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        static UnitDefinition Target() => new UnitDefinition { Id = "mark-target", MaxHealth = 1000,
            Damage = 0, AttackRange = 1, AttackInterval = 100, AttackWindup = .1f, MoveInterval = .2f };
        static UnitDefinition Tauris(float range = 5)
        {
            var definition = DocumentedRoster.CreateTauris();
            definition.EnergyMax = 1000; definition.EnergyPerAttack = definition.EnergyOnDamage = definition.EnergyPerSecond = 0;
            definition.Damage = 0; definition.MoveInterval = 100; definition.AttackInterval = 100;
            definition.Tricks[0].Parameters = new[] {
                new AbilityParameter { Key = "energy", Value = 5 }, new AbilityParameter { Key = "range", Value = range } };
            return definition;
        }
        [TestCase(BoardSide.Blue, 8)] [TestCase(BoardSide.Red, 1)]
        public void GeometryIsOnlyFiveDeployedCellsDirectlyAhead(BoardSide side, int originY)
        {
            var origin = board.GetCell(2, originY); int direction = side == BoardSide.Blue ? -1 : 1;
            foreach (var cell in board.GetAllCells())
            {
                int distance = (cell.Y - originY) * direction;
                Assert.AreEqual(cell.X == 2 && distance >= 1 && distance <= 5,
                    TaurisMarkGeometry.Contains(origin, cell, side, 5), cell.Coordinates.ToString());
            }
            Assert.IsFalse(TaurisMarkGeometry.Contains(null, origin, side, 5));
            Assert.IsFalse(TaurisMarkGeometry.Contains(origin, null, side, 5));
        }
        [TestCase(0f, 0)] [TestCase(3.6f, 4)] [TestCase(-1f, 0)] [TestCase(1e20f, 9)]
        public void EditedRangeResolvesToBoundedWholeCells(float edited, int expected)
        {
            var effect = Tauris(edited).Tricks[0];
            Assert.AreEqual(expected, AbilityBalance.Resolve(effect).Single(p => p.Key == "range").Value);
        }
        [TestCase(BoardSide.Blue, 8)] [TestCase(BoardSide.Red, 1)]
        public void OpeningMarksOnlyOpponentsInForwardColumnAndAwardsEnergyOnce(BoardSide side, int y)
        {
            int d = side == BoardSide.Blue ? -1 : 1; var rival = side == BoardSide.Blue ? BoardSide.Red : BoardSide.Blue;
            var source = Unit(Tauris(), 2, y, side, true);
            var nearest = Unit(Target(), 2, y + d, rival); var edge = Unit(Target(), 2, y + d * 5, rival);
            var outside = Unit(Target(), 2, y + d * 6, rival); var row = Unit(Target(), 3, y, rival);
            var diagonal = Unit(Target(), 3, y + d, rival); var behind = Unit(Target(), 2, y - d, rival);
            var ally = Unit(Target(), 2, y + d * 2, side);
            var marked = new List<BattleUnit>(); simulation = new CombatSimulation(board);
            simulation.PositionMarked += marked.Add;
            simulation.Begin(new[] { source, nearest, edge, outside, row, diagonal, behind, ally });
            CollectionAssert.AreEquivalent(new[] { nearest, edge }, marked);
            Assert.AreEqual(10, source.Energy); Assert.IsFalse(nearest.IsStunned); Assert.IsFalse(edge.IsStunned);
            simulation.Step(.2f); Assert.AreEqual(2, marked.Count, "Marks are applied only at combat start.");
        }
        [TestCase(false, 3, 0)] [TestCase(true, 3, 1)] [TestCase(true, 4, 2)]
        public void UpgradeAndEditedRangeControlWhichEnemiesAreMarked(bool purchased, int range, int count)
        {
            var source = Unit(Tauris(range), 2, 7, BoardSide.Blue, purchased);
            var inside = Unit(Target(), 2, 4, BoardSide.Red); var outside = Unit(Target(), 2, 3, BoardSide.Red);
            int marked = 0; simulation = new CombatSimulation(board); simulation.PositionMarked += _ => marked++;
            simulation.Begin(new[] { source, inside, outside }); Assert.AreEqual(count, marked);
        }
        [Test] public void MarkDoesNotRootOrStopAutomaticCombatMovement()
        {
            var source = Unit(Tauris(), 2, 7, BoardSide.Blue, true);
            var target = Unit(Target(), 2, 3, BoardSide.Red); var initial = target.CurrentCell;
            int marked = 0; simulation = new CombatSimulation(board); simulation.PositionMarked += _ => marked++;
            simulation.Begin(new[] { source, target }); Assert.AreEqual(1, marked);
            simulation.Step(2); Assert.AreNotSame(initial, target.CurrentCell); Assert.IsFalse(target.IsStunned);
        }
        [Test] public void ControllerKeepsMarkForNextPreparationAndJournalButNewMatchClearsIt()
        {
            config = MatchConfig.CreateDocumented(); config.OpeningAbilityWindowSeconds = 0;
            foreach (var definition in config.Units) definition.RevivesPerCombat = 0;
            var mapper = board.gameObject.AddComponent<BoardWorldMapper>(); mapper.Configure(board, board.transform, Vector2.one, Vector2.zero);
            var match = board.gameObject.AddComponent<MatchController>(); match.Configure(config, board, mapper);
            match.StartMatch("tauris", 491); match.Player.Monster.Tricks[0] = true;
            Assert.IsTrue(match.Place("tauris", board.GetCell(2, 5)));
            foreach (var owned in match.Bot.Owned.Values) if (!owned.Definition.IsMonster) owned.Location = UnitLocation.Bench;
            var enemy = match.Bot.Monster; enemy.UpgradePurchasedRound = 0;
            enemy.Deployment = new Vector2Int(2, 4);
            Assert.IsFalse(enemy.IsPositionLocked(1)); Assert.AreEqual(0, enemy.MarkedPositionRound);
            match.Ready(); simulation = match.Simulation;
            Assert.AreEqual(1, enemy.MarkedPositionRound); Assert.IsFalse(enemy.IsPositionLocked(1));
            foreach (var actor in match.Actors.Values.Where(u => u.Side == BoardSide.Red)) actor.ApplyDamage(99999);
            match.Advance(.1f); Assert.AreEqual(MatchPhase.RoundResult, match.Phase);
            match.Advance(match.RoundTransitionDuration + .1f);
            Assert.AreEqual(2, match.Round); Assert.AreEqual(MatchPhase.Preparation, match.Phase);
            Assert.IsTrue(enemy.IsPositionLocked(2)); Assert.AreEqual(new Vector2Int(2, 4), enemy.Deployment);
            Assert.IsFalse(match.Bot.TrySetLocation(enemy, UnitLocation.Field, new Vector2Int(3, 4), out _));
            Assert.IsTrue(match.Bot.TryApplyPreparationJournal(candidate => {
                Assert.AreEqual(1, candidate.Monster.MarkedPositionRound);
                Assert.IsTrue(candidate.Monster.IsPositionLocked(2));
                return !candidate.TrySetLocation(candidate.Monster, UnitLocation.Field, new Vector2Int(3, 4), out _);
            }));
            Assert.AreSame(enemy, match.Bot.Monster); Assert.AreEqual(1, enemy.MarkedPositionRound);
            match.StartMatch("tauris", 492); simulation = null;
            Assert.IsTrue(match.Player.AllOwned.Concat(match.Bot.AllOwned).All(u => u.MarkedPositionRound == 0));
        }
        [Test] public void AuthoredAssetKeepsFiveEnergyAndEditableFiveCellRange()
        {
            var definition = Resources.Load<CharacterDefinition>("MonsterPouch/Characters/tauris/Character").CreateRuntimeDefinition();
            var effect = definition.Tricks[0]; var parameters = AbilityBalance.Resolve(effect);
            Assert.AreEqual("tauris-mark", effect.EffectId); Assert.AreEqual(2, effect.Cost);
            Assert.AreEqual(5, parameters.Single(p => p.Key == "energy").Value);
            Assert.AreEqual(5, parameters.Single(p => p.Key == "range").Value);
            StringAssert.Contains("5", AbilityBalance.Description(effect, definition));
            Assert.IsFalse(AbilityBalance.Description(effect, definition).Contains("25"));
        }
    }
}
