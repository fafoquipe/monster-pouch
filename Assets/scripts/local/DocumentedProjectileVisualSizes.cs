using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        float SizeProjectile(Image image, BattleUnit actor, Vector2 original,
            ProjectileVisualSource source = ProjectileVisualSource.Basic, string variant = "flight")
        {
            var slot = ProjectileVisualSizing.Resolve(projectileVisuals, actor, source, variant);
            return SizeProjectile(image, slot, original);
        }

        float SizeProjectile(Image image, MonsterPouch.Gameplay.Presentation.ProjectileVisualSlot slot, Vector2 original)
        {
            var origin = match.Mapper.GetWorldPosition(match.Board.GetCell(0, 0));
            float tile = Vector2.Distance(Point(origin), Point(origin + Vector3.right * match.Mapper.CellSize.x));
            ProjectileVisualSizing.Apply(image, slot, tile, original);
            return ProjectileVisualSizing.Ratio(slot, original, image.rectTransform.sizeDelta);
        }
    }

    public sealed partial class LocalGameUI
    {
        void SizeProjectile(Image image, BattleUnit actor, string variant)
        {
            var slot = ProjectileVisualSizing.Resolve(projectileVisuals, actor, ProjectileVisualSource.Basic, variant);
            // Existing charged appearances keep their own optional slot. Other legacy users share flight.
            if (slot == null && variant == "charged") slot = ProjectileVisualSizing.Resolve(projectileVisuals, actor);
            var origin = actor.transform.position;
            float tile = Vector2.Distance(ToCanvas(GameCamera.WorldToScreenPoint(origin)),
                ToCanvas(GameCamera.WorldToScreenPoint(origin + Vector3.right * Match.Mapper.CellSize.x)));
            ProjectileVisualSizing.Apply(image, slot, tile, image.rectTransform.sizeDelta);
        }
    }
}
