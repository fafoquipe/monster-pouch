using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        readonly Dictionary<string,string> pendingAbilityPurchases=new Dictionary<string,string>(StringComparer.Ordinal);
        bool permanentAbilityBusy;

        GuestAbilityPurchaseResult PreviewPermanentAbility(UnitDefinition definition,int index)
        {
            if(index<0||index>=definition.Tricks.Length||definition.Tricks[index]==null||guestProfileStore==null)return null;
            try
            {
                var preview=guestProfileStore.PreviewAbilityPurchase(Match.Config,definition.Id,definition.Tricks[index].Id);
                if(preview.Profile!=null)guestProfile=preview.Profile;
                return preview;
            }
            catch(Exception error) when(VendingStorageError(error)){return null;}
        }
        static bool CanReviewPermanentAbility(GuestAbilityPurchaseResult preview)=>preview!=null&&
            (preview.Status==GuestAbilityPurchaseStatus.Ready||preview.Status==GuestAbilityPurchaseStatus.InsufficientFunds);

        void SetPermanentAbilityButton(RectTransform button,int index,GuestAbilityPurchaseResult preview)
        {
            bool owned=preview?.Status==GuestAbilityPurchaseStatus.AlreadyOwned;
            string caption=index<0?(GameText.Spanish?"HABILIDAD BASE":"BASE ABILITY"):
                owned?(GameText.Spanish?"DESBLOQUEADA":"UNLOCKED"):
                preview?.Status==GuestAbilityPurchaseStatus.CharacterLocked?(GameText.Spanish?"PERSONAJE BLOQUEADO":"CHARACTER LOCKED"):
                (GameText.Spanish?"DESBLOQUEAR":"UNLOCK");
            SetMoonkenButtonCost(button,caption,null);
            var costs=button.Find("Permanent ability costs") as RectTransform;
            bool show=index>=0&&!owned;
            if(show&&costs==null)
            {
                costs=Panel(button,"Permanent ability costs",new Rect(264,12,208,44),Color.clear,false);
                PermanentAbilityCost(costs,"earthken",GuestPermanentEconomy.AbilityEarthCost,new Rect(0,0,98,44),blueFrame);
                PermanentAbilityCost(costs,"sunken",GuestPermanentEconomy.AbilitySunCost,new Rect(106,0,98,44),blueFrame);
            }
            if(costs!=null)costs.gameObject.SetActive(show);
            if(show)SetRect(button.Find("Text").GetComponent<RectTransform>(),new Rect(12,4,244,60));
        }
        void PermanentAbilityCost(Transform parent,string currency,long amount,Rect rect,Color textColor)
        {
            var row=Panel(parent,"Permanent cost "+currency,rect,Color.clear,false);
            HeaderImage(row,currency,new Rect(0,4,36,36));
            var number=Label(row,amount.ToString(),new Rect(40,0,rect.width-40,rect.height),22,textColor,FontStyle.Bold);
            number.name="Permanent cost value";
        }
        void PermanentAbilityBadge(Transform parent,UnitDefinition definition,string abilityId,Rect rect)
        {
            if(guestProfile?.OwnsAbility(definition.Id,abilityId)==true)
            {
                var check=Label(parent,"✓",rect,25,blueFrame,FontStyle.Bold);check.name="Permanent ability owned";check.font=symbolFont;return;
            }
            // Reuse the collection's closed padlock geometry and palette.
            var padlock=Panel(parent,"Permanent ability locked",new Rect(rect.x+5,rect.y,30,40),Color.clear,false);
            Panel(padlock,"Shackle border",new Rect(4,0,22,24),ink,false);
            Panel(padlock,"Closed shackle",new Rect(7,3,16,18),paper,false);
            Panel(padlock,"Shackle opening",new Rect(11,7,8,14),ink,false);
            var body=Panel(padlock,"Lock body",new Rect(0,16,30,24),paper,false);PixelBorder(body,ink,2);
            Panel(body,"Keyhole",new Rect(13,8,4,8),ink,false);
        }
        string PermanentAbilityStatus(GuestAbilityPurchaseStatus? status)
        {
            if(status==GuestAbilityPurchaseStatus.Ready)return GameText.Spanish?"Desbloqueo permanente":"Permanent unlock";
            if(status==GuestAbilityPurchaseStatus.InsufficientFunds)return GameText.Spanish?"Saldo insuficiente":"Insufficient balance";
            if(status==GuestAbilityPurchaseStatus.AlreadyOwned)return GameText.Spanish?"Desbloqueada":"Unlocked";
            if(status==GuestAbilityPurchaseStatus.CharacterLocked)return GameText.Spanish?"Consigue primero este personaje":"Obtain this character first";
            return GameText.Spanish?"No se pudo cargar. Inténtalo de nuevo.":"Couldn't load. Please try again.";
        }
        void ConfirmPermanentAbility(OwnedUnit owned,int index,Action returnToPreview)
        {
            if(permanentAbilityBusy||Match==null||Match.Phase!=MatchPhase.Menu)return;
            var preview=PreviewPermanentAbility(owned.Definition,index);
            if(!CanReviewPermanentAbility(preview))
            {ShowAbilityCard(owned,false,true,index,returnToPreview);return;}
            Action back=()=>ShowAbilityCard(owned,false,true,index,returnToPreview);
            var box=LobbyModal("Confirm permanent ability",GameText.Spanish?"DESBLOQUEAR":"UNLOCK",200,560,back);
            var ability=owned.Definition.Tricks[index];
            AbilityIllustration(box,ability,owned.Definition.Id,new Rect(40,100,92,92));
            LobbyNumber(box,GameText.AbilityName(ability),new Rect(152,104,310,88),25,LobbyPaper,TextAnchor.MiddleLeft);
            PermanentAbilityCost(box,"earthken",GuestPermanentEconomy.AbilityEarthCost,new Rect(94,224,136,44),LobbyPaper);
            PermanentAbilityCost(box,"sunken",GuestPermanentEconomy.AbilitySunCost,new Rect(268,224,136,44),LobbyPaper);
            LobbyNumber(box,PermanentAbilityStatus(preview.Status),new Rect(24,300,444,54),21,LobbyPaper).name="Permanent purchase status";
            bool submitted=false;RectTransform accept=null;
            accept=LobbyButton(box,"Confirm permanent ability purchase",new Rect(84,414,324,82),()=>
            {
                if(submitted||preview.Status!=GuestAbilityPurchaseStatus.Ready)return;
                submitted=true;accept.GetComponent<Button>().interactable=false;
                CommitPermanentAbility(owned,index,returnToPreview);
            },true);
            accept.GetComponent<Button>().interactable=preview.Status==GuestAbilityPurchaseStatus.Ready;
            LobbyNumber(accept,GameText.Spanish?"CONFIRMAR":"CONFIRM",new Rect(8,8,308,66),28,LobbyInk);
        }
        void CommitPermanentAbility(OwnedUnit owned,int index,Action returnToPreview)
        {
            if(permanentAbilityBusy||Match==null||Match.Phase!=MatchPhase.Menu||index<0||index>=owned.Definition.Tricks.Length)return;
            permanentAbilityBusy=true;
            string key=owned.Definition.Id+":"+owned.Definition.Tricks[index].Id;
            if(!pendingAbilityPurchases.TryGetValue(key,out string requestId))
            {requestId=Guid.NewGuid().ToString("N");pendingAbilityPurchases.Add(key,requestId);}
            try
            {
                var result=guestProfileStore.PurchaseAbility(Match.Config,requestId,owned.Definition.Id,owned.Definition.Tricks[index].Id);
                if(result.Profile!=null)guestProfile=result.Profile;
                bool confirmed=result.Status==GuestAbilityPurchaseStatus.Purchased||result.Status==GuestAbilityPurchaseStatus.AlreadyApplied||
                    result.Status==GuestAbilityPurchaseStatus.AlreadyOwned;
                if(confirmed)
                {
                    pendingAbilityPurchases.Remove(key);
                    RenderPage();ShowAbilityCard(owned,false,true,index,returnToPreview);
                }
                else if(result.Status==GuestAbilityPurchaseStatus.InsufficientFunds)
                {pendingAbilityPurchases.Remove(key);ConfirmPermanentAbilityAfterRejection(owned,index,returnToPreview);}
                else ShowPermanentAbilityRetry(owned,index,returnToPreview);
            }
            catch(Exception error) when(VendingStorageError(error))
            {
                // The same request survives an ambiguous write, so a retry cannot debit twice.
                ShowPermanentAbilityRetry(owned,index,returnToPreview);
            }
            finally{permanentAbilityBusy=false;}
        }
        void ConfirmPermanentAbilityAfterRejection(OwnedUnit owned,int index,Action returnToPreview)
        {
            permanentAbilityBusy=false;ConfirmPermanentAbility(owned,index,returnToPreview);
        }
        void ShowPermanentAbilityRetry(OwnedUnit owned,int index,Action returnToPreview)
        {
            var box=LobbyModal("Permanent ability retry",GameText.Spanish?"DESBLOQUEAR":"UNLOCK",260,440,
                ()=>ShowAbilityCard(owned,false,true,index,returnToPreview));
            LobbyNumber(box,GameText.Spanish?"No se pudo confirmar la compra.":"Couldn't confirm the purchase.",new Rect(24,100,444,100),24,LobbyPaper);
            bool submitted=false;
            var retry=LobbyButton(box,"Retry permanent ability purchase",new Rect(84,276,324,82),()=>
            {
                if(submitted)return;submitted=true;CommitPermanentAbility(owned,index,returnToPreview);
            },true);
            LobbyNumber(retry,GameText.Spanish?"REINTENTAR":"RETRY",new Rect(8,8,308,66),28,LobbyInk);
        }
    }
}
