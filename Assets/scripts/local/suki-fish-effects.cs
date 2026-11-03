using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        Sprite fishSprite;
        void FishShieldLaunched(CombatSimulation.FishShieldFlight flight)
        {
            if (flight.Target?.CurrentCell == null) return;
            var destination = flight.Target.CurrentCell;
            flight.Source?.GetComponent<UnitPresentation>()?.Face(destination.X - flight.Origin.X, destination.Y - flight.Origin.Y);
            StartCoroutine(FishShieldVisual(flight));
        }

        IEnumerator FishShieldVisual(CombatSimulation.FishShieldFlight flight)
        {
            var simulation = match.Simulation;
            if (fishSprite == null) fishSprite = Resources.LoadAll<Sprite>("MonsterPouch/Suki/suki-fish").FirstOrDefault();
            var fish = Box("Suki shield fish", layer, Vector2.one * 42, Color.white);
            fish.sprite = fishSprite; fish.preserveAspect = true;
            Vector3 from = match.Mapper.GetWorldPosition(flight.Origin) + Vector3.up * .65f;
            float shownTime = 0, lastTime = simulation.Elapsed;
            while (Current(simulation) && flight.Active && flight.Target != null)
            {
                if (simulation.Elapsed != lastTime)
                { shownTime = simulation.Elapsed - flight.StartedAt; lastTime = simulation.Elapsed; }
                else shownTime = Mathf.Min(shownTime + EffectDelta, simulation.Elapsed - flight.StartedAt + CombatSimulation.TickDuration);
                float t = Mathf.Clamp01(shownTime / flight.Duration);
                Vector3 to = flight.Target.transform.position + Vector3.up * .45f;
                Vector2 a = Point(from), b = Point(to);
                float arc = Mathf.Clamp(Vector2.Distance(a, b) * .28f, 38, 95);
                fish.rectTransform.anchoredPosition = Round(Vector2.Lerp(a, b, t) + Vector2.up * (4 * arc * t * (1 - t)));
                Vector2 tangent = b - a + Vector2.up * (4 * arc * (1 - 2 * t));
                var pose=ProjectileFlightRendering.Fish(fish.sprite,Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg);pose.Apply(fish);
                SizeProjectile(fish, flight.Source, pose.Size, ProjectileVisualSource.Ability);
                yield return null;
            }
            if (fish != null) Destroy(fish.gameObject);
        }
    }
}
