using System.Collections.Generic;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Resource lookup only. CombatHudSymbols and the existing badge ring own UI.
    public static class StatusHudSymbolAssets
    {
        const string Folder = "MonsterPouch/StatusPresentation/Icons/";
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public static string ResourcePath(string id)
        {
            switch (id)
            {
                case "poison": case "regeneration": case "armor-up": case "armor-down":
                case "damage-suppressed": case "stun-immunity": case "root-immunity":
                case "push-immunity": return Folder + id;
                default: return null;
            }
        }
        public static Sprite Get(string id)
        {
            string path = ResourcePath(id);
            if (path == null) return null;
            if (cache.TryGetValue(id, out var sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>(path);
            if (sprite != null) cache[id] = sprite;
            return sprite;
        }
        public static Color Tint(string id)
        {
            switch (id)
            {
                case "poison": return new Color(.42f,.83f,.19f);
                case "regeneration": return new Color(.19f,.85f,.56f);
                case "armor-up": return new Color(.42f,.77f,1);
                case "armor-down": return new Color(.94f,.40f,.29f);
                case "damage-suppressed": return new Color(.91f,.38f,.51f);
                case "stun-immunity": return new Color(1,.78f,.23f);
                case "root-immunity": return new Color(.39f,.77f,.37f);
                case "push-immunity": return new Color(.37f,.76f,.95f);
                default: return Color.white;
            }
        }
    }
}
