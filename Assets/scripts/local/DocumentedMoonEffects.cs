using System.Collections;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        void ReflectedMoonLaunched(BattleUnit actor, BattleUnit target, float duration)
        {
            ContactEffects?.TrackReleasedContactFlight(actor,target,duration);
            if (actor != null && target != null && layer != null)
                StartCoroutine(ReflectedMoonFlight(actor, target, duration));
        }

        IEnumerator ReflectedMoonFlight(BattleUnit actor, BattleUnit target, float duration)
        {
            var simulation = match.Simulation;
            float launched = simulation.Elapsed, elapsed = 0;
            Vector2 from = Point(actor.transform.position + Vector3.up * .6f);
            var root = EffectRoot("Bugaloo reflected moon");
            var trail = new Image[4];
            for (int i = 0; i < trail.Length; i++)
                trail[i] = Box("Lunar trail", root, Vector2.one * (7 - i), new Color(.7f,.65f,1, .65f - i*.12f));
            var moon = Box("Reflected moon projectile", root, Vector2.one * 31, Color.white);
            moon.sprite = PixelSprite("reflected-moon");
            moon.preserveAspect = true;
            root.anchoredPosition = Round(from);
            while (Current(simulation) && target != null)
            {
                elapsed += EffectDelta;
                bool arrived = simulation.Elapsed + .001f >= launched + duration;
                if (!target.IsAlive && !arrived) break;
                float t = arrived ? 1 : Mathf.Min(.94f, elapsed / Mathf.Max(.1f, duration));
                Vector2 to = Point(target.transform.position + Vector3.up * .5f);
                Vector2 arc = Vector2.up * Mathf.Sin(t * Mathf.PI) * 20;
                Vector2 point = Vector2.Lerp(from, to, t) + arc;
                root.anchoredPosition = Round(point);
                var pose=ProjectileFlightRendering.ReflectedMoon(moon.sprite,t);pose.Apply(moon);
                float sizeRatio = SizeProjectile(moon, actor, pose.Size, ProjectileVisualSource.Ability);
                for (int i = 0; i < trail.Length; i++)
                {
                    ProjectileFlightRendering.MoonTrail(i,sizeRatio).Apply(trail[i]);
                    float previous = Mathf.Max(0, t - .055f * (i + 1));
                    trail[i].rectTransform.anchoredPosition = Round(Vector2.Lerp(from, to, previous) +
                        Vector2.up * Mathf.Sin(previous * Mathf.PI) * 20 - point);
                }
                if (arrived)
                {
                    // Settled Impacted owns the flash; the shared terminal tracker handles a missed moon.
                    break;
                }
                yield return null;
            }
            ReleaseEffect(root);
        }
    }
}
