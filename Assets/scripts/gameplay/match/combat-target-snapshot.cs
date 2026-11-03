using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        /// <summary>Read the current combat lock, including pursuit. Never acquire or change a target for UI.</summary>
        public BattleUnit CurrentTarget(BattleUnit source)
        {
            if (!begun || Finished || source == null || !source.IsAlive || source.IsReviving || !source.isActiveAndEnabled)
                return null;
            var actor = Clock(source);
            var target = actor?.LockedTarget;
            if (target == null || !target.IsAlive || target.IsReviving || !target.isActiveAndEnabled ||
                target.Side == source.Side || target.CombatResetVersion != actor.TargetResetVersion ||
                target.CurrentCell == null || !board.IsManagedCell(target.CurrentCell) ||
                !ReferenceEquals(target.CurrentCell.OccupiedBy, target) || (documentedCombat && IsInvisible(target)))
                return null;
            return target;
        }
    }
}
