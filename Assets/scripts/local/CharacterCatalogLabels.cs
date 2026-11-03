using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Local
{
    // Presentation only. IsMonster remains the existing hero/deck-slot flag;
    // neither this naming nor browsing changes combat rules or saved identities.
    public static class CharacterCatalogLabels
    {
        public static string Group(bool hero,bool spanish)
            =>hero?(spanish?"Héroes":"Heroes"):(spanish?"Piezas":"Pieces");
        public static string Types(bool hero,bool spanish)
            =>hero?(spanish?"Líderes · Monsters":"Leaders · Monsters"):"Erony · Whelps";
        public static string Kind(string id,bool hero,bool spanish)
            =>EronyaRoster.IsId(id)?(hero?(spanish?"Líder de Eronya":"Eronya leader"):"Erony"):(hero?"Monster":"Whelp");
        public static string Select(bool hero,bool spanish)
            =>spanish?(hero?"ELIGE HÉROE":"ELIGE PIEZA"):(hero?"SELECT HERO":"SELECT PIECE");
    }
}
