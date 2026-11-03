using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    /// <summary>Reuses Kayon's existing wooden prop, anchored at its base, for combat summons.</summary>
    internal static class WoodenDummyArt
    {
        [Serializable] private sealed class Atlas { public Part[] sprites; }
        [Serializable] private sealed class Part { public string name; public float[] rect; }
        private static Sprite sprite;

        public static UnitArt Create(UnitArt source)
        {
            if (source == null) return null;
            if (sprite == null)
            {
                const string path = "MonsterPouch/UI/LobbyRigs/props";
                var texture = Resources.Load<Texture2D>(path);
                var json = Resources.Load<TextAsset>(path + ".parts");
                if (texture == null || json == null) return source;
                var atlas = JsonUtility.FromJson<Atlas>(json.text);
                if (atlas?.sprites == null) return source;
                foreach (var part in atlas.sprites)
                {
                    if (part.name != "wooden-dummy" || part.rect == null || part.rect.Length != 4) continue;
                    var r = part.rect;
                    // This atlas also supplies Kayon's lobby prop; keep its pixel edges crisp.
                    texture.filterMode = FilterMode.Point;
                    sprite = Sprite.Create(texture, new Rect(r[0], r[1], r[2], r[3]),
                        new Vector2(.5f, 0), 100, 0, SpriteMeshType.FullRect);
                    sprite.name = "kayon-wooden-dummy";
                    break;
                }
                if (sprite == null) return source;
            }

            // Do not mutate the catalog or retain any of the white prototype's leg animations.
            var art = new UnitArt
            {
                Id = source.Id, Portrait = sprite, ReferencePixelWidth = sprite.rect.width,
                FootPivot = new Vector2(.5f, 0), WorldWidth = source.WorldWidth,
                DisplayScale = source.DisplayScale, InGameSizePercent = source.InGameSizePercent, MoveEase = source.MoveEase,
                HitFlashDuration = source.HitFlashDuration, AbilityPulseDuration = source.AbilityPulseDuration,
                AbilityPulseScale = source.AbilityPulseScale, SuperReadyPulseScale = source.SuperReadyPulseScale,
                SourceNotes = "Kayon's existing wooden dummy prop; no legs, alternating sideways hops."
            };
            for (int i = 0; i < 8; i++)
            {
                art.IdleDirections[i] = sprite;
                art.AttackDirections[i] = sprite;
                // A planted wooden figure rocks at rest without soft breathing deformation.
                art.DirectionalAnimations[i] = new UnitDirectionalAnimation { Idle = new[] { sprite } };
            }
            return art;
        }
    }
}
