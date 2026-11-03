using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public enum CombatDisplacementKind { OpeningJump, Tunnel, ForcedPush, Teleport }
    public readonly struct CombatDisplacementSnapshot
    {
        public readonly BattleUnit Unit;
        public readonly BoardCell From, To;
        public readonly CombatDisplacementKind Kind;
        public readonly long EventId;
        public readonly int UnitResetVersion, StartTick, EndTick;
        public readonly bool IsAuthoritativeInstant;
        public CombatDisplacementSnapshot(BattleUnit unit, BoardCell from, BoardCell to,
            CombatDisplacementKind kind, long eventId, int startTick, int endTick, bool instant)
        {
            Unit=unit;From=from;To=to;Kind=kind;EventId=eventId;
            UnitResetVersion=unit==null?0:unit.CombatResetVersion;
            StartTick=startTick;EndTick=endTick;IsAuthoritativeInstant=instant;
        }
    }
    public readonly struct CombatStatusSnapshot
    {
        public readonly string Id, EffectId, SourceUnitId;
        public readonly long InstanceId;
        public readonly long ApplicationRevision;
        public readonly int UnitResetVersion, AppliedTick, ExpiresTick, NextPulseTick;
        public readonly float Magnitude, RemainingSeconds;
        public readonly bool IsPermanent;
        public readonly CombatStatusTimingKind TimingKind;
        public CombatStatusSnapshot(string id,string effectId,string sourceUnitId,long instanceId,
            int unitResetVersion,int appliedTick,int expiresTick,int nextPulseTick,float magnitude,int currentTick,
            long applicationRevision=0,CombatStatusTimingKind timingKind=CombatStatusTimingKind.Timed)
        {
            Id=id;EffectId=effectId;SourceUnitId=sourceUnitId;InstanceId=instanceId;
            UnitResetVersion=unitResetVersion;AppliedTick=appliedTick;ExpiresTick=expiresTick;
            ApplicationRevision=applicationRevision;TimingKind=timingKind;
            NextPulseTick=nextPulseTick;Magnitude=magnitude;IsPermanent=expiresTick==int.MaxValue||timingKind!=CombatStatusTimingKind.Timed;
            RemainingSeconds=IsPermanent?-1f:System.Math.Max(0,expiresTick-currentTick)*.1f;
        }
    }
    public readonly struct CombatStatusFrame
    {
        public readonly int UnitResetVersion, SimulationTick;
        public readonly long Revision;
        public readonly bool IsActive;
        public readonly System.Collections.Generic.IReadOnlyList<CombatStatusSnapshot> Statuses;
        // Reuse only an already immutable snapshot owned by this assembly. Public
        // callers retain the defensive-copy constructor below.
        internal static CombatStatusFrame FromImmutable(int resetVersion,int tick,long revision,bool active,
            System.Collections.Generic.IReadOnlyList<CombatStatusSnapshot> statuses)
            => new CombatStatusFrame(resetVersion,tick,revision,active,statuses);
        private CombatStatusFrame(int resetVersion,int tick,long revision,bool active,
            System.Collections.Generic.IReadOnlyList<CombatStatusSnapshot> statuses)
        {UnitResetVersion=resetVersion;SimulationTick=tick;Revision=revision;IsActive=active;Statuses=statuses;}
        public CombatStatusFrame(int resetVersion,int tick,long revision,bool active,CombatStatusSnapshot[] statuses)
        {
            UnitResetVersion=resetVersion;SimulationTick=tick;Revision=revision;IsActive=active;
            Statuses=System.Array.AsReadOnly(statuses==null?System.Array.Empty<CombatStatusSnapshot>():
                (CombatStatusSnapshot[])statuses.Clone());
        }
    }
}
