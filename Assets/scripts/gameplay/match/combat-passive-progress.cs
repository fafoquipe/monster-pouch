using System.Collections.Generic;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    /// <summary>Completed basics in the current cycle. The final attack is the proc.</summary>
    public readonly struct PassiveProgress
    {
        public readonly string Id;
        public readonly int Completed, Every;
        public int AttacksUntilTrigger => Every - Completed;
        public bool Ready => AttacksUntilTrigger == 1;
        public PassiveProgress(string id, int attacks, int every)
        { Id = id; Every = Mathf.Max(1, every); Completed = Mathf.Max(0, attacks) % Every; }
    }

    public sealed partial class CombatSimulation
    {
        // The same counter and balance parameters as QueueBasicAttack. A UI refresh,
        // projectile bounce or energy Super never advances the displayed progress.
        public void CollectPassiveProgress(BattleUnit unit, List<PassiveProgress> output)
        {
            output.Clear();
            if (!begun || Finished || unit == null || !unit.IsAlive || unit.IsReviving) return;
            var actor = Clock(unit);
            if (actor == null) return;
            if(EronyaOwns(actor)){EronyaCollectPassiveProgress(actor,output);return;}
            if (!unit.BaseStats.UsesDocumentedRules)
            {
                if (unit.BaseStats.BonusEveryHits > 0)
                    output.Add(new PassiveProgress("bonus-hit", actor.Hits, unit.BaseStats.BonusEveryHits));
                if (unit.BaseStats.LethalEveryHits > 0)
                    output.Add(new PassiveProgress("lethal-hit", actor.Hits, unit.BaseStats.LethalEveryHits));
                return;
            }
            switch (Id(actor))
            {
                case "trimol": output.Add(new PassiveProgress("trimol-boulder", actor.Attacks, N(actor, "trimol-boulder", "every", 4))); break;
                case "tsu": output.Add(new PassiveProgress("tsu-root", actor.Attacks, N(actor, "tsu-root", "every", 4))); break;
                case "atong": output.Add(new PassiveProgress("atong-critical", actor.Attacks,
                    Has(actor, "atong-always-critical") ? 1 : N(actor, "atong-critical", "every", 2))); break;
                case "popow":
                    if (Has(actor, "popow-sweep")) output.Add(new PassiveProgress("popow-sweep", actor.Attacks, N(actor, "popow-sweep", "every", 3)));
                    break;
            }
        }
    }
}
