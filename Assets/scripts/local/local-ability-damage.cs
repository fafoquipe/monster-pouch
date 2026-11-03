using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using System.Globalization;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        public static string AbilityDetailText(OwnedUnit owned,TrickDefinition ability)
            =>GameText.AbilityDescription(ability,owned.Definition);
        public static string BasicAttackDetailText(OwnedUnit owned)
        {
            if(owned==null)return string.Empty;
            var stats=UnitStats.FromDefinition(owned.Definition,owned,false);
            bool es=GameText.Spanish;
            if(AbilityUiCatalog.TryBasicDescription(owned.Definition.Id+"/basic",stats,es,out string basic))return basic;
            string range=stats.HasEffect("bugui-unlimited-range")?(es?"todo el tablero":"whole board"):
                stats.AttackRange+(es?" casillas":" tiles");
            return (es?"Daño":"Damage")+": "+stats.Attack+".\n"+
                (es?"Alcance: ":"Range: ")+range+".\n"+
                (es?"Intervalo: ":"Interval: ")+stats.AttackInterval.ToString("0.##",CultureInfo.InvariantCulture)+" s.";
        }
    }
}
