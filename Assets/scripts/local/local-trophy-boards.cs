using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local.Boards;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        BoardProgressionScreen trophyBoardScreen;
        static Sprite ResolveBoardTrophyIcon(string boardId)=>Resources.Load<Sprite>("MonsterPouch/UI/Progression/Trophies/"+(boardId=="aireola"?"royal":boardId));
        public long AccountTrophies => guestProfile?.Trophies ?? 0;
        bool TrophyProfileReady => guestProfile != null && guestProfileError == null &&
            guestProfileStore != null && !guestProfileStore.NeedsRecovery;
        long? TrophyBoardThreshold(TableStyle style)=>GuestTrophyLadder.CreatePolicy().Stages.FirstOrDefault(s=>s.BoardId==CardTableTheme.MapId(style))?.MinimumTrophies;
        TableStyle CurrentTrophyStageStyle=>CardTableTheme.FromMapId(GuestTrophyLadder.CreatePolicy().Stages.Last(s=>AccountTrophies>=s.MinimumTrophies).BoardId);

        public bool CanSelectTrophyBoard(TableStyle style)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(TrophyProfileReady && AndroidDeveloperContent.IsUnlocked(guestProfile))return true;
#endif
            // Casino Noche is a visual variant of the existing casino, with identical access.
            if(style==TableStyle.CasinoNight)return CanSelectTrophyBoard(TableStyle.GrandCasaHotel);
            // Cosmetic access stays independent of the trophy road; no purchase is inferred.
            var threshold=TrophyBoardThreshold(style);
            return !threshold.HasValue || TrophyProfileReady && GuestTrophyLadder.ReachedBoardIds(guestProfile.TrophyLadder).Contains(CardTableTheme.MapId(style));
        }

        public void RefreshTrophyBoardAccess()
        {
            // A completed result must never resize/change the board underneath
            // combat actors. Reconcile unavailable selections only in the menu.
            if(Match==null || Match.Phase!=MatchPhase.Menu || !TrophyProfileReady)return;
            if(!CanSelectTrophyBoard(CardTableTheme.Current))CardTableTheme.Select(CurrentTrophyStageStyle);
            if(!CanSelectTrophyBoard(CardTableTheme.Lobby)){CardTableTheme.SelectLobby(CurrentTrophyStageStyle);LoadTableArtwork();}
        }

        BoardScreenState TrophyBoardState()
        {
            var policy=GuestTrophyLadder.CreatePolicy();
            var stages=policy.Stages.OrderBy(s=>s.MinimumTrophies).ToArray();
            var stage=stages.Last(s=>AccountTrophies>=s.MinimumTrophies);
            var next=stages.FirstOrDefault(s=>s.MinimumTrophies>AccountTrophies);
            var adjacent=next??stages[Mathf.Max(0,stages.Length-2)];
            return new BoardScreenState {
                Spanish=GameText.Spanish,Trophies=AccountTrophies,CurrentBoardId=stage.BoardId,
                StageMinimum=stage.MinimumTrophies,NextStageAt=next?.MinimumTrophies,
                AdjacentBoardId=adjacent.BoardId,AdjacentStageMinimum=adjacent.MinimumTrophies,AdjacentIsNext=next!=null,
                VictoryGain=policy.DifferenceBands[0].WinGain,
                DefeatLoss=stage.DefeatLossOverride??policy.DifferenceBands[0].LossAmount,
                SelectionDefined=true,SelectedBoardId=CardTableTheme.MapId(CardTableTheme.Current),
                Options=stages.Select(s=>new BoardSelectionOption { BoardId=s.BoardId,UnlockAt=s.MinimumTrophies,
                    CanSelect=CanSelectTrophyBoard(CardTableTheme.FromMapId(s.BoardId)),
                    Status=CanSelectTrophyBoard(CardTableTheme.FromMapId(s.BoardId))?BoardOptionStatus.Available:BoardOptionStatus.LockedByTrophies })
                    .Concat(new[] {
                        new BoardSelectionOption { BoardId="classic",CanSelect=true,Status=BoardOptionStatus.OwnedCosmetic },
                        new BoardSelectionOption { BoardId="lounge",CanSelect=CanSelectTrophyBoard(TableStyle.GrandCasaHotel),Status=BoardOptionStatus.PaidCosmetic },
                        new BoardSelectionOption { BoardId="casino-night",CanSelect=CanSelectTrophyBoard(TableStyle.GrandCasaHotel),Status=BoardOptionStatus.PaidCosmetic }
                    }).ToArray()
            };
        }

        void OpenTrophyBoards()
        {
            CloseModal();
            if(trophyBoardScreen!=null)trophyBoardScreen.Close();
            if(!ReloadGuestAvailability() || !TrophyProfileReady)
            {
                ShowLobbyNotice(GameText.Spanish?"TABLEROS":"BOARDS",GameText.Spanish?
                    "No se pudo cargar el progreso de tu cuenta. Vuelve a intentarlo.":
                    "Your account progress could not be loaded. Please try again.");
                return;
            }
            if(TrophySavePending)
            {
                var box=LobbyModal("Trophy save pending",GameText.Spanish?"COPAS":"TROPHIES",280,340,CloseModal);
                LobbyNumber(box,GameText.Spanish?"El resultado sigue pendiente de guardar.":"The result is still waiting to be saved.",new Rect(24,92,444,90),21,LobbyPaper);
                GameButton(box,GameText.Spanish?"REINTENTAR":"RETRY",new Rect(75,214,342,72),()=>
                {RetryTrophyResults();OpenTrophyBoards();},true,23);
                return;
            }
            RefreshTrophyBoardAccess();
            trophyBoardScreen=BoardProgressionScreen.Open(TrophyBoardState(),id=>
            {
                if(!ReloadGuestAvailability() || !TrophyProfileReady){OpenTrophyBoards();return;}
                var style=CardTableTheme.FromMapId(id);
                if(!CanSelectTrophyBoard(style)){OpenTrophyBoards();return;}
                SelectTableSkin(style,false);
                if(trophyBoardScreen!=null)trophyBoardScreen.Present(TrophyBoardState());
            },null,ResolveBoardTrophyIcon);
            trophyBoardScreen.transform.SetParent(transform,false);
        }
    }
}


