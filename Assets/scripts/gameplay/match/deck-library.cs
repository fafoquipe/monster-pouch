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
        public bool IsEmpty => string.IsNullOrEmpty(Monster) && (Whelps==null || Whelps.All(string.IsNullOrEmpty));
    }

    /// <summary>Five independent local teams. Invalid saved IDs never reach a match.</summary>
    [Serializable]
    public sealed class DeckLibrary
    {
        public int Active;
        public const int DeckCount = 5;
        public SavedDeck[] Decks = new SavedDeck[DeckCount];
        public SavedDeck Current => Decks[Mathf.Clamp(Active,0,Decks.Length-1)];

        public static DeckLibrary Restore(string json, MatchConfig config, string legacyMonster, IEnumerable<string> legacyWhelps)
        {
            DeckLibrary library=null;
            try { if(!string.IsNullOrEmpty(json))library=JsonUtility.FromJson<DeckLibrary>(json); }
            catch(ArgumentException) { }
            bool newLibrary=library==null;
            library=library??new DeckLibrary();
            library.Active=Mathf.Clamp(library.Active,0,DeckCount-1);
            var existing=newLibrary?null:library.Decks;
            library.Decks=new SavedDeck[DeckCount];
            string fallback=config.Get(legacyMonster)?.IsMonster==true?legacyMonster:
                config.Units.First(d=>d!=null&&d.IsMonster).Id;
            var legacy=(legacyWhelps??Array.Empty<string>()).Where(id=>config.Get(id)!=null&&!config.Get(id).IsMonster).Distinct().Take(config.SelectionLimit).ToArray();
            if(legacy.Length==0)config.TryResolveWhelpSelection(null,out legacy,out _);
            for(int n=0;n<DeckCount;n++)
            {
                var deck=existing!=null&&n<existing.Length?existing[n]:null;
                // Preserve the three legacy defaults; newly added or missing saved slots stay empty.
                if(deck==null && (existing!=null || n>=3) || deck!=null && deck.IsEmpty)
                {library.Decks[n]=new SavedDeck();continue;}
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
            if(slot<0){if(d==null||!d.IsMonster)return false;InitializeEmptyDeck(config,id);Current.Monster=id;return true;}
            if(slot>=config.SelectionLimit||d==null||d.IsMonster)return false;
            InitializeEmptyDeck(config,null);
            int duplicate=Array.IndexOf(Current.Whelps,id);
            string replaced=Current.Whelps[slot];
            Current.Whelps[slot]=id;
            if(duplicate>=0&&duplicate!=slot)Current.Whelps[duplicate]=replaced;
            return true;
        }
        void InitializeEmptyDeck(MatchConfig config,string monster)
        {
            if(!Current.IsEmpty)return;
            // An intentional character choice initializes the blank team using an existing team.
            var template=Decks.FirstOrDefault(d=>d!=null&&!d.IsEmpty);
            Current.Monster=monster??template?.Monster??config.Units.First(d=>d!=null&&d.IsMonster).Id;
            Current.Whelps=new string[MatchConfig.MaxWhelpTypes];
            var initial=template?.Whelps;
            if(initial!=null || config.TryResolveWhelpSelection(null,out initial,out _))
                Array.Copy(initial,Current.Whelps,Math.Min(initial.Length,Current.Whelps.Length));
        }
        public bool Remove(int slot)
        {
            if(slot<0||slot>=Current.Whelps.Length||Current.Whelps.Count(id=>!string.IsNullOrEmpty(id))<=1)return false;
            Current.Whelps[slot]=null;return true;
        }
        /// <summary>
        /// Fills existing empty slots from the supplied player inventory. Null is
        /// legacy access, never inferred ownership. Catalogue entries alone do
        /// not unlock anything, and valid custom slot positions remain intact.
        /// </summary>
        public bool NormalizeAvailableWhelps(MatchConfig config,IEnumerable<string> availableWhelps)
        {
            if(availableWhelps==null)return false;
            var available=AvailableWhelps(config,availableWhelps);
            int required=Math.Min(config.SelectionLimit,available.Length);
            var allowed=new HashSet<string>(available,StringComparer.Ordinal);
            bool changed=false;
            foreach(var deck in Decks)
            {
                if(deck.IsEmpty)continue;
                var old=deck.Whelps??Array.Empty<string>();
                var normalized=new string[MatchConfig.MaxWhelpTypes];
                var seen=new HashSet<string>(StringComparer.Ordinal);
                for(int slot=0;slot<Math.Min(old.Length,config.SelectionLimit);slot++)
                    if(seen.Count<required&&!string.IsNullOrEmpty(old[slot])&&allowed.Contains(old[slot])&&seen.Add(old[slot]))
                        normalized[slot]=old[slot];
                foreach(string id in available)
                {
                    if(seen.Count>=required)break;
                    if(!seen.Add(id))continue;
                    int slot=Array.FindIndex(normalized,0,config.SelectionLimit,string.IsNullOrEmpty);
                    normalized[slot]=id;
                }
                if(old.SequenceEqual(normalized,StringComparer.Ordinal))continue;
                deck.Whelps=normalized;changed=true;
            }
            return changed;
        }
        public bool NormalizeAvailableMonsters(MatchConfig config,IEnumerable<string> availableMonsters)
        {
            if(availableMonsters==null)return false;
            var available=AvailableMonsters(config,availableMonsters);
            // No owned Monster means a blocked selection, never a fabricated
            // unlock or a null replacement for the user's saved choice.
            if(available.Length==0)return false;
            bool changed=false;
            foreach(var deck in Decks)
                if(!deck.IsEmpty&&!available.Contains(deck.Monster,StringComparer.Ordinal))
                {deck.Monster=available[0];changed=true;}
            return changed;
        }
        public bool CanPlayCurrent(MatchConfig config,IEnumerable<string> availableWhelps,out string reason,IEnumerable<string> availableMonsters=null)
            =>CanPlay(Current,config,availableWhelps,out reason,availableMonsters);
        public bool CanPlayAll(MatchConfig config,IEnumerable<string> availableWhelps,out string reason,IEnumerable<string> availableMonsters=null)
        {
            var available=availableWhelps?.ToArray();
            var monsters=availableMonsters?.ToArray();
            foreach(var deck in Decks)if(!deck.IsEmpty&&!CanPlay(deck,config,available,out reason,monsters))return false;
            reason="";return true;
        }
        public bool Assign(int slot,string id,MatchConfig config,IEnumerable<string> availableWhelps,IEnumerable<string> availableMonsters=null)
        {
            if(slot<0&&availableMonsters!=null&&!AvailableMonsters(config,availableMonsters).Contains(id,StringComparer.Ordinal))return false;
            if(availableWhelps==null||slot<0)return Assign(slot,id,config);
            if(!AvailableWhelps(config,availableWhelps).Contains(id,StringComparer.Ordinal))return false;
            return Assign(slot,id,config);
        }
        public bool Remove(int slot,MatchConfig config,IEnumerable<string> availableWhelps)
        {
            if(availableWhelps==null)return Remove(slot);
            int required=Math.Min(config.SelectionLimit,AvailableWhelps(config,availableWhelps).Length);
            if(Current.Whelps.Count(id=>!string.IsNullOrEmpty(id))<=required)return false;
            return Remove(slot);
        }
        public static string[] AvailableWhelps(MatchConfig config,IEnumerable<string> availableWhelps)
        {
            if(config==null)throw new ArgumentNullException(nameof(config));
            return (availableWhelps??Array.Empty<string>()).Where(id=>!string.IsNullOrWhiteSpace(id)&&config.Get(id)?.IsMonster==false)
                .Distinct(StringComparer.Ordinal).ToArray();
        }
        public static string[] AvailableMonsters(MatchConfig config,IEnumerable<string> availableMonsters)
        {
            if(config==null)throw new ArgumentNullException(nameof(config));
            return (availableMonsters??Array.Empty<string>()).Where(id=>!string.IsNullOrWhiteSpace(id)&&config.Get(id)?.IsMonster==true)
                .Distinct(StringComparer.Ordinal).ToArray();
        }
        static bool CanPlay(SavedDeck deck,MatchConfig config,IEnumerable<string> availableWhelps,out string reason,IEnumerable<string> availableMonsters)
        {
            reason="";
            if(deck==null||config.Get(deck.Monster)?.IsMonster!=true){reason="Elige un Monster válido.";return false;}
            if(availableMonsters!=null&&!AvailableMonsters(config,availableMonsters).Contains(deck.Monster,StringComparer.Ordinal))
            {reason="Elige un Monster desbloqueado en tu colección.";return false;}
            var selected=(deck.Whelps??Array.Empty<string>()).Where(id=>!string.IsNullOrEmpty(id)).ToArray();
            if(selected.Distinct(StringComparer.Ordinal).Count()!=selected.Length)
            {reason="El Brief no puede repetir Whelps.";return false;}
            if(!config.TryResolveWhelpSelection(selected,out _,out reason))return false;
            if(availableWhelps==null)return true;
            var available=AvailableWhelps(config,availableWhelps);
            int required=Math.Min(config.SelectionLimit,available.Length);
            if(required==0||selected.Length!=required||selected.Any(id=>!available.Contains(id,StringComparer.Ordinal)))
            {reason="Completa el Brief con tus Whelps disponibles (hasta siete).";return false;}
            return true;
        }
        public string Serialize()=>JsonUtility.ToJson(this);
    }
}
