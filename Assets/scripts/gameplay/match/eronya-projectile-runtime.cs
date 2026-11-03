using System;
using System.Collections.Generic;
using System.Linq;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        private sealed class EronyaProjectileImpact
        {
            public ActorClock Source, Target;
            public int SourceVersion, TargetVersion, DueTick;
            public long AttackId;
            public Action Resolve;
            public Action<float,float> Sweep;
            public int ReleaseTick, LastTick;
        }
        private readonly List<EronyaProjectileImpact> eronyaProjectiles=new List<EronyaProjectileImpact>();

        private void EronyaQueueProjectile(ActorClock source,ActorClock target,string effect,Action impact)
        {
            if(!EronyaValidActor(source)||!EronyaValidActor(target)||target.Unit.IsFlying)return;
            var from=source.Unit.CurrentCell;var to=target.Unit.CurrentCell;
            int distance=Math.Max(Math.Abs(from.X-to.X),Math.Abs(from.Y-to.Y));
            float travel=GetProjectileTravelTime(distance,source.Unit.BaseStats.ProjectileSpeed);
            eronyaProjectiles.Add(new EronyaProjectileImpact {Source=source,Target=target,
                SourceVersion=source.Unit.CombatResetVersion,TargetVersion=target.Unit.CombatResetVersion,
                DueTick=tick+AbilityTiming.Ticks(travel),AttackId=eronyaAttackContext?.AttackId??++eronyaAttackSequence,Resolve=impact});
            // Launch and impact are separate facts. Area/control/damage are emitted by Resolve at arrival.
            PublishAttackPresentation(source,target.Unit,effect,true,travel);
            AbilityUsed?.Invoke(source.Unit,effect);
        }
        private void EronyaQueueWave(ActorClock source, ActorClock target, MaestroWaveFlight wave, Action<float,float> sweep)
        {
            eronyaProjectiles.Add(new EronyaProjectileImpact { Source=source, Sweep=sweep,
                SourceVersion=source.Unit.CombatResetVersion, ReleaseTick=tick, LastTick=tick,
                DueTick=tick+AbilityTiming.Ticks(wave.TravelSeconds),
                AttackId=eronyaAttackContext?.AttackId??++eronyaAttackSequence });
            PublishAttackPresentation(source,target.Unit,MaestroWaveFlight.EffectId,true,wave.TravelSeconds,wave);
            AbilityUsed?.Invoke(source.Unit,MaestroWaveFlight.EffectId);
        }
        private void ResolveEronyaProjectileImpacts()
        {
            foreach(var shot in eronyaProjectiles.Where(p=>p.Sweep!=null||p.DueTick<=tick).OrderBy(p=>p.DueTick)
                .ThenBy(p=>p.Source.Unit.UnitId,StringComparer.Ordinal).ThenBy(p=>p.AttackId).ToArray())
            {
                if(shot.Sweep!=null)
                {
                    if(shot.Source.Unit==null||shot.Source.Unit.CombatResetVersion!=shot.SourceVersion)
                    { eronyaProjectiles.Remove(shot);continue; }
                    int next=Math.Min(tick,shot.DueTick);
                    if(next>shot.LastTick)
                        EronyaWithAttack(shot.Source,shot.AttackId,()=>shot.Sweep(
                            (shot.LastTick-shot.ReleaseTick)*TickDuration,(next-shot.ReleaseTick)*TickDuration));
                    shot.LastTick=next;
                    if(tick>=shot.DueTick)eronyaProjectiles.Remove(shot);
                    continue;
                }
                eronyaProjectiles.Remove(shot);
                if(shot.Source.Unit==null||shot.Source.Unit.CombatResetVersion!=shot.SourceVersion||
                    !EronyaValidActor(shot.Target)||shot.Target.Unit.CombatResetVersion!=shot.TargetVersion||shot.Target.Unit.IsFlying)continue;
                // A released targeted projectile follows legacy semantics: no second range test;
                // source death does not recall it, but reset/end invalidates its identity.
                EronyaWithAttack(shot.Source,shot.AttackId,shot.Resolve);
            }
        }
    }
}
