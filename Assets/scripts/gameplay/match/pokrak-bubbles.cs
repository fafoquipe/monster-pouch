using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        sealed class PokrakBubble
        {
            public BattleUnit Source, Recipient;
            public int SourceVersion, RecipientVersion, Until;
            public float Reduction;
        }
        readonly List<PokrakBubble> pokrakBubbles = new List<PokrakBubble>();

        // A square plus narrow arms at the middle of its four sides, never an extra ring.
        public static IReadOnlyList<Vector2Int> PokrakBubbleCells(Vector2Int center, int width = 3, int extension = 0)
        {
            int radius = Mathf.Clamp(width / 2, 0, BoardManager.Height);
            extension = Mathf.Clamp(extension, 0, BoardManager.Height);
            var cells = new List<Vector2Int>();
            for (int y = -radius; y <= radius; y++) for (int x = -radius; x <= radius; x++)
                cells.Add(center + new Vector2Int(x, y));
            for (int i = 1; i <= extension; i++)
            {
                int d = radius + i;
                cells.Add(center + new Vector2Int(d, 0)); cells.Add(center + new Vector2Int(-d, 0));
                cells.Add(center + new Vector2Int(0, d)); cells.Add(center + new Vector2Int(0, -d));
            }
            return cells;
        }
        bool ActiveBubble(PokrakBubble bubble) => bubble.Until > tick && bubble.Recipient != null &&
            bubble.Recipient.IsAlive && bubble.Recipient.isActiveAndEnabled && !bubble.Recipient.IsReviving &&
            bubble.Recipient.CombatResetVersion == bubble.RecipientVersion;

        public float PokrakBubbleReduction(BattleUnit unit)
        {
            if (!begun || Finished || unit == null) return 0;
            float strongest = 0;
            foreach (var bubble in pokrakBubbles)
                if (bubble.Recipient == unit && ActiveBubble(bubble)) strongest = Mathf.Max(strongest, bubble.Reduction);
            return strongest;
        }
        float PokrakBubbleRemaining(BattleUnit unit, float reduction)
        {
            int until = tick;
            foreach (var bubble in pokrakBubbles)
                if (bubble.Recipient == unit && ActiveBubble(bubble) && bubble.Reduction >= reduction)
                    until = Mathf.Max(until, bubble.Until);
            return (until - tick) * TickDuration;
        }
        void CastPokrakBubbles(ActorClock caster)
        {
            var source = caster.Unit;
            if (!source.IsAlive || source.CurrentCell == null) return;
            int width = Mathf.RoundToInt(V(caster, "pokrak-bubbles", "width", 3));
            int extension = Has(caster, "pokrak-wide-bubbles") ? Mathf.RoundToInt(V(caster, "pokrak-wide-bubbles", "extension", 1)) : 0;
            var cells = PokrakBubbleCells(source.CurrentCell.Coordinates, width, extension)
                .Select(p => board.GetCell(p.x, p.y)).Where(c => c != null).ToArray();
            string areaEffect = Has(caster,"pokrak-wide-bubbles") && HasEditedArea(caster,"pokrak-wide-bubbles") ? "pokrak-wide-bubbles" : "pokrak-bubbles";
            cells = ResolveAbilityArea(caster,areaEffect,source.CurrentCell,cells);
            float reduction = Mathf.Clamp01((Has(caster, "pokrak-strong-bubbles") ?
                V(caster, "pokrak-strong-bubbles", "reduction", 25) : V(caster, "pokrak-bubbles", "reduction", 20)) / 100f);
            int duration = AbilityTiming.Ticks(Has(caster, "pokrak-long-bubbles") ?
                V(caster, "pokrak-long-bubbles", "duration", 3) : V(caster, "pokrak-bubbles", "duration", 2));
            if (duration <= 0 || reduction <= 0) return;
            foreach (var actor in actors)
            {
                var recipient = actor.Unit;
                if (recipient == null || !recipient.IsAlive || !recipient.isActiveAndEnabled || recipient.IsReviving ||
                    recipient.Side != source.Side || !cells.Contains(recipient.CurrentCell)) continue;
                if (!ApplyReduction(actor, reduction, tick + duration, "pokrak-guard")) continue;
                pokrakBubbles.Add(new PokrakBubble { Source = source, Recipient = recipient,
                    SourceVersion = source.CombatResetVersion, RecipientVersion = recipient.CombatResetVersion,
                    Until = tick + duration, Reduction = reduction });
            }
            if (!HasEditedArea(caster,areaEffect)) AreaEffect?.Invoke(source, "pokrak-bubbles", cells);
            lastChangeTick = tick;
        }
        void TickPokrakBubbles() => pokrakBubbles.RemoveAll(b => !ActiveBubble(b));
    }
}
