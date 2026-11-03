using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterPouch.Local
{
    [Serializable] public sealed class LeaderBadgeEntry { public string faction,normal,upgraded; }
    [Serializable] public sealed class LeaderFactionEntry { public string characterId,faction; }
    [Serializable] public sealed class LeaderBadgeManifest { public LeaderBadgeEntry[] badges; public LeaderFactionEntry[] characters; }
    public static class LeaderBadgeArtwork
    {
        static readonly Dictionary<string,LeaderBadgeEntry> Badges=new Dictionary<string,LeaderBadgeEntry>(StringComparer.Ordinal);
        static readonly Dictionary<string,string> Factions=new Dictionary<string,string>(StringComparer.Ordinal);
        static readonly Dictionary<string,Sprite> Sprites=new Dictionary<string,Sprite>(StringComparer.Ordinal);
        static bool loaded;
        static void Load()
        {
            if(loaded)return;loaded=true;
            var source=Resources.Load<TextAsset>("MonsterPouch/UI/LeaderBadges/catalog");
            var manifest=source!=null?JsonUtility.FromJson<LeaderBadgeManifest>(source.text):null;
            if(manifest?.badges!=null)foreach(var entry in manifest.badges)
                if(entry!=null&&!string.IsNullOrEmpty(entry.faction))Badges[entry.faction]=entry;
            if(manifest?.characters!=null)foreach(var entry in manifest.characters)
                if(entry!=null&&!string.IsNullOrEmpty(entry.characterId)&&!string.IsNullOrEmpty(entry.faction))Factions[entry.characterId]=entry.faction;
        }
        public static string FactionFor(string characterId)
        {Load();return characterId!=null&&Factions.TryGetValue(characterId,out var faction)?faction:"monsters";}
        static Sprite SpriteAt(string path)
        {
            if(string.IsNullOrEmpty(path))return null;
            var referenced=UiSpriteReferences.Get(path);if(referenced!=null)return referenced;
            if(Sprites.TryGetValue(path,out var sprite)&&sprite!=null)return sprite;
            sprite=Resources.Load<Sprite>(path);if(sprite!=null)Sprites[path]=sprite;return sprite;
        }
        static Sprite State(string faction,bool upgraded)
        {
            if(!Badges.TryGetValue(faction,out var entry))return null;
            return (upgraded?SpriteAt(entry.upgraded):null)??SpriteAt(entry.normal);
        }
        // Missing upgraded art falls back to that faction's normal state; unknown/missing
        // factions fall back to Monsters. If the full catalog is missing, the HUD retains its legacy mark.
        public static Sprite Get(string characterId,bool upgraded)
        {Load();return State(FactionFor(characterId),upgraded)??State("monsters",upgraded);}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache(){loaded=false;Badges.Clear();Factions.Clear();Sprites.Clear();}
    }
}
