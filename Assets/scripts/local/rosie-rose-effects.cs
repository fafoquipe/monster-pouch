using System.Collections;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        void HealingRoseLaunched(CombatSimulation.HealingRoseFlight flight)
        {
            flight.Source?.GetComponent<UnitPresentation>()?.Face(flight.Destination.X - flight.Origin.X, flight.Destination.Y - flight.Origin.Y);
            StartCoroutine(HealingRoseVisual(flight));
        }

        IEnumerator HealingRoseVisual(CombatSimulation.HealingRoseFlight flight)
        {
            var simulation = match.Simulation;
            var root = EffectRoot("Rosie rose flight");
            var aura = Box("Rosie healing aura", root, Vector2.zero, new Color(.86f, .48f, .64f, .7f));
            aura.sprite = PixelSprite("dash-aura");
            var rose = Box("Rosie healing rose", root, Vector2.one * 64, Color.white);
            var art = projectiles?.Get("rosie");
            rose.sprite = art?.Flight != null && art.Flight.Length > 0 ? art.Flight[0] : null;
            rose.preserveAspect = true;
            Vector3 from = match.Mapper.GetWorldPosition(flight.Origin);
            Vector2 delta = flight.End - new Vector2(flight.Origin.X, flight.Origin.Y);
            float flip = match.Mapper.Flipped ? -1 : 1;
            Vector3 to = from + new Vector3(delta.x * match.Mapper.CellSize.x * flip, delta.y * match.Mapper.CellSize.y * flip);
            float shownTime = 0, lastSimulationTime = simulation.Elapsed;
            while (Current(simulation) && flight.Active && root != null)
            {
                // Smooth between 100ms ticks, but never advance during pause or outrun combat by a tick.
                if (simulation.Elapsed != lastSimulationTime)
                { shownTime = simulation.Elapsed - flight.StartedAt; lastSimulationTime = simulation.Elapsed; }
                else shownTime = Mathf.Min(shownTime + EffectDelta, simulation.Elapsed - flight.StartedAt + CombatSimulation.TickDuration);
                float t = Mathf.Clamp01(shownTime / flight.Duration);
                Vector2 a = Point(from + Vector3.up * .45f), b = Point(to + Vector3.up * .45f), direction = (b - a).normalized;
                Vector2 normal = new Vector2(-direction.y, direction.x);
                float wave = Mathf.Sin(shownTime * Mathf.PI * 3);
                // The footprint remains straight; only the sprite sways, rolls, and breathes.
                rose.rectTransform.anchoredPosition = Round(Vector2.Lerp(a, b, t) + normal * wave * 5);
                rose.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + wave * 11);
                float cellPixels = Vector2.Distance(Point(from), Point(from + Vector3.right * match.Mapper.CellSize.x));
                float cellHeight = Vector2.Distance(Point(from), Point(from + Vector3.up * match.Mapper.CellSize.y));
                // Ground footprint stays on the true straight path; only the flower sways.
                aura.rectTransform.anchoredPosition = Round(Point(Vector3.Lerp(from, to, t)));
                aura.rectTransform.sizeDelta = new Vector2(cellPixels, cellHeight) * flight.Width;
                aura.color = new Color(.86f, .48f, .64f, .7f + wave * .06f);
                var pose=ProjectileFlightRendering.HealingRose(rose.sprite,cellPixels,flight.Width,wave,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
                pose.Apply(rose);SizeProjectile(rose, flight.Source, pose.Size, ProjectileVisualSource.Ability);
                yield return null;
            }
            ReleaseEffect(root);
        }
    }
}
