using System;

namespace MonsterPouch.Gameplay.Match
{
    // Pure comparison keeps the displayed rounded percentages out of adjudication.
    public readonly struct EronyaRoundScore
    {
        public readonly decimal Health,InitialHealth,HeroHealth,HeroInitialHealth,Damage,EnemyInitialHealth;
        public readonly int Survivors,Deployed;
        public EronyaRoundScore(decimal health,decimal initialHealth,decimal heroHealth,decimal heroInitialHealth,
            int survivors,int deployed,decimal damage,decimal enemyInitialHealth)
        {Health=health;InitialHealth=initialHealth;HeroHealth=heroHealth;HeroInitialHealth=heroInitialHealth;
            Survivors=survivors;Deployed=deployed;Damage=damage;EnemyInitialHealth=enemyInitialHealth;}
    }
    public enum EronyaRoundCriterion { ArmyHealth, HeroHealth, Survivors, Damage, Token }
    public static class EronyaRoundRules
    {
        public const int TimeoutTicks=300;
        static int Ratio(decimal a,decimal b,decimal c,decimal d)
        {if(b<=0){a=0;b=1;}if(d<=0){c=0;d=1;}return (a*d).CompareTo(c*b);}
        public static int Compare(EronyaRoundScore blue,EronyaRoundScore red) => Compare(blue,red,out _);
        public static int Compare(EronyaRoundScore blue,EronyaRoundScore red,out EronyaRoundCriterion criterion)
        {
            criterion=EronyaRoundCriterion.ArmyHealth;
            int v=Ratio(blue.Health,blue.InitialHealth,red.Health,red.InitialHealth);if(v!=0)return v;
            criterion=EronyaRoundCriterion.HeroHealth;
            v=Ratio(blue.HeroHealth,blue.HeroInitialHealth,red.HeroHealth,red.HeroInitialHealth);if(v!=0)return v;
            criterion=EronyaRoundCriterion.Survivors;
            v=Ratio(blue.Survivors,blue.Deployed,red.Survivors,red.Deployed);if(v!=0)return v;
            criterion=EronyaRoundCriterion.Damage;
            v=Ratio(blue.Damage,blue.EnemyInitialHealth,red.Damage,red.EnemyInitialHealth);if(v!=0)return v;
            criterion=EronyaRoundCriterion.Token;return 0;
        }
        public static int TokenSide(int matchSeed,int round)=>((matchSeed&1)^((Math.Max(1,round)-1)&1));
        public static int MovementPrioritySide(int tokenSide,int tick)=>(tokenSide^1)^((Math.Max(1,tick)-1)&1);
    }
}
