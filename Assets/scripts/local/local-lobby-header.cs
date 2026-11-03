using System;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        LobbyAccountSummary lobbyAccountSummary;
        LobbyAccountSummary CurrentLobbyAccount=>guestProfile!=null&&lobbyAccountSummary?.ProfileId==guestProfile.ProfileId?lobbyAccountSummary:null;
        public void SetLobbyAccountSummary(LobbyAccountSummary summary)
        {
            if(summary!=null&&summary.ProfileId!=guestProfile?.ProfileId)throw new ArgumentException("The summary belongs to a different player.",nameof(summary));
            lobbyAccountSummary=summary;
            if(Match!=null&&Match.Phase==MatchPhase.Menu)RenderPage();
        }
        void RenderLobbyHeader()
        {
            ReadLobbyDailyGifts();
            RefreshTrophyBoardAccess();
            float y=LobbyTop+10;
            LobbyResource("Earthkens","earthken",CurrentLobbyAccount?.Earthkens??LobbyEarnedBalance(DailyGiftCurrency.Earthkens),new Rect(12,y,168,76));
            LobbyResource("Sunkens","sunken",CurrentLobbyAccount?.Sunkens??LobbyEarnedBalance(DailyGiftCurrency.Sunkens),new Rect(188,y,168,76));
            var news=LobbySlimButton("Lobby news",new Rect(364,y,76,76),ShowLobbyNews);
            LobbyMegaphone(news);
            var settings=LobbySlimButton("Lobby settings",new Rect(448,y,80,76),ShowLobbyQuickMenu);
            for(int i=0;i<3;i++)Panel(settings,"Menu line",new Rect(25,27+i*9,30,4),LobbyPaper,false);
            if(dailyGiftError==null&&dailyGiftAccount?.CanClaim(DateTime.UtcNow)==true)
            {
                var badge=Panel(settings,"Gift available",new Rect(57,8,20,23),Color.clear,false);
                var green=new Color32(166,215,128,255);
                Panel(badge,"Gift box",new Rect(0,7,20,16),green,false);
                Panel(badge,"Gift ribbon",new Rect(8,7,4,16),LobbyPaper,false);
                Panel(badge,"Gift bow left",new Rect(2,1,7,6),green,false);
                Panel(badge,"Gift bow right",new Rect(11,1,7,6),green,false);
            }

            float second=y+84;
            ProfileAvatar(page,SelectedProfileAvatar,new Rect(12,second+4,80,80),ShowBattleCard);
            var trophies=LobbyButton(page,"Lobby trophies",new Rect(100,second,210,88),ShowLobbyTrophies);
            DrawLobbyPlayerHeader(trophies,font,PlayerPrefs.GetString("monster-pouch.player-name","Player One"),
                AccountTrophies,ResolveLobbyTrophyIcon(AccountTrophies));
            var missions=LobbyButton(page,"Lobby missions",new Rect(452,y+184,76,88),ShowLobbyMissions);
            LobbyClipboard(missions);

            var cases=LobbyButton(page,"Lobby cases",new Rect(332,second,196,80),ShowPouchRoom);
            HeaderImage(cases,"daily-pouch",new Rect(8,8,48,64));
            var label=Label(cases,"DAILY CASES",new Rect(60,8,128,64),16,LobbyPaper,FontStyle.Bold);
            label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=16;
        }
        // Presentation only: the account balance and saved display name are supplied
        // by the caller. The existing 210x88 button and its action stay unchanged.
        static void DrawLobbyPlayerHeader(RectTransform box,Font bodyFont,string playerName,long trophies,Sprite icon)
        {
            Text TextSlot(string slotName,string content,Rect bounds,int size)
            {
                var go=new GameObject(slotName,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));
                go.transform.SetParent(box,false);SetRect((RectTransform)go.transform,bounds);
                var text=go.GetComponent<Text>();text.font=bodyFont;text.text=content;text.fontSize=size;
                text.color=LobbyPaper;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
                text.supportRichText=false;text.alignByGeometry=false;text.lineSpacing=1;
                text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
                return text;
            }
            var name=TextSlot("Lobby player name",playerName,new Rect(12,2,186,40),22);
            name.resizeTextForBestFit=true;name.resizeTextMinSize=16;name.resizeTextMaxSize=22;
            name.lineSpacing=.95f;

            const float iconSize=32,gap=8,maxAmountWidth=142;
            var amount=TextSlot("Lobby trophy amount",LobbyAccountSummary.Number(trophies),new Rect(0,44,maxAmountWidth,38),30);
            float amountWidth=Mathf.Clamp(Mathf.Ceil(amount.preferredWidth)+2,18,maxAmountWidth);
            float left=(box.rect.width-amountWidth-gap-iconSize)*.5f;
            SetRect(amount.rectTransform,new Rect(left,44,amountWidth,38));
            amount.resizeTextForBestFit=true;amount.resizeTextMinSize=12;amount.resizeTextMaxSize=30;
            var imageRoot=new GameObject("Lobby trophy icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            imageRoot.transform.SetParent(box,false);
            SetRect((RectTransform)imageRoot.transform,new Rect(left+amountWidth+gap,47,iconSize,iconSize));
            var image=imageRoot.GetComponent<Image>();image.sprite=icon;image.preserveAspect=true;image.raycastTarget=false;
            // Reserve the approved layout while an icon awaits visual approval/import.
            image.color=icon!=null?Color.white:Color.clear;
        }
        static Sprite ResolveLobbyTrophyIcon(long trophies)=>Resources.Load<Sprite>(LobbyTrophyIconResource(trophies));
        static string LobbyTrophyIconResource(long trophies)
        {
            var stages=GuestTrophyLadder.CreatePolicy().Stages;
            string id=stages[0].BoardId;
            foreach(var stage in stages)if(trophies>=stage.MinimumTrophies)id=stage.BoardId;
            return "MonsterPouch/UI/Progression/Trophies/"+id;
        }

        RectTransform LobbySlimButton(string name,Rect bounds,Action action)
        {
            // Keep the generous touch area while the visible plate is slimmer.
            var root=HitArea(page,name,bounds);
            var plate=Panel(root,"Slim button face",new Rect(0,16,bounds.width,44),Color.clear,false);
            var face=plate.gameObject.AddComponent<LobbyControlFace>();face.raycastTarget=false;
            var button=root.gameObject.AddComponent<Button>();button.targetGraphic=face;
            var colors=button.colors;colors.normalColor=colors.selectedColor=Color.white;colors.fadeDuration=.08f;button.colors=colors;
            button.onClick.AddListener(()=>{PlaySound("button");action();});return root;
        }
        void FitLobbyAmount(Text label,int size)
        {
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
            label.resizeTextForBestFit=true;label.resizeTextMinSize=12;label.resizeTextMaxSize=size;
        }
        void HeaderImage(Transform parent,string resource,Rect rect)
        {
            Image(parent,Resources.Load<Sprite>("MonsterPouch/UI/Lobby/Header/"+resource),rect);
        }
        void LobbyResource(string title,string icon,long? amount,Rect bounds)
        {
            var button=LobbySlimButton("Lobby resource "+title,bounds,ShowLobbyDailyGift);
            HeaderImage(button,icon,new Rect(5,20,36,36));
            var value=LobbyNumber(button,LobbyAccountSummary.Number(amount),new Rect(45,20,86,36),24,LobbyPaper,TextAnchor.MiddleLeft);
            value.name="Lobby amount "+title;FitLobbyAmount(value,24);
            Label(button,"+",new Rect(133,20,29,36),26,LobbyPaper,FontStyle.Bold);
        }
        void LobbyMegaphone(Transform parent)
        {
            // Integer rectangles share the same crisp pixel construction as LobbyLock.
            Panel(parent,"Speaker back",new Rect(20,32,6,14),LobbyPaper,false);
            Panel(parent,"Speaker body",new Rect(28,29,9,20),LobbyPaper,false);
            Panel(parent,"Speaker bell",new Rect(37,25,12,28),LobbyPaper,false);
            Panel(parent,"Speaker handle",new Rect(29,47,5,9),LobbyPaper,false);
            Panel(parent,"Sound wave",new Rect(54,31,3,16),LobbyPaper,false);
        }
        void LobbyClipboard(Transform parent)
        {
            Panel(parent,"Clipboard border",new Rect(17,18,43,56),new Color32(109,139,157,255),false);
            Panel(parent,"Clipboard paper",new Rect(21,22,35,47),LobbyPaper,false);
            Panel(parent,"Clipboard clip",new Rect(28,13,21,15),new Color32(156,172,180,255),false);
            Panel(parent,"Clipboard tab",new Rect(33,9,11,8),new Color32(156,172,180,255),false);
            for(int i=0;i<2;i++)Panel(parent,"Task line",new Rect(26,34+i*11,23,4),LobbyInk,false);
            Panel(parent,"Task check",new Rect(26,57,7,5),new Color32(75,133,109,255),false);
            Panel(parent,"Task check tip",new Rect(33,51,5,11),new Color32(75,133,109,255),false);
        }
        void ShowLobbyNews()=>ShowLobbyNotice(GameText.Spanish?"NOTICIAS":"NEWS",GameText.Spanish?"No hay noticias por ahora.":"No news yet.");
        void ShowLobbyTrophies()=>OpenProgressionRoad();
        void ShowLobbyMissions()
        {
            var box=LobbyModal("Lobby missions panel",GameText.Spanish?"MISIONES":"MISSIONS",160,630,CloseModal);
            LobbyMissionSlot(box,GameText.Spanish?"MISIONES NORMALES":"REGULAR MISSIONS",98);
            LobbyMissionSlot(box,GameText.Spanish?"TUTORIALES POR PERSONAJE":"CHARACTER TUTORIALS",254);
            var practice=LobbyButton(box,"Open existing tutorials",new Rect(24,444,444,98),ShowTutorialReviewMenu,true);
            Label(practice,GameText.Spanish?"TUTORIAL Y PRÁCTICA":"TUTORIAL AND PRACTICE",new Rect(12,12,420,74),24,LobbyInk,FontStyle.Bold);
        }
        void LobbyMissionSlot(Transform box,string title,float top)
        {
            var row=Panel(box,title,new Rect(24,top,444,136),new Color32(45,64,68,255),false);
            Label(row,title,new Rect(16,14,412,47),22,LobbyPaper,FontStyle.Bold);
            LobbyNumber(row,GameText.Spanish?"Próximamente":"Coming soon",new Rect(16,77,412,35),19,LobbyPaper);
        }
    }
}
