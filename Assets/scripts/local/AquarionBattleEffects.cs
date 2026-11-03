using System.Collections;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Presentation only. Core emits these callbacks at authoritative contact/release.
    public sealed partial class DocumentedBattleEffects
    {
        readonly Dictionary<string, Sprite> aquarionSprites = new Dictionary<string, Sprite>();

        Sprite AquarionSprite(string id)
        {
            if (!aquarionSprites.TryGetValue(id, out var sprite))
            {
                sprite = Resources.Load<Sprite>("MonsterPouch/Eronya/Aquarion/VFX/" + id);
                aquarionSprites[id] = sprite;
            }
            return sprite;
        }

        bool TryAquarionBasicFlight(BattleUnit actor, BattleUnit target)
        {
            string id = actor.BaseStats.DefinitionId;
            if (id != "jabalinero" && id != "maestro-de-las-mareas") return false;
            // Capture ownership synchronously, before the coroutine yields. No recent-cast inference.
            var emission = match.CurrentAttackPresentation;
            if(emission==null||emission.Source!=actor||emission.Target!=target)return false;
            bool net=emission.IsAbility&&emission.EffectId=="jabalinero-red-de-cienaga";
            if(emission.IsAbility&&!net)return false;
            var timing=CaptureFactionFlight(emission);
            var slot = ProjectileVisualSizing.ResolveEmission(projectileVisuals, emission, actor, target, net?"binding-javelin":"flight");
            string spriteId = net ? "binding-javelin" : id == "jabalinero" ? "javelin" : "water-bolt";
            var sprite = AquarionSprite(spriteId);
            if (sprite == null) return false;
            string renderer=net?"aquarion-binding-javelin":id=="jabalinero"?"aquarion-javelin":"aquarion-water-bolt";
            StartCoroutine(AquarionFlight(actor, target, net ? .35f : .45f,
                sprite, slot, AquarionFlightRendering.Width(renderer), timing));
            return true;
        }

        static bool IsAquarionAbility(string kind)
            => kind == "drayven-marea-del-norte" || kind == "titanica-ancla-glacial"
                || kind == "jabalinero-red-de-cienaga" || kind == "maestro-de-las-mareas-oleaje";

        bool TryAquarionAreaEffect(BattleUnit source, string kind, BoardCell[] cells)
        {
            if (!IsAquarionAbility(kind) || source == null) return false;
            if (kind == "maestro-de-las-mareas-oleaje") return true; // The single release flight owns this effect.
            if (kind == "jabalinero-red-de-cienaga")
            {
                var sprite = AquarionSprite("binding-javelin");
                if (sprite == null) return false;
                // AreaEffect is actual arrival. Display contact now, never launch a second flight.
                foreach(var cell in cells)
                    if(cell!=null)StartCoroutine(AquarionArea(source,cell,sprite,ContactCells(source,true)));
                return true;
            }
            string spriteId = kind == "drayven-marea-del-norte" ? "north-tide"
                : kind == "titanica-ancla-glacial" ? "glacial-impact" : "tidal-wave";
            var areaSprite = AquarionSprite(spriteId);
            if (areaSprite == null) return false;
            // Cells come from the combat module. No local target selection or range calculation.
            foreach (var cell in cells)
                if (cell != null) StartCoroutine(AquarionArea(source, cell, areaSprite,ContactCells(source,true)));
            return true;
        }

        float AquarionCellPixels(Vector3 origin)
            => Vector2.Distance(Point(origin), Point(origin + Vector3.right * match.Mapper.CellSize.x));

        IEnumerator AquarionFlight(BattleUnit source, BattleUnit target, float targetHeight,
            Sprite sprite, ProjectileVisualSlot slot, float defaultCellWidth, FactionFlightTiming timing)
        {
            while(timing.Waiting&&Current(timing.Simulation)&&timing.CanLaunch)yield return null;
            if(!Current(timing.Simulation)||!timing.CanLaunch||timing.Elapsed>=timing.Duration)yield break;
            var simulation = timing.Simulation;
            int sourceVersion = source.CombatResetVersion;
            int targetVersion = target != null ? target.CombatResetVersion : 0;
            Vector3 origin = source.transform.position + Vector3.up * .5f;
            Vector2 from = Point(origin), to = Point(target.transform.position+Vector3.up*targetHeight), direction = to - from;
            // Preview and runtime share the owner's existing visible-width formula.
            var pose=AquarionFlightRendering.Sample(sprite,slot,AquarionCellPixels(origin),defaultCellWidth,
                Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
            var root = EffectRoot("aquarion-flight-root");
            var image = Box("aquarion-flight", root,pose.Size,Color.white);
            pose.Apply(image);
            SizeProjectile(image, slot, image.rectTransform.sizeDelta);
            float visualElapsed=timing.Elapsed,visualDelta=0;

            try
            {
                while (timing.Elapsed < timing.Duration && image != null && Current(simulation) && source != null
                    && source.CombatResetVersion == sourceVersion
                    && target != null && target.CombatResetVersion == targetVersion)
                {
                    visualElapsed=ProjectileFlightRendering.VisualElapsed(visualElapsed,timing.Elapsed,visualDelta,timing.Duration,MonsterPouch.Gameplay.Match.CombatSimulation.TickDuration);
                    to=Point(target.transform.position+Vector3.up*targetHeight);
                    direction=to-from;
                    image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);
                    image.rectTransform.anchoredPosition = Vector2.Lerp(from, to, Mathf.Clamp01(visualElapsed / timing.Duration))+ProjectileVisualSizing.CenterOffset(image,slot);
                    yield return null;
                    visualDelta=EffectDelta;
                }
            }
            finally { ReleaseEffect(root); }
        }

        IEnumerator AquarionArea(BattleUnit source, BoardCell cell, Sprite sprite, float cellWidth=.38f)
        {
            var simulation = match.Simulation;
            int sourceVersion = source.CombatResetVersion;
            Vector3 origin = match.Mapper.GetWorldPosition(cell) + Vector3.up * .2f;
            var root = EffectRoot("aquarion-area-root");
            var image = Box("aquarion-area", root, Vector2.one * AquarionCellPixels(origin) * cellWidth, Color.white);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.rectTransform.anchoredPosition = Round(Point(origin));
            float elapsed = 0;
            const float duration = .38f;
            try
            {
                while (elapsed < duration && image != null && Current(simulation) && source != null && source.CombatResetVersion == sourceVersion)
                {
                    image.color = new Color(1, 1, 1, 1 - Mathf.Clamp01(elapsed / duration));
                    elapsed += EffectDelta;
                    yield return null;
                }
            }
            finally { ReleaseEffect(root); }
        }
    }
}
