using System.Globalization;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    public static class EronyaRoundPresentation
    {
        public static string Percent(decimal value,decimal initial) =>
            (initial>0?100m*value/initial:0m).ToString("0.##",CultureInfo.InvariantCulture)+"%";

        public static string CriterionLabel(EronyaRoundCriterion criterion,bool spanish)
        {
            switch(criterion)
            {
                case EronyaRoundCriterion.ArmyHealth:return spanish?"Vida del equipo":"Team health";
                case EronyaRoundCriterion.HeroHealth:return spanish?"Vida del líder":"Leader health";
                case EronyaRoundCriterion.Survivors:return spanish?"Supervivientes":"Survivors";
                case EronyaRoundCriterion.Damage:return spanish?"Daño causado":"Damage dealt";
                default:return spanish?"Ficha de desempate":"Tiebreak token";
            }
        }
        public static string TokenLabel(BoardSide token,BoardSide local,bool spanish) =>
            spanish?(token==local?"Ficha: tú":"Ficha: rival"):(token==local?"Token: you":"Token: opponent");
    }

    public sealed partial class LocalGameUI
    {
        void RenderEronyaRoundComparison()
        {
            healthSummaryRoot=Panel(roundTransitionRoot,"Eronya normalized comparison",new Rect(0,0,540,960),Color.clear,false);
            healthSummaryGroup=healthSummaryRoot.gameObject.AddComponent<CanvasGroup>();
            healthSummaryGroup.interactable=healthSummaryGroup.blocksRaycasts=false;
            var sim=Match.Simulation;
            foreach(var side in new[]{BoardSide.Blue,BoardSide.Red})
            {
                var score=side==BoardSide.Blue?sim.EronyaBlueScore:sim.EronyaRedScore;
                bool local=side==Match.LocalSide;
                var panel=Panel(healthSummaryRoot,side+" normalized score",new Rect(48,local?660:120,444,148),ink,false);
                PixelBorder(panel,HealthColor(side),2);
                Label(panel,local?LocalDisplayName:RivalDisplayName,new Rect(12,7,420,24),18,HealthColor(side),FontStyle.Bold).supportRichText=false;
                var labels=new[]{EronyaRoundCriterion.ArmyHealth,EronyaRoundCriterion.HeroHealth,EronyaRoundCriterion.Survivors,EronyaRoundCriterion.Damage};
                var values=new[]{EronyaRoundPresentation.Percent(score.Health,score.InitialHealth),
                    EronyaRoundPresentation.Percent(score.HeroHealth,score.HeroInitialHealth),
                    EronyaRoundPresentation.Percent(score.Survivors,score.Deployed),
                    EronyaRoundPresentation.Percent(score.Damage,score.EnemyInitialHealth)};
                for(int i=0;i<labels.Length;i++)
                {
                    Color tint=labels[i]==sim.EronyaRoundCriterion?gold:Color.white;
                    Label(panel,EronyaRoundPresentation.CriterionLabel(labels[i],GameText.Spanish),new Rect(14,36+i*25,282,24),16,tint,FontStyle.Normal,TextAnchor.MiddleLeft);
                    Label(panel,values[i],new Rect(296,36+i*25,132,24),17,tint,FontStyle.Bold,TextAnchor.MiddleRight);
                }
            }
            string reason=(GameText.Spanish?"Decide: ":"Decided by: ")+EronyaRoundPresentation.CriterionLabel(sim.EronyaRoundCriterion,GameText.Spanish);
            Label(healthSummaryRoot,reason,new Rect(48,412,444,32),21,gold,FontStyle.Bold);
            Label(healthSummaryRoot,EronyaRoundPresentation.TokenLabel(sim.EronyaTiebreakSide,Match.LocalSide,GameText.Spanish),new Rect(48,448,444,28),18,Color.white,FontStyle.Bold);
            // The authoritative comparison uses exact ratios even if display rounding makes them look equal.
            Label(healthSummaryRoot,GameText.Spanish?"Porcentajes redondeados para mostrar.":"Percentages rounded for display.",new Rect(48,482,444,24),13,Color.white);
        }

        void UpdateEronyaRoundComparison()
        {
            if(healthSummaryGroup==null)return;
            var sim=Match.Simulation;
            float age=Match.RoundResultElapsed-Match.RoundHealthStartTime;
            healthSummaryGroup.alpha=age>=0&&age<RoundHudSettings.HealthDuration?1:0;
            PresentedBlueHealth=sim.EronyaBlueScore.InitialHealth>0?(float)(100m*sim.EronyaBlueScore.Health/sim.EronyaBlueScore.InitialHealth):0;
            PresentedRedHealth=sim.EronyaRedScore.InitialHealth>0?(float)(100m*sim.EronyaRedScore.Health/sim.EronyaRedScore.InitialHealth):0;
        }
    }
}
