using System.Collections.Generic;
using System.Globalization;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        sealed class HealthNumber
        {
            public RoundHealthContribution Contribution;
            public Text Label;
            public Text UpperHalf, LowerHalf;
            public RectTransform Cut;
            public RoundHealthOrb Orb;
            public Vector2 Origin;
            public bool AboveEnemyBar;
        }
        RectTransform healthSummaryRoot;
        RoundHealthBarArtwork blueHealthBar, redHealthBar;
        CanvasGroup healthSummaryGroup;
        Text blueHealthTotal, redHealthTotal;
        readonly List<HealthNumber> healthNumbers=new List<HealthNumber>();
        public float PresentedBlueHealth { get; private set; }
        public float PresentedRedHealth { get; private set; }
        static readonly Color HealthNumberGreen=new Color32(89,255,130,255);
        static readonly Color HealthNumberRed=new Color32(255,80,88,255);
        Color HealthColor(BoardSide side)=>side==Match.LocalSide?HealthNumberGreen:HealthNumberRed;

        void ResetRoundHealth()
        {
            healthSummaryRoot=null; blueHealthBar=redHealthBar=null;
            healthSummaryGroup=null;blueHealthTotal=redHealthTotal=null;
            healthNumbers.Clear();PresentedBlueHealth=PresentedRedHealth=0;
        }
        void RenderRoundHealth()
        {
            if(Match.Simulation?.EronyaRoundCompared==true){RenderEronyaRoundComparison();return;}
            var summary=Match.Simulation?.HealthSummary;
            if(!Match.UsesHealthTiebreaker||summary==null)return;
            healthSummaryRoot=Panel(roundTransitionRoot,"Round health comparison",new Rect(0,0,540,960),Color.clear,false);
            healthSummaryGroup=healthSummaryRoot.gameObject.AddComponent<CanvasGroup>();
            healthSummaryGroup.interactable=healthSummaryGroup.blocksRaycasts=false;
            foreach(var side in new[]{BoardSide.Blue,BoardSide.Red})
            {
                float y=HealthBarY(side);
                var track=Panel(healthSummaryRoot,side+" health track",new Rect(48,y,444,52),Color.clear,false);
                var artwork=track.gameObject.AddComponent<RoundHealthBarArtwork>();
                artwork.Configure(Match.MapForSide(side),side==Match.LocalSide,side+" health fill");
                var total=Label(track,"0",artwork.TotalRect,19,HealthColor(side),FontStyle.Bold);
                total.resizeTextForBestFit=true;total.resizeTextMinSize=10;total.resizeTextMaxSize=19;
                AddHealthOutline(total);
                total.name=side+" health total";
                if(side==BoardSide.Blue){blueHealthBar=artwork;blueHealthTotal=total;}
                else{redHealthBar=artwork;redHealthTotal=total;}
            }
            foreach(var item in summary.Contributions)
            {
                Vector2 origin=HealthNumberOrigin(item);
                var number=new HealthNumber{Contribution=item,Origin=origin};
                // Top-row numbers need their own band between the options button
                // and the enemy bar; their pieces separate sideways within it.
                number.AboveEnemyBar=item.Side!=Match.LocalSide&&origin.y>80&&origin.y<208;
                if(number.AboveEnemyBar)number.Origin.y=Mathf.Min(origin.y,98);
                if(!item.IsMonster)
                {
                    number.UpperHalf=CreateHealthNumber(item,"Health upper half ",true);
                    number.LowerHalf=CreateHealthNumber(item,"Health lower half ",false);
                    number.Cut=Panel(healthSummaryRoot,"Health diagonal cut "+item.UnitId,
                        new Rect(0,0,1,2),HealthColor(item.Side),false);
                    number.Cut.pivot=new Vector2(.5f,.5f);
                }
                number.Label=CreateHealthNumber(item,"Health contribution ");
                if(number.AboveEnemyBar)
                {
                    number.Label.fontSize=24;
                    if(number.UpperHalf!=null){number.UpperHalf.fontSize=24;number.LowerHalf.fontSize=24;}
                }
                var orbObject=new GameObject("Health orb "+item.UnitId,typeof(RectTransform),typeof(CanvasRenderer));
                orbObject.transform.SetParent(healthSummaryRoot,false);
                number.Orb=orbObject.AddComponent<RoundHealthOrb>();
                number.Orb.raycastTarget=false;number.Orb.rectTransform.sizeDelta=new Vector2(24,24);
                healthNumbers.Add(number);
            }
        }
        Text CreateHealthNumber(RoundHealthContribution item,string prefix,bool? upperHalf=null)
        {
            var label=Label(healthSummaryRoot,item.CurrentHealth.ToString(CultureInfo.InvariantCulture),
                new Rect(0,0,180,64),30,HealthColor(item.Side),FontStyle.Bold);
            label.name=prefix+item.UnitId;
            label.horizontalOverflow=HorizontalWrapMode.Overflow;
            label.verticalOverflow=VerticalWrapMode.Overflow;
            if(upperHalf.HasValue)label.gameObject.AddComponent<DiagonalHealthHalf>().UpperHalf=upperHalf.Value;
            // Outline runs after clipping, so the diagonal edge has the same readable border.
            AddHealthOutline(label);
            return label;
        }
        static void AddHealthOutline(Text label)
        {
            var outline=label.gameObject.AddComponent<Outline>();
            outline.effectColor=Color.black;outline.effectDistance=new Vector2(2.5f,-2.5f);
        }
        Vector2 HealthNumberOrigin(RoundHealthContribution contribution)
        {
            Vector3 top=Match.Mapper.GetWorldPosition(Match.Board.GetCell(contribution.Cell.x,contribution.Cell.y))+Vector3.up*1.5f;
            if(visuals.TryGetValue(contribution.Unit,out var view)&&view!=null&&view.Renderer!=null)
            {
                var bounds=view.Renderer.bounds;
                top=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
            }
            Vector2 point=ToCanvas(GameCamera.WorldToScreenPoint(top));
            // Leave space for both the glyph height and the downward-moving cut piece.
            return new Vector2(point.x,Mathf.Max(48,point.y-52));
        }
        float HealthBarY(BoardSide side)=>side==Match.LocalSide?694:120;
        void UpdateRoundHealth()
        {
            if(Match.Simulation?.EronyaRoundCompared==true){UpdateEronyaRoundComparison();return;}
            if(healthSummaryGroup==null)return;
            var summary=Match.Simulation?.HealthSummary;if(summary==null)return;
            float age=Match.RoundResultElapsed-Match.RoundHealthStartTime;
            float duration=RoundHudSettings.HealthDuration;
            // Briefly hold the completed bars before the winner's cards enter.
            float progress=duration<=0?1:Mathf.Clamp01(age/(duration*.85f));
            healthSummaryGroup.alpha=age>=0&&age<duration?1:0;
            PresentedBlueHealth=summary.PresentedTotal(BoardSide.Blue,progress);
            PresentedRedHealth=summary.PresentedTotal(BoardSide.Red,progress);
            SetHealthBar(blueHealthBar,blueHealthTotal,PresentedBlueHealth,summary.Maximum);
            SetHealthBar(redHealthBar,redHealthTotal,PresentedRedHealth,summary.Maximum);
            PulseHealthBar(blueHealthBar,summary,BoardSide.Blue,progress);
            PulseHealthBar(redHealthBar,summary,BoardSide.Red,progress);
            foreach(var item in healthNumbers)
            {
                float slot=summary.SlotProgress(item.Contribution,progress);
                float transfer=RoundHealthSummary.TransferProgress(slot);
                bool isWhelp=!item.Contribution.IsMonster;
                bool divided=isWhelp&&slot>=.23f;
                item.Label.text=divided?FormatHealth(item.Contribution.Amount)
                    :item.Contribution.CurrentHealth.ToString(CultureInfo.InvariantCulture);
                float emergence=isWhelp?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.23f,.35f,slot)):1;
                float alpha=isWhelp&&slot>=.04f?emergence:1;
                float scale=divided?Mathf.Lerp(.65f,1,emergence):1;
                float morph=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.35f,.48f,slot));
                float numberScale=scale*Mathf.Lerp(1,.2f,morph);
                float orbScale=Mathf.Lerp(.15f,1,morph);
                // A quick enlargement, then collapse into the bar, at the end of the flight.
                if(transfer>.75f)
                    orbScale=transfer<.89f?Mathf.Lerp(1,1.65f,Mathf.InverseLerp(.75f,.89f,transfer))
                        :Mathf.Lerp(1.65f,.15f,Mathf.InverseLerp(.89f,1,transfer));
                alpha*=1-Mathf.InverseLerp(.93f,1,transfer);
                var targetBar=item.Contribution.Side==BoardSide.Blue?blueHealthBar:redHealthBar;
                Vector2 end=new Vector2(48,HealthBarY(item.Contribution.Side))+targetBar.FillCenter;
                Vector2 point=Vector2.Lerp(item.Origin,end,transfer);
                Color tint=HealthColor(item.Contribution.Side);
                PositionHealthNumber(item.Label,point,numberScale,slot>=1?0:alpha*(1-morph),tint);
                var orbRect=item.Orb.rectTransform;
                orbRect.anchorMin=orbRect.anchorMax=new Vector2(0,1);orbRect.pivot=new Vector2(.5f,.5f);
                orbRect.anchoredPosition=new Vector2(point.x,-point.y);orbRect.localScale=Vector3.one*orbScale;
                tint.a=slot>=1?0:morph*(1-Mathf.InverseLerp(.93f,1,transfer));item.Orb.color=tint;
                if(isWhelp)UpdateHealthCut(item,slot);
            }
        }
        static void PositionHealthNumber(Text label,Vector2 point,float scale,float alpha,Color tint)
        {
            var rect=label.rectTransform;
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=new Vector2(point.x,-point.y);
            rect.localScale=Vector3.one*scale;
            tint.a=alpha;label.color=tint;
        }
        void UpdateHealthCut(HealthNumber item,float slot)
        {
            bool cutting=slot>=.04f&&slot<.65f;
            float separation=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.3f,slot));
            // The cut pieces fade continuously while the half-value emerges and
            // becomes an orb; they never disappear abruptly at the start of flight.
            float opacity=cutting?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.1f,.65f,slot)):0;
            var offset=new Vector2(-11,item.AboveEnemyBar?0:-18)*separation;
            PositionHealthNumber(item.UpperHalf,item.Origin+offset,1,opacity,HealthColor(item.Contribution.Side));
            PositionHealthNumber(item.LowerHalf,item.Origin-offset,1,opacity,HealthColor(item.Contribution.Side));
            float sweep=Mathf.InverseLerp(.04f,.16f,slot);
            float width=Mathf.Max(28,item.UpperHalf.preferredWidth+8);
            var direction=new Vector2(width, -19);
            Vector2 cutPoint=item.Origin+direction*(sweep*.5f-.5f);
            item.Cut.anchoredPosition=new Vector2(cutPoint.x,-cutPoint.y);
            item.Cut.sizeDelta=new Vector2(direction.magnitude*sweep,2);
            item.Cut.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(19,width)*Mathf.Rad2Deg);
            var color=HealthColor(item.Contribution.Side);color.a=cutting?1-Mathf.InverseLerp(.16f,.23f,slot):0;
            item.Cut.GetComponent<Image>().color=color;
        }
        static string FormatHealth(float amount)=>Mathf.CeilToInt(amount).ToString(CultureInfo.InvariantCulture);
        void PulseHealthBar(RoundHealthBarArtwork artwork,RoundHealthSummary summary,BoardSide side,float progress)
        {
            float pulse=0;
            foreach(var item in summary.Contributions)
                if(item.Side==side)
                    pulse=Mathf.Max(pulse,Mathf.Sin(RoundHealthSummary.TransferProgress(summary.SlotProgress(item,progress))*Mathf.PI));
            artwork.SetPulse(pulse);
        }
        void SetHealthBar(RoundHealthBarArtwork artwork,Text text,float value,float maximum)
        {
            artwork.SetValue(value,maximum);
            text.text=FormatHealth(value);
        }
    }
}
