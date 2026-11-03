using System;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        string guidedModuleId,guidedSaveError,guidedWhelpId;
        bool guidedActive,guidedCompleted,guidedSaved;
        readonly bool[] guidedActions=new bool[4];
        PouchState guidedPlayer;
        int guidedInitialRerolls,guidedBankCoins,guidedBankCost,guidedBankCopies,guidedBankTricks;
        public bool GuidedTutorialActive=>guidedActive&&Match!=null&&Match.IsTraining&&
            Match.Phase!=MatchPhase.Menu&&ReferenceEquals(Match.Player,guidedPlayer);

        public bool StartGuidedTutorial(string moduleId)
        {
            if(!TryGuidedLesson(moduleId,out var lesson)||guestProfile==null||guestProfile.HasLegacyAccess||
                guestProfileError!=null||guestProfileStore==null||guestProfile.Starter==null||
                Match==null||Match.Phase!=MatchPhase.Menu||
                (guestProfile.NextTutorialModuleId!=moduleId&&!guestProfile.CompletedTutorialModules.Contains(moduleId)))return false;
            CloseModal();CloseBankContents();SelectedId=null;selectedOfferSlot=-1;
            guidedActive=false;guidedCompleted=guidedSaved=false;guidedSaveError=null;Array.Clear(guidedActions,0,guidedActions.Length);
            var starter=guestProfile.Starter.Deck;
            if(!Match.StartTraining(starter.Monster,starter.Whelps,lesson))return false;
            guidedModuleId=moduleId;guidedPlayer=Match.Player;guidedActive=true;
            guidedWhelpId=Match.TrainingWhelpId;guidedInitialRerolls=Match.Player.Rerolls;
            RenderPage();return true;
        }

        static bool TryGuidedLesson(string id,out TrainingLesson lesson)
        {
            switch(id)
            {
                case "positioning":lesson=TrainingLesson.Positioning;return true;
                case "attacks-health":lesson=TrainingLesson.AttacksHealth;return true;
                case "simple-combat":lesson=TrainingLesson.SimpleCombat;return true;
                case "preparation-economy":lesson=TrainingLesson.PreparationEconomy;return true;
                case "tactical-bank":lesson=TrainingLesson.TacticalBank;return true;
                default:lesson=TrainingLesson.FirstContact;return false;
            }
        }

        bool GuidedUsesBrief=>guidedModuleId=="preparation-economy"||guidedModuleId=="tactical-bank";
        int GuidedActionCount=>guidedModuleId=="attacks-health"?4:3;
        int GuidedCompletedActions=>guidedActions.Take(GuidedActionCount).Count(value=>value);
        int GuidedNextAction
        {
            get{for(int i=0;i<GuidedActionCount;i++)if(!guidedActions[i])return i;return GuidedActionCount;}
        }

        bool RenderGuidedTutorial()
        {
            if(!GuidedTutorialActive)return false;
            foreach(var pair in Match.Actors)CreateBar(pair.Value,pair.Key);
            if(GuidedUsesBrief)
            {
                RenderBrief();
                if(readyButton!=null)readyButton.gameObject.SetActive(false);
            }
            var header=Panel(page,"Guided tutorial",new Rect(16,14,508,82),ink,false);
            Label(header,GuidedTitle(),new Rect(12,8,260,66),21,gold,FontStyle.Bold,TextAnchor.MiddleLeft);
            var restart=Button(header,GameText.Spanish?"REPETIR":"RESTART",new Rect(280,7,106,68),RestartGuidedTutorial,ink,Color.white,16);
            restart.name="Restart guided tutorial";
            var exit=Button(header,GameText.Spanish?"SALIR":"EXIT",new Rect(390,7,106,68),ExitGuidedTutorial,ink,Color.white,17);
            exit.name="Exit guided tutorial";
            // Economy exercises keep the actual Brief and bank in their usual
            // positions; their instructions occupy the unused opposing half.
            float top=GuidedUsesBrief?100:CardTableArena.BriefTop+4;
            var guide=Panel(page,"Guided tutorial instructions",new Rect(16,top,508,204),ink,false);
            bool showMonster=!GuidedUsesBrief&&Match.Player.Monster.Location==UnitLocation.Brief;
            if(showMonster)
            {
                var monster=Match.Player.Monster;
                var card=HitArea(guide,"Guided tutorial Monster",new Rect(12,12,100,112));
                Portrait(card,monster.Definition.Id,new Rect(6,0,88,88));AddUnitGesture(card,monster,false);
                Label(card,monster.Definition.DisplayName,new Rect(0,90,100,22),16,gold,FontStyle.Bold);
                if(SelectedId==monster.Definition.Id)PixelBorder(card,gold,2);
            }
            Label(guide,GuidedInstruction(),new Rect(showMonster?132:16,8,showMonster?360:476,110),20,Color.white,FontStyle.Normal,TextAnchor.MiddleLeft);
            Label(guide,GuidedCompletedActions+" / "+GuidedActionCount,new Rect(16,144,84,40),19,gold,FontStyle.Bold);
            if(guidedCompleted)
            {
                string completedLabel=CanResumeTutorial?(GameText.Spanish?"CONTINUAR":"CONTINUE"):
                    (GameText.Spanish?"VOLVER AL MENÚ":"BACK TO MENU");
                var finish=Button(guide,guidedSaved?completedLabel:
                    (GameText.Spanish?"REINTENTAR GUARDADO":"RETRY SAVE"),new Rect(132,128,360,68),()=>
                    {
                        if(guidedSaved){guidedActive=false;guidedPlayer=null;ContinueTutorialFromMenu();}
                        else{SaveGuidedCompletion();RenderPage();}
                    },gold,ink,18);
                finish.name=guidedSaved?"Continue guided tutorial":"Retry guided tutorial";
            }
            else if((guidedModuleId=="attacks-health"&&guidedActions[0]&&guidedActions[1]||
                guidedModuleId=="simple-combat"&&guidedActions[0])&&Match.Phase==MatchPhase.Preparation)
            {
                var begin=Button(guide,GameText.Spanish?"COMENZAR COMBATE":"START COMBAT",new Rect(132,128,360,68),BeginGuidedCombat,gold,ink,18);
                begin.name="Begin guided combat";
            }
            else if(guidedModuleId=="simple-combat"&&Match.TrainingCombatFinished&&!Match.TrainingCombatWon)
            {
                var retry=Button(guide,GameText.Spanish?"VOLVER A INTENTAR":"TRY AGAIN",new Rect(132,128,360,68),RestartGuidedTutorial,gold,ink,18);
                retry.name="Retry guided combat";
            }
            return true;
        }

        string GuidedTitle()
        {
            switch(guidedModuleId)
            {
                case "positioning":return GameText.Spanish?"2 · POSICIONAMIENTO":"2 · POSITIONING";
                case "attacks-health":return GameText.Spanish?"3 · ATAQUES Y VIDA":"3 · ATTACKS AND HP";
                case "simple-combat":return GameText.Spanish?"4 · COMBATE SENCILLO":"4 · SIMPLE COMBAT";
                case "preparation-economy":return GameText.Spanish?"5 · PREPARACIÓN":"5 · PREPARATION";
                default:return GameText.Spanish?"6 · BANCO TÁCTICO":"6 · TACTICAL BANK";
            }
        }

        string GuidedInstruction()
        {
            if(guidedCompleted)
                return guidedSaveError!=null?(GameText.Spanish?"Práctica completada. No se pudo guardar; reintenta sin repetir las acciones.":
                    "Practice completed. Saving failed; retry without repeating the actions."):
                    (CanResumeTutorial?(GameText.Spanish?"Módulo completado. Puedes continuar con el siguiente paso.":"Module completed. You can continue to the next step."):
                        (GameText.Spanish?"Práctica completada. Puedes repasarla desde Ayuda.":"Practice completed. You can review it from Help."));
            int step=GuidedNextAction;
            switch(guidedModuleId)
            {
                case "positioning":
                    if(step==0)return GameText.Spanish?"Arrastra tu Monster a otra casilla iluminada de tu zona.":"Drag your Monster to another highlighted tile in your zone.";
                    if(step==1)return GameText.Spanish?"Intenta arrastrarlo a la zona enemiga. El tablero rechazará ese destino.":"Try dragging it into the opposing zone. The board will reject that destination.";
                    return GameText.Spanish?"Arrastra tu Monster sobre tu Whelp. Intercambian casillas: nunca se apilan dos unidades.":"Drag your Monster onto your Whelp. They swap tiles: two units never stack.";
                case "attacks-health":
                    if(Match.Player.Monster.Location!=UnitLocation.Field)return GameText.Spanish?"Toca tu Monster y una casilla iluminada para colocarlo y ver su alcance.":"Tap your Monster and a highlighted tile to place it and see its range.";
                    if(step==0)return GameText.Spanish?"Toca tu Monster en el tablero para ver su alcance de ataque.":"Tap your Monster on the board to see its attack range.";
                    if(step==1)return GameText.Spanish?"Mantén pulsado tu Monster para consultar su habilidad y cuándo se activa automáticamente.":"Hold your Monster to inspect its ability and when it activates automatically.";
                    if(step==2)return GameText.Spanish?"Cierra la información y comienza el combate. El objetivo de práctica no te atacará.":"Close the information and start combat. The practice target will not attack you.";
                    return GameText.Spanish?"Observa la vida del objetivo y la energía de tu Monster. Al llenarse, su habilidad se activa automáticamente.":"Watch the target's HP and your Monster's energy. When it fills, its ability activates automatically.";
                case "simple-combat":
                    if(Match.TrainingCombatFinished&&!Match.TrainingCombatWon)return GameText.Spanish?"Puedes volver a practicar. Cambia la posición de tu equipo antes del combate.":"You can try again. Change your team's position before combat.";
                    if(step==0)return GameText.Spanish?"Coloca tu Monster en una casilla válida para preparar el enfrentamiento.":"Place your Monster on a valid tile to prepare for the fight.";
                    if(step==1)return GameText.Spanish?"Comienza el combate contra el rival de entrenamiento.":"Start combat against the training opponent.";
                    return GameText.Spanish?"Tu equipo combate automáticamente. Gana el enfrentamiento para completar la práctica.":"Your team fights automatically. Win the encounter to complete the practice.";
                case "preparation-economy":
                    if(step==0)return GameText.Spanish?"Arrastra una oferta del Brief al campo para comprar tu Whelp con Moon-Kens temporales.":"Drag a Brief offer onto your field to buy your Whelp with temporary Moon-Kens.";
                    if(step==1)return GameText.Spanish?"Pulsa la flecha del Brief para rerollear las ofertas. Observa cómo baja el número de rerolls.":"Tap the Brief arrow to reroll its offers. Watch the reroll count decrease.";
                    return GameText.Spanish?"Compra otra copia del Whelp en campo. Esa copia mejora a la misma unidad; no crea otra.":"Buy another copy of your field Whelp. That copy upgrades the same unit; it does not create another.";
                default:
                    if(step==0)return GameText.Spanish?"Arrastra tu Whelp al banco. Recuperas lo invertido y conserva sus mejoras.":"Drag your Whelp into the bank. It refunds its investment and keeps its upgrades.";
                    if(step==1)return GameText.Spanish?"Toca el banco para abrirlo. Guarda una unidad por tipo y oculta sus otras copias del Brief.":"Tap the bank to open it. It holds one unit per type and hides that type's other Brief copies.";
                    return GameText.Spanish?"Arrastra el Whelp del banco a una casilla válida. El banco muestra el coste para volver al campo.":"Drag the banked Whelp onto a valid tile. The bank shows its deployment cost.";
            }
        }

        void BeginGuidedCombat()
        {
            if(!GuidedTutorialActive||guidedCompleted)return;
            int index=guidedModuleId=="attacks-health"?2:1;
            if(guidedModuleId=="attacks-health"&&(!guidedActions[0]||!guidedActions[1])||
                guidedModuleId=="simple-combat"&&!guidedActions[0])return;
            CloseModal();SelectedId=null;
            if(Match.BeginTrainingCombat()){guidedActions[index]=true;rebuild=true;}
        }

        void UpdateGuidedTutorial()
        {
            if(!GuidedTutorialActive||guidedCompleted)return;
            int before=GuidedCompletedActions;
            if(guidedModuleId=="attacks-health")
            {
                if(Match.Phase==MatchPhase.Preparation&&attackRangePreview!=null&&attackRangePreview.gameObject.activeInHierarchy&&
                    ReferenceEquals(rangeShownUnit,Match.Player.Monster))guidedActions[0]=true;
                if(guidedActions[2]&&Match.TrainingAttackCount>0&&Match.TrainingDamageDealt>0&&Match.TrainingAbilityCount>0)guidedActions[3]=true;
            }
            else if(guidedModuleId=="simple-combat")
            {
                if(guidedActions[1]&&Match.TrainingCombatFinished&&Match.TrainingCombatWon)guidedActions[2]=true;
            }
            else if(guidedModuleId=="preparation-economy")
            {
                if(Match.Player.Owned.TryGetValue(guidedWhelpId,out var unit)&&unit.Location==UnitLocation.Field)
                {
                    if(unit.Copies>=1)guidedActions[0]=true;
                    if(unit.Copies>=2&&unit.TrickCount>0)guidedActions[2]=true;
                }
                if(Match.Player.Rerolls<guidedInitialRerolls)guidedActions[1]=true;
            }
            else if(guidedModuleId=="tactical-bank"&&guidedActions[0]&&bankVisible&&
                Match.Player.FindOwned(guidedWhelpId,true) is OwnedUnit stored&&BankCopiesSuppressed(stored))
                guidedActions[1]=true;
            if(GuidedCompletedActions==GuidedActionCount)
            {
                guidedCompleted=true;
                // AbilityUsed marks the real start of the Super. Let the passive
                // target exercise keep running so its animation can finish.
                if(guidedModuleId!="attacks-health")Match.Pause(true);
                SaveGuidedCompletion();rebuild=true;
            }
            else if(GuidedCompletedActions!=before)rebuild=true;
        }

        void ShowInspectionFromGuidedTutorialHold(OwnedUnit unit,bool enemy)
        {
            ShowInspectionFromFirstContactHold(unit,enemy);
            if(GuidedTutorialActive&&!guidedCompleted&&guidedModuleId=="attacks-health"&&!enemy&&
                ReferenceEquals(unit,Match.Player.Monster)&&ReferenceEquals(inspected,unit)&&modal!=null)
            {guidedActions[1]=true;rebuild=true;}
        }

        public void DropWithGuidedTutorialGesture(OwnedUnit unit,Vector2 screen,GestureResult gesture)
        {
            bool watched=GuidedTutorialActive&&!guidedCompleted&&unit!=null;
            var location=unit?.Location??UnitLocation.Brief;
            var previous=unit?.Deployment??default;
            int coins=Match.Player?.Coins??0,copies=unit?.Copies??0,tricks=unit?.TrickCount??0;
            int bankRefund=unit?.SaleRefund??0;
            BoardCell target=null;OwnedUnit occupant=null;
            if(watched&&Match.Mapper.TryGetCell(GameCamera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,-GameCamera.transform.position.z)),out target))
                occupant=Match.Player.Owned.Values.FirstOrDefault(other=>other!=unit&&other.Location==UnitLocation.Field&&other.Deployment==target.Coordinates);
            DropWithFirstContactGesture(unit,screen,gesture);
            if(!watched||!GuidedTutorialActive||gesture!=GestureResult.Drag&&gesture!=GestureResult.Tap)return;
            if(guidedModuleId=="positioning"&&ReferenceEquals(unit,Match.Player.Monster)&&gesture==GestureResult.Drag)
            {
                if(location==UnitLocation.Field&&unit.Location==UnitLocation.Field&&unit.Deployment!=previous&&
                    Match.Config.IsDeploymentLegal(Match.LocalSide,unit.Deployment))guidedActions[0]=true;
                if(target!=null&&!Match.Config.IsDeploymentLegal(Match.LocalSide,target.Coordinates)&&unit.Location==location&&unit.Deployment==previous)
                    guidedActions[1]=true;
                if(occupant!=null&&target!=null&&unit.Deployment==target.Coordinates&&occupant.Deployment==previous&&
                    Match.Player.Owned.Values.Where(other=>other.Location==UnitLocation.Field).Select(other=>other.Deployment).Distinct().Count()==
                    Match.Player.Owned.Values.Count(other=>other.Location==UnitLocation.Field))guidedActions[2]=true;
            }
            else if(guidedModuleId=="simple-combat"&&ReferenceEquals(unit,Match.Player.Monster)&&unit.Location==UnitLocation.Field&&
                (location!=UnitLocation.Field||unit.Deployment!=previous)&&Match.Config.IsDeploymentLegal(Match.LocalSide,unit.Deployment))
                guidedActions[0]=true;
            else if(guidedModuleId=="tactical-bank"&&unit.Definition.Id==guidedWhelpId&&gesture==GestureResult.Drag)
            {
                if(location==UnitLocation.Field&&unit.Location==UnitLocation.Bench&&coins+bankRefund==Match.Player.Coins&&
                    copies==unit.Copies&&tricks==unit.TrickCount&&BankCopiesSuppressed(unit))
                {
                    guidedBankCoins=Match.Player.Coins;guidedBankCost=Match.Player.GetDeploymentCost(unit);
                    guidedBankCopies=copies;guidedBankTricks=tricks;guidedActions[0]=true;
                }
                if(guidedActions[0]&&guidedActions[1]&&location==UnitLocation.Bench&&unit.Location==UnitLocation.Field&&
                    Match.Player.Coins==guidedBankCoins-guidedBankCost&&unit.Copies==guidedBankCopies&&unit.TrickCount==guidedBankTricks)
                    guidedActions[2]=true;
            }
            UpdateGuidedTutorial();rebuild=true;
        }

        bool BankCopiesSuppressed(OwnedUnit unit)=>Match.Player.BankedUnits.Count(other=>other.Definition.Id==unit.Definition.Id)==1&&
            Match.Player.Offers.All(offer=>offer==null||offer.UnitId!=unit.Definition.Id);

        void SaveGuidedCompletion()
        {
            if(!GuidedTutorialActive||!guidedCompleted||guidedSaved)return;
            guidedSaveError=null;
            try
            {
                var result=guestProfileStore.CompleteTutorialModule(guidedModuleId);
                if(result==LocalGuestProfileStore.TutorialStepResult.Completed||result==LocalGuestProfileStore.TutorialStepResult.AlreadyCompleted)
                {guestProfile=guestProfileStore.OpenOrCreate(false);guidedSaved=true;return;}
                guidedSaveError="save";
            }
            catch(Exception error) when(IsDeckStorageError(error)){guidedSaveError="save";}
        }

        void ExitGuidedTutorial()
        {
            guidedActive=false;guidedPlayer=null;CloseModal();CloseBankContents();SelectedId=null;Match.Menu();RenderPage();
        }
        void RestartGuidedTutorial()
        {
            if(!GuidedTutorialActive)return;
            string module=guidedModuleId;ExitGuidedTutorial();StartGuidedTutorial(module);
        }
    }
}
