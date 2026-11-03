using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        // Wiring is a one-line merge in Awake: Match.Displaced += HandleDisplaced;
        // MatchController forwards Simulation.Displaced before calling Begin.
        private void HandleDisplaced(CombatDisplacementSnapshot motion)
        {
            if (motion.Unit == null || !visuals.TryGetValue(motion.Unit, out var view)) return;
            float remaining = 0;
            var simulation = Match?.Simulation;
            if (simulation != null && simulation.IsOpeningWindow)
            {
                float configured = Match.Config.OpeningAbilityWindowSeconds;
                if (!float.IsNaN(configured) && !float.IsInfinity(configured))
                    remaining = Mathf.Max(0, Mathf.Clamp(configured, 0, 40) - simulation.Elapsed);
            }
            view.PresentDisplacement(motion, remaining);
        }
    }
}
