using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        string economyTutorialModule;
        RectTransform economyTutorialReturn;
        bool EconomyTutorialActive => !string.IsNullOrEmpty(economyTutorialModule) && Match != null && Match.Phase == MatchPhase.Menu;

        bool StartEconomyTutorial(string module)
        {
            if (module != GuestTutorialModules.ProgressionCollection && module != GuestTutorialModules.FirstMachine ||
                Match == null || Match.Phase != MatchPhase.Menu || guestProfileStore == null || guestProfile == null ||
                guestProfile.NextTutorialModuleId != module && !guestProfile.CompletedTutorialModules.Contains(module)) return false;
            economyTutorialModule = module; ShowEconomyTutorial(); return true;
        }
        void ClearEconomyTutorialReturn()
        {
            if (economyTutorialReturn == null) return;
            economyTutorialReturn.gameObject.SetActive(false); Destroy(economyTutorialReturn.gameObject); economyTutorialReturn = null;
        }
        void ShowEconomyTutorialReturn()
        {
            ClearEconomyTutorialReturn(); if (!EconomyTutorialActive) return;
            economyTutorialReturn = LobbyButton(CanvasRoot, "Return to economy tutorial", new Rect(76,866,388,70), ShowEconomyTutorial, true);
            LobbyNumber(economyTutorialReturn, GameText.Spanish ? "VOLVER AL TUTORIAL" : "BACK TO TUTORIAL", new Rect(8,8,372,54), 22, LobbyInk);
            economyTutorialReturn.SetAsLastSibling();
        }
        void ExitEconomyTutorial()
        { economyTutorialModule = null; ClearEconomyTutorialReturn(); CloseModal(); RenderPage(); }
        void EconomyTutorialError(Action retry)
        {
            ClearEconomyTutorialReturn();
            var box = LobbyModal("Economy tutorial retry", "TUTORIAL", 250,460,ExitEconomyTutorial);
            LobbyNumber(box, GameText.Spanish ? "No se pudo guardar el progreso." : "Couldn't save progress.", new Rect(24,104,444,100),25,LobbyPaper);
            var button = LobbyButton(box,"Retry economy tutorial",new Rect(84,304,324,82),retry,true);
            LobbyNumber(button,GameText.Spanish?"REINTENTAR":"RETRY",new Rect(8,8,308,66),26,LobbyInk);
        }
        bool ReloadEconomyTutorial()
        {
            guestProfile = guestProfileStore.Reload();
            return !guestProfileStore.NeedsRecovery && guestProfile != null;
        }
        void ShowEconomyTutorial()
        {
            if (!EconomyTutorialActive) return;
            ClearEconomyTutorialReturn();
            try { if (!ReloadEconomyTutorial()) { EconomyTutorialError(ShowEconomyTutorial); return; } }
            catch (Exception error) when (VendingStorageError(error)) { EconomyTutorialError(ShowEconomyTutorial); return; }
            bool collection = economyTutorialModule == GuestTutorialModules.ProgressionCollection;
            var box = LobbyModal("Economy tutorial", collection ? (GameText.Spanish?"TU COLECCIÓN":"YOUR COLLECTION") :
                (GameText.Spanish?"CAMINO DE COPAS":"TROPHY ROAD"),100,744,ExitEconomyTutorial);
            if (collection)
            {
                bool claimed = guestProfile.TutorialPouches.Count != 0;
                bool opened = claimed && !string.IsNullOrEmpty(guestProfile.TutorialPouches[0].OpenedUtc);
                TutorialMenuButton(box,"Tutorial blue pouch",opened?(GameText.Spanish?"VER PREMIO":"VIEW REWARD"):
                    claimed?(GameText.Spanish?"ABRIR CASE":"OPEN CASE"):(GameText.Spanish?"RECLAMAR CASE":"CLAIM CASE"),100,ShowTutorialPouch,opened);
                TutorialMenuButton(box,"Tutorial deck",GameText.Spanish?"VER MALETA":"VIEW BRIEF",198,()=>
                { OpenDecks(); if(ObserveEconomyTutorialView("deck")) ShowEconomyTutorialReturn(); },guestProfile.TutorialMenuVisits.Contains("deck"));
                TutorialMenuButton(box,"Tutorial collection",GameText.Spanish?"VER PERSONAJES":"VIEW CHARACTERS",296,()=>
                { OpenCharacterPicker(-2); if(ObserveEconomyTutorialView("collection")) ShowEconomyTutorialReturn(); },guestProfile.TutorialMenuVisits.Contains("collection"));
                TutorialMenuButton(box,"Tutorial abilities",GameText.Spanish?"EXPLORAR HABILIDADES":"EXPLORE ABILITIES",394,()=>
                { ShowAbilityCard(new OwnedUnit(Match.Config.Get(guestProfile.Starter.Deck.Monster)),false,true,-1,ShowEconomyTutorial); },guestProfile.TutorialMenuVisits.Contains("ability"));
                TutorialMenuButton(box,"Tutorial wallet",GameText.Spanish?"VER MONEDAS":"VIEW CURRENCIES",492,ShowTutorialWallet,guestProfile.TutorialMenuVisits.Contains("wallet"));
                var next = TutorialMenuButton(box,"Continue economy tutorial",GameText.Spanish?"CONTINUAR":"CONTINUE",622,FinishEconomyTutorial,false);
                next.GetComponent<Button>().interactable = guestProfile.TutorialCollectionReady;
            }
            else
            {
                LobbyNumber(box,GameText.Spanish?"Avanza por el camino de copas, reclama hitos y descubre los Cases de cada facción.":
                    "Advance along the trophy road, claim milestones and discover each faction's Cases.",new Rect(32,130,428,170),24,LobbyPaper);
                TutorialMenuButton(box,"Tutorial trophy road",GameText.Spanish?"VER CAMINO":"VIEW ROAD",360,()=> {
                    try {OpenProgressionRoad();ShowEconomyTutorialReturn();}
                    catch(Exception error) when(VendingStorageError(error)){EconomyTutorialError(ShowEconomyTutorial);}
                },guestProfile.TutorialMachineReady);
                var next=TutorialMenuButton(box,"Continue road tutorial",GameText.Spanish?"CONTINUAR":"CONTINUE",550,FinishEconomyTutorial,false);
                next.GetComponent<Button>().interactable=guestProfile.TutorialMachineReady;
            }
        }
        RectTransform TutorialMenuButton(Transform parent,string name,string caption,float y,Action action,bool complete)
        {
            var button=LobbyButton(parent,name,new Rect(30,y,432,78),action,true);
            LobbyNumber(button,(complete?"✓ ":"")+caption,new Rect(10,8,412,62),23,LobbyInk);
            return button;
        }
        bool ObserveEconomyTutorialView(string view)
        {
            if (!EconomyTutorialActive || economyTutorialModule != GuestTutorialModules.ProgressionCollection) return true;
            try { guestProfile=guestProfileStore.RecordTutorialMenuVisit(view); return true; }
            catch(Exception error) when(VendingStorageError(error))
            { EconomyTutorialError(()=>{if(ObserveEconomyTutorialView(view))ShowEconomyTutorial();}); return false; }
        }
        void ShowTutorialPouch()
        {
            try
            {
                guestProfile=guestProfileStore.ClaimTutorialPouch(Match.Config,PouchRoomConfiguration());
                var pouch=guestProfile.TutorialPouches.Single();
                if(!string.IsNullOrEmpty(pouch.OpenedUtc)){ShowTutorialPouchReward();return;}
                var box=LobbyModal("Tutorial pouch",GameText.Spanish?"TU CASE":"YOUR CASE",200,568,ShowEconomyTutorial);
                HeaderImage(box,"daily-pouch",new Rect(120,100,252,252));
                var open=LobbyButton(box,"Open tutorial pouch",new Rect(84,422,324,82),OpenTutorialPouch,true);
                LobbyNumber(open,GameText.Spanish?"ABRIR":"OPEN",new Rect(8,8,308,66),28,LobbyInk);
            }
            catch(Exception error) when(VendingStorageError(error)){EconomyTutorialError(ShowTutorialPouch);}
        }
        void OpenTutorialPouch()
        {
            try { guestProfile=guestProfileStore.OpenTutorialPouch();RenderPage();ShowTutorialPouchReward(); }
            catch(Exception error) when(VendingStorageError(error)){EconomyTutorialError(OpenTutorialPouch);}
        }
        void ShowTutorialPouchReward()
        {
            var pouch=guestProfile.TutorialPouches.Single();
            var reward=guestProfile.WalletEvents.Single(e=>e.Id==pouch.Id).Amounts.Single();
            var box=LobbyModal("Tutorial pouch reward",GameText.Spanish?"RECOMPENSA":"REWARD",210,540,ShowEconomyTutorial);
            HeaderImage(box,VendingCurrencyIcon(reward.Currency),new Rect(180,102,132,132));
            LobbyNumber(box,"+"+reward.Amount,new Rect(24,258,444,70),48,LobbyPaper).name="Tutorial reward amount";
            LobbyNumber(box,VendingCurrencyName(reward.Currency),new Rect(24,332,444,36),25,LobbyPaper);
            TutorialMenuButton(box,"Continue tutorial reward",GameText.Spanish?"CONTINUAR":"CONTINUE",420,ShowEconomyTutorial,false);
        }
        void ShowTutorialWallet()
        {
            try
            {
                if(!ReloadEconomyTutorial()){EconomyTutorialError(ShowTutorialWallet);return;}
                var box=LobbyModal("Tutorial wallet",GameText.Spanish?"TUS MONEDAS":"YOUR CURRENCIES",180,620,ShowEconomyTutorial);
                var currencies=new[]{PermanentCurrency.Earthcoins,PermanentCurrency.Suncoins,PermanentCurrency.Saturncoins};
                for(int i=0;i<currencies.Length;i++)
                {
                    HeaderImage(box,VendingCurrencyIcon(currencies[i]),new Rect(64,112+i*104,60,60));
                    LobbyNumber(box,guestProfile.FreeBalance(currencies[i])+" "+VendingCurrencyName(currencies[i]),new Rect(140,112+i*104,320,60),26,LobbyPaper,TextAnchor.MiddleLeft);
                }
                LobbyNumber(box,GameText.Spanish?"Se conservan entre partidas.":"Kept between matches.",new Rect(24,432,444,44),22,LobbyPaper);
                TutorialMenuButton(box,"Continue tutorial wallet",GameText.Spanish?"CONTINUAR":"CONTINUE",512,ShowEconomyTutorial,false);
                ObserveEconomyTutorialView("wallet");
            }
            catch(Exception error) when(VendingStorageError(error)){EconomyTutorialError(ShowTutorialWallet);}
        }
        void ShowTutorialPreviousDraw(GuestWalletEvent receipt)
        {
            var catalogue=VendingCatalogueOverride??Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue");
            var entry=catalogue.Machines.FirstOrDefault(e=>e.Configuration.Definition.MachineId==receipt.MachineId);
            if(entry==null){EconomyTutorialError(ShowEconomyTutorial);return;}
            ShowVendingReward(entry,receipt);
        }
        void ContinueTutorialMachineReward(PlayerMachineEntry entry,GuestWalletEvent receipt)
        {
            if(!EconomyTutorialActive||economyTutorialModule!=GuestTutorialModules.FirstMachine){ShowVendingMachine(entry);return;}
            try { guestProfile=guestProfileStore.RecordTutorialMachineReward(receipt.Id);FinishEconomyTutorial(); }
            catch(Exception error) when(VendingStorageError(error)){EconomyTutorialError(()=>ContinueTutorialMachineReward(entry,receipt));}
        }
        void FinishEconomyTutorial()
        {
            try
            {
                var status=guestProfileStore.CompleteTutorialModule(economyTutorialModule);
                if(status!=LocalGuestProfileStore.TutorialStepResult.Completed&&status!=LocalGuestProfileStore.TutorialStepResult.AlreadyCompleted)
                {EconomyTutorialError(FinishEconomyTutorial);return;}
                guestProfile=guestProfileStore.Reload();economyTutorialModule=null;ClearEconomyTutorialReturn();ContinueTutorialFromMenu();
            }
            catch(Exception error) when(VendingStorageError(error)){EconomyTutorialError(FinishEconomyTutorial);}
        }
    }
}


