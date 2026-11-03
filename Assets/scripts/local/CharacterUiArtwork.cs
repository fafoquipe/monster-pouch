using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Characters;
using UnityEngine;

namespace MonsterPouch.Local
{
    // UI-only access. MatchConfig's runtime clone deliberately drops Characters.
    // These stable per-character resources preserve Icon, Card and Portrait as separate roles.
    public static class CharacterUiArtwork
    {
        static readonly Dictionary<string,CharacterDefinition> Characters=new Dictionary<string,CharacterDefinition>(StringComparer.Ordinal);
        public static CharacterDefinition GetCharacter(string id)
        {
            if(string.IsNullOrEmpty(id))return null;
            if(Characters.TryGetValue(id,out var character)&&character!=null)return character;
            character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+id+"/Character");
            if(character!=null&&character.Id==id)Characters[id]=character;
            else character=null;
            return character;
        }
        public static Sprite Icon(string id)=>GetCharacter(id)?.Icon??UiSpriteReferences.Get("character/"+id);
        public static Sprite Card(string id)=>GetCharacter(id)?.Card??UiSpriteReferences.Get("card/"+id);
        public static Sprite Portrait(string id)=>GetCharacter(id)?.Portrait??UiSpriteReferences.Get("portrait/"+id);
        // Transitional/summon fallback is explicit; delivery validation requires every roster Icon.
        public static Sprite Thumbnail(string id,Sprite fallback=null)=>Icon(id)??Card(id)??Portrait(id)??fallback;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()=>Characters.Clear();
    }
}
