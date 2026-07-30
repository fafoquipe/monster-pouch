using System.Collections.Generic;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Board
{
    public enum CombatTargetSelectionStatus
    {
        NoTarget,
        ReadyToAttack,
        MoveRequested
    }

    public readonly struct CombatTargetSelection
    {
        public BattleUnit Actor { get; }
        public BattleUnit Target { get; }
        public BoardCell AttackCell { get; }
        public BoardCell NextCell { get; }
        public int PathLength { get; }
        public CombatTargetSelectionStatus Status { get; }

        internal CombatTargetSelection(
            BattleUnit actor,
            BattleUnit target,
            BoardCell attackCell,
            BoardCell nextCell,
            int pathLength,
            CombatTargetSelectionStatus status)
        {
            Actor = actor;
            Target = target;
            AttackCell = attackCell;
            NextCell = nextCell;
            PathLength = pathLength;
            Status = status;
        }

        internal static CombatTargetSelection NoTarget(BattleUnit actor)
        {
            return new CombatTargetSelection(
                actor,
                null,
                null,
                null,
                -1,
                CombatTargetSelectionStatus.NoTarget);
        }
    }

    public static class CombatTargetSelector
    {
        private sealed class Candidate
        {
            public BattleUnit Target { get; }
            public BoardCell AttackCell { get; }
            public BoardCell NextCell { get; }
            public int PathLength { get; }

            public Candidate(
                BattleUnit target,
                BoardCell attackCell,
                BoardCell nextCell,
                int pathLength)
            {
                Target = target;
                AttackCell = attackCell;
                NextCell = nextCell;
                PathLength = pathLength;
            }
        }

        private sealed class AttackPosition
        {
            public BoardCell Cell { get; }
            public BoardCell NextCell { get; }
            public int PathLength { get; }

            public AttackPosition(
                BoardCell cell,
                BoardCell nextCell,
                int pathLength)
            {
                Cell = cell;
                NextCell = nextCell;
                PathLength = pathLength;
            }
        }

        public static CombatTargetSelection SelectTarget(
            BoardManager boardManager,
            BattleUnit actor,
            IReadOnlyList<BattleUnit> availableUnits)
        {
            if (boardManager == null ||
                !IsValidBoardPlacement(boardManager, actor) ||
                availableUnits == null)
            {
                return CombatTargetSelection.NoTarget(actor);
            }

            var candidates = new List<Candidate>();
            var evaluatedEnemies = new List<BattleUnit>();
            int shortestPathLength = int.MaxValue;

            for (int i = 0; i < availableUnits.Count; i++)
            {
                BattleUnit possibleTarget = availableUnits[i];

                if (possibleTarget == null ||
                    ReferenceEquals(possibleTarget, actor) ||
                    possibleTarget.Side == actor.Side ||
                    ContainsReference(evaluatedEnemies, possibleTarget) ||
                    !IsValidBoardPlacement(boardManager, possibleTarget))
                {
                    continue;
                }

                evaluatedEnemies.Add(possibleTarget);

                if (!TryEvaluateEnemy(
                        boardManager,
                        actor,
                        possibleTarget,
                        out Candidate candidate))
                {
                    continue;
                }

                if (candidate.PathLength < shortestPathLength)
                {
                    shortestPathLength = candidate.PathLength;
                    candidates.Clear();
                    candidates.Add(candidate);
                }
                else if (candidate.PathLength == shortestPathLength)
                {
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
                return CombatTargetSelection.NoTarget(actor);

            Candidate selected = SelectTargetByPriority(candidates);
            CombatTargetSelectionStatus status =
                selected.PathLength == 0
                    ? CombatTargetSelectionStatus.ReadyToAttack
                    : CombatTargetSelectionStatus.MoveRequested;

            return new CombatTargetSelection(
                actor,
                selected.Target,
                selected.AttackCell,
                selected.NextCell,
                selected.PathLength,
                status);
        }

        public static bool IsInBasicAttackRange(
            BoardCell actorCell,
            BoardCell targetCell)
        {
            if (actorCell == null || targetCell == null)
                return false;

            int deltaX = Abs(actorCell.X - targetCell.X);
            int deltaY = Abs(actorCell.Y - targetCell.Y);

            return (deltaX > 0 || deltaY > 0) &&
                   deltaX <= 1 &&
                   deltaY <= 1;
        }

        internal static bool IsValidBoardPlacement(
            BoardManager boardManager,
            BattleUnit unit)
        {
            if (boardManager == null ||
                unit == null ||
                !unit.isActiveAndEnabled ||
                unit.CurrentCell == null ||
                !boardManager.IsManagedCell(unit.CurrentCell))
            {
                return false;
            }

            return ReferenceEquals(unit.CurrentCell.OccupiedBy, unit);
        }

        private static bool TryEvaluateEnemy(
            BoardManager boardManager,
            BattleUnit actor,
            BattleUnit target,
            out Candidate candidate)
        {
            candidate = null;

            if (IsInBasicAttackRange(actor.CurrentCell, target.CurrentCell))
            {
                candidate = new Candidate(
                    target,
                    actor.CurrentCell,
                    null,
                    0);
                return true;
            }

            var shortestPositions = new List<AttackPosition>();
            int shortestPathLength = int.MaxValue;

            for (int deltaY = -1; deltaY <= 1; deltaY++)
            {
                for (int deltaX = -1; deltaX <= 1; deltaX++)
                {
                    if (deltaX == 0 && deltaY == 0)
                        continue;

                    BoardCell attackCell = boardManager.GetCell(
                        target.CurrentCell.X + deltaX,
                        target.CurrentCell.Y + deltaY);

                    if (attackCell == null)
                        continue;

                    var path = new List<BoardCell>();

                    if (!BoardPathfinder.TryFindPath(
                            boardManager,
                            actor,
                            actor.CurrentCell,
                            attackCell,
                            path))
                    {
                        continue;
                    }

                    int pathLength = path.Count;
                    BoardCell nextCell =
                        pathLength > 0 ? path[0] : null;
                    var position =
                        new AttackPosition(attackCell, nextCell, pathLength);

                    if (pathLength < shortestPathLength)
                    {
                        shortestPathLength = pathLength;
                        shortestPositions.Clear();
                        shortestPositions.Add(position);
                    }
                    else if (pathLength == shortestPathLength)
                    {
                        shortestPositions.Add(position);
                    }
                }
            }

            if (shortestPositions.Count == 0)
                return false;

            AttackPosition selected =
                SelectAttackPositionByPriority(shortestPositions, actor.Side);

            candidate = new Candidate(
                target,
                selected.Cell,
                selected.NextCell,
                selected.PathLength);
            return true;
        }

        private static Candidate SelectTargetByPriority(
            List<Candidate> candidates)
        {
            Candidate dominant = null;

            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate possibleDominant = candidates[i];
                bool beatsEveryOther = true;

                for (int j = 0; j < candidates.Count; j++)
                {
                    if (i == j)
                        continue;

                    Candidate other = candidates[j];
                    BoardPriorityResult result =
                        BoardPriorityResolver.ComparePositionalPriority(
                            possibleDominant.Target.CurrentCell,
                            possibleDominant.Target.Side,
                            other.Target.CurrentCell,
                            other.Target.Side);

                    if (result != BoardPriorityResult.First)
                    {
                        beatsEveryOther = false;
                        break;
                    }
                }

                if (!beatsEveryOther)
                    continue;

                if (dominant != null)
                {
                    dominant = null;
                    break;
                }

                dominant = possibleDominant;
            }

            if (dominant != null)
                return dominant;

            Candidate selected = candidates[0];

            for (int i = 1; i < candidates.Count; i++)
            {
                if (CompareStableCoordinates(
                        candidates[i].Target.CurrentCell,
                        selected.Target.CurrentCell) < 0)
                {
                    selected = candidates[i];
                }
            }

            return selected;
        }

        private static AttackPosition SelectAttackPositionByPriority(
            List<AttackPosition> positions,
            BoardSide actorSide)
        {
            AttackPosition dominant = null;

            for (int i = 0; i < positions.Count; i++)
            {
                AttackPosition possibleDominant = positions[i];
                bool beatsEveryOther = true;

                for (int j = 0; j < positions.Count; j++)
                {
                    if (i == j)
                        continue;

                    BoardPriorityResult result =
                        BoardPriorityResolver.ComparePositionalPriority(
                            possibleDominant.Cell,
                            actorSide,
                            positions[j].Cell,
                            actorSide);

                    if (result != BoardPriorityResult.First)
                    {
                        beatsEveryOther = false;
                        break;
                    }
                }

                if (!beatsEveryOther)
                    continue;

                if (dominant != null)
                {
                    dominant = null;
                    break;
                }

                dominant = possibleDominant;
            }

            if (dominant != null)
                return dominant;

            AttackPosition selected = positions[0];

            for (int i = 1; i < positions.Count; i++)
            {
                if (CompareStableCoordinates(
                        positions[i].Cell,
                        selected.Cell) < 0)
                {
                    selected = positions[i];
                }
            }

            return selected;
        }

        private static int CompareStableCoordinates(
            BoardCell first,
            BoardCell second)
        {
            if (first.Y != second.Y)
                return first.Y < second.Y ? -1 : 1;

            if (first.X != second.X)
                return first.X < second.X ? -1 : 1;

            return 0;
        }

        private static bool ContainsReference(
            List<BattleUnit> units,
            BattleUnit candidate)
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (ReferenceEquals(units[i], candidate))
                    return true;
            }

            return false;
        }

        private static int Abs(int value)
        {
            return value < 0 ? -value : value;
        }
    }
}
