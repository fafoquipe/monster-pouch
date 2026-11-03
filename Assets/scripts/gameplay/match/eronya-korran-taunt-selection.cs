using System.Collections.Generic;
using System.Linq;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        // Human-confirmed: adjacent enemies which can reach Korran; no diagonal/global radius.
        private List<ActorClock> EronyaKorranTauntTargets(ActorClock actor)
        {
            if(actor?.Unit==null||!actor.Unit.IsAlive||actor.Unit.CurrentCell==null)return new List<ActorClock>();
            var origin=actor.Unit.CurrentCell;
            return EronyaEnemies(actor,1).Where(enemy=>enemy.Unit.CurrentCell!=null&&
                EronyaMechanics.KorranTauntCandidate(origin.X,origin.Y,enemy.Unit.CurrentCell.X,
                    enemy.Unit.CurrentCell.Y,EronyaCanReach(enemy,actor))).ToList();
        }
    }
}
