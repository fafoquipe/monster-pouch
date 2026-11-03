using System;
using System.Collections.Generic;
using System.Linq;

namespace MonsterPouch.Gameplay.Match
{
    public static class EronyaRoster
    {
        static readonly HashSet<string> Ids = new HashSet<string>(new[] {
            "korran","golem-tectonico","la-minera","protectora-del-mausoleo",
            "serya","legionario","dagata","canonero-del-orvek",
            "drayven","titanica","jabalinero","maestro-de-las-mareas",
            "alveyr","zephalontropo","alboguera","palista",
            "cael","guarda-veterano","arboreo","antidota"
        }, StringComparer.Ordinal);
        public static bool IsId(string id) => id != null && Ids.Contains(id);
        public static UnitDefinition[] CreateAll()
        {
            var all = EronyaGeotharDefinitions.CreateAll()
                .Concat(EronyaVolkanarDefinitions.CreateAll())
                .Concat(EronyaAquarionDefinitions.CreateAll())
                .Concat(EronyaAireolaDefinitions.CreateAll())
                .Concat(EronyaNaturalisDefinitions.CreateAll()).ToArray();
            if (all.Length != 20 || all.Any(x=>x==null||!IsId(x.Id)) || all.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=20)
                throw new InvalidOperationException("Eronya requires its 20 exact unique stable IDs.");
            foreach (var unit in all)
                if (unit.Ruleset!=CombatRuleset.Eronya || !unit.UsesDocumentedRules || unit.PassiveAbility==null ||
                    unit.BaseAbility==null || unit.Tricks==null || unit.Tricks.Length!=3 || unit.Tricks.Any(x=>x==null))
                    throw new InvalidOperationException("Missing Eronya rules, passive, active or three upgrades: "+unit.Id);
            return all;
        }
    }
}
