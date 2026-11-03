using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class CombatSimulation
    {
        // Main-thread synchronous projection; no callbacks or borrowed output escape.
        readonly List<CombatStatusSnapshot> statusPresentationScratch=new List<CombatStatusSnapshot>(16);

        static string PeriodicPresentationId(EronyaPeriodicKind kind)
        {
            switch(kind)
            {
                case EronyaPeriodicKind.Burn:return "burn";
                case EronyaPeriodicKind.Poison:return "poison";
                case EronyaPeriodicKind.Regeneration:return "regeneration";
                default:return kind.ToString().ToLowerInvariant();
            }
        }
    }

    // Exact structural comparison replaces per-read string signatures. A simulation
    // tick is not an invalidation key: every call compares freshly projected values.
    internal sealed class StatusPresentationCache
    {
        static readonly System.Collections.ObjectModel.ReadOnlyCollection<CombatStatusSnapshot> NoStatuses=
            Array.AsReadOnly(Array.Empty<CombatStatusSnapshot>());
        CombatStatusFrame previous;
        bool initialized;

        internal static CombatStatusFrame Empty(int reset,int tick,bool active)
            => CombatStatusFrame.FromImmutable(reset,tick,0,active,NoStatuses);

        internal CombatStatusFrame Capture(int reset,int tick,ref long revision,bool active,List<CombatStatusSnapshot> values)
        {
            bool signatureSame=initialized&&previous.UnitResetVersion==reset&&previous.IsActive==active&&previous.Statuses.Count==values.Count;
            bool contentSame=signatureSame;
            if(signatureSame)
                for(int i=0;i<values.Count;i++)
                {
                    var a=previous.Statuses[i];var b=values[i];
                    // Match the original presentation-signature fields exactly.
                    if(a.Id!=b.Id||a.InstanceId!=b.InstanceId||a.ApplicationRevision!=b.ApplicationRevision||
                        a.AppliedTick!=b.AppliedTick||a.ExpiresTick!=b.ExpiresTick||!a.Magnitude.Equals(b.Magnitude)||a.NextPulseTick!=b.NextPulseTick)
                        signatureSame=false;
                    if(a.Id!=b.Id||a.EffectId!=b.EffectId||a.SourceUnitId!=b.SourceUnitId||a.InstanceId!=b.InstanceId||
                        a.ApplicationRevision!=b.ApplicationRevision||a.UnitResetVersion!=b.UnitResetVersion||
                        a.AppliedTick!=b.AppliedTick||a.ExpiresTick!=b.ExpiresTick||a.NextPulseTick!=b.NextPulseTick||
                        !a.Magnitude.Equals(b.Magnitude)||!a.RemainingSeconds.Equals(b.RemainingSeconds)||
                        a.IsPermanent!=b.IsPermanent||a.TimingKind!=b.TimingKind)contentSame=false;
                }
            if(!signatureSame)revision++;
            if(!contentSame)
                previous=new CombatStatusFrame(reset,tick,revision,active,values.ToArray());
            else if(previous.SimulationTick!=tick||previous.Revision!=revision)
                previous=CombatStatusFrame.FromImmutable(reset,tick,revision,active,previous.Statuses);
            initialized=true;
            return previous;
        }

        // OrderBy was stable. Insertion sort keeps equal IDs (e.g. independent
        // shield layers) in source order, with no iterator/comparer allocation.
        internal static void StableSort(List<CombatStatusSnapshot> values)
        {
            for(int i=1;i<values.Count;i++)
            {
                var value=values[i];int j=i-1;
                while(j>=0&&string.CompareOrdinal(values[j].Id,value.Id)>0)
                {values[j+1]=values[j];j--;}
                values[j+1]=value;
            }
        }
    }
}
