using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        DailyGiftAccount dailyGiftAccount;
        LocalDailyGiftStore dailyGiftStore;
        DailyGiftTable dailyGiftTable;
        string dailyGiftError;
        [Tooltip("Approved blue Pouch artwork. When empty, loads the daily-pouch resource.")]
        public Sprite DailyGiftPouchArtwork;

        void ShowLobbyQuickMenu()
        {
            CloseModal();
            modal=Panel(CanvasRoot,"Lobby quick menu",new Rect(0,0,540,960),new Color(0,0,0,.28f));
            modal.gameObject.AddComponent<Button>().onClick.AddListener(CloseModal);
            var menu=Panel(modal,"Quick menu panel",new Rect(252,LobbyTop+96,276,282),Color.clear,false);
            menu.gameObject.AddComponent<LobbyControlFace>();
            LobbyMenuRow(menu,"Friendly room",GameText.Spanish?"SALA AMISTOSA":"FRIENDLY ROOM",8,()=>{CloseModal();OpenOnline();});
            LobbyMenuRow(menu,"Daily gift",GameText.Spanish?"REGALO DIARIO":"DAILY GIFT",98,ShowLobbyDailyGift);
            LobbyMenuRow(menu,"Open settings",GameText.Settings,188,ShowSettings);
        }
        void LobbyMenuRow(Transform parent,string name,string caption,float top,Action action)
        {
            var row=LobbyButton(parent,name,new Rect(8,top,260,82),action);
            var text=Label(row,caption,new Rect(12,8,236,66),24,LobbyPaper,FontStyle.Bold);
            text.resizeTextForBestFit=true;text.resizeTextMinSize=17;text.resizeTextMaxSize=24;
        }
        void ShowLobbySettings()
        {
            var box=LobbyModal("Settings",GameText.Settings,94,772,CloseModal);
            var body=Panel(box,"Settings paper",new Rect(16,86,460,666),new Color32(240,239,225,255),false);
            SettingsSlider(body,"Music",GameText.Spanish?"MÚSICA":"MUSIC",24,PlayerPrefs.GetFloat("monster-pouch.music-volume",.65f),v=>
                {PlayerPrefs.SetFloat("monster-pouch.music-volume",v);PlayerPrefs.Save();});
            LobbyNumber(body,GameText.Spanish?"Sin pista de música por ahora":"No music track yet",new Rect(162,83,280,21),12,LobbyInk);
            SettingsSlider(body,"Effects",GameText.Spanish?"EFECTOS":"EFFECTS",119,audioSource!=null?audioSource.volume:.65f,v=>
                {if(audioSource!=null)audioSource.volume=v;PlayerPrefs.SetFloat("monster-pouch.volume",v);PlayerPrefs.Save();});
            LobbyNumber(body,GameText.Spanish?"IDIOMA":"LANGUAGE",new Rect(18,222,138,56),20,LobbyInk,TextAnchor.MiddleLeft);
            var language=LobbyButton(body,"Settings language",new Rect(164,214,278,72),()=>{GameText.SetLanguage(!GameText.Spanish);RenderPage();ShowSettings();});
            Label(language,GameText.Spanish?"ESPAÑOL":"ENGLISH",new Rect(8,8,262,56),24,LobbyPaper,FontStyle.Bold);
            SettingsAction(body,"Settings help",GameText.Spanish?"AYUDA Y ASISTENCIA":"HELP AND SUPPORT",new Rect(18,330,208,98),ShowHelp);
            SettingsAction(body,"Settings rename",GameText.Spanish?"CAMBIAR NOMBRE":"CHANGE NAME",new Rect(234,330,208,98),ShowLobbyRename);
            SettingsAction(body,"Settings practice",GameText.Spanish?"TUTORIAL Y PRÁCTICA":"TUTORIAL AND PRACTICE",new Rect(18,444,208,98),ShowTutorialReviewMenu);
            SettingsAction(body,"Settings room",GameText.Spanish?"SALA AMISTOSA":"FRIENDLY ROOM",new Rect(234,444,208,98),OpenOnline);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevelopmentAccountActive)
            {
                SettingsAction(body,"Development start fresh",GameText.Spanish?"COMENZAR DESDE CERO":"START FROM ZERO",new Rect(18,554,208,78),()=>ShowDevelopmentAccountConfirmation(true));
                SettingsAction(body,"Development unlock all",GameText.Spanish?"DESBLOQUEAR TODO":"UNLOCK ALL",new Rect(234,554,208,78),()=>ShowDevelopmentAccountConfirmation(false));
                LobbyNumber(body,AndroidDeveloperContent.IsUnlocked(guestProfile)?
                    (GameText.Spanish?"DESARROLLO · TODO DESBLOQUEADO":"DEVELOPMENT · ALL UNLOCKED"):
                    (GameText.Spanish?"DESARROLLO · CUENTA NUEVA":"DEVELOPMENT · FRESH ACCOUNT"),new Rect(18,636,424,22),12,LobbyInk);
            }
            else
#endif
                Image(body,SuppliedBranding.Logo,new Rect(120,553,220,100));
        }
        void SettingsAction(Transform parent,string name,string caption,Rect rect,Action action)
        {
            var button=LobbyButton(parent,name,rect,action);
            var face=button.GetComponent<LobbyControlFace>();face.Fill=new Color32(56,111,137,255);face.Shine=new Color32(111,166,181,255);
            Label(button,caption,new Rect(10,10,rect.width-20,rect.height-20),22,LobbyPaper,FontStyle.Bold);
        }
        void SettingsSlider(Transform parent,string name,string caption,float top,float value,Action<float> update)
        {
            LobbyNumber(parent,caption,new Rect(18,top,138,58),20,LobbyInk,TextAnchor.MiddleLeft);
            var root=HitArea(parent,"Settings "+name+" slider",new Rect(164,top-6,278,76));
            var background=Panel(root,"Slider track",new Rect(10,30,258,16),new Color32(161,169,161,255),false);
            var fillArea=Panel(root,"Fill area",new Rect(10,30,258,16),Color.clear,false);
            var fill=Panel(fillArea,"Fill",new Rect(0,0,258,16),new Color32(65,134,169,255),false);
            fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=Vector2.zero;fill.offsetMax=Vector2.zero;
            var handleArea=Panel(root,"Handle area",new Rect(20,15,238,46),Color.clear,false);
            var handle=Panel(handleArea,"Handle",new Rect(0,0,40,0),Color.clear,false);
            handle.pivot=new Vector2(.5f,.5f);handle.anchorMin=handle.anchorMax=new Vector2(0,.5f);handle.anchoredPosition=Vector2.zero;
            var handleGraphic=handle.gameObject.AddComponent<LobbyControlFace>();handleGraphic.Fill=LobbyInk;
            var slider=root.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handleGraphic;
            slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;slider.SetValueWithoutNotify(Mathf.Clamp01(value));slider.onValueChanged.AddListener(v=>update(v));
        }
        void ShowLobbyRename()
        {
            var box=LobbyModal("Player name dialog",GameText.Spanish?"CAMBIAR NOMBRE":"CHANGE NAME",270,398,ShowSettings);
            var field=Panel(box,"Player name",new Rect(24,100,444,84),new Color32(240,239,225,255),true);
            var input=field.gameObject.AddComponent<InputField>();
            var text=LobbyNumber(field,"",new Rect(14,14,416,56),26,LobbyInk,TextAnchor.MiddleLeft);
            input.textComponent=text;input.characterLimit=20;input.lineType=InputField.LineType.SingleLine;input.text=PlayerPrefs.GetString("monster-pouch.player-name","Player One");
            var save=LobbyButton(box,"Save player name",new Rect(90,244,312,88),()=>
            {
                string name=input.text.Trim();if(string.IsNullOrEmpty(name))name="Player One";
                PlayerPrefs.SetString("monster-pouch.player-name",name);PlayerPrefs.Save();RenderPage();ShowSettings();
            },true);
            Label(save,GameText.Spanish?"GUARDAR":"SAVE",new Rect(8,8,296,72),30,LobbyInk,FontStyle.Bold);
        }
        void ReadLobbyDailyGifts()
        {
            dailyGiftAccount=null;dailyGiftError=null;
            if(guestProfile==null||guestProfileError!=null){dailyGiftError="profile";return;}
            try
            {
                string directory=AccountDailyGiftDirectory;
                dailyGiftStore=new LocalDailyGiftStore(directory);dailyGiftAccount=dailyGiftStore.Read(guestProfile.ProfileId);
                var resource=Resources.Load<TextAsset>("MonsterPouch/UI/Lobby/Header/DailyGifts");
                dailyGiftTable=resource!=null?JsonUtility.FromJson<DailyGiftTable>(resource.text):null;
                if(dailyGiftTable==null)throw new InvalidDataException("Missing gift table.");dailyGiftTable.Validate();
                if(dailyGiftStore.NeedsRecovery)dailyGiftError="recovery";
                if(dailyGiftError==null&&!SyncLobbyEarnedWallet())dailyGiftError="wallet";
            }
            catch(Exception e) when(e is IOException||e is InvalidDataException||e is ArgumentException||e is NotSupportedException||e is UnauthorizedAccessException)
            {dailyGiftError="read";dailyGiftAccount=null;}
        }
        void ShowLobbyDailyGift()
        {
            ReadLobbyDailyGifts();
            var box=LobbyModal("Daily gift panel",GameText.Spanish?"REGALO DIARIO":"DAILY GIFT",190,580,CloseModal);
            bool claimed=dailyGiftAccount!=null&&!dailyGiftAccount.CanClaim(DateTime.UtcNow);
            var last=claimed?dailyGiftAccount.Receipts.LastOrDefault():null;
            bool canOpen=dailyGiftError==null&&dailyGiftAccount!=null&&!claimed;
            DrawLobbyGift(box,new Rect(136,98,220,220),canOpen);
            string status=dailyGiftError!=null?(GameText.Spanish?"No se pudo cargar tu regalo. Inténtalo de nuevo.":"Couldn't load your gift. Please try again."):
                claimed?(GameText.Spanish?"Ya abriste tu regalo de hoy":"Today's gift is already open"):(GameText.Spanish?"Un regalo gratis cada día":"A free gift every day");
            LobbyNumber(box,status,new Rect(24,340,444,54),23,LobbyPaper);
            var claim=LobbyButton(box,"Claim daily gift",new Rect(72,416,348,86),ClaimLobbyDailyGift,true);
            claim.GetComponent<Button>().interactable=canOpen;
            Label(claim,claimed?(GameText.Spanish?"RECLAMADO":"CLAIMED"):(GameText.Spanish?"ABRIR":"OPEN"),new Rect(8,8,332,70),28,LobbyInk,FontStyle.Bold);
            if(last!=null&&dailyGiftError==null)
            {
                var next=DateTime.SpecifyKind(DateTime.ParseExact(last.Day,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),DateTimeKind.Utc).AddDays(1);
                var remaining=next-DateTime.UtcNow;int hours=Math.Max(0,(int)remaining.TotalHours),minutes=Math.Max(0,remaining.Minutes);
                LobbyNumber(box,(GameText.Spanish?"Próximo regalo en ":"Next gift in ")+hours+"h "+minutes+"m",new Rect(24,523,444,34),17,LobbyPaper);
            }
        }
        void ClaimLobbyDailyGift()
        {
            ReadLobbyDailyGifts();if(dailyGiftError!=null||dailyGiftAccount==null)return;
            try
            {
                var result=dailyGiftStore.Claim(guestProfile.ProfileId,DateTime.UtcNow,dailyGiftTable,out dailyGiftAccount);
                if(result==LocalDailyGiftStore.ClaimResult.RecoveryRequired){ShowLobbyDailyGift();return;}
                var receipt=dailyGiftAccount.Receipts.LastOrDefault();
                RenderPage();
                if(result==LocalDailyGiftStore.ClaimResult.Claimed&&receipt!=null)ShowLobbyDailyGiftReward(receipt);
                else ShowLobbyDailyGift();
            }
            catch(Exception e) when(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is ArgumentException||e is NotSupportedException)
            {ShowLobbyNotice(GameText.Spanish?"REGALO DIARIO":"DAILY GIFT",GameText.Spanish?"No se pudo guardar el regalo. Inténtalo de nuevo.":"Couldn't save your gift. Please try again.");}
        }
        void ShowLobbyDailyGiftReward(DailyGiftReceipt receipt)
        {
            var box=LobbyModal("Daily gift reward",GameText.Spanish?"RECOMPENSA":"REWARD",215,530,CloseModal);
            HeaderImage(box,receipt.Currency.ToString().ToLowerInvariant().TrimEnd('s'),new Rect(184,112,124,124));
            var amount=LobbyNumber(box,"+"+receipt.Amount,new Rect(24,259,444,70),48,LobbyPaper);
            amount.name="Daily gift reward amount";
            var currency=LobbyNumber(box,receipt.Currency.ToString(),new Rect(24,332,444,36),25,LobbyPaper);
            currency.name="Daily gift reward currency";
            var close=LobbyButton(box,"Close daily gift reward",new Rect(72,400,348,86),CloseModal,true);
            Label(close,GameText.Spanish?"CONTINUAR":"CONTINUE",new Rect(8,8,332,70),28,LobbyInk,FontStyle.Bold);
        }
        void DrawLobbyGift(Transform parent,Rect bounds,bool canOpen)
        {
            var root=HitArea(parent,"Gift artwork",bounds);
            // An Inspector assignment can override the approved imported artwork.
            var sprite=DailyGiftPouchArtwork!=null?DailyGiftPouchArtwork:Resources.Load<Sprite>("MonsterPouch/UI/Lobby/Header/daily-pouch");
            Image(root,sprite,new Rect(0,0,bounds.width,bounds.height));
            var open=root.gameObject.AddComponent<Button>();open.interactable=canOpen;
            open.onClick.AddListener(()=>{PlaySound("button");ClaimLobbyDailyGift();});
        }
    }
}
