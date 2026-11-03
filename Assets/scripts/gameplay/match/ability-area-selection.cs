using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        bool HasEditedArea(ActorClock source, string effect) => AbilityAreaCatalog.Supports(effect) &&
            source?.Unit?.BaseStats.AreaFor(effect)?.IsEdited == true;

        BoardCell[] ResolveAbilityArea(ActorClock source, string effect, BoardCell origin, IEnumerable<BoardCell> original,
            BoardCell excluded = null, Func<BoardCell,bool> allowed = null, bool emitEffect = true)
        {
            if (!HasEditedArea(source,effect)) return original.ToArray();
            if (origin == null) return Array.Empty<BoardCell>();
            var cells = AbilityAreaResolver.Resolve(source.Unit.BaseStats.AreaFor(effect),origin.X,origin.Y,
                BoardManager.Width,BoardManager.Height).Select(c=>board.GetCell(c.X,c.Y))
                .Where(c=>c!=null && c!=excluded && (allowed==null || allowed(c))).ToArray();
            // A dedicated event bypasses authored fixed-radius bursts, using these exact cells.
            if (emitEffect) AreaEffect?.Invoke(source.Unit,"edited-area:"+effect,cells);
            return cells;
        }

        List<ActorClock> SelectAbilityArea(ActorClock source, string effect, BoardCell origin, BoardSide side,
            Func<IEnumerable<ActorClock>> original, bool includeSource = true, bool visibleOnly = false,
            BoardCell excluded = null, bool emitEffect = true)
        {
            if (!HasEditedArea(source,effect)) return original().ToList();
            var cells = new HashSet<BoardCell>(ResolveAbilityArea(source,effect,origin,Array.Empty<BoardCell>(),excluded,emitEffect:emitEffect));
            // Snapshot recipients once before damage, healing, push or control can move a unit.
            return EronyaOrder(actors.Where(a=>EronyaValidActor(a) && a.Unit.Side==side &&
                (includeSource || a!=source) && (!visibleOnly || !IsInvisible(a.Unit)) && cells.Contains(a.Unit.CurrentCell)),origin).ToList();
        }

        IEnumerable<ActorClock> LegacyAbilityNeighbors(ActorClock source, string effect, BoardCell origin, BoardSide side)
        {
            var cells = ResolveAbilityArea(source,effect,origin,AreaCells(origin,1));
            return actors.Where(a=>a.Unit!=null && a.Unit.IsAlive && a.Unit.Side==side && cells.Contains(a.Unit.CurrentCell)).ToList();
        }
    }
}
