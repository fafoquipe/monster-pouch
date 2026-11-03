using MonsterPouch.Gameplay.Units;

namespace MonsterPouch.Gameplay.Match
{
    public sealed class CombatAttackPresentationSnapshot
    {
        public BattleUnit Source { get; }
        public BattleUnit Target { get; }
        public string EffectId { get; }
        public bool IsAbility { get; }
        public float PreparationSeconds { get; }
        public float TravelSeconds { get; }
        public MaestroWaveFlight Wave { get; }
        public long EmissionId { get; }
        public int SourceResetVersion { get; }
        public int TargetResetVersion { get; }
        internal CombatAttackPresentationSnapshot(BattleUnit source,BattleUnit target,string effect,bool ability,long emission,float travelSeconds=-1,MaestroWaveFlight wave=null)
        {Source=source;Target=target;EffectId=effect;IsAbility=ability;EmissionId=emission;Wave=wave;
            SourceResetVersion=source.CombatResetVersion;TargetResetVersion=target.CombatResetVersion;
            PreparationSeconds=ability?0:CombatSimulation.GetAttackWindup(source);
            TravelSeconds=travelSeconds>=0?travelSeconds:CombatSimulation.GetProjectileTravelTime(source,target);}
    }
    public sealed partial class CombatSimulation
    {
        private long presentationEmissionSequence;
        // Present only during the synchronous Attacked callback. Consumers capture it
        // into their flight object before yielding; it is never last-action state.
        public CombatAttackPresentationSnapshot CurrentAttackPresentation { get; private set; }
        private void PublishAttackPresentation(ActorClock source,BattleUnit target,string effect,bool ability,float travelSeconds=-1,MaestroWaveFlight wave=null)
        {
            var previous=CurrentAttackPresentation;
            CurrentAttackPresentation=new CombatAttackPresentationSnapshot(source.Unit,target,
                ability?AbilityTiming.Effect(effect):effect,ability,++presentationEmissionSequence,travelSeconds,wave);
            try{Attacked?.Invoke(source.Unit,target,source.Unit.BaseStats.AttackRange>1);}
            finally{CurrentAttackPresentation=previous;}
        }
    }
}
