using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        readonly List<GuestPouchVictoryRequest> pendingPouchVictories=new List<GuestPouchVictoryRequest>();
        readonly HashSet<string> observedPouchVictories=new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string,string> pendingPouchOpenRequests=new Dictionary<string,string>(StringComparer.Ordinal);
        bool pouchRoomBusy;
        const int PouchRoomPageSize=5;
        int pouchRoomPage;
        string pouchRoomPageDay;

        GuestPouchRoomConfiguration PouchRoomConfiguration()
        {
            Resources.Load<GuestCaseRewardSettings>("MonsterPouch/Cases/CaseRewardSettings")?.Apply();
            var asset=Resources.Load<TextAsset>("MonsterPouch/UI/Lobby/Header/DailyGifts");
            var table=asset!=null?JsonUtility.FromJson<DailyGiftTable>(asset.text):null;
            if(table==null)throw new InvalidDataException("Missing approved pouch resource table.");
            table.Validate();
            return new GuestPouchRoomConfiguration{
                ApprovalId="approved-pouch-room-2026-10-24",ResourceTableId=table.Id,
                Resources=table.Rewards.Select(r=>new GuestPouchResourceReward{
                    Currency=EarnedCurrency(r.Currency),Amount=r.Amount,Chances=r.Chances}).ToArray()};
        }

        // Called by the existing Match.Changed subscription. Only a live full-match
        // victory enters this queue; saved XP results are never replayed as rewards.
        void UpdatePouchRoomVictory(bool captureResult=true)
        {
            if(guestProfileStore==null||guestProfile==null||guestProfileError!=null)return;
            if(captureResult&&!Match.IsTraining&&Match.Phase==MatchPhase.MatchResult&&Match.WinsToWin==3&&
                Match.PlayerWins==3&&Match.BotWins>=0&&Match.BotWins<3&&
                !string.IsNullOrWhiteSpace(Match.SessionId)&&observedPouchVictories.Add(Match.SessionId))
                pendingPouchVictories.Add(new GuestPouchVictoryRequest{
                    MatchId=Match.SessionId,SourceModeId=Match.IsOnline?"online":"bot",
                    Completed=true,Won=true,CompletedUtc=DateTime.UtcNow});
            while(pendingPouchVictories.Count>0)
            {
                try
                {
                    var result=guestProfileStore.RecordPouchVictory(Match.Config,PouchRoomConfiguration(),pendingPouchVictories[0],DateTime.UtcNow);
                    if(result.Profile!=null)guestProfile=result.Profile;
                    if(result.Status==GuestPouchRoomStatus.RecoveryRequired||result.Status==GuestPouchRoomStatus.NeedsConfiguration||
                        result.Status==GuestPouchRoomStatus.MissingProfile||result.Status==GuestPouchRoomStatus.ClockRollback)return;
                    pendingPouchVictories.RemoveAt(0);
                }
                catch(Exception error) when(VendingStorageError(error)){return;}
            }
        }

        void ShowPouchRoom()
        {
            UpdatePouchRoomVictory(false);
            var box=LobbyModal("Pouch room panel","DAILY CASES",58,844,CloseModal);
            box.GetComponentInChildren<Button>().name="Close pouch room";
            GuestPouchRoomResult result;
            try
            {
                result=guestProfileStore?.ReadPouchRoom(Match.Config,PouchRoomConfiguration(),DateTime.UtcNow);
                if(result?.Profile!=null)guestProfile=result.Profile;
            }
            catch(Exception error) when(VendingStorageError(error)){result=null;}
            if(result?.Snapshot==null||result.Status!=GuestPouchRoomStatus.Ready)
            {
                string message=result?.Status==GuestPouchRoomStatus.LegacyUnavailable?
                    (GameText.Spanish?"Tu colección anterior se conserva.":"Your existing collection is preserved."):
                    (GameText.Spanish?"No se pudo cargar la sala. Inténtalo de nuevo.":"Couldn't load the room. Please try again.");
                LobbyNumber(box,message,new Rect(28,310,436,130),23,LobbyPaper);
                var retry=LobbyButton(box,"Retry pouch room",new Rect(90,500,312,84),ShowPouchRoom);
                LobbyNumber(retry,GameText.Spanish?"REINTENTAR":"RETRY",new Rect(8,8,296,68),26,LobbyPaper);
                return;
            }
            // Road acquisitions persist beyond the daily cycle and share the same
            // Cases inventory view. Reading this view never claims a milestone.
            var visible=result.Snapshot.Slots.Where(s=>s.Kind!=GuestPouchKind.Empty).ToList();
            foreach(var pouch in guestProfile.ProgressionPouches) {
                bool opened=guestProfile.ProgressionOpenings.Any(o=>o.PouchId==pouch.Id);
                bool available=pouch.CharacterOrder.Any(id=>!guestProfile.OwnsCharacter(id));
                visible.Add(new GuestPouchSlotSnapshot {PouchId=pouch.Id,WorldId=pouch.WorldId,
                    Kind=pouch.Kind==GuestRoadRewardKind.FactionPouch?GuestPouchKind.Faction:GuestPouchKind.Blue,
                    Opened=opened,CanOpen=!opened,CanReceiveNewCharacter=available,CharacterChanceBasisPoints=available?2000:0});
            }
            int visibleCount=Math.Max(PouchRoomPageSize,((visible.Count+PouchRoomPageSize-1)/PouchRoomPageSize)*PouchRoomPageSize);
            while(visible.Count<visibleCount)visible.Add(new GuestPouchSlotSnapshot {Kind=GuestPouchKind.Empty});
            for(int i=0;i<visible.Count;i++)visible[i].Index=i;
            result.Snapshot.Slots=visible.ToArray();
            LobbyNumber(box,GameText.Spanish?"Gana partidas para conseguir Cases":"Win matches to earn Cases",new Rect(24,66,444,25),17,LobbyPaper);
            if(pouchRoomPageDay!=result.Snapshot.Day){pouchRoomPageDay=result.Snapshot.Day;pouchRoomPage=0;}
            int pages=Math.Max(1,(result.Snapshot.Slots.Length+PouchRoomPageSize-1)/PouchRoomPageSize);
            pouchRoomPage=Mathf.Clamp(pouchRoomPage,0,pages-1);
            var previous=LobbyButton(box,"Pouch room previous page",new Rect(24,96,110,44),()=>{pouchRoomPage--;ShowPouchRoom();});
            previous.GetComponent<Button>().interactable=pouchRoomPage>0;
            LobbyNumber(previous,"<",new Rect(0,0,110,44),25,LobbyPaper);
            LobbyNumber(box,(GameText.Spanish?"PÁGINA ":"PAGE ")+(pouchRoomPage+1)+" / "+pages,
                new Rect(144,96,204,44),20,LobbyPaper).name="Pouch room page number";
            var next=LobbyButton(box,"Pouch room next page",new Rect(358,96,110,44),()=>{pouchRoomPage++;ShowPouchRoom();});
            next.GetComponent<Button>().interactable=pouchRoomPage+1<pages;
            LobbyNumber(next,">",new Rect(0,0,110,44),25,LobbyPaper);
            foreach(var state in result.Snapshot.Slots.Skip(pouchRoomPage*PouchRoomPageSize).Take(PouchRoomPageSize))
            {
                int index=state.Index;string pouchId=state.PouchId;
                int row=index-pouchRoomPage*PouchRoomPageSize;
                var bounds=new Rect(24,152+row*132,444,120);
                RectTransform slot;
                if(state.CanOpen)
                {
                    bool submitted=false;
                    slot=LobbyButton(box,"Pouch room slot "+index,bounds,()=>{
                        if(submitted)return;submitted=true;OpenPouchRoomSlot(pouchId);});
                }
                else
                {
                    slot=Panel(box,"Pouch room slot "+index,bounds,Color.clear,false);
                    slot.gameObject.AddComponent<LobbyControlFace>().raycastTarget=false;
                }
                if(state.Kind==GuestPouchKind.Empty)
                {
                    LobbyNumber(slot,GameText.Spanish?"VACÍO":"EMPTY",new Rect(18,38,408,44),25,LobbyPaper).name="Pouch room empty "+index;
                    continue;
                }
                var sprite=state.Kind==GuestPouchKind.Faction?Resources.Load<Sprite>("MonsterPouch/Cases/Artwork/"+state.WorldId):Resources.Load<Sprite>(state.Kind==GuestPouchKind.Character?
                    "MonsterPouch/Pouches/Artwork/"+state.CharacterId:"MonsterPouch/UI/Lobby/Header/daily-pouch");
                if(sprite==null)sprite=Resources.Load<Sprite>("MonsterPouch/UI/Lobby/Header/daily-pouch");
                if(sprite!=null)
                {
                    var artwork=Panel(slot,"Pouch room artwork "+index,new Rect(14,8,104,104),Color.clear,false);
                    Image(artwork,sprite,new Rect(0,0,104,104),state.Opened?new Color(.55f,.55f,.55f,1):Color.white);
                }
                string name=state.Kind==GuestPouchKind.Faction?"CASE "+state.WorldId.ToUpperInvariant():state.Kind==GuestPouchKind.Character?Match.Config.Get(state.CharacterId)?.DisplayName:
                    (GameText.Spanish?"CASE BÁSICO":"BASIC CASE");
                var title=LobbyNumber(slot,name??"",new Rect(136,8,290,36),24,LobbyPaper,TextAnchor.MiddleLeft);
                title.resizeTextForBestFit=true;title.resizeTextMinSize=18;title.resizeTextMaxSize=24;
                if(state.Kind==GuestPouchKind.Faction) {
                    LobbyNumber(slot,state.CanReceiveNewCharacter?(GameText.Spanish?"20% personaje nuevo":"20% new character"):
                        (GameText.Spanish?"Colección completa · 100% recursos":"Collection complete · 100% resources"),new Rect(136,43,290,26),14,LobbyPaper,TextAnchor.MiddleLeft);
                    string world=state.WorldId;
                    var pool=LobbyButton(slot,"Case pool "+index,new Rect(260,76,166,34),()=>ShowCasePool(world));
                    LobbyNumber(pool,GameText.Spanish?"VER POOL":"VIEW POOL",new Rect(0,0,166,34),15,LobbyPaper);
                }
                LobbyNumber(slot,state.Opened?(GameText.Spanish?"ABIERTO":"OPENED"):(GameText.Spanish?"ABRIR":"OPEN"),
                    new Rect(136,76,124,34),22,state.Opened?new Color32(166,181,174,255):LobbyGold,TextAnchor.MiddleLeft);
            }
            StartCoroutine(RefreshPouchRoomAtMidnight(modal,result.Snapshot.Day));
        }

        IEnumerator RefreshPouchRoomAtMidnight(RectTransform owner,string day)
        {
            while(owner!=null&&modal==owner)
            {
                if(DateTime.UtcNow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)!=day){ShowPouchRoom();yield break;}
                yield return null;
            }
        }

        void OpenPouchRoomSlot(string pouchId)
        {
            if(pouchRoomBusy)return;pouchRoomBusy=true;
            GuestPouchOpenReceipt committed=null;
            try
            {
                if(guestProfile.ProgressionPouches.Any(p=>p.Id==pouchId)) {
                    var road=guestProfileStore.OpenProgressionPouch(Match.Config,PouchRoomConfiguration(),pouchId,DateTime.UtcNow);
                    if(road.Profile!=null)guestProfile=road.Profile;
                    if((road.Status==GuestProgressionStatus.Opened||road.Status==GuestProgressionStatus.AlreadyApplied)&&road.OpenReceipt!=null) {
                        committed=new GuestPouchOpenReceipt {PouchId=pouchId,CharacterId=road.OpenReceipt.CharacterId,
                            RewardKind=road.OpenReceipt.RewardKind,Amounts=road.OpenReceipt.Amounts,TimestampUtc=road.OpenReceipt.TimestampUtc};
                        RefreshDeckAvailability();RenderPage();ShowPouchRoomReward(committed);
                    } else ShowPouchRoom();
                    return;
                }
                if(!pendingPouchOpenRequests.TryGetValue(pouchId,out string requestId))
                {requestId=Guid.NewGuid().ToString("N");pendingPouchOpenRequests.Add(pouchId,requestId);}
                var result=guestProfileStore.OpenPouch(Match.Config,PouchRoomConfiguration(),pouchId,requestId,DateTime.UtcNow);
                if(result.Profile!=null)guestProfile=result.Profile;
                if((result.Status==GuestPouchRoomStatus.Opened||result.Status==GuestPouchRoomStatus.AlreadyApplied)&&result.OpenReceipt!=null)
                {
                    committed=result.OpenReceipt;
                    pendingPouchOpenRequests.Remove(pouchId);RefreshDeckAvailability();RenderPage();
                    ShowPouchRoomReward(committed);
                }
                else
                {pendingPouchOpenRequests.Remove(pouchId);ShowPouchRoom();}
            }
            catch(Exception error) when(VendingStorageError(error))
            {
                if(committed!=null)ShowPouchRoomReward(committed);
                else ShowLobbyNotice("CASES",
                        GameText.Spanish?"No se pudo confirmar la apertura. Inténtalo de nuevo.":"Couldn't confirm the opening. Please try again.");
            }
            finally{pouchRoomBusy=false;}
        }

        void ShowCasePool(string worldId)
        {
            var world=GuestProgressionCatalogue.GetWorld(worldId);
            if(world==null)return;
            var box=LobbyModal("Case faction pool","CASE "+worldId.ToUpperInvariant(),70,820,ShowPouchRoom);
            bool available=world.CharacterIds.Any(id=>!guestProfile.OwnsCharacter(id));
            LobbyNumber(box,available?(GameText.Spanish?"20% personaje nuevo · 80% recursos":"20% new character · 80% resources"):
                (GameText.Spanish?"Colección completa · 100% recursos":"Collection complete · 100% resources"),new Rect(20,78,452,46),18,LobbyPaper);
            LobbyNumber(box,GameText.Spanish?"Eronya 3:1 clásicos si quedan ambos grupos":"Eronya 3:1 classics while both groups remain",new Rect(20,124,452,42),15,LobbyPaper);
            var viewport=VendingScroll(box,"Case pool",new Rect(20,178,452,620),out var content,Mathf.CeilToInt(world.CharacterIds.Length/2f)*176);
            viewport.gameObject.AddComponent<CollectionGridWindow>().Configure(viewport.GetComponent<ScrollRect>(),world.CharacterIds,2,0,
                parent=>Panel(parent,"Case pool card",new Rect(0,0,226,176),Color.clear,false),
                (card,index)=> {
                    string id=world.CharacterIds[index];bool owned=guestProfile.OwnsCharacter(id);card.name="Case pool character "+id;
                    Figure(card,id,new Rect(53,0,120,120),false,color:owned?Color.white:Color.black);
                    string label=(Match.Config.Get(id)?.DisplayName??id);
                    LobbyNumber(card,label,new Rect(4,119,218,28),18,owned?LobbyGold:LobbyPaper);
                    LobbyNumber(card,owned?(GameText.Spanish?"OBTENIDO":"OWNED"):(GameText.Spanish?"BLOQUEADO":"LOCKED"),new Rect(4,145,218,25),14,LobbyPaper);
                },card=>{foreach(Transform child in card){child.gameObject.SetActive(false);Destroy(child.gameObject);}},null,
                columnStride:226,rowStride:176,cellWidth:226,cellHeight:176);
        }

        void ShowPouchRoomReward(GuestPouchOpenReceipt receipt)
        {
            var box=LobbyModal("Pouch room reward",GameText.Spanish?"RECOMPENSA":"REWARD",215,530,ShowPouchRoom);
            var artwork=Panel(box,"Pouch room reward artwork",new Rect(146,84,200,206),Color.clear,false);
            if(receipt.RewardKind==GuestPouchRewardKind.Character)
            {
                Figure(artwork,receipt.CharacterId,new Rect(24,12,152,178),false);
                LobbyNumber(box,Match.Config.Get(receipt.CharacterId)?.DisplayName??"",new Rect(24,300,444,64),30,LobbyPaper).name="Pouch room reward character";
            }
            else
            {
                var amount=receipt.Amounts.Single();
                HeaderImage(artwork,VendingCurrencyIcon(amount.Currency),new Rect(38,28,124,124));
                LobbyNumber(box,"+"+amount.Amount,new Rect(24,259,444,70),48,LobbyPaper).name="Pouch room reward amount";
                LobbyNumber(box,VendingCurrencyName(amount.Currency),new Rect(24,332,444,36),25,LobbyPaper).name="Pouch room reward currency";
            }
            var close=LobbyButton(box,"Continue after pouch reward",new Rect(72,400,348,86),ShowPouchRoom,true);
            LobbyNumber(close,GameText.Spanish?"CONTINUAR":"CONTINUE",new Rect(8,8,332,70),28,LobbyInk);
            StartCoroutine(VendingRewardArrival(artwork));
        }
    }
}
