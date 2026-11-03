using System.Collections.Generic;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public enum ProjectileVisualSource { Basic, Ability, Upgrade1, Upgrade2, Upgrade3 }

    public sealed class ProjectileVisualRegistry
    {
        readonly Dictionary<string, CharacterDefinition> characters = new Dictionary<string, CharacterDefinition>();
        public ProjectileVisualRegistry(CharacterDefinition[] source)
        {
            if (source != null) foreach (var character in source)
                if (character != null) characters.Add(character.Id, character);
        }
        public CharacterDefinition Get(string id) => id != null && characters.TryGetValue(id, out var character) ? character : null;
    }

    /// <summary>One presentation consumer for the existing renderer paths. No simulation writes.</summary>
    public static class ProjectileVisualSizing
    {
        static readonly Dictionary<Sprite, Rect> proceduralBounds = new Dictionary<Sprite, Rect>();
        static readonly HashSet<Sprite> reportedMissing = new HashSet<Sprite>();

        public static ProjectileVisualSlot Resolve(ProjectileVisualRegistry registry, BattleUnit actor,
            ProjectileVisualSource source = ProjectileVisualSource.Basic, string variant = "flight")
        {
            if (actor == null) return null;
            return Resolve(registry, actor.BaseStats.DefinitionId, source, variant);
        }

        public static ProjectileVisualSlot Resolve(ProjectileVisualRegistry registry, string unitId,
            ProjectileVisualSource source = ProjectileVisualSource.Basic, string variant = "flight")
        {
            var character = registry?.Get(unitId);
            return character != null ? Resolve(character, source, variant) : null;
        }

        public static ProjectileVisualSlot Resolve(CharacterDefinition character, ProjectileVisualSource source, string variant)
        {
            ProjectileVisualSlot[] slots = null;
            if (source == ProjectileVisualSource.Basic) slots = character.Attack?.ProjectileVisuals;
            else if (source == ProjectileVisualSource.Ability) slots = character.Ability?.ProjectileVisuals;
            else
            {
                int index = (int)source - (int)ProjectileVisualSource.Upgrade1;
                if (character.Upgrades != null && index >= 0 && index < character.Upgrades.Length)
                    slots = character.Upgrades[index]?.ProjectileVisuals;
            }
            if (slots != null) foreach (var slot in slots)
                if (slot != null && slot.VariantId == variant) return slot;
            return null;
        }

        public static ProjectileVisualSlot ResolveEmission(ProjectileVisualRegistry registry,
            CombatAttackPresentationSnapshot emission, BattleUnit actor, BattleUnit target, string variant = "flight")
        {
            // Called synchronously at emission, before Flight's first yield. Never infer a recent ability.
            if (emission == null || actor == null || target == null || emission.Source != actor || emission.Target != target ||
                emission.SourceResetVersion != actor.CombatResetVersion || emission.TargetResetVersion != target.CombatResetVersion)
                return null;
            var character = registry?.Get(actor.BaseStats.DefinitionId);
            if (character == null) return null;
            if (!emission.IsAbility) return Resolve(character, ProjectileVisualSource.Basic, variant);
            if (character.Ability?.Definition?.EffectId == emission.EffectId)
                return Resolve(character, ProjectileVisualSource.Ability, variant);
            if (character.Upgrades != null) for (int i = 0; i < character.Upgrades.Length && i < 3; i++)
                if (character.Upgrades[i]?.Definition?.EffectId == emission.EffectId)
                    return Resolve(character, (ProjectileVisualSource)((int)ProjectileVisualSource.Upgrade1 + i), variant);
            return null; // Unknown/missing emission context retains the exact legacy visual.
        }

        public static void RegisterProcedural(Sprite sprite, Color32[] pixels, int width, int height)
        {
            if (sprite != null)
                proceduralBounds[sprite] = ProjectileVisualGeometry.AlphaBounds(pixels, width, height, sprite.rect);
        }

        public static void UnregisterProcedural(Sprite sprite)
        { if(sprite!=null){proceduralBounds.Remove(sprite);reportedMissing.Remove(sprite);} }

        public static bool TryVisibleRect(ProjectileVisualSlot slot, Sprite sprite, out Rect rect)
        {
            if (sprite == null) { rect = new Rect(0, 0, 1, 1); return true; } // Solid UI primitive.
            var frame = slot?.Frame(sprite);
            if (frame != null) { rect = slot.ClipRect(sprite); return rect.width > 0 && rect.height > 0; }
            return proceduralBounds.TryGetValue(sprite, out rect);
        }

        public static void Apply(Image image, ProjectileVisualSlot slot, float cellPixels, Vector2 legacyRect)
        {
            if (image == null) return;
            image.rectTransform.sizeDelta = legacyRect;
            if (slot == null || !slot.OverrideSize) return;
            if (slot.ClampedPercent == 0) { image.rectTransform.sizeDelta = Vector2.zero; return; }
            if (!TryVisibleRect(slot, image.sprite, out var visible))
            {
                // Registration validates this before integration; never silently invent alpha calibration.
                if (image.sprite != null && reportedMissing.Add(image.sprite))
                    Debug.LogError("Missing projectile visible bounds: " + image.sprite.name);
                return;
            }
            image.rectTransform.sizeDelta = ProjectileVisualGeometry.Size(legacyRect,
                image.sprite != null ? image.sprite.rect.size : legacyRect, image.preserveAspect,
                image.rectTransform.localScale, visible, cellPixels, slot.ClampedPercent);
        }

        // UI Images render the sprite rectangle centered on their RectTransform; an imported sprite pivot
        // does not center off-axis alpha. Use the registered per-frame bounds after sizing and rotation.
        public static Vector2 CenterOffset(Image image,ProjectileVisualSlot slot)
        {
            if(image==null||image.sprite==null)return Vector2.zero;
            var frame=slot?.Frame(image.sprite);
            if(frame==null)return Vector2.zero;
            var drawn=ProjectileVisualGeometry.Fit(image.rectTransform.sizeDelta,image.sprite.rect.size,image.preserveAspect);
            drawn=Vector2.Scale(drawn,new Vector2(image.rectTransform.localScale.x,image.rectTransform.localScale.y));
            return ProjectileFlightRendering.CenterOffset(frame.VisibleRect,drawn,image.rectTransform.localEulerAngles.z);
        }
        // Trails are presentation satellites: retain their trajectory and ratio, but disappear at visual zero.
        public static float Ratio(ProjectileVisualSlot slot, Vector2 original, Vector2 resized)
            => slot != null && slot.OverrideSize && original.x > 0 ? resized.x / original.x : 1;
    }
}
