using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        readonly Dictionary<BattleUnit,decimal> eronyaInitialHealth=new Dictionary<BattleUnit,decimal>();
        readonly Dictionary<BoardSide,decimal> eronyaHealthDamage=new Dictionary<BoardSide,decimal>();
        public bool UsesEronyaCombat=>eronyaCombat;
        public bool EronyaRoundCompared {get;private set;}
        public bool EronyaRoundTimedOut {get;private set;}
        public EronyaRoundCriterion EronyaRoundCriterion {get;private set;}
        public BoardSide EronyaTiebreakSide {get;internal set;}
        public EronyaRoundScore EronyaBlueScore {get;private set;}
        public EronyaRoundScore EronyaRedScore {get;private set;}
        private void EronyaCaptureRound()
        {
            eronyaInitialHealth.Clear();eronyaHealthDamage.Clear();
            EronyaRoundCompared=false;EronyaRoundTimedOut=false;
            EronyaBlueScore=EronyaRedScore=default;EronyaRoundCriterion=EronyaRoundCriterion.Token;
            foreach(var actor in actors)if(!actor.Unit.IsCombatSummon)eronyaInitialHealth[actor.Unit]=actor.Unit.BaseStats.MaxHealth;
            // Preserve an explicitly shortened/extended isolated fixture budget.
            if(TimeoutTickLimit==TimeoutTicks)TimeoutTickLimit=EronyaRoundRules.TimeoutTicks;
        }
        private EronyaRoundScore EronyaScore(BoardSide side)
        {
            var own=eronyaInitialHealth.Where(p=>p.Key.Side==side).ToArray();
            var heroes=own.Where(p=>p.Key.Category==UnitCategory.Monster).ToArray();
            eronyaHealthDamage.TryGetValue(side,out var damage);
            return new EronyaRoundScore(own.Sum(p=>p.Key.IsAlive&&!p.Key.IsReviving?p.Key.PreciseHealth:0m),own.Sum(p=>p.Value),
                heroes.Sum(p=>p.Key.IsAlive&&!p.Key.IsReviving?p.Key.PreciseHealth:0m),heroes.Sum(p=>p.Value),
                own.Count(p=>p.Key.IsAlive&&!p.Key.IsReviving),own.Length,damage,eronyaInitialHealth.Where(p=>p.Key.Side!=side).Sum(p=>p.Value));
        }
        private void EronyaRecordDamage(EronyaHit hit)
        {
            if(!eronyaInitialHealth.ContainsKey(hit.Target.Unit)||hit.PreciseHealthDamage<=0m)return;
            var side=hit.Source.Unit.Side;eronyaHealthDamage.TryGetValue(side,out var before);eronyaHealthDamage[side]=before+hit.PreciseHealthDamage;
        }
        private void EronyaResolveRound(string reason,bool timeout=false)
        {
            EronyaBlueScore=EronyaScore(BoardSide.Blue);EronyaRedScore=EronyaScore(BoardSide.Red);
            int compare=EronyaRoundRules.Compare(EronyaBlueScore,EronyaRedScore,out var criterion);
            EronyaRoundCompared=true;EronyaRoundTimedOut=timeout;EronyaRoundCriterion=criterion;
            Finish(compare>0?BoardSide.Blue:compare<0?BoardSide.Red:EronyaTiebreakSide,reason);
        }
        private List<BoardMovementResult> EronyaReserveMoves(List<BoardMovementIntent> intents)
        {
            var result=new BoardMovementResult[intents.Count];var next=new BoardCell[intents.Count];
            for(int i=0;i<intents.Count;i++)
            {
                var intent=intents[i];var path=new List<BoardCell>();
                bool found=BoardPathfinder.TryFindPath(board,intent.Unit,intent.Unit.CurrentCell,intent.TargetCell,path);
                if(found&&path.Count>0)next[i]=path[0];
                result[i]=new BoardMovementResult(intent.Unit,intent.TargetCell,next[i],found?BoardMovementStatus.ReservationRejected:BoardMovementStatus.NoPath);
            }
            var preferred=(BoardSide)EronyaRoundRules.MovementPrioritySide((int)EronyaTiebreakSide,tick);
            foreach(var group in Enumerable.Range(0,intents.Count).Where(i=>next[i]!=null).GroupBy(i=>next[i]))
            {
                int winner=group.OrderBy(i=>EronyaDistance(intents[i].Unit.CurrentCell,next[i]))
                    .ThenBy(i=>intents[i].Unit.Side==preferred?0:1)
                    .ThenBy(i=>(intents[i].Unit as BattleUnit)?.CombatTeamSlot??int.MaxValue)
                    .ThenBy(i=>intents[i].Unit.UnitId,StringComparer.Ordinal).First();
                var intent=intents[winner];
                if(board.TryReserveCell(intent.Unit,next[winner]))result[winner]=new BoardMovementResult(intent.Unit,intent.TargetCell,next[winner],BoardMovementStatus.Reserved);
            }
            return result.ToList();
        }
        private BoardCell EronyaNextStep(ActorClock actor,BoardCell destination)
        {
            // Reverse BFS chooses an owner-relative first step among shortest routes.
            var distances=new Dictionary<BoardCell,int>{{destination,0}};var queue=new Queue<BoardCell>();queue.Enqueue(destination);
            var origin=actor.Unit.CurrentCell;
            while(queue.Count>0)
            {
                var cell=queue.Dequeue();
                foreach(var neighbor in new[]{board.GetCell(cell.X+1,cell.Y),board.GetCell(cell.X-1,cell.Y),board.GetCell(cell.X,cell.Y+1),board.GetCell(cell.X,cell.Y-1)})
                    if(neighbor!=null&&!distances.ContainsKey(neighbor)&&(neighbor==origin||EronyaIsFree(neighbor)))
                    {distances[neighbor]=distances[cell]+1;queue.Enqueue(neighbor);}
            }
            bool rotated=actor.Unit.Side==BoardSide.Red;
            return distances.Keys.Where(c=>EronyaIsFree(c)&&EronyaDistance(c,origin)==1).OrderBy(c=>distances[c])
                .ThenBy(c=>EronyaSelection.OwnerRow(c.Y,BoardManager.Height,rotated)).ThenBy(c=>EronyaSelection.OwnerColumn(c.X,BoardManager.Width,rotated)).FirstOrDefault();
        }
    }
}
