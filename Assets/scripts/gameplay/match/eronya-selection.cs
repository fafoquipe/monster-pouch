using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private int EronyaDistance(BoardCell first,BoardCell second) => first==null||second==null?int.MaxValue:
            EronyaMechanics.Manhattan(first.X,first.Y,second.X,second.Y);
        private bool EronyaValidActor(ActorClock actor) => actor?.Unit!=null && actor.Unit.IsAlive &&
            !actor.Unit.IsReviving && actor.Unit.isActiveAndEnabled && actor.Unit.CombatResetVersion==actor.UnitResetVersion &&
            CombatTargetSelector.IsValidBoardPlacement(board,actor.Unit);
        private decimal EronyaHealth(ActorClock actor) => actor?.Unit==null?0m:actor.Unit.PreciseHealth;
        private decimal EronyaMaxHealth(ActorClock actor) => actor?.Unit==null?0m:actor.Unit.BaseStats.MaxHealth;
        private decimal EronyaHealthFraction(ActorClock actor) => EronyaMaxHealth(actor)<=0m?0m:EronyaHealth(actor)/EronyaMaxHealth(actor);
        private int EronyaTeamSlot(ActorClock actor) => actor.Unit.CombatTeamSlot;
        private decimal EronyaRawBasicDamage(ActorClock actor) => actor?.Unit==null?0m:
            EronyaSelection.RawBasicDamage(actor.Unit.BaseStats.Attack,actor.DamageBonus,
                (decimal)actor.DamageMultiplier,actor.ExternalDamageBonus,
                (decimal)actor.ExternalDamageMultiplier,actor.ExternalDamageUntil>tick);
        private IOrderedEnumerable<ActorClock> EronyaOrder(IEnumerable<ActorClock> candidates,BoardCell center) =>
            EronyaSelection.Nearest(candidates,a=>EronyaDistance(center,a.Unit.CurrentCell),EronyaHealthFraction,
                EronyaTeamSlot,a=>a.Unit.UnitId);
        private List<ActorClock> EronyaEnemies(ActorClock source,int range) => source?.Unit?.CurrentCell==null?new List<ActorClock>():
            EronyaOrder(actors.Where(a=>EronyaValidActor(a)&&a.Unit.Side!=source.Unit.Side&&!IsInvisible(a.Unit)&&
                EronyaDistance(source.Unit.CurrentCell,a.Unit.CurrentCell)<=range),source.Unit.CurrentCell).ToList();
        private List<ActorClock> EronyaAllies(ActorClock source,int range,bool includeSelf=true) => source?.Unit?.CurrentCell==null?new List<ActorClock>():
            EronyaOrder(actors.Where(a=>EronyaValidActor(a)&&a.Unit.Side==source.Unit.Side&&(includeSelf||a!=source)&&
                EronyaDistance(source.Unit.CurrentCell,a.Unit.CurrentCell)<=range),source.Unit.CurrentCell).ToList();
        private List<ActorClock> EronyaInRadius(BoardCell center,BoardSide side,int radius) =>
            EronyaOrder(actors.Where(a=>EronyaValidActor(a)&&a.Unit.Side==side&&EronyaDistance(center,a.Unit.CurrentCell)<=radius),center).ToList();
        private ActorClock EronyaMostInjured(ActorClock source,int range,bool includeSelf=true) =>
            EronyaSelection.MostInjured(EronyaAllies(source,range,includeSelf),a=>EronyaDistance(source.Unit.CurrentCell,a.Unit.CurrentCell),
                EronyaHealthFraction,EronyaTeamSlot,a=>a.Unit.UnitId);
        private ActorClock EronyaHighestBasicDamageAlly(ActorClock source,int range,bool includeSelf=true) =>
            EronyaSelection.HighestBasicDamage(EronyaAllies(source,range,includeSelf),EronyaRawBasicDamage,
                a=>EronyaDistance(source.Unit.CurrentCell,a.Unit.CurrentCell),EronyaHealthFraction,EronyaTeamSlot,a=>a.Unit.UnitId);
        // Side-effect-free selection is shared by cast planning and basic attacks.
        // A full-mana cast can start before the first basic target is committed.
        private CombatTargetSelection EronyaSelectBasicAction(ActorClock actor)
        {
            if(!EronyaValidActor(actor))return CombatTargetSelection.NoTarget(actor?.Unit);
            if(actor.TargetReactionUntil>tick)return CombatTargetSelection.NoTarget(actor.Unit);
            // Reuse the legacy selector without committing a lock during cast planning.
            var taunt=GetTauntTarget(actor);
            if(taunt!=null)return CombatTargetSelector.SelectTarget(board,actor.Unit,new[]{taunt});
            if(CanKeepTarget(actor))
                return CombatTargetSelector.SelectTarget(board,actor.Unit,new[]{actor.LockedTarget});
            var enemies=actors.Where(a=>EronyaValidActor(a)&&a.Unit.Side!=actor.Unit.Side&&!IsInvisible(a.Unit))
                .Select(a=>a.Unit).ToList();
            return CombatTargetSelector.SelectTarget(board,actor.Unit,enemies);
        }
        private ActorClock EronyaSelectBasicTarget(ActorClock actor)
        {
            var selected=EronyaSelectBasicAction(actor);
            return selected.Target==null?null:Clock(selected.Target);
        }
        private bool EronyaCanReach(ActorClock actor,ActorClock target)
        {
            if(!EronyaValidActor(actor)||!EronyaValidActor(target)||IsInvisible(target.Unit))return false;
            return CombatTargetSelector.SelectTarget(board,actor.Unit,new[]{target.Unit}).Status!=CombatTargetSelectionStatus.NoTarget;
        }
        private BoardCell[] EronyaLine(BoardCell from,BoardCell toward,int length) => from==null||toward==null?Array.Empty<BoardCell>():
            EronyaMechanics.Line(new EronyaMechanics.Cell(from.X,from.Y),new EronyaMechanics.Cell(toward.X,toward.Y),
                length,BoardManager.Width,BoardManager.Height).Select(c=>board.GetCell(c.X,c.Y)).ToArray();
        private bool EronyaIsFree(BoardCell cell) => cell!=null&&board.IsManagedCell(cell)&&!cell.IsBlocked&&cell.OccupiedBy==null&&cell.ReservedBy==null;
        private BoardCell EronyaTunnelDestination(ActorClock actor,int forwardCells=5)
        {
            var origin=actor?.Unit?.CurrentCell;if(origin==null)return null;
            int targetY=Math.Max(0,Math.Min(BoardManager.Height-1,origin.Y+Forward(actor.Unit)*forwardCells));
            bool rotated=actor.Unit.Side==BoardSide.Red;
            return board.GetAllCells().Where(EronyaIsFree)
                .OrderBy(c=>EronyaMechanics.Manhattan(c.X,c.Y,origin.X,targetY))
                .ThenBy(c=>EronyaDistance(c,origin))
                .ThenBy(c=>EronyaSelection.OwnerRow(c.Y,BoardManager.Height,rotated))
                .ThenBy(c=>EronyaSelection.OwnerColumn(c.X,BoardManager.Width,rotated)).FirstOrDefault();
        }
    }
}
