using System.Collections;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Presentation only. Core has already decided contact, damage and affected cells.
    public sealed partial class DocumentedBattleEffects
    {
        readonly Dictionary<string, Sprite[]> naturalisClips = new Dictionary<string, Sprite[]>();
        static bool NaturalisOwns(string id) => id == "cael" || id == "guarda-veterano"
            || id == "arboreo" || id == "antidota";

        static bool IsNaturalisActive(BattleUnit source, string effect)
        {
            string id = source?.BaseStats.DefinitionId;
            return id == "cael" && effect == "cael-torbellino-de-hojas"
                || id == "guarda-veterano" && effect == "guarda-veterano-liana-vinculante"
                || id == "arboreo" && effect == "arboreo-abrazo-del-bosque"
                || id == "antidota" && effect == "antidota-brote-vital";
        }

        Sprite[] NaturalisClip(string id, string kind)
        {
            string key = id + ":" + kind;
            if (naturalisClips.TryGetValue(key, out var frames)) return frames;
            frames = new Sprite[4];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Resources.Load<Sprite>("MonsterPouch/Characters/" + id + "/Art/VFX/" + kind + "-" + i);
                if (frames[i] == null)
                {
                    Debug.LogError("Missing Naturalis " + kind + " sprite: " + id + "/" + i);
                    naturalisClips[key] = null;
                    return null;
                }
            }
            naturalisClips[key] = frames;
            return frames;
        }

        bool TryNaturalisBasic(BattleUnit actor, BattleUnit target)
        {
            if (actor == null || target == null || !isActiveAndEnabled || layer == null || match == null) return false;
            string id = actor.BaseStats.DefinitionId;
            if (!NaturalisOwns(id) || id == "arboreo") return false;
            // Capture exact ownership/reset identities inside Attacked, before any yield.
            var emission = match.CurrentAttackPresentation;
            if (emission == null || emission.IsAbility || emission.Source != actor || emission.Target != target
                || emission.SourceResetVersion != actor.CombatResetVersion
                || emission.TargetResetVersion != target.CombatResetVersion) return false;
            var style = projectiles?.Get(id);
            if (style?.Flight == null || style.Flight.Length != 4) return false;
            foreach (var frame in style.Flight) if (frame == null) return false;
            var slot = ProjectileVisualSizing.ResolveEmission(projectileVisuals, emission, actor, target);
            if (slot == null)
            {
                Debug.LogError("Missing Naturalis basic flight slot: " + id);
                return false;
            }
            var timing = CaptureFactionFlight(emission);
            Vector3 origin = actor.transform.position + Vector3.up * .5f;
            // Capture the queued basic schedule now; create the flight only after its release.
            StartCoroutine(NaturalisFlight(emission, style, slot, origin, timing));
            return true;
        }

        IEnumerator NaturalisFlight(CombatAttackPresentationSnapshot emission, ProjectileArt style,
            ProjectileVisualSlot slot, Vector3 origin, FactionFlightTiming timing)
        {
            while(timing.Waiting&&Current(timing.Simulation)&&timing.CanLaunch)yield return null;
            if(!Current(timing.Simulation)||!timing.CanLaunch||timing.Elapsed>=timing.Duration)yield break;
            var simulation = timing.Simulation;
            var root = EffectRoot("naturalis-flight-" + emission.EmissionId);
            var image = Box("finite-flight", root, Vector2.zero, Color.white);
            Vector2 from = Point(emission.Source.transform.position+Vector3.up*.5f);
            float visualElapsed=timing.Elapsed,visualDelta=0;
            try
            {
                while (Current(simulation) && root != null && image != null && timing.Elapsed < timing.Duration
                    && emission.Source != null && emission.Target != null
                    && emission.Source.CombatResetVersion == emission.SourceResetVersion
                    && emission.Target.CombatResetVersion == emission.TargetResetVersion)
                {
                    visualElapsed=ProjectileFlightRendering.VisualElapsed(visualElapsed,timing.Elapsed,visualDelta,timing.Duration,CombatSimulation.TickDuration);
                    float elapsed = visualElapsed;
                    Vector2 to = Point(emission.Target.transform.position + Vector3.up * .5f);
                    var pose = ProjectileFlightRendering.Catalog(style, elapsed);
                    if (style.OrientToTarget)
                        pose.Rotation = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
                    pose.Apply(image);
                    image.rectTransform.anchoredPosition = Vector2.Lerp(from, to, Mathf.Clamp01(elapsed / timing.Duration));
                    // Same sampler and alpha-envelope geometry as the existing editor preview.
                    SizeProjectile(image, slot, pose.Size);
                    image.rectTransform.anchoredPosition+=ProjectileVisualSizing.CenterOffset(image,slot);
                    yield return null;
                    visualDelta=EffectDelta;
                }
            }
            finally { ReleaseEffect(root); }
        }

        bool TryNaturalisContact(BattleUnit source, string kind, MonsterPouch.Gameplay.Board.BoardCell[] cells)
        {
            if (kind != "naturalis-basic-contact" || source == null || !NaturalisOwns(source.BaseStats.DefinitionId)) return false;
            var contact=match.Simulation?.CurrentImpactPresentation;
            if(contact!=null&&!contact.Applied)return true;
            bool heavy=source.BaseStats.DefinitionId=="arboreo";
            var frames = NaturalisClip(source.BaseStats.DefinitionId, "impact");
            if (frames == null) return false;
            int reset = source.CombatResetVersion;
            foreach (var cell in cells)
                if (cell != null) StartCoroutine(NaturalisBurst(source, reset, frames,
                    (contact?.Position??match.Mapper.GetWorldPosition(cell))+Vector3.up*(heavy?0:.35f),
                    ContactCellPixels(match.Mapper.GetWorldPosition(cell))*ContactCells(source,false)));
            return true;
        }

        bool TryNaturalisActive(BattleUnit source, string effect)
        {
            if (!IsNaturalisActive(source, effect)
                || !isActiveAndEnabled || layer == null || match == null) return false;
            var frames = NaturalisClip(source.BaseStats.DefinitionId, "active");
            if (frames == null) return false;
            int reset = source.CombatResetVersion;
            // Exactly one release burst; authoritative AreaEffect must not replay it per cell.
            StartCoroutine(NaturalisBurst(source, reset, frames,
                source.transform.position + Vector3.up * .5f, 0));
            return true;
        }

        IEnumerator NaturalisBurst(BattleUnit source, int sourceReset, Sprite[] frames, Vector3 position, float size)
        {
            var simulation = match.Simulation;
            var root = EffectRoot("naturalis-contact-burst");
            // Preserve Core44's one-cell display size for the authored release burst.
            if (size == 0) size = Vector2.Distance(Point(position), Point(position + Vector3.right * match.Mapper.CellSize.x));
            var image = Box("authored-burst", root, Vector2.one * size, Color.white);
            image.preserveAspect = true;
            root.anchoredPosition = Round(Point(position));
            float elapsed = 0;
            try
            {
                while (root != null && image != null && Current(simulation) && elapsed < .32f
                    && source != null && source.CombatResetVersion == sourceReset)
                {
                    image.sprite = frames[Mathf.Min(3, Mathf.FloorToInt(elapsed / .32f * 4))];
                    // Stationary impacts/abilities stay visible when flight size is zero.
                    elapsed += EffectDelta;
                    yield return null;
                }
            }
            finally { ReleaseEffect(root); }
        }
    }
}
