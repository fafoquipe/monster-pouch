using System;
using System.Linq;
using MonsterPouch.Gameplay.Machines;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local.Boards;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        ProgressionRoadScreen progressionRoad;
        public void OpenProgressionRoad()
        {
            CloseModal();
            if(trophyBoardScreen!=null)trophyBoardScreen.Close();
            if(progressionRoad!=null)progressionRoad.Close();
            if(!ReloadGuestAvailability()||!TrophyProfileReady){ShowProgressionUnavailable();return;}
            if(TrophySavePending)
            {
                var box=LobbyModal("Road result pending",GameText.Spanish?"COPAS":"TROPHIES",280,340,CloseModal);
                LobbyNumber(box,GameText.Spanish?"El resultado sigue pendiente de guardar.":"The result is still waiting to be saved.",new Rect(24,92,444,90),21,LobbyPaper);
                GameButton(box,GameText.Spanish?"REINTENTAR":"RETRY",new Rect(75,214,342,72),()=>{RetryTrophyResults();OpenProgressionRoad();},true,23);return;
            }
            try
            {
                var result=guestProfileStore.ReadProgression(Match.Config,PouchRoomConfiguration(),DateTime.UtcNow);
                if(result.Profile!=null)guestProfile=result.Profile;
                if(result.Status!=GuestProgressionStatus.Ready||result.Snapshot==null){ShowProgressionUnavailable();return;}
                progressionRoad=ProgressionRoadScreen.Open(RoadPresentation(result.Snapshot),ClaimRoadMilestone,()=>
                {OpenTrophyBoards();if(trophyBoardScreen!=null){trophyBoardScreen.RoadNavigation=OpenProgressionRoad;trophyBoardScreen.ShowBoards();}},ShowPouchRoom);
                progressionRoad.transform.SetParent(transform,false);
                // Mark only a successfully displayed road; the tutorial owner completes its module.
                if(EconomyTutorialActive&&economyTutorialModule==GuestTutorialModules.FirstMachine)
                    guestProfile=guestProfileStore.RecordTutorialProgressionVisit();
            }
            catch(Exception error) when(VendingStorageError(error)){ShowProgressionUnavailable();}
        }
        void ClaimRoadMilestone(string id)
        {
            try
            {
                var result=guestProfileStore.ClaimProgressionMilestone(Match.Config,PouchRoomConfiguration(),id,DateTime.UtcNow);
                if(result.Profile!=null)guestProfile=result.Profile;
                bool success=result.Status==GuestProgressionStatus.Claimed||result.Status==GuestProgressionStatus.AlreadyApplied;
                if(result.Snapshot!=null&&progressionRoad!=null)
                {
                    var snapshot=RoadPresentation(result.Snapshot);
                    if(!success)snapshot.Error=RoadRetryText;
                    progressionRoad.Present(snapshot);
                    if(success){ReadLobbyDailyGifts();RefreshDeckAvailability();}
                }
                else ShowProgressionUnavailable();
            }
            catch(Exception error) when(VendingStorageError(error))
            {
                // Keep the same milestone ID for retry; the store resolves any committed receipt.
                if(progressionRoad!=null){var snapshot=progressionRoad.State;snapshot.Error=RoadRetryText;progressionRoad.Present(snapshot);}
                else ShowProgressionUnavailable();
            }
        }
        string RoadRetryText=>GameText.Spanish?"No se pudo confirmar. Toca el premio para reintentar.":"Could not confirm. Tap the reward to retry.";
        void ShowProgressionUnavailable()
        {
            if(progressionRoad!=null)progressionRoad.Close();
            ShowLobbyNotice(GameText.Spanish?"CAMINO DE ERONYA":"ERONYA ROAD",GameText.Spanish?
                "No se pudo cargar el progreso. Vuelve a intentarlo.":"Could not load progression. Please try again.");
        }
        ProgressionRoadState RoadPresentation(GuestProgressionSnapshot snapshot)=>new ProgressionRoadState{
            Spanish=GameText.Spanish,Trophies=snapshot.CurrentTrophies,
            Worlds=snapshot.Worlds.Select(world=>new RoadWorld{
                BoardId=world.BoardId,Name=RoadWorldName(world.BoardId),StartsAt=world.StartTrophies,
                Characters=world.Characters.OrderByDescending(c=>GuestProgressionCatalogue.IsEronya(c.Id)).Select(c=>new RoadCharacter{
                    Id=c.Id,Name=Match.Config.Get(c.Id)?.DisplayName??c.Id,Owned=c.Owned,HideLockedArtwork=!GuestProgressionCatalogue.IsEronya(c.Id)
                }).ToArray()
            }).ToArray(),
            Rewards=snapshot.Milestones.Select(m=>new RoadReward{
                Id=m.Id,Trophies=m.RequiredTrophies,Title=RoadRewardTitle(m),IconResource=RoadRewardIcon(m),
                Status=m.State==GuestMilestoneState.Claimed?RoadRewardStatus.Claimed:m.State==GuestMilestoneState.Claimable?RoadRewardStatus.Claimable:RoadRewardStatus.Locked
            }).ToArray()
        };
        static string RoadWorldName(string id)=>id=="naturalis"?"Naturalis":id=="aquarion"?"Aquarion":id=="geothar"?"Geothar":id=="volkanar"?"Volkanar":"Aireola";
        static string RoadCurrencyIcon(PermanentCurrency currency)=>currency==PermanentCurrency.Earthcoins?"earthken":currency==PermanentCurrency.Suncoins?"sunken":"saturnken";
        static string RoadCurrencyName(PermanentCurrency currency)=>currency==PermanentCurrency.Earthcoins?"Earthkens":currency==PermanentCurrency.Suncoins?"Sunkens":"Saturnkens";
        static string RoadRewardTitle(GuestProgressionMilestoneSnapshot milestone)=>milestone.Reward.Kind==GuestRoadRewardKind.Currency?
            milestone.Reward.Amount+" "+RoadCurrencyName(milestone.Reward.Currency):milestone.Reward.Kind==GuestRoadRewardKind.FactionPouch?"CASE "+RoadWorldName(milestone.WorldId):"CASE";
        static string RoadRewardIcon(GuestProgressionMilestoneSnapshot milestone)=>milestone.Reward.Kind==GuestRoadRewardKind.Currency?
            "MonsterPouch/UI/Lobby/Header/"+RoadCurrencyIcon(milestone.Reward.Currency):milestone.Reward.Kind==GuestRoadRewardKind.FactionPouch?
            "MonsterPouch/Cases/Artwork/"+milestone.WorldId:"MonsterPouch/UI/Lobby/Header/daily-pouch";
    }
}
