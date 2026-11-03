using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        // Captured view of the existing simulation schedule, never an advancing gameplay clock.
        readonly struct FactionFlightTiming
        {
            public readonly CombatSimulation Simulation;
            public readonly BattleUnit Source, Target;
            public readonly bool HasTarget;
            public readonly int SourceReset, TargetReset, AttackReset;
            public readonly float ReleaseAt, Duration;
            public FactionFlightTiming(CombatSimulation simulation, BattleUnit source, BattleUnit target,
                float windup, float duration)
            {
                Simulation=simulation;Source=source;Target=target;HasTarget=target!=null;
                SourceReset=source.CombatResetVersion;TargetReset=target==null?0:target.CombatResetVersion;
                AttackReset=source.AttackResetVersion;ReleaseAt=simulation.Elapsed+windup;Duration=duration;
            }
            public bool IdentitiesMatch => Source!=null&&Source.CombatResetVersion==SourceReset&&
                (!HasTarget||Target!=null&&Target.CombatResetVersion==TargetReset);
            public bool CanLaunch => IdentitiesMatch&&Source.IsAlive&&Source.isActiveAndEnabled&&
                Source.AttackResetVersion==AttackReset&&(!HasTarget||Target!=null&&Target.IsAlive);
            public bool Waiting => Simulation.Elapsed+.00001f<ReleaseAt;
            public float Elapsed => Mathf.Max(0,Simulation.Elapsed-ReleaseAt);
        }

        FactionFlightTiming CaptureFactionFlight(CombatAttackPresentationSnapshot emission)
        {
            // QueueImpact and the three released Supers supply their own immutable schedule.
            return new FactionFlightTiming(match.Simulation,emission.Source,emission.Target,
                emission.PreparationSeconds,emission.TravelSeconds);
        }
    }
}
