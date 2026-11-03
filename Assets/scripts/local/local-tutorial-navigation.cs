using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        bool CanResumeTutorial=>guestProfile!=null&&guestProfileError==null&&
            guestProfile.OnboardingStage==GuestOnboardingStage.Tutorial&&starterSelectionComplete&&
            GuestTutorialModules.IsImplemented(guestProfile.NextTutorialModuleId);

        public bool StartNextTutorial()
        {
            if(!CanResumeTutorial||Match==null||Match.Phase!=MatchPhase.Menu)return false;
            string module=guestProfile.NextTutorialModuleId;
            if(module==GuestTutorialModules.ProgressionCollection||module==GuestTutorialModules.FirstMachine)return StartEconomyTutorial(module);
            return module==GuestTutorialModules.FirstContact?StartFirstContactTutorial():StartGuidedTutorial(module);
        }

        // Called only after a completed exercise has committed its receipt. The
        // next module starts in the same session; an ordinary Exit stays in Menu.
        void ContinueTutorialFromMenu()
        {
            firstContactActive=false;firstContactPlayer=null;
            CloseModal();CloseBankContents();SelectedId=null;
            Match.Menu();
            if(!StartNextTutorial())RenderPage();
        }

        void ShowTutorialReviewMenu()
        {
            if(Match==null||Match.Phase!=MatchPhase.Menu)return;
            if(guestProfile==null||guestProfileError!=null||guestProfile.Starter==null||
                guestProfile.OnboardingStage!=GuestOnboardingStage.Tutorial)
            {ShowLateTutorialReview();return;}
            CloseModal();
            modal=Panel(CanvasRoot,"Tutorial review",new Rect(0,0,540,960),new Color(.025f,.065f,.09f,.97f));
            Label(modal,GameText.Spanish?"TUTORIAL":"TUTORIAL",new Rect(32,106,476,58),30,gold,FontStyle.Bold);
            Label(modal,GameText.Spanish?"Repasa lo que has aprendido.":"Review what you have learned.",new Rect(32,168,476,40),18,Color.white);
            string[] labels=GameText.Spanish?
                new[]{"PRIMER CONTACTO","POSICIONAMIENTO","ATAQUES Y VIDA","COMBATE SENCILLO","PREPARACIÓN Y ECONOMÍA","BANCO TÁCTICO","COLECCIÓN Y CASE","CAMINO DE COPAS"}:
                new[]{"FIRST CONTACT","POSITIONING","ATTACKS AND HEALTH","SIMPLE COMBAT","PREPARATION AND ECONOMY","TACTICAL BANK","COLLECTION AND CASE","TROPHY ROAD"};
            for(int i=0;i<labels.Length;i++)
            {
                string module=GuestTutorialModules.OrderedIds[i];
                bool available=GuestTutorialModules.IsImplemented(module)&&
                    (guestProfile.CompletedTutorialModules.Contains(module)||guestProfile.NextTutorialModuleId==module);
                var button=Button(modal,labels[i],new Rect(48,214+i*64,444,58),()=>
                {
                    if(module==GuestTutorialModules.FirstContact)StartFirstContactTutorial();
                    else if(module==GuestTutorialModules.ProgressionCollection||module==GuestTutorialModules.FirstMachine)StartEconomyTutorial(module);
                    else StartGuidedTutorial(module);
                },ink,Color.white,19);
                button.name="Review tutorial "+module;button.GetComponent<Button>().interactable=available;
            }
            var later=Button(modal,GameText.Spanish?"PARTIDAS Y SALAS":"MATCHES AND ROOMS",new Rect(48,746,444,68),ShowLateTutorialReview,gold,ink,19);
            later.name="Review matches and rooms";
            Button(modal,GameText.Back,new Rect(48,832,444,68),ShowHelp,ink,Color.white,19);
        }
    }
}

