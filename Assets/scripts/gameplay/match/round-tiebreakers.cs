using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // Each Whelp contributes half its current HP rounded up, without changing combat HP.
    // Both resolution and presentation use the same captured contributions.
    public sealed class TimeoutResolution
    {
        public RoundHealthSummary Health { get; internal set; }
        public BattleUnit Blue { get; internal set; }
        public BattleUnit Red { get; internal set; }
        public BoardSide? Winner { get; internal set; }
        public IReadOnlyDictionary<BattleUnit, float> AbsorbedHealth { get; internal set; }
        public float BlueRemaining { get; internal set; }
        public float RedRemaining { get; internal set; }
        public float DrainedPerSide { get; internal set; }
    }

    public static class RoundTiebreakers
    {
        public static BattleUnit Champion(IEnumerable<BattleUnit> source)
        {
            var live = source.Where(RoundHealthSummary.Eligible).ToList();
            var monster = live.FirstOrDefault(u => u.Category == UnitCategory.Monster);
            if (monster != null) return monster;
            if (live.Count == 0) return null;
            int health = live.Max(u => u.CurrentHealth);
            var tied = live.Where(u => u.CurrentHealth == health).ToList();
            if (tied.Count == 1) return tied[0];
            // Only a unique board winner may represent otherwise equal allies.
            return tied.FirstOrDefault(a => tied.All(b => a == b || BoardPriorityResolver.CompareFinalSimultaneousDeath(
                a.CurrentCell, a.Side, b.CurrentCell, b.Side) == BoardPriorityResult.First));
        }

        public static TimeoutResolution Timeout(IEnumerable<BattleUnit> units)
            => Timeout(new RoundHealthSummary(units));

        internal static TimeoutResolution Timeout(RoundHealthSummary summary)
        {
            var live = summary.Contributions.Select(c => c.Unit).ToList();
            var result = new TimeoutResolution {
                Health = summary,
                Blue = Champion(live.Where(u => u.Side == BoardSide.Blue)),
                Red = Champion(live.Where(u => u.Side == BoardSide.Red)),
                // Retain this API for existing consumers; entries now contain each unit's
                // actual contribution, never HP transferred into an arbitrary Whelp.
                AbsorbedHealth = new ReadOnlyDictionary<BattleUnit, float>(
                    summary.Contributions.ToDictionary(c => c.Unit, c => c.Amount))
            };
            float blue = summary.BlueTotal, red = summary.RedTotal;
            result.DrainedPerSide = Mathf.Min(blue, red);
            result.BlueRemaining = blue - result.DrainedPerSide;
            result.RedRemaining = red - result.DrainedPerSide;
            if (blue > red) result.Winner = BoardSide.Blue;
            else if (red > blue) result.Winner = BoardSide.Red;
            else if (blue > 0 && result.Blue != null && result.Red != null)
                result.Winner = SideOf(BoardPriorityResolver.CompareFinalSimultaneousDeath(
                    result.Blue.CurrentCell, BoardSide.Blue, result.Red.CurrentCell, BoardSide.Red));
            return result;
        }

        public static BoardSide? SideOf(BoardPriorityResult priority) =>
            priority == BoardPriorityResult.First ? BoardSide.Blue :
            priority == BoardPriorityResult.Second ? BoardSide.Red : (BoardSide?)null;
    }

    public sealed partial class CombatSimulation
    {
        public TimeoutResolution TimeoutResult { get; private set; }

        void ResolveTimeout()
        {
            if(eronyaCombat){EronyaResolveRound("Tiempo: comparación normalizada de ejércitos Eronya",true);return;}
            FreezeRoundHealth();
            TimeoutResult = RoundTiebreakers.Timeout(HealthSummary);
            Finish(TimeoutResult.Winner, TimeoutResult.Winner.HasValue
                ? "Desempate por tiempo: vida del Monster y 50 % de Whelps vivos"
                : "Ronda anulada: empate de vida y prioridad del tablero");
        }

        void ResolveFinalDeaths()
        {
            if(eronyaCombat){EronyaResolveRound("Eliminación simultánea: criterios Eronya");return;}
            // GDD's final-death rule is only valid for the last pair at the same instant.
            var last = actors.Where(a => a.Unit != null && a.DeathTick == tick).ToList();
            if (last.Count != 2 || last[0].Unit.Side == last[1].Unit.Side)
            { Finish(null, "Ronda anulada: eliminación simultánea sin ventaja única"); return; }
            var blue = last.First(a => a.Unit.Side == BoardSide.Blue);
            var red = last.First(a => a.Unit.Side == BoardSide.Red);
            var winner = RoundTiebreakers.SideOf(BoardPriorityResolver.CompareFinalSimultaneousDeath(
                blue.LastCell, BoardSide.Blue, red.LastCell, BoardSide.Red));
            Finish(winner, winner.HasValue ? "Muerte simultánea: ventaja de casilla, cuadrante o territorio"
                : "Ronda anulada: muerte simultánea sin ventaja");
        }
    }
}
