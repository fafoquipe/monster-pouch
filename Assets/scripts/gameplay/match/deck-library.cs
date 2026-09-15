using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    [Serializable]
    public sealed class SavedDeck
    {
        public string Monster;
        public string[] Whelps = new string[MatchConfig.MaxWhelpTypes];
    }

    /// <summary>Three independent local teams. Invalid saved IDs never reach a match.</summary>
    [Serializable]
    public sealed class DeckLibrary
    {
        public int Active;
        public SavedDeck[] Decks = new SavedDeck[3];
        public SavedDeck Current => Decks[Mathf.Clamp(Active,0,2)];

        public static DeckLibrary Restore(string json, MatchConfig config, string legacyMonster, IEnumerable<string> legacyWhelps)
        {
            DeckLibrary library=null;
            try { if(!string.IsNullOrEmpty(json))library=JsonUtility.FromJson<DeckLibrary>(json); }
            catch(ArgumentException) { }
            library=library??new DeckLibrary();
            library.Active=Mathf.Clamp(library.Active,0,2);
            var existing=library.Decks;
            library.Decks=new SavedDeck[3];
            string fallback=config.Get(legacyMonster)?.IsMonster==true?legacyMonster:
                config.Units.First(d=>d!=null&&d.IsMonster).Id;
            var legacy=(legacyWhelps??Array.Empty<string>()).Where(id=>config.Get(id)!=null&&!config.Get(id).IsMonster).Distinct().Take(config.SelectionLimit).ToArray();
            if(legacy.Length==0)config.TryResolveWhelpSelection(null,out legacy,out _);
            for(int n=0;n<3;n++)
            {
                var deck=existing!=null&&n<existing.Length?existing[n]:null;
                deck=deck??new SavedDeck {Monster=fallback,Whelps=legacy};
                if(config.Get(deck.Monster)?.IsMonster!=true)deck.Monster=fallback;
                var old=deck.Whelps??Array.Empty<string>();
                deck.Whelps=new string[MatchConfig.MaxWhelpTypes];
                var seen=new HashSet<string>(StringComparer.Ordinal);
                for(int i=0;i<Math.Min(old.Length,config.SelectionLimit);i++)
                    if(config.Get(old[i]) is UnitDefinition d&&!d.IsMonster&&seen.Add(d.Id))deck.Whelps[i]=d.Id;
                if(seen.Count==0)Array.Copy(legacy,deck.Whelps,Math.Min(legacy.Length,deck.Whelps.Length));
                library.Decks[n]=deck;
            }
            return library;
        }

        public bool Assign(int slot,string id,MatchConfig config)
        {
            var d=config.Get(id);
            if(slot<0){if(d==null||!d.IsMonster)return false;Current.Monster=id;return true;}
            if(slot>=config.SelectionLimit||d==null||d.IsMonster)return false;
            int duplicate=Array.IndexOf(Current.Whelps,id);
            string replaced=Current.Whelps[slot];
            Current.Whelps[slot]=id;
            if(duplicate>=0&&duplicate!=slot)Current.Whelps[duplicate]=replaced;
            return true;
        }
        public bool Remove(int slot)
        {
            if(slot<0||slot>=Current.Whelps.Length||Current.Whelps.Count(id=>!string.IsNullOrEmpty(id))<=1)return false;
            Current.Whelps[slot]=null;return true;
        }
        public string Serialize()=>JsonUtility.ToJson(this);
    }
}
