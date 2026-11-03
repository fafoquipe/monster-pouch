using System;

namespace MonsterPouch.Gameplay.Match
{
    public enum TrainingLesson { FirstContact, Positioning, AttacksHealth, SimpleCombat, PreparationEconomy, TacticalBank }

    [Serializable]
    public sealed class TrainingCombatOptions
    {
        public string RivalMonsterId;
        public float StatMultiplier;
        public TrainingCombatOptions Copy()=>new TrainingCombatOptions{RivalMonsterId=RivalMonsterId,StatMultiplier=StatMultiplier};
        public bool IsValid(MatchConfig config)=>config?.Get(RivalMonsterId)?.IsMonster==true&&
            (RivalMonsterId=="bugaloo"||RivalMonsterId=="popow")&&!float.IsNaN(StatMultiplier)&&!float.IsInfinity(StatMultiplier)&&StatMultiplier>0&&StatMultiplier<=1;
        // The creator approved the other starter Monster with all beneficial
        // stats halved and inverse costs/timings doubled, only for practice.
        public static TrainingCombatOptions ForStarterOpponent(string selected)=>selected=="bugaloo"||selected=="popow"?
            new TrainingCombatOptions{RivalMonsterId=selected=="bugaloo"?"popow":"bugaloo",StatMultiplier=.5f}:null;
    }
}
