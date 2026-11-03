using System;
using System.Collections.Generic;
using System.Globalization;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    [Serializable] public sealed class AbilityUiEntry
    {
        public string key,resource,spriteName,titleEs,titleEn,descriptionEs,descriptionEn;
    }
    [Serializable] public sealed class AbilityUiManifest { public AbilityUiEntry[] entries; }
    // Presentation catalog only. Stable effect IDs and resolved parameters remain owned by combat.
    public static class AbilityUiCatalog
    {
        static readonly Dictionary<string,AbilityUiEntry> Entries=new Dictionary<string,AbilityUiEntry>(StringComparer.Ordinal);
        static readonly Dictionary<string,Sprite> Sprites=new Dictionary<string,Sprite>(StringComparer.Ordinal);
        static bool loaded;
        static void Load()
        {
            if(loaded)return;loaded=true;
            var source=Resources.Load<TextAsset>("MonsterPouch/UI/AbilityArt/catalog");
            var manifest=source!=null?JsonUtility.FromJson<AbilityUiManifest>(source.text):null;
            if(manifest?.entries==null)return;
            foreach(var entry in manifest.entries)
                if(entry!=null&&!string.IsNullOrEmpty(entry.key))Entries[entry.key]=entry;
        }
        public static bool Contains(string key){Load();return key!=null&&Entries.ContainsKey(key);}
        public static bool HasDescription(string key)
        {Load();return key!=null&&Entries.TryGetValue(key,out var e)&&!string.IsNullOrWhiteSpace(e.descriptionEs)&&!string.IsNullOrWhiteSpace(e.descriptionEn);}
        public static bool HasManaCost(string key,bool spanish)
        {Load();return key!=null&&Entries.TryGetValue(key,out var e)&&(spanish?e.descriptionEs:e.descriptionEn)?.Contains("{manaCost}")==true;}
        public static string Title(string key,bool spanish)
        {Load();return key!=null&&Entries.TryGetValue(key,out var entry)?(spanish?entry.titleEs:entry.titleEn):null;}
        public static Sprite Get(string key)
        {
            var referenced=UiSpriteReferences.Get(key);if(referenced!=null)return referenced;
            Load();if(key==null||!Entries.TryGetValue(key,out var entry)||string.IsNullOrEmpty(entry.resource))return null;
            if(Sprites.TryGetValue(key,out var sprite)&&sprite!=null)return sprite;
            if(string.IsNullOrEmpty(entry.spriteName))sprite=Resources.Load<Sprite>(entry.resource);
            else foreach(var candidate in Resources.LoadAll<Sprite>(entry.resource))
                if(candidate.name==entry.spriteName){sprite=candidate;break;}
            if(sprite!=null)Sprites[key]=sprite;return sprite;
        }
        public static bool TryDescription(TrickDefinition effect,UnitDefinition owner,bool spanish,out string text)
        {
            Load();text=null;var key=effect?.EffectId??effect?.Id;
            if(key==null||!Entries.TryGetValue(key,out var entry))return false;
            var template=spanish?entry.descriptionEs:entry.descriptionEn;
            if(string.IsNullOrWhiteSpace(template))return false;
            text=FormatTemplate(effect,owner,template,spanish);return true;
        }
        public static bool TryBasicDescription(string key,MonsterPouch.Gameplay.Units.UnitStats stats,bool spanish,out string text)
        {
            Load();text=null;
            if(!Entries.TryGetValue(key,out var entry))return false;
            string template=spanish?entry.descriptionEs:entry.descriptionEn;
            if(string.IsNullOrWhiteSpace(template))return false;
            text=template.Replace("{basicDamage}",stats.Attack.ToString(CultureInfo.InvariantCulture))
                .Replace("{basicRange}",stats.AttackRange.ToString(CultureInfo.InvariantCulture))
                .Replace("{basicInterval}",stats.AttackInterval.ToString("0.##",CultureInfo.InvariantCulture));
            return true;
        }
        public static string FormatTemplate(TrickDefinition effect,UnitDefinition owner,string template,bool spanish)
        {
            // Fractions and upgrade comparisons share the core's resolved formatter.
            return AbilityBalance.FormatDescription(effect,owner,template,spanish);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache(){loaded=false;Entries.Clear();Sprites.Clear();}
    }
}
