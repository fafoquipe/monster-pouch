using System;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // Read-only context of the existing callback. Never queues or applies combat work.
    public sealed class CombatImpactPresentationSnapshot
    {
        public long Sequence { get; }
        public BattleUnit Source { get; }
        public BattleUnit Target { get; }
        public int SourceResetVersion { get; }
        public int TargetResetVersion { get; }
        public string EffectId { get; }
        public bool IsAbility { get; }
        public bool IsBasic { get; }
        public bool IsPeriodic { get; }
        public bool Applied { get; }
        public Vector3 SourcePosition { get; }
        public Vector3 Position { get; }
        public BoardCell Cell { get; }

        internal CombatImpactPresentationSnapshot(long sequence, BattleUnit source, BattleUnit target,
            string effectId, bool ability, bool periodic, bool applied, bool basic)
        {
            Sequence=sequence; Source=source; Target=target; EffectId=effectId;
            IsAbility=ability; IsPeriodic=periodic; Applied=applied; IsBasic=basic;
            SourceResetVersion=source==null?0:source.CombatResetVersion;
            TargetResetVersion=target==null?0:target.CombatResetVersion;
            SourcePosition=source==null?Vector3.zero:source.transform.position;
            Position=target==null?Vector3.zero:target.transform.position;
            Cell=target==null?null:target.CurrentCell;
        }
    }

    public sealed partial class CombatSimulation
    {
        long impactPresentationSequence;
        // Only valid inside settled module/Impacted callbacks; nesting restores the outer context.
        public CombatImpactPresentationSnapshot CurrentImpactPresentation { get; private set; }
        CombatImpactPresentationSnapshot CaptureImpactPresentation(BattleUnit source, BattleUnit target,
            string effectId, bool ability, bool periodic, bool applied, bool basic=true)
            => new CombatImpactPresentationSnapshot(++impactPresentationSequence,source,target,effectId,ability,periodic,applied,basic);

        void WithImpactPresentation(CombatImpactPresentationSnapshot snapshot, Action notify)
        {
            var previous=CurrentImpactPresentation; CurrentImpactPresentation=snapshot;
            try { notify(); }
            finally { CurrentImpactPresentation=previous; }
        }

        void PublishImpactPresentation(BattleUnit source, BattleUnit target, int amount,
            string effectId, bool ability=false, bool periodic=false)
        {
            var snapshot=CaptureImpactPresentation(source,target,effectId,ability,periodic,amount>0,!ability&&!periodic);
            WithImpactPresentation(snapshot,()=>Impacted?.Invoke(source,target,amount));
        }
    }
}
