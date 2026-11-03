using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        RectTransform bankLid,bankContents,bankGroundShadow,bankContactShadow;
        Image bankClosedImage,bankBodyImage,bankLidImage;
        CanvasGroup bankContentsGroup;
        bool bankVisible,bankHover;
        float bankOpen;
        int originalSleepTimeout=SleepTimeout.SystemSetting;
        bool applicationPaused;
        public float BankOpenAmount=>bankOpen;
        // Image.preserveAspect keeps this top-pivot sprite at its actual fitted height.
        // Align that artwork's base, not the taller invisible drop rectangle.
        float BankRestHeight=>ClosedBriefArt==null?70:Mathf.Min(70,130*ClosedBriefArt.rect.height/ClosedBriefArt.rect.width);
        float BankRestTop=>202-BankRestHeight;
        float BankColumnOffset=>BankRestTop-132;

        void UpdateScreenAwakePolicy()
        {
            Screen.sleepTimeout=!applicationPaused&&Match!=null&&Match.Phase!=MatchPhase.Menu
                ?SleepTimeout.NeverSleep:originalSleepTimeout;
        }
        void OnApplicationPause(bool paused){applicationPaused=paused;UpdateScreenAwakePolicy();}

        void RenderBank(RectTransform tray)
        {
            benchDrop=HitArea(tray,"Bench drop",new Rect(374,BankRestTop,150,74));
            // A wide cast shadow and a tighter contact shadow anchor the case
            // to the table. Both sit behind its artwork and ignore pointer input.
            bankGroundShadow=BankShadow("Bank ground shadow",new Rect(7,BankRestHeight-8,142,20),.18f);
            bankContactShadow=BankShadow("Bank contact shadow",new Rect(19,BankRestHeight-4,114,10),.30f);
            var body=Panel(benchDrop,"Bank body",new Rect(10,20,130,54),Color.clear,false);
            bankBodyImage=body.gameObject.AddComponent<Image>();bankBodyImage.sprite=briefBodySprite;bankBodyImage.raycastTarget=false;
            bankLid=Panel(benchDrop,"Bank lid",new Rect(10,0,130,22),Color.clear,false);
            bankLid.pivot=new Vector2(.5f,0);bankLid.anchoredPosition=new Vector2(75,-21);
            bankLidImage=bankLid.gameObject.AddComponent<Image>();bankLidImage.sprite=briefLidSprite;bankLidImage.raycastTarget=false;
            var closed=Panel(benchDrop,"Bank closed",new Rect(10,0,130,70),Color.clear,false);
            bankClosedImage=closed.gameObject.AddComponent<Image>();bankClosedImage.sprite=ClosedBriefArt;
            bankClosedImage.preserveAspect=true;bankClosedImage.raycastTarget=false;
            int count=Match.Player.BankedUnits.Count();
            Label(benchDrop,count+" / "+MatchConfig.MaxBenchUnits,new Rect(109,50,41,24),17,gold,FontStyle.Bold);
            benchDrop.gameObject.AddComponent<Button>().onClick.AddListener(ToggleBankContents);
            bankContents=null;bankContentsGroup=null;
            if(Match.Phase!=MatchPhase.Preparation)bankVisible=false;
            if(bankVisible)ShowBankContents();
            ApplyBankPose();
        }
        RectTransform BankShadow(string name,Rect bounds,float opacity)
        {
            var shadow=Panel(benchDrop,name,bounds,Color.clear,false);
            var image=shadow.gameObject.AddComponent<Image>();
            image.sprite=MonsterPouch.Gameplay.Presentation.UnitPresentation.ContactShadowSprite;
            image.color=new Color(.025f,.02f,.035f,opacity);
            image.raycastTarget=false;
            return shadow;
        }
        public void ToggleBankContents()
        {
            if(bankVisible){CloseBankContents();return;}
            bankVisible=true;ShowBankContents();
        }
        void ShowBankContents()
        {
            if(Match.Player==null||bankContents!=null)return;
            bankContents=BluePanel(page,"Bank contents",new Rect(16,CardTableArena.BriefTop+45,346,171),ink);
            bankContentsGroup=bankContents.gameObject.AddComponent<CanvasGroup>();
            Label(bankContents,GameText.Spanish?"BANCO":"BANK",new Rect(14,2,252,34),18,gold,FontStyle.Bold,TextAnchor.MiddleLeft);
            Button(bankContents,"×",new Rect(292,-2,48,40),CloseBankContents,new Color(0,0,0,.001f),Color.white,25).name="bank-close";
            var stored=Match.Player.BankedUnits.ToArray();
            for(int i=0;i<MatchConfig.MaxBenchUnits;i++)
            {
                var card=BluePanel(bankContents,i<stored.Length?"bank-unit-"+stored[i].Definition.Id:"bank-empty-"+i,
                    new Rect(10+i*110,38,106,120),new Color(.055f,.19f,.21f));
                if(i>=stored.Length){Label(card,"—",new Rect(0,0,106,120),24,new Color(.45f,.58f,.58f));continue;}
                var owned=stored[i];Portrait(card,owned.Definition.Id,new Rect(13,8,80,80));
                Label(card,Stars(owned.TrickCount),new Rect(6,92,58,24),14,gold);
                int cost=Match.Player.GetDeploymentCost(owned);
                if(cost>=0)MoonkenCost(card,cost,new Rect(70,87,32,32));
                AddUnitGesture(card,owned,false);
            }
        }
        void CloseBankContents()
        {
            bankVisible=false;
            if(bankContents!=null){bankContents.gameObject.SetActive(false);Destroy(bankContents.gameObject);}
            bankContents=null;bankContentsGroup=null;
        }
        void PreviewBankDrop(OwnedUnit owned,Vector2 screen)
        {
            bankHover=owned!=null&&!owned.Definition.IsMonster&&Contains(benchDrop,screen);
            // Keep the pointer's source alive through release, but uncover the board.
            if(owned!=null&&owned.Location==UnitLocation.Bench&&bankContentsGroup!=null)
            {bankContentsGroup.alpha=0;bankContentsGroup.blocksRaycasts=false;}
        }
        void EndBankDrag()
        {
            bankHover=false;
            if(bankContentsGroup!=null){bankContentsGroup.alpha=1;bankContentsGroup.blocksRaycasts=true;}
        }
        void UpdateBankMotion()
        {
            bankOpen=Mathf.MoveTowards(bankOpen,bankHover?1:0,Time.unscaledDeltaTime/.18f);ApplyBankPose();
        }
        void ApplyBankPose()
        {
            if(bankLid==null)return;
            float open=Mathf.SmoothStep(0,1,bankOpen);
            float baseHeight=Mathf.Lerp(BankRestHeight,74,open);
            if(bankGroundShadow!=null)SetRect(bankGroundShadow,new Rect(7,baseHeight-8,142,20));
            if(bankContactShadow!=null)SetRect(bankContactShadow,new Rect(19,baseHeight-4,114,10));
            bankClosedImage.color=new Color(1,1,1,1-open);
            bankBodyImage.color=bankLidImage.color=new Color(1,1,1,open);
            bankLid.localScale=new Vector3(1,Mathf.Lerp(.12f,1,open),1);
            bankLid.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-12,0,open));
        }
    }
}
