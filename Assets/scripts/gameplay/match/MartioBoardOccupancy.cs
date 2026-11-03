using System;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    // Adapter for the eventual combat hook. The caller supplies existing hit eligibility
    // (alive, active, statuses); this prototype does not invent a new status/faction rule.
    public sealed class MartioBoardOccupancy : IMartioOccupancy
    {
        readonly BoardManager board;
        readonly BoardSide sourceSide;
        readonly Func<BattleUnit, bool> canHit;

        public MartioBoardOccupancy(BoardManager board, BoardSide sourceSide, Func<BattleUnit, bool> canHit)
        {
            this.board = board ?? throw new ArgumentNullException(nameof(board));
            this.sourceSide = sourceSide;
            this.canHit = canHit ?? throw new ArgumentNullException(nameof(canHit));
        }

        public string EnemyOccupying(ProjectileGridPoint point)
        {
            var cell = board.GetCell(point.X, point.Y);
            var unit = cell?.OccupiedBy as BattleUnit;
            if (unit == null || unit.Side == sourceSide || !ReferenceEquals(unit.CurrentCell, cell) || !canHit(unit)) return null;
            return unit.UnitId;
        }
    }
}
