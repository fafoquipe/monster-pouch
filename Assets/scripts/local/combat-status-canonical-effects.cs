using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    // New status symbols belong to UnitStatusBadges, just like legacy states.
    // Keep only reconciliation of legacy world visuals; combat owns all timers.
    public sealed partial class CombatStatusEffects
    {
        readonly Dictionary<BattleUnit, CombatStatusFrame> canonicalVisualFrames =
            new Dictionary<BattleUnit, CombatStatusFrame>();

        static bool IsCanonicalVisual(string id)
        {
            switch (id)
            {
                case "poison": case "regeneration": case "armor-up": case "armor-down":
                case "damage-suppressed": case "burn": case "haste": case "anti-heal":
                case "damage-reduction": case "empowered": return true;
                default: return false; // Immunities use distinct HUD glyphs, no false control VFX.
            }
        }

        bool CanonicalVisualIsCurrent(BattleUnit actor, string id)
        {
            if (actor == null || !canonicalVisualFrames.TryGetValue(actor, out var frame) ||
                frame.UnitResetVersion != actor.CombatResetVersion) return false;
            foreach (var state in frame.Statuses)
                if (state.Id == id && state.UnitResetVersion == frame.UnitResetVersion) return true;
            return false;
        }

        void ClearCanonicalVisuals()
        { canonicalVisualFrames.Clear(); }

        // Called before the paused early return. Reads every frame so same-tick
        // cleanse, reset, death and winner replacement cannot leave stale effects.
        void ReconcileCanonicalVisuals()
        {
            ClearCanonicalVisuals();
            if (match != null && match.Phase == MatchPhase.Combat && simulation != null)
                foreach (var pair in match.Actors)
                {
                    var actor = pair.Value;
                    if (actor == null) continue;
                    var frame = simulation.CaptureStatusFrame(actor);
                    // HUD ownership depends on the current status, not artwork calibration.
                    if (!frame.IsActive || frame.UnitResetVersion != actor.CombatResetVersion) continue;
                    canonicalVisualFrames.Add(actor, frame);
                }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var fx = active[i];
                // Canonical defense circles replace their duplicate world markers,
                // including while paused; legacy-only shields and reflect stay intact.
                if (fx.Persistent && ((fx.Kind == "protection" && CanonicalVisualIsCurrent(fx.Actor,"damage-reduction")) ||
                    (fx.Kind == "shield" && CanonicalVisualIsCurrent(fx.Actor,"shield"))))
                { Release(i); continue; }
                if (!fx.Persistent || !IsCanonicalVisual(fx.Kind)) continue;
                // Retire any old duplicate glyphs, including while paused.
                Release(i);
            }
        }
    }
}
