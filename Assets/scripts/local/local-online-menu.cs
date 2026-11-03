using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        OnlineRoom online;
        Text onlineStatus,onlineCode;
        string OnlineStatusText=>online.StatusIsError?string.Empty:GameText.Resolve(online.Status);
        InputField onlineInput;
        Button onlineHost,onlineJoin,onlineStart;
        RectTransform joinPanel;
        string roomView;
        float keyboardShift;
        bool displayedOpponent;
        void SetupOnline()
        {
            online=gameObject.AddComponent<OnlineRoom>();online.Configure(Match);
            online.LocalTeamValidator=IsLocalRoomTeamAvailable;
            online.LocalAbilitySnapshotProvider=ReadLocalRoomAbilityAccess;
            online.LocalMapSelector=id=>CardTableTheme.KnownMap(id)&&CanSelectTrophyBoard(CardTableTheme.FromMapId(id))?id:CardTableTheme.MapId(CurrentTrophyStageStyle);
            online.LocalTrophyProvider=ReadAutomaticDuelTrophies;
            online.MatchCompletedVerified+=RecordVerifiedTrophyMatch;
            online.Changed+=RefreshOnlineRoom;online.Failed+=HandleOnlineFailure;
        }
        RectTransform RoomModal(string name,string title,float y,float height,Action close)
        {
            CloseModal();joinPanel=null;keyboardShift=0;
            // The shared modal shield expands this image over the whole display.
            modal=Panel(CanvasRoot,name,new Rect(0,0,540,960),new Color(.015f,.025f,.055f,.8f));
            var box=BluePanel(modal,"Room panel",new Rect(24,y,492,height),new Color(.035f,.075f,.12f));
            Label(box,title,new Rect(20,16,392,48),26,Color.white,FontStyle.Bold);
            GameButton(box,"×",new Rect(430,16,46,46),close,false,28);
            return box;
        }
        Button ModeCard(RectTransform parent,string name,string caption,string hero,Rect rect,Action action,bool primary)
        {
            var card=GameButton(parent,"",rect,action,primary);
            Portrait(card,hero,new Rect(18,14,104,104));
            Label(card,name,new Rect(144,27,rect.width-158,40),27,primary?ink:Color.white,FontStyle.Bold,TextAnchor.MiddleLeft);
            Label(card,caption,new Rect(144,72,rect.width-158,38),16,primary?ink:cyan,FontStyle.Normal,TextAnchor.MiddleLeft);
            return card.GetComponent<Button>();
        }
        public void ShowBattleModes()=>ShowLobbyBattleModes();
        void RenderComingSoon()
        {
            bool shop=menuPage=="shop";Backdrop(homeBackground);
            var box=BluePanel(page,shop?"Shop page":"Challenges page",new Rect(36,260,468,398),new Color(.025f,.06f,.11f,.96f));
            Label(box,shop?"TIENDA":"DESAFÍOS",new Rect(20,30,428,54),36,gold,FontStyle.Bold);
            Image(box,MenuItem(shop?"brief":"batalla"),new Rect(158,106,152,152));
            Label(box,"PRÓXIMAMENTE",new Rect(20,286,428,48),28,Color.white,FontStyle.Bold);
            Label(box,shop?"Nuevas sorpresas para tu colección":"Torneos y retos entre amigos",new Rect(20,344,428,32),18,cyan);
        }
        public void OpenOnline()
        {
            if(online.DuelPending){ShowAutomaticDuelSearch();return;}
            if(Match.IsOnline)return;
            if(online.Active){ShowWaitingRoom();return;}
            roomView="options";
            var box=RoomModal("Online room","SALA DE AMIGOS",278,422,CloseModal);
            onlineHost=ModeCard(box,"CREAR SALA","Comparte tu código",chosen,new Rect(16,80,460,130),async()=>
            {if(!PrepareCurrentDeckForMatch()){CloseModal();RenderPage();return;}await online.Host(chosen,chosenWhelps.ToArray());},true);
            onlineJoin=ModeCard(box,"UNIRSE A SALA","Entra con un código","bugaloo",new Rect(16,228,460,130),ShowJoinRoom,false);
            onlineStatus=Label(box,OnlineStatusText,new Rect(20,370,452,40),15,cyan);
            RefreshOnlineRoom();
        }
        public static string NormalizeRoomCode(string value)=>new string((value??"").ToUpperInvariant().Where(c=>c>='A'&&c<='Z'||c>='0'&&c<='9').Take(6).ToArray());
        public void ShowJoinRoom()
        {
            roomView="join";
            joinPanel=RoomModal("Join room","UNIRSE A UNA SALA",290,370,OpenOnline);
            var field=BluePanel(joinPanel,"Room code input",new Rect(20,90,452,66),paper);
            onlineInput=field.gameObject.AddComponent<InputField>();
            var text=Label(field,"",new Rect(16,4,420,58),32,ink,FontStyle.Bold);text.supportRichText=false;
            onlineInput.textComponent=text;onlineInput.placeholder=Label(field,"INTRODUCE EL CÓDIGO",new Rect(16,4,420,58),20,blueFrame);
            onlineInput.targetGraphic=field.GetComponent<Image>();onlineInput.characterLimit=6;
            onlineInput.contentType=InputField.ContentType.Custom;onlineInput.lineType=InputField.LineType.SingleLine;
            onlineInput.keyboardType=TouchScreenKeyboardType.ASCIICapable;onlineInput.shouldHideMobileInput=false;
            onlineInput.onValidateInput+=(value,index,c)=>{c=char.ToUpperInvariant(c);return c>='A'&&c<='Z'||c>='0'&&c<='9'?c:'\0';};
            onlineInput.onValueChanged.AddListener(value=>
            {string normalized=NormalizeRoomCode(value);if(value!=normalized)onlineInput.SetTextWithoutNotify(normalized);RefreshOnlineRoom();});
            GameButton(joinPanel,"PEGAR",new Rect(20,170,216,46),()=>{onlineInput.text=NormalizeRoomCode(GUIUtility.systemCopyBuffer);},false,18);
            GameButton(joinPanel,"COPIAR",new Rect(256,170,216,46),()=>GUIUtility.systemCopyBuffer=onlineInput.text,false,18);
            onlineJoin=GameButton(joinPanel,"UNIRSE",new Rect(100,234,292,60),async()=>
            {string code=onlineInput.text;onlineInput.DeactivateInputField();if(!PrepareCurrentDeckForMatch()){CloseModal();RenderPage();return;}await online.Join(code,chosen,chosenWhelps.ToArray());},true,25).GetComponent<Button>();
            onlineStatus=Label(joinPanel,OnlineStatusText,new Rect(20,308,452,46),16,cyan);RefreshOnlineRoom();
        }
        void ShowWaitingRoom()
        {
            roomView="waiting";displayedOpponent=online.HasOpponent;
            var box=RoomModal("Online room","SALA DE AMIGOS",52,804,LeaveRoom);
            Label(box,"CÓDIGO DE LA SALA",new Rect(30,106,432,34),21,cyan,FontStyle.Bold);
            onlineCode=Label(box,online.Code,new Rect(50,148,274,58),43,Color.white,FontStyle.Bold);
            GameButton(box,"COPIAR",new Rect(334,153,128,48),()=>{GUIUtility.systemCopyBuffer=online.Code;},false,17);
            var left=BluePanel(box,"Local player",new Rect(30,264,168,192));Figure(left,chosen,new Rect(14,8,140,146),false);
            Label(left,PlayerPrefs.GetString("monster-pouch.player-name","TÚ"),new Rect(8,152,152,32),19,Color.white,FontStyle.Bold);
            Label(box,"VS",new Rect(198,308,96,64),42,gold,FontStyle.Bold);
            var right=BluePanel(box,"Other player",new Rect(294,264,168,192));
            if(online.HasOpponent)Figure(right,online.OpponentMonster,new Rect(14,8,140,146),false);
            else Label(right,"?",new Rect(14,10,140,138),76,cyan,FontStyle.Bold);
            var rivalName=Label(right,online.HasOpponent?online.OpponentName:"ESPERANDO",new Rect(8,152,152,32),18,Color.white,FontStyle.Bold);rivalName.supportRichText=false;rivalName.resizeTextForBestFit=true;rivalName.resizeTextMinSize=12;rivalName.resizeTextMaxSize=18;
            onlineStatus=Label(box,OnlineStatusText,new Rect(30,482,432,66),20,cyan);
            var team=chosenWhelps.ToArray();float step=432f/Mathf.Max(1,team.Length);
            for(int i=0;i<team.Length;i++)Portrait(box,team[i],new Rect(30+i*step,584,step,68));
            GameButton(box,"ABANDONAR",new Rect(20,706,216,64),LeaveRoom,false,21);
            onlineStart=GameButton(box,online.IsHost?"INICIAR":"ESPERANDO",new Rect(256,706,216,64),online.StartRoom,true,23).GetComponent<Button>();
            onlineStart.interactable=online.IsHost&&online.HasOpponent;
        }
        void LeaveRoom(){roomView=null;online.Leave();CloseModal();}
        void RefreshOnlineRoom()
        {
            if(RefreshAutomaticDuel())return;
            if(Match.IsOnline){if(modal!=null&&(modal.name=="Online room"||modal.name=="Join room"))CloseModal();return;}
            if(modal==null||(modal.name!="Online room"&&modal.name!="Join room"))return;
            if(online.Active&&!online.Busy&&(roomView!="waiting"||displayedOpponent!=online.HasOpponent)){ShowWaitingRoom();return;}
            if(onlineStatus!=null)onlineStatus.text=OnlineStatusText;if(onlineCode!=null)onlineCode.text=online.Code;
            bool available=!online.Busy&&!online.Active;
            if(onlineHost!=null)onlineHost.interactable=available;
            if(onlineJoin!=null)onlineJoin.interactable=available&&(roomView!="join"||onlineInput!=null&&OnlineRoom.ValidCode(onlineInput.text));
            if(onlineInput!=null)onlineInput.interactable=available;
        }
        void UpdateOnlineKeyboard()
        {
            if(joinPanel==null||onlineInput==null)return;
            float height=TouchScreenKeyboard.area.height;
            if(onlineInput.isFocused&&TouchScreenKeyboard.visible&&height<1)height=AndroidKeyboardHeight();
            if(onlineInput.isFocused&&TouchScreenKeyboard.visible&&height<1)height=Screen.height*.48f;
            float bottom=ToCanvas(new Vector2(0,height)).y;
            float desired=height>0?Mathf.Max(0,290+370+16-bottom):0;
            float minTop=ToCanvas(new Vector2(0,Screen.safeArea.yMax)).y+12;
            desired=Mathf.Min(desired,Mathf.Max(0,290-minTop));
            keyboardShift=Mathf.MoveTowards(keyboardShift,desired,Time.unscaledDeltaTime*1800);
            SetRect(joinPanel,new Rect(24,290-keyboardShift,492,370));
        }
        static float AndroidKeyboardHeight()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var window=activity.Call<AndroidJavaObject>("getWindow"))
                using(var view=window.Call<AndroidJavaObject>("getDecorView"))
                using(var rect=new AndroidJavaObject("android.graphics.Rect"))
                {view.Call("getWindowVisibleDisplayFrame",rect);return Mathf.Max(0,Screen.height-rect.Get<int>("bottom"));}
            }
            catch(AndroidJavaException){return 0;}
            #else
            return 0;
            #endif
        }
    }
}
