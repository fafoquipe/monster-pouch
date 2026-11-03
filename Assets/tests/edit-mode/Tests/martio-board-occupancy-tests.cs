using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;

namespace MonsterPouch.Gameplay.Tests.EditMode
{
    // Needs Unity's real GameObject/Board lifecycle; compile offline, execute in Edit Mode later.
    public sealed class MartioBoardOccupancyTests
    {
        GameObject root;
        BoardManager board;
        [SetUp] public void Setup() { root = new GameObject("martio-occupancy-qa"); board = root.AddComponent<BoardManager>(); board.BuildBoard(); }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); }
        WhelpUnit Unit(string id, BoardSide side, int x, int y)
        {
            var go = new GameObject(id); go.transform.SetParent(root.transform);
            var unit = go.AddComponent<WhelpUnit>();
            unit.Initialize(id, side, UnitStats.FromDefinition(new UnitDefinition { Id = id, MaxHealth = 1000 }));
            Assert.IsTrue(board.TryOccupyCell(unit, x, y)); return unit;
        }
        static ProjectileGridPoint P(int x, int y) => new ProjectileGridPoint(x, y);

        [Test]
        public void ReservationDoesNotBecomeAHitBeforeArrival()
        {
            var enemy = Unit("enemy", BoardSide.Red, 2, 5);
            var occupancy = new MartioBoardOccupancy(board, BoardSide.Blue, u => u.IsAlive);
            Assert.IsTrue(board.TryReserveCell(enemy, 2, 6));
            Assert.AreEqual("enemy", occupancy.EnemyOccupying(P(2, 5)));
            Assert.IsNull(occupancy.EnemyOccupying(P(2, 6)));
            var step = new CombatMovementStep(enemy, 8);
            Assert.IsFalse(step.Advance(board, 7)); Assert.IsNull(occupancy.EnemyOccupying(P(2, 6)));
            Assert.IsTrue(step.Advance(board, 8));
            Assert.IsNull(occupancy.EnemyOccupying(P(2, 5)));
            Assert.AreEqual("enemy", occupancy.EnemyOccupying(P(2, 6)));
        }

        [Test]
        public void AllyOutsideBoardAndCallerRejectedOccupantsAreIgnored()
        {
            Unit("ally", BoardSide.Blue, 2, 7); Unit("enemy", BoardSide.Red, 2, 5);
            var occupancy = new MartioBoardOccupancy(board, BoardSide.Blue, u => u.UnitId != "enemy");
            Assert.IsNull(occupancy.EnemyOccupying(P(2, 7))); Assert.IsNull(occupancy.EnemyOccupying(P(2, 5)));
            Assert.IsNull(occupancy.EnemyOccupying(P(-1, 3)));
        }
    }
}
