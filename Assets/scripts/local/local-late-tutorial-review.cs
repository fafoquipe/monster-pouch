using System;
using System.IO;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Progression;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        string bo5ReviewSession,bo5ReviewCompletedSession;
        bool CanOpenLateTutorialReview=>Match!=null&&Match.Phase==MatchPhase.Menu&&
            online!=null&&!online.Active&&!online.Busy;
        bool CanOpenMultiplayerReview=>guestProfile?.OnboardingRequired!=true||HasSavedReviewBo5();

        // Optional review only. These screens never write tutorial completion or
        // bypass the sequential account onboarding modules.
        public void ShowLateTutorialReview()
        {
            if(!CanOpenLateTutorialReview)return;
            var box=RoomModal("Late tutorial review",GameText.Spanish?"REPASO":"REVIEW",224,510,CloseModal);
            Label(box,GameText.Spanish?"Practica una partida completa o consulta cómo jugar con un amigo.":
                "Play a complete match or learn how to play with a friend.",new Rect(24,80,444,90),21,Color.white);
            var bo5=GameButton(box,GameText.Spanish?"PARTIDA BO5":"BO5 MATCH",new Rect(24,194,444,76),ShowNormalBo5Review,true,23);
            bo5.name="Open BO5 review";
            var multiplayer=GameButton(box,GameText.Spanish?"SALAS DE AMIGOS":"FRIEND ROOMS",new Rect(24,290,444,76),ShowMultiplayerReview,false,23);
            multiplayer.name="Open multiplayer review";
            bool multiplayerAvailable=CanOpenMultiplayerReview;
            multiplayer.GetComponent<UnityEngine.UI.Button>().interactable=multiplayerAvailable;
            if(!multiplayerAvailable)Label(box,GameText.Spanish?"Completa y guarda primero un BO5 de este repaso.":
                "First finish and save a BO5 from this review.",new Rect(24,372,444,32),16,cyan);
            GameButton(box,GameText.Back,new Rect(114,408,264,68),CloseModal,false,22);
        }

        public void ShowNormalBo5Review()
        {
            if(!CanOpenLateTutorialReview)return;
            var box=RoomModal("BO5 review",GameText.Spanish?"PARTIDA BO5":"BO5 MATCH",164,650,ShowLateTutorialReview);
            Label(box,GameText.Spanish?
                "Gana tres rondas para vencer. Entre rondas puedes preparar tu equipo. Jugarás con tu mazo actual contra la IA de las partidas normales.":
                "Win three rounds to win the match. Prepare your team between rounds. Use your current deck against the normal match AI.",
                new Rect(24,82,444,146),21,Color.white);
            var status=Label(box,Bo5ReviewReceiptText(),new Rect(24,252,444,118),19,cyan);
            status.name="BO5 review saved experience";
            var play=GameButton(box,GameText.Spanish?"JUGAR BO5":"PLAY BO5",new Rect(24,396,444,70),StartBo5ReviewMatch,true,24);
            play.name="Start BO5 review";
            play.GetComponent<UnityEngine.UI.Button>().interactable=Match.WinsToWin==3;
            var xp=GameButton(box,GameText.Spanish?"VER EXPERIENCIA":"VIEW EXPERIENCE",new Rect(24,484,444,68),ShowProgression,false,22);
            xp.name="View BO5 experience";
            GameButton(box,GameText.Back,new Rect(114,564,264,68),ShowLateTutorialReview,false,20);
        }

        void StartBo5ReviewMatch()
        {
            if(!CanOpenLateTutorialReview||Match.WinsToWin!=3)return;
            Match.Changed-=CaptureBo5ReviewResult;
            bo5ReviewSession=bo5ReviewCompletedSession=null;
            StartChosenMatch();
            if(Match.IsTraining||Match.IsOnline||Match.Phase!=MatchPhase.Preparation||string.IsNullOrWhiteSpace(Match.SessionId))return;
            bo5ReviewSession=Match.SessionId;
            Match.Changed+=CaptureBo5ReviewResult;
        }

        void CaptureBo5ReviewResult()
        {
            if(string.IsNullOrEmpty(bo5ReviewSession))return;
            if(Match.IsTraining||Match.IsOnline||Match.SessionId!=bo5ReviewSession||Match.Phase==MatchPhase.Menu)
            {
                bo5ReviewSession=null;Match.Changed-=CaptureBo5ReviewResult;return;
            }
            if(Match.Phase!=MatchPhase.MatchResult||Match.WinsToWin!=3||
                Match.PlayerWins<3&&Match.BotWins<3)return;
            bo5ReviewCompletedSession=bo5ReviewSession;
            bo5ReviewSession=null;Match.Changed-=CaptureBo5ReviewResult;
        }

        string Bo5ReviewReceiptText()
        {
            if(string.IsNullOrEmpty(bo5ReviewCompletedSession))return GameText.Spanish?
                "Al terminar la partida, recibirás la experiencia normal. Después puedes consultar aquí si se guardó.":
                "Finishing the match grants normal experience. Return here to check that it was saved.";
            try
            {
                var reader=new LocalProgressStore(AccountProgressDirectory);
                var committed=reader.ReadExisting();
                if(reader.NeedsRecovery)return GameText.Spanish?
                    "El progreso necesita recuperarse. No se puede confirmar que la experiencia de esta partida esté guardada.":
                    "Progress needs recovery. This match's saved experience cannot be confirmed.";
                if(committed?.CompletedMatches.Contains(bo5ReviewCompletedSession)==true)return GameText.Spanish?
                    "Partida completada. Experiencia guardada. Puedes verla en Progreso.":
                    "Match completed. Experience saved. View it in Progress.";
                return GameText.Spanish?"Partida completada. La experiencia aún no está guardada. Abre Progreso para reintentar.":
                    "Match completed. Experience has not been saved yet. Open Progress to retry.";
            }
            catch(Exception error) when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException||
                error is ArgumentException||error is NotSupportedException)
            {
                return GameText.Spanish?"No se pudo verificar la experiencia guardada. Abre Progreso para revisar el estado.":
                    "Saved experience could not be verified. Open Progress to check its status.";
            }
        }

        bool HasSavedReviewBo5()
        {
            // Only this UI session knows that the match was started by the
            // offline review and actually finished. Generic saved XP receipts
            // carry no mode; never infer offline tutorial evidence from them.
            if(string.IsNullOrEmpty(bo5ReviewCompletedSession))return false;
            try
            {
                var reader=new LocalProgressStore(AccountProgressDirectory);
                var committed=reader.ReadExisting();
                return !reader.NeedsRecovery&&committed?.CompletedMatches.Contains(bo5ReviewCompletedSession)==true;
            }
            catch(Exception error) when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException||
                error is ArgumentException||error is NotSupportedException)
            {return false;}
        }

        public void ShowMultiplayerReview()
        {
            if(!CanOpenLateTutorialReview||!CanOpenMultiplayerReview)return;
            var box=RoomModal("Multiplayer review",GameText.Spanish?"SALAS DE AMIGOS":"FRIEND ROOMS",172,640,ShowLateTutorialReview);
            Label(box,GameText.Spanish?
                "Para jugar con un amigo, uno crea una sala y comparte su código. El otro elige Unirse e introduce ese código. Cuando ambos estén en la sala, quien la creó inicia la partida.":
                "To play with a friend, one player creates a room and shares its code. The other chooses Join and enters that code. Once both are in the room, its creator starts the match.",
                new Rect(24,82,444,192),21,Color.white);
            Label(box,GameText.Spanish?"Necesitan conexión a Internet. En la sala puedes copiar el código para compartirlo.":
                "Both players need an Internet connection. Copy the code from the room to share it.",new Rect(24,290,444,82),19,cyan);
            var rooms=GameButton(box,GameText.Spanish?"VER SALAS":"VIEW ROOMS",new Rect(24,398,444,68),OpenOnline,true,23);
            rooms.name="Review open friend rooms";
            var join=GameButton(box,GameText.Spanish?"INTRODUCIR CÓDIGO":"ENTER CODE",new Rect(24,478,444,68),ShowJoinRoom,false,22);
            join.name="Review open room code";
            GameButton(box,GameText.Back,new Rect(114,552,264,68),ShowLateTutorialReview,false,20);
        }
    }
}
