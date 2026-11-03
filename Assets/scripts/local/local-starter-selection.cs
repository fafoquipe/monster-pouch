using System;
using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        string starterCandidateId,starterSelectionError;
        bool starterSelectionComplete;
        bool tutorialLaunchQueued;
        bool StarterSelectionOpen=>modal!=null&&modal.name=="Starter selection";
        bool CanResumeFirstContactTutorial=>guestProfile!=null&&guestProfileError==null&&
            guestProfile.OnboardingStage==GuestOnboardingStage.Tutorial&&
            !guestProfile.CompletedTutorialModules.Contains("first-contact")&&starterSelectionComplete;

        bool RenderStarterSelection()
        {
            if(guestProfile==null||!guestProfile.OnboardingRequired||guestProfile.HasLegacyAccess||guestProfileError!=null)
                return false;
            if(starterSelectionComplete){QueuePendingTutorial();return false;}
            // Guests created before this screen may already have customized decks.
            // Their choices take precedence over a new introduction.
            if(guestProfile.CollectionMode==GuestCollectionMode.Legacy&&guestProfile.Starter==null&&deckStorageLoaded&&deckRevision!=0)
            {starterSelectionComplete=true;return false;}
            if(guestProfile.Starter!=null&&starterSelectionError==null&&TryApplyStarterDeck())
            {QueuePendingTutorial();return false;}
            Backdrop(homeBackground);
            CloseModal();
            modal=Panel(CanvasRoot,"Starter selection",new Rect(0,0,540,960),new Color(.015f,.06f,.18f,.97f));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(DevelopmentAccountActive)
            {
                var dev=GameButton(modal,GameText.Spanish?"DEV · DESBLOQUEAR TODO":"DEV · UNLOCK ALL",new Rect(270,20,250,54),()=>ShowDevelopmentAccountConfirmation(false),false,16);
                dev.name="Starter development unlock all";
            }
#endif
            var box=BluePanel(modal,"Starter panel",new Rect(16,94,508,772),paper);
            Label(box,GameText.Spanish?"ELIGE TU MONSTER":"CHOOSE YOUR MONSTER",new Rect(16,20,476,62),29,blueFrame,FontStyle.Bold);
            Label(box,GameText.Spanish?"Tu primer equipo comienza aquí.":"Your first team starts here.",new Rect(22,86,464,42),19,blueFrame);
            string selected=guestProfile.Starter?.Deck.Monster??starterCandidateId;
            for(int i=0;i<StarterRoster.Monsters.Count;i++)
            {
                string id=StarterRoster.Monsters[i];
                var definition=Match.Config.Get(id);
                var card=BluePanel(box,"Starter choice "+id,new Rect(20+i*244,146,224,274),selected==id?brightBlue:blueFrame);
                if(selected==id)PixelBorder(card,gold,4);
                Figure(card,id,new Rect(20,12,184,190),false);
                Label(card,definition?.DisplayName??"",new Rect(8,211,208,48),25,Color.white,FontStyle.Bold);
                var button=card.gameObject.AddComponent<Button>();
                button.interactable=guestProfile.Starter==null&&definition!=null;
                button.onClick.AddListener(()=>{if(guestProfile.Starter!=null)return;starterCandidateId=id;starterSelectionError=null;PlaySound("button");RenderPage();});
            }
            var starterWhelps=(guestProfile.Starter?.Deck.Whelps??StarterRoster.Whelps.ToArray()).Where(id=>!string.IsNullOrEmpty(id)).ToArray();
            Label(box,(GameText.Spanish?"TUS WHELPS":"YOUR WHELPS")+" · "+starterWhelps.Length,new Rect(16,443,476,40),21,blueFrame,FontStyle.Bold);
            float whelpWidth=Mathf.Min(104,(484-6*Mathf.Max(0,starterWhelps.Length-1))/Mathf.Max(1,starterWhelps.Length));
            float whelpLeft=(508-(starterWhelps.Length*whelpWidth+6*Mathf.Max(0,starterWhelps.Length-1)))*.5f;
            for(int i=0;i<starterWhelps.Length;i++)
            {
                string id=starterWhelps[i];
                float x=whelpLeft+i*(whelpWidth+6);
                var slot=Panel(box,"Starter Whelp "+id,new Rect(x,495,whelpWidth,100),Color.clear,false);
                Portrait(slot,id,new Rect((whelpWidth-64)*.5f,0,64,65));
                Label(slot,Match.Config.Get(id)?.DisplayName??"",new Rect(0,69,whelpWidth,28),whelpWidth>=90?16:13,blueFrame,FontStyle.Bold);
            }
            if(starterSelectionError!=null)
                Label(box,StarterSelectionMessage(),new Rect(20,602,468,62),16,blueFrame);
            string label=starterSelectionError!=null?(GameText.Spanish?"REINTENTAR":"RETRY"):(GameText.Spanish?"CONFIRMAR":"CONFIRM");
            var confirm=GameButton(box,label,new Rect(74,680,360,68),ConfirmStarterSelection,true,23);
            confirm.name=starterSelectionError!=null?"Retry starter selection":"Confirm starter selection";
            confirm.GetComponent<Button>().interactable=StarterRoster.IsChoice(selected);
            return true;
        }

        void ConfirmStarterSelection()
        {
            if(guestProfile==null||!guestProfile.OnboardingRequired||guestProfile.HasLegacyAccess||guestProfileError!=null)return;
            starterSelectionError=null;
            try
            {
                if(guestProfile.Starter==null)
                {
                    var result=guestProfileStore.ChooseStarter(Match.Config,starterCandidateId);
                    if(result==LocalGuestProfileStore.SelectionResult.InvalidCatalogue||result==LocalGuestProfileStore.SelectionResult.InvalidStarter)
                    {starterSelectionError="catalogue";RenderPage();return;}
                    if(result==LocalGuestProfileStore.SelectionResult.RecoveryRequired)
                    {starterSelectionError="recovery";RenderPage();return;}
                    // Another session may have committed first: use its persisted choice.
                    guestProfile=guestProfileStore.OpenOrCreate(false);
                    if(result==LocalGuestProfileStore.SelectionResult.NotRequired)
                    {starterSelectionComplete=true;CloseModal();RenderPage();return;}
                }
                TryApplyStarterDeck();
            }
            catch(Exception error) when(IsDeckStorageError(error))
            {starterSelectionError=error is NotSupportedException?"version":"save";}
            if(starterSelectionComplete)CloseModal();
            RenderPage();
        }

        void QueuePendingTutorial()
        {
            if(tutorialLaunchQueued||guestProfile.OnboardingStage!=GuestOnboardingStage.Tutorial||
                !GuestTutorialModules.IsImplemented(guestProfile.NextTutorialModuleId))return;
            tutorialLaunchQueued=true;
            StartCoroutine(EnterPendingTutorial());
        }
        IEnumerator EnterPendingTutorial()
        {
            // Leave the menu render before the training session rebuilds the board.
            yield return null;
            if(Match.Phase==MatchPhase.Menu)StartNextTutorial();
        }

        bool TryApplyStarterDeck()
        {
            try
            {
                if(!deckStorageLoaded)LoadDeckStorage();
                if(!deckStorageLoaded){starterSelectionError="save";return false;}
                var result=deckStore.ApplyStarterSelection(guestProfile.ProfileId,guestProfile.Starter,deckRevision,Match.Config);
                if(result==LocalDeckStore.StarterApplyResult.Applied||result==LocalDeckStore.StarterApplyResult.AlreadyApplied)
                {
                    LoadDeckStorage();ApplyDeck();
                    if(deckStorageError!=null){starterSelectionError="save";return false;}
                    starterSelectionComplete=true;starterSelectionError=null;return true;
                }
                if(result==LocalDeckStore.StarterApplyResult.Conflict)
                {
                    // A pre-existing or concurrently edited deck wins. Do not trap
                    // its owner in a retry loop that can never replace revision 0.
                    deckAvailabilityNeedsConfirmation=true;
                    LoadDeckStorage();ApplyDeck();
                    if(deckStorageError!=null){starterSelectionError="save";return false;}
                    deckStorageError="conflict";starterSelectionComplete=true;
                    starterSelectionError=null;return true;
                }
                starterSelectionError=result==LocalDeckStore.StarterApplyResult.Conflict?"conflict":
                    result==LocalDeckStore.StarterApplyResult.RecoveryRequired?"recovery":"catalogue";
            }
            catch(Exception error) when(IsDeckStorageError(error))
            {starterSelectionError=error is NotSupportedException?"version":"save";}
            return false;
        }

        string StarterSelectionMessage()
        {
            if(starterSelectionError=="conflict")return GameText.Spanish?"Tus mazos cambiaron en otra sesión. Se han conservado.":"Your decks changed in another session. They have been preserved.";
            if(starterSelectionError=="catalogue")return GameText.Spanish?"No se pudo cargar el equipo inicial. Reintenta cuando termine la carga.":"The starter team could not be loaded. Please retry once loading finishes.";
            if(starterSelectionError=="recovery"||starterSelectionError=="version")return GameText.Spanish?"El guardado necesita recuperación o una versión más reciente. Tu elección se conserva.":"Your save needs recovery or a newer version. Your choice is preserved.";
            return GameText.Spanish?"No se pudo guardar. Reintenta para conservar tu elección.":"Saving failed. Retry to keep your choice.";
        }
    }
}
