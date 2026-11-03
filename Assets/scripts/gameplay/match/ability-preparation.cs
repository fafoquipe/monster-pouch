using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        sealed class FlyingCones { public ActorClock Source; public BoardCell[] Cells; public int Due; }
        sealed class PreparedBoulder { public ActorClock Source; public BattleUnit Target; public int Due,Reset; public string Timing; }
        readonly List<FlyingCones> flyingCones=new List<FlyingCones>();
        readonly List<PreparedBoulder> preparedBoulders=new List<PreparedBoulder>();
        public event Action<BattleUnit,BoardCell,BoardCell[],float> ConesLaunched;

        void ReserveAbilityMotion(ActorClock actor,string effect)
        {
            actor.AbilityMotionUntil=tick+AbilityTiming.Ticks(AbilityTiming.Duration(actor.Unit.BaseStats,effect,0));
            if(actor.EnergySuperActive)actor.EnergySuperUntil=Mathf.Max(actor.EnergySuperUntil,actor.AbilityMotionUntil);
            actor.NextAttack=Mathf.Max(actor.NextAttack,actor.AbilityMotionUntil);
            actor.NextMove=Mathf.Max(actor.NextMove,actor.AbilityMotionUntil);
        }
        void CancelAbilityPreparation(ActorClock actor)
        {
            if(actor.EnergyCastDue>0 && actor.Unit!=null)actor.Unit.IsEnergyLocked=false;
            actor.EnergyCastDue=0;actor.EnergyCastTarget=null;
        }
        void PrepareOpeningFlight(ActorClock actor)
        {
            int delay=AbilityTiming.Ticks(AbilityTiming.Cast(actor.Unit.BaseStats,"aky-opening-flight"));
            if(delay==0){BeginAkyFlight(actor);return;}
            actor.OpeningFlightDue=tick+delay;
            ReserveAbilityMotion(actor,"aky-opening-flight");
            AbilityUsed?.Invoke(actor.Unit,"aky-opening-flight");
        }
        bool PreparingBoulder(ActorClock actor)=>actor.OpeningFlightDue>0 || preparedBoulders.Any(b=>b.Source==actor);
        void ResolveAbilityPreparations()
        {
            for(int actorIndex=0,actorCount=actors.Count;actorIndex<actorCount;actorIndex++)
            {
                var actor=actors[actorIndex];
                var unit=actor.Unit;
                if(actor.OpeningFlightDue>0 && tick>=actor.OpeningFlightDue)
                {
                    actor.OpeningFlightDue=0;
                    if(unit!=null && unit.IsAlive && actor.StunUntil<=tick)BeginAkyFlight(actor);
                }
                if(actor.EnergyCastDue==0)continue;
                if(unit==null||!unit.IsAlive||!unit.isActiveAndEnabled||unit.IsFlying||unit.IsBeingPulled||actor.StunUntil>tick||actor.SleepStarted)
                {CancelAbilityPreparation(actor);continue;}
                if(tick<actor.EnergyCastDue)continue;
                var target=actor.EnergyCastTarget;
                bool valid=Id(actor)!="sepora" || target!=null && target.CombatResetVersion==actor.EnergyCastTargetVersion && CanReach(actor,target);
                CancelAbilityPreparation(actor);
                if(!valid){InterruptAttack(actor);continue;}
                // The charge consumed this energy at its start. Restore it only inside
                // the synchronous release transaction; a failed summon can retry later.
                unit.AddEnergy(unit.BaseStats.EnergyMax);
                if(!TryEnergyAbility(actor,target,true))InterruptAttack(actor);
            }
            foreach(var cast in preparedBoulders.ToArray())
            {
                var unit=cast.Source.Unit;
                if(unit==null||!unit.IsAlive||unit.AttackResetVersion!=cast.Reset||cast.Source.StunUntil>tick)
                {preparedBoulders.Remove(cast);continue;}
                if(tick<cast.Due)continue;
                preparedBoulders.Remove(cast);
                if(cast.Target==null || CanReach(cast.Source,cast.Target))QueueBoulder(cast.Source,cast.Target,cast.Timing,true);
            }
        }
        void ResolveFlyingCones()
        {
            foreach(var flight in flyingCones.ToArray())
            {
                if(tick<flight.Due)continue;
                flyingCones.Remove(flight);LandCones(flight.Source,flight.Cells);
            }
        }
        void LandCones(ActorClock source,BoardCell[] cells)
        {
            foreach(var cell in cells)
            {
                tiles.RemoveAll(t=>t.Cell==cell&&t.Kind=="cone");
                var cone=new TileEffect{Source=source,Cell=cell,Kind="cone"};
                if(!TryConsumeCone(cone))tiles.Add(cone);
            }
        }
    }
}
