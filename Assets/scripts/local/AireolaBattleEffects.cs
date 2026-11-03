using System.Collections;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        const string AireolaSphereEffect = "zephalontropo-esfera-comprimida";
        const string AireolaWindsEffect = "alveyr-escudo-de-los-vientos";
        AireolaVisualCatalog aireolaVisuals;

        AireolaVisualClip AireolaClip(string id)
        {
            if (aireolaVisuals == null)
                aireolaVisuals = Resources.Load<AireolaVisualCatalog>("MonsterPouch/Aireola/AireolaVisuals");
            return aireolaVisuals?.Get(id);
        }

        static bool AireolaOwns(BattleUnit actor)
        {
            string id = actor?.BaseStats.DefinitionId;
            return id == "alveyr" || id == "zephalontropo" || id == "alboguera" || id == "palista";
        }

        static bool IsAireolaAbility(string kind)
            => kind == AireolaSphereEffect || kind == AireolaWindsEffect || kind == "alveyr-anillo-al-cielo"
                || kind == "alboguera-afinacion" || kind == "alboguera-compas-ascendente" || kind == "palista-golpe-de-vendaval";

        // Called inside Attacked while the immutable combat emission is in scope.
        // Basics arrive at QueueImpact; ability emissions already represent their own release.
        bool TryAireolaFlight(BattleUnit source, BattleUnit target)
        {
            if (!AireolaOwns(source) || target == null) return false;
            var emission = match.CurrentAttackPresentation;
            if (emission == null || emission.Source != source || emission.Target != target ||
                emission.SourceResetVersion != source.CombatResetVersion || emission.TargetResetVersion != target.CombatResetVersion)
                return false;
            bool sphere = emission.IsAbility && emission.EffectId == AireolaSphereEffect;
            if (emission.IsAbility && !sphere) return false;
            string id = source.BaseStats.DefinitionId;
            if (!sphere && id != "zephalontropo" && id != "alboguera") return false;
            string renderer = sphere ? AireolaFlightRendering.Sphere
                : id == "zephalontropo" ? AireolaFlightRendering.Bolt : AireolaFlightRendering.Note;
            var art = AireolaFlightRendering.Resolve(renderer);
            if (art?.Flight == null || art.Flight.Length == 0) return false;
            var slot = ProjectileVisualSizing.ResolveEmission(projectileVisuals, emission, source, target);
            var simulation = match.Simulation;
            var timing = CaptureFactionFlight(emission);
            int sourceReset = source.CombatResetVersion, targetReset = target.CombatResetVersion;
            // Zephalontropo travels on the line through the unit/cell centers; the other clip retains its authored height.
            float flightHeight=id=="zephalontropo"?0:.5f;
            float cellPixels = AireolaCellPixels();
            StartCoroutine(AireolaFlight(simulation, source, target, sourceReset, targetReset, flightHeight, cellPixels, art, slot, timing));
            return true;
        }

        float AireolaCellPixels()
        {
            var origin = match.Mapper.GetWorldPosition(match.Board.GetCell(0, 0));
            return Vector2.Distance(Point(origin), Point(origin + Vector3.right * match.Mapper.CellSize.x));
        }

        bool AireolaCurrent(CombatSimulation simulation, BattleUnit source, int reset)
            => Current(simulation) && source != null && source.CombatResetVersion == reset;

        IEnumerator AireolaFlight(CombatSimulation simulation, BattleUnit source, BattleUnit target,
            int sourceReset, int targetReset, float flightHeight, float cellPixels,
            ProjectileArt art, ProjectileVisualSlot slot, FactionFlightTiming timing)
        {
            while(timing.Waiting&&Current(timing.Simulation)&&timing.CanLaunch)yield return null;
            if(!Current(timing.Simulation)||!timing.CanLaunch||timing.Elapsed>=timing.Duration)yield break;
            var root = EffectRoot("aireola-flight");
            var image = Box("flight", root, Vector2.one, Color.white);
            Vector2 from=Point(source.transform.position+Vector3.up*flightHeight),to;
            float visualElapsed=timing.Elapsed,visualDelta=0;
            try
            {
                while (timing.Elapsed < timing.Duration && root != null && image != null &&
                    AireolaCurrent(simulation, source, sourceReset) && target != null && target.CombatResetVersion == targetReset)
                {
                    visualElapsed=ProjectileFlightRendering.VisualElapsed(visualElapsed,timing.Elapsed,visualDelta,timing.Duration,CombatSimulation.TickDuration);
                    float elapsed = visualElapsed;
                    to = Point(target.transform.position + Vector3.up * flightHeight);
                    float angle = Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg;
                    var pose = AireolaFlightRendering.Sample(art, elapsed, cellPixels, angle);
                    pose.Apply(image);
                    ProjectileVisualSizing.Apply(image, slot, cellPixels, pose.Size);
                    image.rectTransform.anchoredPosition = Vector2.Lerp(from, to, elapsed / timing.Duration)+ProjectileVisualSizing.CenterOffset(image,slot);
                    yield return null;
                    visualDelta=EffectDelta;
                }
            }
            finally { ReleaseEffect(root); }
        }

        // The module emits exact affected cells at release/settled contact. Never select or recalculate targets here.
        bool TryAireolaAreaEffect(BattleUnit source, string kind, BoardCell[] cells)
        {
            if (!AireolaOwns(source)) return false;
            var contact=match.Simulation?.CurrentImpactPresentation;
            if(kind=="aireola-basic-contact"&&contact!=null&&!contact.Applied)return true;
            string clipId;
            switch (kind)
            {
                case "aireola-basic-contact":
                    clipId = source.BaseStats.DefinitionId + "/" + (source.BaseStats.DefinitionId == "alveyr" ? "basic-palm"
                        : source.BaseStats.DefinitionId == "palista" ? "basic-slash" : "basic-impact");
                    break;
                case "alveyr-anillo-al-cielo": clipId = "alveyr/opening-halo"; break;
                case AireolaWindsEffect: clipId = "alveyr/winds-loop"; break;
                case "alboguera-afinacion": case "alboguera-compas-ascendente": clipId = "alboguera/resonance-onset"; break;
                case AireolaSphereEffect: clipId = "zephalontropo/sphere-impact"; break;
                case "palista-golpe-de-vendaval": clipId = "palista/gale-impact"; break;
                default: return false;
            }
            var clip = AireolaClip(clipId);
            if (clip?.Frames == null || clip.Frames.Length == 0) return false;
            var simulation = match.Simulation;
            int reset = source.CombatResetVersion;
            float cellPixels = AireolaCellPixels();
            if (kind == AireolaWindsEffect)
            {
                // The shared status snapshot, not an independent visual timeout, owns persistence.
                StartCoroutine(AireolaWinds(simulation, source, reset, clip, cellPixels));
            }
            else foreach (var cell in cells)
            {
                if (cell == null) continue;
                bool basic=kind=="aireola-basic-contact";
                Vector3 point = (basic&&contact!=null?contact.Position:match.Mapper.GetWorldPosition(cell)) + Vector3.up * (kind == "alveyr-anillo-al-cielo" ? .8f : .35f);
                StartCoroutine(AireolaOnset(simulation, source, reset, clip, cellPixels*((basic||kind==AireolaSphereEffect||kind=="palista-golpe-de-vendaval")?ContactCells(source,!basic)/Mathf.Max(.01f,clip.WidthCells):1), Point(point)));
            }
            return true;
        }

        static void DrawAireolaClip(UnityEngine.UI.Image image, AireolaVisualClip clip, int index, float cellPixels)
        {
            var sprite = clip.Frames[Mathf.Clamp(index, 0, clip.Frames.Length - 1)];
            image.sprite = sprite;
            image.preserveAspect = false;
            image.rectTransform.sizeDelta = sprite.rect.size * (cellPixels * clip.WidthCells / Mathf.Max(1, clip.VisibleEnvelopePixels));
        }

        IEnumerator AireolaOnset(CombatSimulation simulation, BattleUnit source, int reset,
            AireolaVisualClip clip, float cellPixels, Vector2 point)
        {
            var root = EffectRoot("aireola-" + clip.Id);
            var image = Box("contact", root, Vector2.one, Color.white);
            image.rectTransform.anchoredPosition = Round(point);
            float elapsed = 0, duration = clip.Frames.Length / Mathf.Max(1, clip.FramesPerSecond);
            try
            {
                while (elapsed < duration && root != null && image != null && AireolaCurrent(simulation, source, reset))
                {
                    DrawAireolaClip(image, clip, Mathf.FloorToInt(elapsed * clip.FramesPerSecond), cellPixels);
                    elapsed += EffectDelta;
                    yield return null;
                }
            }
            finally { ReleaseEffect(root); }
        }

        IEnumerator AireolaWinds(CombatSimulation simulation, BattleUnit source, int reset,
            AireolaVisualClip clip, float cellPixels)
        {
            var root = EffectRoot("aireola-winds");
            var image = Box("winds", root, Vector2.one, Color.white);
            float elapsed = 0;
            try
            {
                while (root != null && image != null && AireolaCurrent(simulation, source, reset))
                {
                    if (!simulation.TryCaptureAireolaWinds(source, out var status) || status.UnitResetVersion != reset) yield break;
                    DrawAireolaClip(image, clip, Mathf.FloorToInt(elapsed * clip.FramesPerSecond) % clip.Frames.Length, cellPixels);
                    image.rectTransform.anchoredPosition = Round(Point(source.transform.position + Vector3.up * .35f));
                    elapsed += EffectDelta;
                    yield return null;
                }
            }
            finally { ReleaseEffect(root); }
        }
    }
}
