using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Board
{
    public enum CombatTickStatus
    {
        NoTarget,
        ReadyToAttack,
        Moved,
        Blocked,
        Busy,
        VisualSyncFailed
    }

    public readonly struct CombatTickResult
    {
        public BattleUnit Actor { get; }
        public BattleUnit Target { get; }
        public BoardCell AttackCell { get; }
        public BoardCell DestinationCell { get; }
        public CombatTickStatus Status { get; }

        internal CombatTickResult(
            BattleUnit actor,
            BattleUnit target,
            BoardCell attackCell,
            BoardCell destinationCell,
            CombatTickStatus status)
        {
            Actor = actor;
            Target = target;
            AttackCell = attackCell;
            DestinationCell = destinationCell;
            Status = status;
        }
    }

    public sealed class CombatTickController
    {
        private sealed class PendingMovement
        {
            public int ReportIndex { get; }
            public BattleUnit Actor { get; }
            public CombatTargetSelection Selection { get; }
            public BoardUnitView View { get; }
            public Vector3 TargetWorldPosition { get; }

            public PendingMovement(
                int reportIndex,
                BattleUnit actor,
                CombatTargetSelection selection,
                BoardUnitView view,
                Vector3 targetWorldPosition)
            {
                ReportIndex = reportIndex;
                Actor = actor;
                Selection = selection;
                View = view;
                TargetWorldPosition = targetWorldPosition;
            }
        }

        private readonly BoardManager boardManager;
        private readonly BoardWorldMapper worldMapper;

        public BoardManager BoardManager => boardManager;
        public BoardWorldMapper WorldMapper => worldMapper;

        public CombatTickController(
            BoardManager newBoardManager,
            BoardWorldMapper newWorldMapper)
        {
            boardManager = newBoardManager;
            worldMapper = newWorldMapper;
        }

        public List<CombatTickResult> ExecuteTick(
            IReadOnlyList<BattleUnit> availableUnits)
        {
            var snapshot = BuildStableSnapshot(availableUnits);
            var redUnits = new List<BattleUnit>();
            var blueUnits = new List<BattleUnit>();

            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].Side == BoardSide.Red)
                    redUnits.Add(snapshot[i]);
                else
                    blueUnits.Add(snapshot[i]);
            }

            var reports = new CombatTickResult[snapshot.Count];
            var movementIntents = new List<BoardMovementIntent>();
            var pendingMovements = new List<PendingMovement>();

            for (int i = 0; i < snapshot.Count; i++)
            {
                BattleUnit actor = snapshot[i];
                BoardUnitView view = actor.GetComponent<BoardUnitView>();

                if (view != null && view.IsMoving)
                {
                    reports[i] = new CombatTickResult(
                        actor,
                        null,
                        null,
                        actor.CurrentCell,
                        CombatTickStatus.Busy);
                    continue;
                }

                IReadOnlyList<BattleUnit> opposingUnits =
                    actor.Side == BoardSide.Red ? blueUnits : redUnits;
                CombatTargetSelection selection =
                    CombatTargetSelector.SelectTarget(
                        boardManager,
                        actor,
                        opposingUnits);

                if (selection.Status == CombatTargetSelectionStatus.NoTarget)
                {
                    reports[i] = new CombatTickResult(
                        actor,
                        null,
                        null,
                        actor.CurrentCell,
                        CombatTickStatus.NoTarget);
                    continue;
                }

                if (selection.Status ==
                    CombatTargetSelectionStatus.ReadyToAttack)
                {
                    reports[i] = new CombatTickResult(
                        actor,
                        selection.Target,
                        selection.AttackCell,
                        actor.CurrentCell,
                        CombatTickStatus.ReadyToAttack);
                    continue;
                }

                if (view == null ||
                    !view.isActiveAndEnabled ||
                    view.IsMoving ||
                    worldMapper == null ||
                    selection.NextCell == null ||
                    !worldMapper.TryGetWorldPosition(
                        selection.NextCell,
                        out Vector3 targetWorldPosition))
                {
                    reports[i] = new CombatTickResult(
                        actor,
                        selection.Target,
                        selection.AttackCell,
                        actor.CurrentCell,
                        CombatTickStatus.Blocked);
                    continue;
                }

                reports[i] = new CombatTickResult(
                    actor,
                    selection.Target,
                    selection.AttackCell,
                    actor.CurrentCell,
                    CombatTickStatus.Blocked);
                movementIntents.Add(
                    new BoardMovementIntent(actor, selection.AttackCell));
                pendingMovements.Add(
                    new PendingMovement(
                        i,
                        actor,
                        selection,
                        view,
                        targetWorldPosition));
            }

            List<BoardMovementResult> movementResults =
                BoardMovementResolver.ResolveMovement(
                    boardManager,
                    movementIntents);

            for (int i = 0; i < pendingMovements.Count; i++)
            {
                PendingMovement pending = pendingMovements[i];
                BoardMovementResult movementResult =
                    i < movementResults.Count
                        ? movementResults[i]
                        : default;

                if (movementResult.Status != BoardMovementStatus.Moved)
                {
                    reports[pending.ReportIndex] = new CombatTickResult(
                        pending.Actor,
                        pending.Selection.Target,
                        pending.Selection.AttackCell,
                        pending.Actor.CurrentCell,
                        CombatTickStatus.Blocked);
                    continue;
                }

                bool movedToExpectedCell =
                    ReferenceEquals(
                        pending.Actor.CurrentCell,
                        movementResult.NextCell) &&
                    ReferenceEquals(
                        movementResult.NextCell,
                        pending.Selection.NextCell);

                if (!movedToExpectedCell)
                {
                    pending.View.SnapToCurrentCell();
                    reports[pending.ReportIndex] = new CombatTickResult(
                        pending.Actor,
                        pending.Selection.Target,
                        pending.Selection.AttackCell,
                        pending.Actor.CurrentCell,
                        CombatTickStatus.VisualSyncFailed);
                    continue;
                }

                if (!pending.View.TryMoveTo(
                        pending.TargetWorldPosition))
                {
                    pending.View.SnapToCurrentCell();
                    reports[pending.ReportIndex] = new CombatTickResult(
                        pending.Actor,
                        pending.Selection.Target,
                        pending.Selection.AttackCell,
                        pending.Actor.CurrentCell,
                        CombatTickStatus.VisualSyncFailed);
                    continue;
                }

                reports[pending.ReportIndex] = new CombatTickResult(
                    pending.Actor,
                    pending.Selection.Target,
                    pending.Selection.AttackCell,
                    pending.Actor.CurrentCell,
                    CombatTickStatus.Moved);
            }

            return new List<CombatTickResult>(reports);
        }

        private List<BattleUnit> BuildStableSnapshot(
            IReadOnlyList<BattleUnit> availableUnits)
        {
            var snapshot = new List<BattleUnit>();

            if (boardManager == null || availableUnits == null)
                return snapshot;

            for (int i = 0; i < availableUnits.Count; i++)
            {
                BattleUnit unit = availableUnits[i];

                if (!CombatTargetSelector.IsValidBoardPlacement(
                        boardManager,
                        unit) ||
                    ContainsReference(snapshot, unit))
                {
                    continue;
                }

                snapshot.Add(unit);
            }

            for (int i = 1; i < snapshot.Count; i++)
            {
                BattleUnit current = snapshot[i];
                int insertAt = i - 1;

                while (insertAt >= 0 &&
                       CompareStableUnitOrder(
                           current,
                           snapshot[insertAt]) < 0)
                {
                    snapshot[insertAt + 1] = snapshot[insertAt];
                    insertAt--;
                }

                snapshot[insertAt + 1] = current;
            }

            return snapshot;
        }

        private static int CompareStableUnitOrder(
            BattleUnit first,
            BattleUnit second)
        {
            if (first.CurrentCell.Y != second.CurrentCell.Y)
                return first.CurrentCell.Y < second.CurrentCell.Y ? -1 : 1;

            if (first.CurrentCell.X != second.CurrentCell.X)
                return first.CurrentCell.X < second.CurrentCell.X ? -1 : 1;

            if (first.Side != second.Side)
                return (int)first.Side < (int)second.Side ? -1 : 1;

            return string.CompareOrdinal(first.UnitId, second.UnitId);
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
    }
}
