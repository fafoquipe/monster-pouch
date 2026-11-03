using System.Collections.Generic;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Authored ImageGen artwork; circular plates and timers remain dynamic.
    public static class CombatHudSymbols
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public static Color Tint(string id)
        {
            if (StatusHudSymbolAssets.ResourcePath(id) != null) return StatusHudSymbolAssets.Tint(id);
            switch (id)
            {
                case "attack-up": case "empowered": case "atong-critical": case "bonus-hit": case "lethal-hit": return new Color(.94f, .23f, .32f);
                case "haste": case "rose-haste": case "rhythm": case "storm": return new Color(.13f, .67f, 1);
                case "stealth": return new Color(.22f, .66f, .95f);
                case "burn": case "chocolate": return new Color(1, .43f, .15f);
                case "weakness": case "anti-heal": case "taunt": return new Color(.88f, .25f, .59f);
                case "root": case "tsu-root": return new Color(.87f, .38f, .83f);
                case "slow": return new Color(.55f, .77f, .30f);
                case "sleep": case "reflect": return new Color(.66f, .48f, 1);
                case "trimol-boulder": return new Color(.76f, .65f, .43f);
                case "popow-sweep": case "pull": return new Color(.35f, .79f, .85f);
                case "stun": case "electric-stun": case "control-immunity": return new Color(1, .78f, .23f);
                case "invulnerable": return new Color(.61f, .64f, .69f);
                case "pokrak-guard": return new Color(.77f, .66f, .46f);
                default: return new Color(.33f, .76f, .78f);
            }
        }
        static Texture2D atlas;
        static int Index(string id)
        {
            switch (id)
            {
                case "attack-up": case "empowered": case "atong-critical": case "bonus-hit": case "lethal-hit": return 0;
                case "haste": case "rose-haste": case "rhythm": case "storm": return 1;
                case "stealth": return 2;
                case "damage-reduction": case "guard": case "opening-guard": case "protection": case "pokrak-guard": return 3;
                case "shield": return 4;
                case "stun": return 5;
                case "electric-stun": return 6;
                case "root": case "tsu-root": return 7;
                case "slow": return 8;
                case "sleep": return 9;
                case "burn": return 10;
                case "anti-heal": return 11;
                case "weakness": return 12;
                case "taunt": return 13;
                case "reflect": return 14;
                case "control-immunity": return 15;
                case "invulnerable": return 16;
                case "trimol-boulder": return 17;
                case "popow-sweep": case "pull": return 18;
                case "chocolate": return 19;
                default: return -1;
            }
        }
        public static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var existing) && existing != null) return existing;
            if (StatusHudSymbolAssets.ResourcePath(id) != null) return StatusHudSymbolAssets.Get(id);
            if (id == "pokrak-guard") return BubbleSymbol();
            if (atlas == null) atlas = Resources.Load<Texture2D>("MonsterPouch/UI/CombatIcons/combat-status-atlas");
            if (atlas == null) return null;
            int index = Index(id);
            if (index < 0) return null; // Missing art must not claim this is a guard.
            float width = atlas.width / 5f, height = atlas.height / 4f;
            var rect = new Rect(index % 5 * width, (3 - index / 5) * height, width, height);
            var sprite = Sprite.Create(atlas, rect, Vector2.one * .5f, width);
            sprite.name = "hud-" + id; sprite.hideFlags = HideFlags.HideAndDontSave;
            cache[id] = sprite; return sprite;
        }
        static Sprite BubbleSymbol()
        {
            // Same simple pixel geometry as the live bubble; no miniature ability card.
            const int size = 16; var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = x - 7.5f, dy = y - 7.5f, radius = Mathf.Sqrt(dx * dx + dy * dy);
                bool rim = radius >= 5 && radius < 7;
                bool glint = dx < -1 && dy > 1 && radius >= 3.5f && radius < 4.5f;
                if (rim || glint) pixels[y * size + x] = glint || dx < 0 && dy > 0
                    ? new Color32(244, 232, 203, 255) : new Color32(177, 158, 118, 255);
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "pokrak-bubble-symbol", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
            sprite.name = "hud-pokrak-guard"; sprite.hideFlags = HideFlags.HideAndDontSave;
            cache["pokrak-guard"] = sprite; return sprite;
        }
    }
}
