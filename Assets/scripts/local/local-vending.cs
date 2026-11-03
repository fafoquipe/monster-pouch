using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Machines;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        readonly Dictionary<string,MachineDrawRequest> pendingVendingRequests=new Dictionary<string,MachineDrawRequest>(StringComparer.Ordinal);
        bool vendingBusy;
        internal static PlayerMachineCatalogue VendingCatalogueOverride { get; set; }

        // Daily receipts are immutable earned grants. Copy each by stable identity
        // into the spendable account exactly once; interrupted imports retry on read.
        bool SyncLobbyEarnedWallet()
        {
            if(guestProfileStore==null||guestProfile==null||dailyGiftAccount==null||dailyGiftStore==null||
                dailyGiftStore.NeedsRecovery||dailyGiftAccount.ProfileId!=guestProfile.ProfileId)return false;
            foreach(var receipt in dailyGiftAccount.Receipts)
            {
                var result=guestProfileStore.CreditFreeCurrency("daily-gift:"+receipt.Day,"daily-gift:"+receipt.TableId,
                    EarnedCurrency(receipt.Currency),receipt.Amount);
                if(result.Status!=GuestVendingStatus.Committed&&result.Status!=GuestVendingStatus.AlreadyApplied)return false;
                guestProfile=result.Profile;
            }
            // A different view may have spent currency since the last gift.
            guestProfile=guestProfileStore.Reload();return !guestProfileStore.NeedsRecovery;
        }
        long? LobbyEarnedBalance(DailyGiftCurrency currency)=>dailyGiftError==null&&guestProfile!=null?
            (long?)guestProfile.FreeBalance(EarnedCurrency(currency)):null;
        static PermanentCurrency EarnedCurrency(DailyGiftCurrency currency)
        {
            // Stable storage IDs predate the visible Earthkens/Sunkens/Saturnkens names.
            switch(currency)
            {
                case DailyGiftCurrency.Earthkens:return PermanentCurrency.Earthcoins;
                case DailyGiftCurrency.Sunkens:return PermanentCurrency.Suncoins;
                case DailyGiftCurrency.Saturnkens:return PermanentCurrency.Saturncoins;
                default:throw new ArgumentOutOfRangeException(nameof(currency));
            }
        }
        static string VendingCurrencyName(PermanentCurrency currency)=>currency==PermanentCurrency.Earthcoins?"Earthkens":
            currency==PermanentCurrency.Suncoins?"Sunkens":"Saturnkens";
        static string VendingCurrencyIcon(PermanentCurrency currency)=>currency==PermanentCurrency.Earthcoins?"earthken":
            currency==PermanentCurrency.Suncoins?"sunken":"saturnken";

        // Legacy entry points redirect to Cases; saved machine receipts remain readable.
        void ShowVendingCatalogue()=>ShowPouchRoom();
        RectTransform VendingScroll(Transform parent,string name,Rect rect,out RectTransform content,float height)
        {
            var viewport=HitArea(parent,name+" viewport",rect);viewport.gameObject.AddComponent<RectMask2D>();
            content=Panel(viewport,name+" content",new Rect(0,0,rect.width,Mathf.Max(rect.height,height)),Color.clear,false);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            return viewport;
        }
        void VendingBalance(Transform parent,Rect rect)
        {
            HeaderImage(parent,"saturnken",new Rect(rect.x,rect.y,48,48));
            LobbyNumber(parent,LobbyAccountSummary.Number(LobbyEarnedBalance(DailyGiftCurrency.Saturnkens)),
                new Rect(rect.x+58,rect.y,rect.width-58,48),29,LobbyPaper,TextAnchor.MiddleLeft).name="Vending Saturnkens balance";
        }
        void ShowVendingMachine(PlayerMachineEntry entry)
        { ShowPouchRoom(); }
        void VendingRewardPreview(RectTransform card,PlayerMachineConfiguration config,MachineReward reward,bool available)
        {
            Color textColor=available?LobbyPaper:new Color32(170,173,175,255);
            if(reward.Kind==MachineRewardKind.Resource)
            {
                var amounts=config.Packages.Single(p=>p.RewardId==reward.RewardId).Amounts;
                float column=200f/amounts.Length;
                for(int i=0;i<amounts.Length;i++)
                {
                    var amount=amounts[i];float left=10+i*column;
                    HeaderImage(card,VendingCurrencyIcon(amount.Currency),new Rect(left+(column-76)*.5f,6,76,76));
                    LobbyNumber(card,amount.Amount.ToString(CultureInfo.InvariantCulture),new Rect(left,88,column,48),30,textColor)
                        .name="Preview amount "+amount.Currency;
                    LobbyNumber(card,VendingCurrencyName(amount.Currency),new Rect(left,138,column,28),18,textColor)
                        .name="Preview currency "+amount.Currency;
                }
                return;
            }
            if(reward.Kind==MachineRewardKind.Ability)
            {
                var ability=Match.Config.Get(config.Definition.CharacterId).Tricks.Single(t=>t.Id==reward.AbilityId);
                var artwork=AbilityIllustration(card,ability,config.Definition.CharacterId,new Rect(72,12,76,76));
                artwork.name="Preview ability artwork";
                artwork.gameObject.AddComponent<AbilityAvailabilityVisual>().Configure(()=>available);
            }
            else Portrait(card,config.Definition.CharacterId,new Rect(70,10,80,80));
            string label=reward.Kind==MachineRewardKind.Character?(GameText.Spanish?"PERSONAJE":"CHARACTER"):
                (GameText.Spanish?"NUEVA HABILIDAD":"NEW ABILITY");
            LobbyNumber(card,label,new Rect(8,112,204,38),20,textColor).name="Preview category";
        }
        void ConfirmVendingDraw(PlayerMachineEntry entry)
        {
            if(vendingBusy)return;var cost=entry.Configuration.Cost;
            var box=LobbyModal("Confirm vending draw",GameText.Spanish?"CONFIRMAR":"CONFIRM",276,408,()=>ShowVendingMachine(entry));
            LobbyNumber(box,(GameText.Spanish?"¿Extraer por ":"Draw for ")+cost.Amount+" "+VendingCurrencyName(cost.Currency)+"?",
                new Rect(24,100,444,110),27,LobbyPaper);
            bool submitted=false;RectTransform accept=null;
            accept=LobbyButton(box,"Confirm spend Saturnkens",new Rect(84,260,324,82),()=>
            {
                if(submitted)return;submitted=true;accept.GetComponent<Button>().interactable=false;
                CommitVendingDraw(entry);
            },true);
            LobbyNumber(accept,GameText.Spanish?"CONFIRMAR":"CONFIRM",new Rect(8,8,308,66),28,LobbyInk);
        }
        void CommitVendingDraw(PlayerMachineEntry entry)
        {
            if(vendingBusy)return;vendingBusy=true;
            GuestWalletEvent committed=null;
            try
            {
                ReadLobbyDailyGifts();if(dailyGiftError!=null){ShowVendingError();return;}
                var config=entry.Configuration;string id=config.Definition.MachineId;
                if(!pendingVendingRequests.TryGetValue(id,out var request))
                {
                    var preview=guestProfileStore.PreviewMachine(Match.Config,config);
                    if(preview.Status!=GuestVendingStatus.Ready){vendingBusy=false;ShowVendingMachine(entry);return;}
                    request=new MachineDrawRequest{RequestId=Guid.NewGuid().ToString("N"),MachineId=id,
                        CatalogVersion=config.Definition.CatalogVersion,ExpectedRevision=preview.MachineRevision};
                    pendingVendingRequests.Add(id,request);
                }
                var result=guestProfileStore.DrawMachine(Match.Config,config,request);
                if(result.Status==GuestVendingStatus.Committed||result.Status==GuestVendingStatus.AlreadyApplied)
                {
                    committed=result.Receipt;guestProfile=result.Profile;
                    RefreshDeckAvailability();RenderPage();ShowVendingReward(entry,result.Receipt);
                    pendingVendingRequests.Remove(id);
                }
                else
                {
                    // Keep an ambiguous I/O retry's identity. Definite rejections
                    // can be refreshed and confirmed again against a current preview.
                    pendingVendingRequests.Remove(id);vendingBusy=false;ShowVendingMachine(entry);
                }
            }
            catch(Exception error) when(VendingStorageError(error))
            {
                if(committed!=null)
                {
                    ShowVendingReward(entry,committed);
                    pendingVendingRequests.Remove(entry.Configuration.Definition.MachineId);
                }
                else ShowVendingError();
            }
            finally{vendingBusy=false;}
        }
        void ShowVendingReward(PlayerMachineEntry entry,GuestWalletEvent receipt)
        {
            ClearEconomyTutorialReturn();
            var config=entry.Configuration;var reward=config.Definition.Rewards.Single(r=>r.RewardId==receipt.Draw.RewardId);
            var box=LobbyModal("Vending committed reward",GameText.Spanish?"OBTENIDO":"OBTAINED",188,584,()=>ContinueTutorialMachineReward(entry,receipt));
            var artwork=Panel(box,"Confirmed reward artwork",new Rect(146,92,200,226),Color.clear,false);
            if(reward.Kind==MachineRewardKind.Resource)
            {
                var package=config.Packages.Single(p=>p.RewardId==reward.RewardId);
                for(int i=0;i<package.Amounts.Length;i++)HeaderImage(artwork,VendingCurrencyIcon(package.Amounts[i].Currency),
                    new Rect(55+(i-(package.Amounts.Length-1)*.5f)*88,68,88,88));
            }
            else if(reward.Kind==MachineRewardKind.Ability)
            {
                var ability=Match.Config.Get(config.Definition.CharacterId).Tricks.Single(t=>t.Id==reward.AbilityId);
                AbilityIllustration(artwork,ability,config.Definition.CharacterId,new Rect(24,34,152,152)).name="Confirmed ability icon";
            }
            else Figure(artwork,config.Definition.CharacterId,new Rect(24,24,152,178),false);
            LobbyNumber(box,VendingRewardName(config,reward),new Rect(24,330,444,90),26,LobbyPaper);
            var close=LobbyButton(box,"Continue after vending reward",new Rect(84,462,324,82),()=>ContinueTutorialMachineReward(entry,receipt),true);
            LobbyNumber(close,GameText.Spanish?"CONTINUAR":"CONTINUE",new Rect(8,8,308,66),28,LobbyInk);
            StartCoroutine(VendingRewardArrival(artwork));
        }
        IEnumerator VendingRewardArrival(RectTransform artwork)
        {
            for(float age=0;age<.28f&&artwork!=null;age+=Time.unscaledDeltaTime)
            {artwork.localScale=Vector3.one*Mathf.Lerp(.85f,1f,Mathf.SmoothStep(0,1,age/.28f));yield return null;}
            if(artwork!=null)artwork.localScale=Vector3.one;
        }
        string VendingRewardName(PlayerMachineConfiguration config,MachineReward reward)
        {
            var character=Match.Config.Get(config.Definition.CharacterId);
            if(reward.Kind==MachineRewardKind.Character)return character.DisplayName;
            if(reward.Kind==MachineRewardKind.Ability)return GameText.Resolve(character.Tricks.Single(t=>t.Id==reward.AbilityId).Name);
            return string.Join(" + ",config.Packages.Single(p=>p.RewardId==reward.RewardId).Amounts.Select(a=>a.Amount+" "+VendingCurrencyName(a.Currency)));
        }
        string VendingStatusText(GuestVendingStatus? status)=>status==GuestVendingStatus.LegacyUnavailable?
            (GameText.Spanish?"Tu colección anterior se conserva.":"Your existing collection is preserved."):
            status==GuestVendingStatus.StarterRequired?(GameText.Spanish?"Elige primero tu Monster inicial.":"Choose your starter Monster first."):
            (GameText.Spanish?"No se pudo abrir la máquina. Inténtalo de nuevo.":"Couldn't open the machine. Please try again.");
        void ShowVendingError()=>ShowLobbyNotice(GameText.Spanish?"EXPENDEDORAS":"VENDING",
            GameText.Spanish?"No se pudo confirmar la extracción. Inténtalo de nuevo.":"Couldn't confirm the draw. Please try again.");
        static bool VendingStorageError(Exception error)=>error is IOException||error is InvalidDataException||error is UnauthorizedAccessException||
            error is ArgumentException||error is NotSupportedException||error is InvalidOperationException||error is OverflowException;
    }
}

