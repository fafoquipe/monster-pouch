using System.Collections;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    /// <summary>Volkanar visuals consume authoritative events and never mutate combat state.</summary>
    public sealed partial class DocumentedBattleEffects
    {
        private VolkanarEffectsCatalog volkanarEffects;
        private VolkanarEffectsCatalog VolkanarArt => volkanarEffects != null ? volkanarEffects :
            (volkanarEffects = Resources.Load<VolkanarEffectsCatalog>("MonsterPouch/Eronya/Volkanar/EffectsCatalog"));

        private bool TryVolkanarAttack(BattleUnit actor, BattleUnit target)
        {
            if (actor == null || target == null || actor.BaseStats.DefinitionId != "canonero-del-orvek" ||
                layer == null || !isActiveAndEnabled || VolkanarArt == null) return false;
            // Capture and resolve inside Attacked, before StartCoroutine can reach its first yield.
            var emission = match.CurrentAttackPresentation;
            if (emission == null || emission.Source != actor || emission.Target != target) return false;
            bool bomb = emission.IsAbility && emission.EffectId == "canonero-del-orvek-bomba-de-crater";
            if (emission.IsAbility && !bomb) return false;
            string variant = bomb ? "crater-bomb-flight" : "slag-flight";
            var slot = ProjectileVisualSizing.ResolveEmission(projectileVisuals, emission, actor, target, variant);
            var frames = bomb ? VolkanarArt.CraterBombFlight : VolkanarArt.SlagFlight;
            if (frames == null || frames.Length == 0) return false;
            var timing=CaptureFactionFlight(emission);
            StartCoroutine(VolkanarFlight(actor, target, emission, slot, frames, bomb, timing));
            return true;
        }

        private IEnumerator VolkanarFlight(BattleUnit actor, BattleUnit target,
            CombatAttackPresentationSnapshot emission,
            MonsterPouch.Gameplay.Presentation.ProjectileVisualSlot slot, Sprite[] frames, bool bomb, FactionFlightTiming timing)
        {
            while(timing.Waiting&&Current(timing.Simulation)&&timing.CanLaunch)yield return null;
            if(!Current(timing.Simulation)||!timing.CanLaunch||timing.Elapsed>=timing.Duration)yield break;
            var simulation = timing.Simulation;
            Vector3 start = actor.transform.position + Vector3.up * .5f;
            Vector3 end = target.transform.position + Vector3.up * .5f;
            var shot = Box("volkanar-" + (bomb ? "crater-bomb" : "slag-shot"), layer, Vector2.one, Color.white);
            shot.preserveAspect = true;
            float tile = VolkanarTilePixels(start);
            StartCoroutine(VolkanarBurst(VolkanarArt.OrvekMuzzle, start, .42f, .12f, simulation));
            float visualElapsed=timing.Elapsed,visualDelta=0;
            // Core owns arrival and damage. This view reads its captured release/travel schedule.
            try
            {
                while (timing.Elapsed < timing.Duration && Current(simulation) && actor != null && target != null &&
                    actor.CombatResetVersion == emission.SourceResetVersion && target.CombatResetVersion == emission.TargetResetVersion)
                {
                    visualElapsed=ProjectileFlightRendering.VisualElapsed(visualElapsed,timing.Elapsed,visualDelta,timing.Duration,CombatSimulation.TickDuration);
                    float t = Mathf.Clamp01(visualElapsed / timing.Duration);
                    end = target.transform.position + Vector3.up * .5f;
                    Vector2 delta = Point(end) - Point(start);
                    var pose=VolkanarFlightRendering.Sample(frames,slot,t,tile,bomb,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                    pose.Apply(shot);shot.rectTransform.anchoredPosition = Vector2.Lerp(Point(start), Point(end), t);
                    SizeProjectile(shot, slot, pose.Size); // Same sampled frame as the real-art preview.
                    shot.rectTransform.anchoredPosition+=ProjectileVisualSizing.CenterOffset(shot,slot);
                    yield return null;
                    visualDelta=EffectDelta;
                }
            }
            finally { if (shot != null) Destroy(shot.gameObject); }
        }

        private static bool IsVolkanarEffect(string kind) =>
            kind == "serya-avance-de-la-llama" || kind == "serya-conquista" ||
            kind == "legionario-tajo-de-forja" || kind == "dagata-doble-ascua" ||
            kind == "dagata-entrada-entre-humo" || kind == "canonero-del-orvek-bomba-de-crater";

        private bool TryVolkanarArea(BattleUnit source, string kind, BoardCell[] cells)
        {
            if (!IsVolkanarEffect(kind) || VolkanarArt == null || source == null) return false;
            Sprite[] frames;
            float size = ContactCells(source,true), duration = .24f;
            switch (kind)
            {
                case "serya-avance-de-la-llama": case "serya-conquista": frames = VolkanarArt.SeryaSlash; break;
                case "legionario-tajo-de-forja": frames = VolkanarArt.LegionSlash; break;
                case "dagata-doble-ascua": frames = VolkanarArt.DagataSlash; break;
                case "dagata-entrada-entre-humo": frames = VolkanarArt.DagataSmoke; size = .38f; duration = .3f; break;
                default: frames = VolkanarArt.OrvekImpact; duration = .27f; break;
            }
            var seen = new HashSet<BoardCell>();
            foreach (var cell in cells) if (cell != null && seen.Add(cell))
                StartCoroutine(VolkanarBurst(frames, match.Mapper.GetWorldPosition(cell) + Vector3.up * .35f, size, duration, match.Simulation));
            return true;
        }

        private float VolkanarTilePixels(Vector3 origin) =>
            Vector2.Distance(Point(origin), Point(origin + Vector3.right * match.Mapper.CellSize.x));

        private IEnumerator VolkanarBurst(Sprite[] frames, Vector3 world, float tiles, float duration, CombatSimulation simulation)
        {
            if (frames == null || frames.Length == 0) yield break;
            var image = Box("volkanar-impact", layer, Vector2.one * VolkanarTilePixels(world) * tiles, Color.white);
            image.preserveAspect = true; image.rectTransform.anchoredPosition = Round(Point(world));
            float elapsed = 0;
            try
            {
                while (elapsed < duration && Current(simulation))
                {
                    image.sprite = frames[Mathf.Min(frames.Length - 1, Mathf.FloorToInt(elapsed / duration * frames.Length))];
                    elapsed += EffectDelta; yield return null;
                }
            }
            finally { if (image != null) Destroy(image.gameObject); }
        }
    }
}
