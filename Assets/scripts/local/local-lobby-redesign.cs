using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        static readonly Color LobbyInk=new Color32(33,45,49,255);
        static readonly Color LobbyPaper=new Color32(255,241,212,255);
        static readonly Color LobbyGold=new Color32(237,193,91,255);
        // Header and footer use the same effective safe bounds as the portrait layout.
        float LobbyTop=>ToCanvas(new Vector2(0,appliedSafeArea.yMax)).y;
        float LobbyBottom=>ToCanvas(new Vector2(0,appliedSafeArea.yMin)).y;
        float LobbyFooterTop=>LobbyBottom-104;
        string LobbyPreferenceKey(string suffix)=>"monster-pouch.lobby."+suffix+"."+(guestProfile?.ProfileId??"unavailable");
        public string SelectedLobbyMode
        {
            get {var value=PlayerPrefs.GetString(LobbyPreferenceKey("mode"),"bot");return value=="duel"||value=="friend"?value:"bot";}
        }
        string[] AvailableProfileAvatars=>LobbyAvatarSelection.Available(guestProfile,Match.Config.Units.Where(d=>d!=null).Select(d=>d.Id));
        public string SelectedProfileAvatar=>LobbyAvatarSelection.Resolve(PlayerPrefs.GetString(LobbyAvatarSelection.PreferenceKey(guestProfile?.ProfileId),""),chosen,AvailableProfileAvatars);
        string LobbyModeName(string mode)=>mode=="duel"?(GameText.Spanish?"DUELO":"DUEL"):mode=="friend"?(GameText.Spanish?"CONTRA AMIGO":"WITH A FRIEND"):(GameText.Spanish?"CONTRA BOT":"VS BOT");

        RectTransform LobbyButton(Transform parent,string name,Rect bounds,Action click,bool primary=false)
        {
            var root=Panel(parent,name,bounds,Color.clear,false);
            var face=root.gameObject.AddComponent<LobbyControlFace>();
            if(primary){face.Fill=LobbyGold;face.Shine=new Color32(255,226,152,255);}
            var button=root.gameObject.AddComponent<Button>();button.targetGraphic=face;
            var colors=button.colors;colors.normalColor=colors.selectedColor=Color.white;colors.fadeDuration=.08f;button.colors=colors;
            button.onClick.AddListener(()=>{PlaySound("button");click();});return root;
        }
        Text LobbyNumber(Transform parent,string text,Rect rect,int size,Color color,TextAnchor anchor=TextAnchor.MiddleCenter)
        {
            var label=Label(parent,text,rect,size,color,FontStyle.Normal,anchor);
            label.font=font;label.supportRichText=false;return label;
        }
        void LobbyLock(Transform parent,Rect rect)
        {
            var root=Panel(parent,"Locked",rect,Color.clear,false);
            Panel(root,"Shackle outline",new Rect(7,0,22,23),LobbyInk,false);
            Panel(root,"Shackle",new Rect(10,3,16,20),LobbyPaper,false);
            Panel(root,"Opening",new Rect(14,7,8,15),LobbyInk,false);
            Panel(root,"Lock border",new Rect(1,16,34,28),LobbyInk,false);
            Panel(root,"Lock",new Rect(4,19,28,22),LobbyPaper,false);
            Panel(root,"Key",new Rect(16,25,5,10),LobbyInk,false);
        }
        RectTransform ProfileAvatar(Transform parent,string id,Rect bounds,Action action=null,bool locked=false,bool selected=false)
        {
            var root=LobbyButton(parent,"Player avatar "+(id??"none"),bounds,action??(()=>{}));
            root.GetComponent<LobbyControlFace>().Fill=new Color32(144,173,153,255);
            if(selected)root.GetComponent<LobbyControlFace>().Edge=LobbyGold;
            var image=Panel(root,"Character face",new Rect(7,7,bounds.width-14,bounds.height-14),Color.clear,false).gameObject.AddComponent<PlayerAvatarGraphic>();
            if(!image.Configure(id))Label(root,"?",new Rect(8,8,bounds.width-16,bounds.height-16),32,LobbyPaper,FontStyle.Bold);
            if(locked){image.color=new Color(.5f,.5f,.5f,.65f);LobbyLock(root,new Rect(bounds.width-41,bounds.height-47,36,44));}
            root.GetComponent<Button>().interactable=action!=null&&!locked;
            return root;
        }
        void RenderLobbyBattleControls()
        {
            float top=LobbyFooterTop-208;
            var events=LobbyButton(page,"Locked event",new Rect(18,top,112,186),()=>ShowLobbyNotice(GameText.Spanish?"EVENTO":"EVENT",GameText.Spanish?"Próximamente":"Coming soon"));
            var texture=Resources.Load<Texture2D>("MonsterPouch/UI/Lobby/event-duel-v1");
            if(texture!=null)
            {
                var artRoot=Panel(events,"Event artwork",new Rect(5,5,102,176),Color.clear,false);
                var image=artRoot.gameObject.AddComponent<RawImage>();image.texture=texture;image.raycastTarget=false;image.color=new Color(.65f,.65f,.65f,1);
                float cropWidth=(102f/176f)/((float)texture.width/texture.height);
                image.uvRect=new Rect((1-cropWidth)*.5f,0,cropWidth,1);
            }
            Panel(events,"Event caption shade",new Rect(5,148,102,33),new Color(.05f,.08f,.1f,.86f),false);
            LobbyLock(events,new Rect(70,9,36,44));
            Label(events,GameText.Spanish?"EVENTO":"EVENT",new Rect(3,149,106,32),22,LobbyPaper,FontStyle.Bold);
            var mode=LobbyButton(page,"Selected battle mode",new Rect(142,top,380,84),ShowBattleModes);
            Image(mode,Resources.Load<Sprite>("MonsterPouch/UI/BoardMenu/"+(SelectedLobbyMode=="friend"?"challenges":SelectedLobbyMode=="bot"?"brief":"battle")),new Rect(9,16,51,52));
            Label(mode,LobbyModeName(SelectedLobbyMode),new Rect(65,20,270,43),24,LobbyPaper,FontStyle.Bold,TextAnchor.MiddleLeft);
            Label(mode,"▼",new Rect(340,23,30,38),20,LobbyPaper);
            bool tutorial=CanResumeTutorial;
            var play=LobbyButton(page,tutorial?(CanResumeFirstContactTutorial?"Resume first contact tutorial":"Resume tutorial"):"BATALLA",new Rect(142,top+92,380,94),()=>{if(tutorial)StartNextTutorial();else PlaySelectedLobbyMode();},true);
            Label(play,tutorial?(GameText.Spanish?"CONTINUAR TUTORIAL":"CONTINUE TUTORIAL"):(GameText.Spanish?"JUGAR":"PLAY"),new Rect(8,10,364,72),tutorial?25:42,LobbyInk,FontStyle.Bold);
        }
        void PlaySelectedLobbyMode()
        {
            if(SelectedLobbyMode=="bot")StartChosenMatch();
            else if(SelectedLobbyMode=="duel")OpenAutomaticDuel();
            else OpenOnline();
        }
        void ShowLobbyBattleModes()
        {
            var box=LobbyModal("Battle modes",GameText.Spanish?"MODO DE PARTIDA":"BATTLE MODE",222,514,CloseModal);
            string[] modes={"duel","bot","friend"};
            for(int i=0;i<modes.Length;i++)
            {
                string mode=modes[i];bool selected=SelectedLobbyMode==mode;
                var option=LobbyButton(box,"mode-"+mode,new Rect(16,87+i*132,460,116),()=>
                {PlayerPrefs.SetString(LobbyPreferenceKey("mode"),mode);PlayerPrefs.Save();CloseModal();RenderPage();},selected);
                Image(option,Resources.Load<Sprite>("MonsterPouch/UI/BoardMenu/"+(mode=="duel"?"battle":mode=="bot"?"brief":"challenges")),new Rect(12,13,86,86));
                Label(option,LobbyModeName(mode),new Rect(112,20,334,39),27,selected?LobbyInk:LobbyPaper,FontStyle.Bold,TextAnchor.MiddleLeft);
                string detail=mode=="duel"?(GameText.Spanish?"Rival automático · Por copas":"Automatic opponent · Trophies"):mode=="bot"?(GameText.Spanish?"Practica a tu ritmo":"Practice at your own pace"):(GameText.Spanish?"Sala con código · Por copas":"Room with a code · Trophies");
                LobbyNumber(option,detail,new Rect(112,64,334,29),17,selected?LobbyInk:LobbyPaper,TextAnchor.MiddleLeft);
            }
        }
        RectTransform LobbyModal(string name,string title,float y,float height,Action close)
        {
            var box=RoomModal(name,title,y,height,close);
            var button=box.GetComponentInChildren<Button>();
            SetRect((RectTransform)button.transform,new Rect(396,2,84,84));
            var caption=button.GetComponentInChildren<Text>();SetRect(caption.rectTransform,new Rect(8,8,68,68));
            var heading=box.GetComponentsInChildren<Text>().First(t=>t!=caption);SetRect(heading.rectTransform,new Rect(20,16,366,48));
            return box;
        }
        void ShowLobbyNotice(string title,string message)
        {
            var box=LobbyModal("Lobby notice",title,320,284,CloseModal);
            LobbyNumber(box,message,new Rect(25,96,442,100),21,LobbyPaper);
        }
        public void ShowBattleCard()
        {
            ReloadGuestAvailability();
            var box=LobbyModal("Battle card",GameText.Spanish?"TARJETA DE BATALLA":"BATTLE CARD",155,642,CloseModal);
            var info=Panel(box,"Player information",new Rect(16,86,460,201),new Color32(240,239,225,255),false);
            ProfileAvatar(info,SelectedProfileAvatar,new Rect(14,17,124,124),ShowProfileAvatars);
            LobbyNumber(info,GameText.Spanish?"CAMBIAR ICONO":"CHANGE ICON",new Rect(10,150,132,28),12,LobbyInk);
            var name=Label(info,PlayerPrefs.GetString("monster-pouch.player-name","Player One"),new Rect(155,15,291,46),28,LobbyInk,FontStyle.Bold,TextAnchor.MiddleLeft);name.supportRichText=false;
            name.resizeTextForBestFit=true;name.resizeTextMinSize=13;name.resizeTextMaxSize=28;
            LobbyNumber(info,(GameText.Spanish?"NIVEL ":"LEVEL ")+(progress!=null?progress.Level.ToString():"0"),new Rect(155,69,285,30),19,LobbyInk,TextAnchor.MiddleLeft);
            int needed=progressionConfig!=null&&progress!=null?progressionConfig.Requirement(progress.Level):100;
            Panel(info,"XP track",new Rect(155,111,284,12),LobbyInk,false);
            if(progress!=null)Panel(info,"XP progress",new Rect(157,113,280*Mathf.Clamp01((float)progress.Experience/Mathf.Max(1,needed)),8),new Color32(123,181,157,255),false);
            LobbyNumber(info,progress!=null?progress.Experience+" / "+needed+" XP":"0 XP",new Rect(155,137,285,30),19,LobbyInk,TextAnchor.MiddleLeft);
            LobbyNumber(info,GameText.Spanish?"VER PROGRESO":"VIEW PROGRESS",new Rect(155,172,285,20),12,LobbyInk,TextAnchor.MiddleLeft);
            var progressHit=HitArea(info,"Open player progression",new Rect(149,63,297,132));
            progressHit.gameObject.AddComponent<Button>().onClick.AddListener(ShowProgression);
            BattleCardStat(box,"Collection",GameText.Spanish?"COLECCIÓN":"COLLECTION",AvailableProfileAvatars.Length+" / "+Match.Config.Units.Count(d=>d!=null),new Rect(16,304,222,108));
            BattleCardStat(box,"Matches",GameText.Spanish?"PARTIDAS":"MATCHES",progress!=null?progress.CompletedMatches.Count.ToString():"0",new Rect(254,304,222,108));
            var monster=Panel(box,"Featured Monster",new Rect(16,430,460,118),new Color32(240,239,225,255),false);
            Portrait(monster,chosen,new Rect(14,5,100,105));
            LobbyNumber(monster,GameText.Spanish?"MONSTER ACTUAL":"CURRENT MONSTER",new Rect(130,20,311,28),14,LobbyInk,TextAnchor.MiddleLeft);
            Label(monster,Match.Config.Get(chosen)?.DisplayName??chosen,new Rect(130,52,311,45),28,LobbyInk,FontStyle.Bold,TextAnchor.MiddleLeft);
            LobbyNumber(box,"ID: "+(guestProfile?.ProfileId??"—"),new Rect(18,569,456,51),13,LobbyPaper);
        }
        void BattleCardStat(Transform parent,string name,string title,string value,Rect rect)
        {
            var panel=Panel(parent,name,rect,new Color32(240,239,225,255),false);
            LobbyNumber(panel,title,new Rect(8,11,rect.width-16,26),15,LobbyInk);
            LobbyNumber(panel,value,new Rect(8,44,rect.width-16,47),29,LobbyInk);
        }
        public void ShowProfileAvatars()
        {
            bool ready=ReloadGuestAvailability();var available=ready?AvailableProfileAvatars:Array.Empty<string>();
            var box=LobbyModal("Profile icons",GameText.Spanish?"ELIGE TU ICONO":"CHOOSE YOUR ICON",60,822,ShowBattleCard);
            LobbyNumber(box,GameText.Spanish?"Desbloquea un personaje para usar su cara como icono.":"Unlock a character to use their face as your icon.",new Rect(18,80,456,64),17,LobbyPaper);
            var viewport=HitArea(box,"Avatar viewport",new Rect(16,153,460,646));viewport.gameObject.AddComponent<RectMask2D>();
            var units=Match.Config.Units.Where(d=>d!=null).OrderBy(d=>d.DisplayName,StringComparer.CurrentCultureIgnoreCase).ToArray();
            int rows=Mathf.CeilToInt(units.Length/4f);
            var content=Panel(viewport,"Avatar content",new Rect(0,0,460,Mathf.Max(646,rows*144)),Color.clear,false);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            for(int i=0;i<units.Length;i++)
            {
                string id=units[i].Id;float x=(i%4)*115,y=(i/4)*144;bool unlocked=available.Contains(id,StringComparer.Ordinal);
                ProfileAvatar(content,id,new Rect(x+5,y+4,104,104),()=>SelectProfileAvatar(id),!unlocked,SelectedProfileAvatar==id);
                LobbyNumber(content,units[i].DisplayName,new Rect(x,y+111,114,27),14,LobbyPaper);
            }
        }
        public bool SelectProfileAvatar(string id)
        {
            if(!ReloadGuestAvailability()||!AvailableProfileAvatars.Contains(id,StringComparer.Ordinal))return false;
            PlayerPrefs.SetString(LobbyAvatarSelection.PreferenceKey(guestProfile.ProfileId),id);PlayerPrefs.Save();
            CloseModal();RenderPage();ShowBattleCard();return true;
        }
        void RenderDockedNavigation()
        {
            menuNavigation=Panel(CanvasRoot,"Menu navigation",new Rect(0,0,540,960),Color.clear,false);
            Vector2 lower=ToCanvas(Vector2.zero),upper=ToCanvas(new Vector2(Screen.width,Screen.height));
            Panel(menuNavigation,"Navigation",new Rect(lower.x,LobbyFooterTop,upper.x-lower.x,lower.y-LobbyFooterTop),BoardMenuStyle.Ink,false);
            Panel(menuNavigation,"Navigation edge",new Rect(lower.x,LobbyFooterTop,upper.x-lower.x,3),BoardMenuStyle.Muted,false);
            string[] labels={GameText.Spanish?"TIENDA":"SHOP","BRIEF",GameText.Battle,GameText.Guide,GameText.Spanish?"DESAFÍOS":"CHALLENGES"};
            string[] icons={"shop","brief","battle","guide","challenges"};
            for(int i=0;i<5;i++)
            {
                int index=i;bool selected=menuTab==i;float lift=selected?8:0;
                var tab=LobbyButton(menuNavigation,"nav-"+labels[i],new Rect(3+i*108,LobbyFooterTop+3-lift,102,101+lift),()=>NavigateMenu(index),selected);
                var face=tab.GetComponent<LobbyControlFace>();if(!selected)face.Fill=face.Edge=face.Shine=BoardMenuStyle.Ink;
                Image(tab,Resources.Load<Sprite>("MonsterPouch/UI/BoardMenu/"+icons[i]),new Rect(11,3,80,70+lift));
                var label=Label(tab,labels[i],new Rect(1,75+lift,100,22),18,selected?LobbyInk:LobbyPaper);
                label.resizeTextForBestFit=true;label.resizeTextMinSize=13;label.resizeTextMaxSize=18;
            }
        }
    }
}
