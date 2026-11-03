using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterPouch.Local
{
    [Serializable] public sealed class UiSpriteReference { public string Key; public Sprite Sprite; }
    // Explicit asset references also support faction artwork outside Resources.
    public sealed class UiSpriteReferences : ScriptableObject
    {
        public UiSpriteReference[] Entries;
        static UiSpriteReferences instance;
        static readonly Dictionary<string,Sprite> Cache = new Dictionary<string,Sprite>(StringComparer.Ordinal);
        static bool loaded;
        public static Sprite Get(string key)
        {
            if (!loaded)
            {
                loaded = true; instance = Resources.Load<UiSpriteReferences>("MonsterPouch/UI/sprite-references");
                if (instance != null && instance.Entries != null) foreach (var entry in instance.Entries)
                    if (entry != null && !string.IsNullOrEmpty(entry.Key) && entry.Sprite != null) Cache[entry.Key] = entry.Sprite;
            }
            return key != null && Cache.TryGetValue(key,out var sprite) ? sprite : null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { loaded = false; instance = null; Cache.Clear(); }
    }
}
