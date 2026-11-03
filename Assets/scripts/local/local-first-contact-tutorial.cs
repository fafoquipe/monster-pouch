using System;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        const string FirstContactModuleId="first-contact";
        bool firstContactActive,firstContactSaved;
        int firstContactStep;
        string firstContactSaveError;
        PouchState firstContactPlayer;
        public bool FirstContactTutorialActive=>firstContactActive&&Match!=null&&Match.IsTraining&&
            Match.Phase==MatchPhase.Preparation&&ReferenceEquals(Match.Player,firstContactPlayer);

        // Only the explicit onboarding/resume flow calls this. Opening the game
        // or opening an inspection never starts a tutorial by itself.
        public bool StartFirstContactTutorial()
        {
            if(guestProfile==null||guestProfile.HasLegacyAccess||guestProfileError!=null||
                guestProfile.OnboardingStage!=GuestOnboardingStage.Tutorial||guestProfile.Starter==null||
                guestProfileStore==null||Match==null||Match.Phase!=MatchPhase.Menu)return false;
            CloseModal();CloseBankContents();SelectedId=null;selectedOfferSlot=-1;
            firstContactStep=0;firstContactSaved=false;firstContactSaveError=null;firstContactActive=false;
            var selected=guestProfile.Starter.Deck;
            if(!Match.StartTraining(selected.Monster,selected.Whelps))return false;
            firstContactPlayer=Match.Player;firstContactActive=true;
            RenderPage();return true;
        }

        bool RenderFirstContactTutorial()
        {
            if(!FirstContactTutorialActive)return false;
            // This uses the same board and gestures as a match. Only the controls
            // outside this first exercise (combat, money, offers and bank) are absent.
            foreach(var pair in Match.Actors)CreateBar(pair.Value,pair.Key);
            var heading=Panel(page,"First contact tutorial",new Rect(16,14,508,82),ink,false);
            Label(heading,GameText.Spanish?"TUTORIAL · PRIMER CONTACTO":"TUTORIAL · FIRST CONTACT",
                new Rect(12,8,366,66),21,gold,FontStyle.Bold,TextAnchor.MiddleLeft);
            var close=Button(heading,GameText.Spanish?"SALIR":"EXIT",new Rect(390,7,106,68),ExitFirstContactTutorial,ink,Color.white,17);
            close.name="Exit first contact tutorial";
            var guide=Panel(page,"First contact instructions",new Rect(16,CardTableArena.BriefTop+4,508,204),ink,false);
            var monster=Match.Player.Monster;
            var portrait=HitArea(guide,"Tutorial Monster",new Rect(12,23,100,112));
            Portrait(portrait,monster.Definition.Id,new Rect(6,0,88,88));
            Label(portrait,monster.Definition.DisplayName,new Rect(0,90,100,22),16,gold,FontStyle.Bold);
            if(monster.Location!=UnitLocation.Field)AddUnitGesture(portrait,monster,false);
            else portrait.GetComponent<Image>().raycastTarget=false;
            if(SelectedId==monster.Definition.Id&&monster.Location!=UnitLocation.Field)PixelBorder(portrait,gold,2);
            Label(guide,FirstContactInstruction(),new Rect(132,12,360,106),20,Color.white,FontStyle.Normal,TextAnchor.MiddleLeft);
            string count=(Mathf.Min(firstContactStep,3))+" / 3";
            Label(guide,count,new Rect(16,153,84,30),18,gold,FontStyle.Bold);
            if(firstContactStep==3)
            {
                string text=firstContactSaved?(GameText.Spanish?"CONTINUAR":"CONTINUE"):
                    (GameText.Spanish?"REINTENTAR GUARDADO":"RETRY SAVE");
                var finish=Button(guide,text,new Rect(132,128,360,68),()=>
                {
                    if(firstContactSaved)ContinueTutorialFromMenu();
                    else{SaveFirstContactCompletion();RenderPage();}
                },gold,ink,17);
                finish.name=firstContactSaved?"Finish first contact tutorial":"Retry first contact tutorial";
            }
            return true;
        }

        string FirstContactInstruction()
        {
            if(firstContactStep==0)return GameText.Spanish?
                "Toca tu Monster y después una casilla iluminada para colocarlo.":
                "Tap your Monster, then tap a highlighted tile to place it.";
            if(firstContactStep==1)return GameText.Spanish?
                "Mantén pulsado tu Monster en el tablero para ver su información.":
                "Hold your Monster on the board to view its information.";
            if(firstContactStep==2)return GameText.Spanish?
                "Cierra la información y arrastra tu Monster a otra casilla iluminada.":
                "Close the information and drag your Monster to another highlighted tile.";
            if(firstContactSaveError!=null)return GameText.Spanish?
                "Completaste la práctica, pero no se pudo guardar. Reintenta; tus datos anteriores se conservan.":
                "You completed the practice, but saving failed. Retry; your previous data is preserved.";
            return GameText.Spanish?
                "Primer contacto completado: colocaste, inspeccionaste y moviste tu Monster.":
                "First contact completed: you placed, inspected and moved your Monster.";
        }

        void ExitFirstContactTutorial()
        {
            firstContactActive=false;firstContactPlayer=null;
            CloseModal();CloseBankContents();SelectedId=null;Match.Menu();RenderPage();
        }

        // Called only by the existing board gesture's Hold branch. Directly
        // opening an info card is deliberately insufficient to complete the task.
        void ShowInspectionFromFirstContactHold(OwnedUnit unit,bool enemy)
        {
            ShowInspection(unit,enemy);
            if(!FirstContactTutorialActive||firstContactStep!=1||enemy||
                !ReferenceEquals(unit,Match.Player.Monster)||unit.Location!=UnitLocation.Field||
                !ReferenceEquals(inspected,unit)||modal==null)return;
            firstContactStep=2;rebuild=true;
        }

        // Called after the real pointer recognizer identifies Tap or Drag. A
        // rejected move, a stationary release, or a different unit never counts.
        void DropWithFirstContactGesture(OwnedUnit unit,Vector2 screen,GestureResult gesture)
        {
            bool watched=FirstContactTutorialActive&&ReferenceEquals(unit,Match.Player.Monster);
            bool wasField=unit!=null&&unit.Location==UnitLocation.Field;
            Vector2Int previous=unit!=null?unit.Deployment:default;
            Drop(unit,screen);
            if(!watched||!FirstContactTutorialActive||unit.Location!=UnitLocation.Field||
                !Match.Config.IsDeploymentLegal(Match.LocalSide,unit.Deployment)||
                (wasField&&unit.Deployment==previous))return;
            if(firstContactStep==0&&gesture==GestureResult.Tap)firstContactStep=1;
            else if(firstContactStep==2&&gesture==GestureResult.Drag&&wasField)
            {firstContactStep=3;SaveFirstContactCompletion();}
            rebuild=true;
        }

        void SaveFirstContactCompletion()
        {
            if(!FirstContactTutorialActive||firstContactStep!=3||firstContactSaved)return;
            firstContactSaveError=null;
            try
            {
                var result=guestProfileStore.CompleteTutorialModule(FirstContactModuleId);
                if(result==LocalGuestProfileStore.TutorialStepResult.Completed||
                    result==LocalGuestProfileStore.TutorialStepResult.AlreadyCompleted)
                {
                    guestProfile=guestProfileStore.OpenOrCreate(false);
                    firstContactSaved=true;return;
                }
                firstContactSaveError="save";
            }
            catch(Exception error) when(IsDeckStorageError(error))
            {firstContactSaveError="save";}
        }
    }
}
