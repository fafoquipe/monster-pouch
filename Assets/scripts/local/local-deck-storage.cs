using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        internal static string DeckDirectoryOverride { get; set; }
        LocalDeckStore deckStore;
        long deckRevision;
        string committedDeckJson,pendingDeckJson,deckStorageError;
        bool deckStorageLoaded;
        bool deckAvailabilityNeedsConfirmation;
        IEnumerable<string> AvailableWhelpIdsForDeck=>guestProfile?.CollectionMode==GuestCollectionMode.Legacy?null:
            guestProfile?.CollectionMode==GuestCollectionMode.StarterInventory?
                guestProfile.GetAvailableWhelpIds(Match.Config):Array.Empty<string>();
        IEnumerable<string> AvailableMonsterIdsForDeck=>guestProfile?.CollectionMode==GuestCollectionMode.Legacy?null:
            guestProfile?.CollectionMode==GuestCollectionMode.StarterInventory?
                guestProfile.GetOwnedCharacterIds().Where(id=>Match.Config.Get(id)?.IsMonster==true).ToArray():Array.Empty<string>();

        bool ReloadGuestAvailability()
        {
            if(guestProfileStore==null||guestProfile==null)
            {deckStorageError="load";return false;}
            try
            {
                var latest=guestProfileStore.Reload();
                if(guestProfileStore.NeedsRecovery){deckStorageError="recovery";return false;}
                guestProfile=latest;return true;
            }
            catch(Exception error) when(IsDeckStorageError(error))
            {deckStorageError=error is NotSupportedException?"version":"load";return false;}
        }
        bool RefreshDeckAvailability()
        {
            if(!ReloadGuestAvailability())return false;
            if(decks==null)return true;
            var available=AvailableWhelpIdsForDeck;
            if(available==null)return true;
            // A concurrent/pre-existing deck that the starter could not replace
            // is never silently rewritten. Explicit deck editing confirms it.
            if(deckAvailabilityNeedsConfirmation||deckStore==null||!deckStore.HasConfirmedInventoryRoster(guestProfile.ProfileId))
            {
                deckAvailabilityNeedsConfirmation=true;
                if(!decks.CanPlayCurrent(Match.Config,available,out _,AvailableMonsterIdsForDeck))deckStorageError="conflict";
                return true;
            }
            bool changed=decks.NormalizeAvailableWhelps(Match.Config,available);
            changed=decks.NormalizeAvailableMonsters(Match.Config,AvailableMonsterIdsForDeck)||changed;
            if(changed)SaveDeckStorage();
            ApplyDeck();
            return true;
        }
        bool PrepareCurrentDeckForMatch()
        {
            if(decks==null)LoadDeckStorage();
            if(!RefreshDeckAvailability())return false;
            RefreshTrophyBoardAccess();
            if(decks==null||!decks.CanPlayCurrent(Match.Config,AvailableWhelpIdsForDeck,out _,AvailableMonsterIdsForDeck))
            {deckStorageError="roster";return false;}
            try
            {if(!Match.SetLocalAbilityAccess(CurrentAccountAbilityAccess())){deckStorageError="load";return false;}}
            catch(Exception error) when(IsDeckStorageError(error)){deckStorageError="load";return false;}
            ApplyDeck();return true;
        }
        AbilityAccessSnapshot CurrentAccountAbilityAccess()
        {
            if(guestProfile==null||guestProfileStore==null||guestProfileStore.NeedsRecovery)
                throw new InvalidDataException("The account ability snapshot is unavailable.");
            if(guestProfile.CollectionMode==GuestCollectionMode.Legacy)return AbilityAccessSnapshot.Unrestricted;
            if(guestProfile.CollectionMode!=GuestCollectionMode.StarterInventory||guestProfile.Starter==null)
                throw new InvalidDataException("Choose a starter before creating an ability snapshot.");
            return AbilityAccessSnapshot.Create(Match.Config,Match.Config.Units.Where(unit=>unit!=null).Select(unit=>
                new AbilityAccessEntry{characterId=unit.Id,abilityIds=guestProfile.GetOwnedAbilityIds(unit.Id).ToArray()}));
        }
        AbilityAccessSnapshot ReadLocalRoomAbilityAccess()
        {
            if(!ReloadGuestAvailability())throw new InvalidDataException("The account could not be loaded for this room.");
            return CurrentAccountAbilityAccess();
        }
        bool IsLocalRoomTeamAvailable(string monster,IEnumerable<string> whelps)
        {
            if(!ReloadGuestAvailability())return false;
            var available=AvailableWhelpIdsForDeck;
            if(available==null)return true;
            var library=new DeckLibrary();
            library.Decks[0]=new SavedDeck{Monster=monster,Whelps=(whelps??Array.Empty<string>()).ToArray()};
            return library.CanPlayCurrent(Match.Config,available,out _,AvailableMonsterIdsForDeck);
        }

        void LoadDeckStorage()
        {
            if(deckStore==null)deckStore=new LocalDeckStore(AccountDeckDirectory);
            try
            {
                decks=deckStore.LoadOrImport(LegacyAccountValue(DeckKey),Match.Config,chosen,chosenWhelps);
                deckRevision=deckStore.Revision;deckStorageLoaded=true;
                committedDeckJson=decks.Serialize();
                deckStorageError=deckStore.NeedsRecovery?"recovery":deckStore.NeedsCatalogue?"catalogue":null;
            }
            catch(Exception error) when(IsDeckStorageError(error))
            {
                deckStorageError=error is NotSupportedException?"version":"load";
                if(decks==null)decks=DeckLibrary.Restore(LegacyAccountValue(DeckKey),Match.Config,chosen,chosenWhelps);
                committedDeckJson=decks.Serialize();
            }
        }
        void SaveDeckStorage()
        {
            if(!ReloadGuestAvailability()){RestoreCommittedDeckView();return;}
            if(deckStore==null)LoadDeckStorage();
            if(!deckStorageLoaded||deckStore.NeedsRecovery||deckStore.NeedsCatalogue)
            {
                RestoreCommittedDeckView();return;
            }
            var available=AvailableWhelpIdsForDeck;
            if(available!=null)decks.NormalizeAvailableWhelps(Match.Config,available);
            decks.NormalizeAvailableMonsters(Match.Config,AvailableMonsterIdsForDeck);
            string desired=decks.Serialize();
            if(desired==committedDeckJson&&(available==null||deckStore.HasConfirmedInventoryRoster(guestProfile.ProfileId)))return;
            pendingDeckJson=desired;
            TrySavePendingDeck();
        }
        void TrySavePendingDeck()
        {
            if(!ReloadGuestAvailability()){RestoreCommittedDeckView();return;}
            try
            {
                var desired=JsonUtility.FromJson<DeckLibrary>(pendingDeckJson);
                var available=AvailableWhelpIdsForDeck;
                if(available!=null)desired.NormalizeAvailableWhelps(Match.Config,available);
                desired.NormalizeAvailableMonsters(Match.Config,AvailableMonsterIdsForDeck);
                var result=deckStore.Save(desired,deckRevision,Match.Config,available,available==null?null:guestProfile.ProfileId,AvailableMonsterIdsForDeck);
                if(result==LocalDeckStore.SaveResult.Saved||result==LocalDeckStore.SaveResult.Unchanged)
                {
                    decks=desired;deckRevision=deckStore.Revision;committedDeckJson=decks.Serialize();
                    pendingDeckJson=null;deckStorageError=null;deckAvailabilityNeedsConfirmation=false;ApplyDeck();return;
                }
                if(result==LocalDeckStore.SaveResult.Conflict)
                {
                    // Reject this selection instead of overwriting another session.
                    pendingDeckJson=null;LoadDeckStorage();ApplyDeck();deckStorageError="conflict";deckAvailabilityNeedsConfirmation=true;return;
                }
                deckStorageError=result==LocalDeckStore.SaveResult.RecoveryRequired?"recovery":
                    result==LocalDeckStore.SaveResult.IncompleteRoster?"roster":"catalogue";
            }
            catch(Exception error) when(IsDeckStorageError(error))
            {deckStorageError=error is NotSupportedException?"version":"save";}
            RestoreCommittedDeckView();
        }
        void RestoreCommittedDeckView()
        {
            if(committedDeckJson!=null)decks=DeckLibrary.Restore(committedDeckJson,Match.Config,chosen,chosenWhelps);
            ApplyDeck();
        }
        static bool IsDeckStorageError(Exception error)=>error is IOException||error is InvalidDataException||
            error is UnauthorizedAccessException||error is ArgumentException||error is NotSupportedException||error is InvalidOperationException;
        void RetryDeckStorage()
        {
            if(pendingDeckJson!=null)TrySavePendingDeck();
            else{LoadDeckStorage();ApplyDeck();}
            RenderPage();
        }
        void RenderDeckStorageNotice()
        {
            if(deckStorageError==null)return;
            var notice=Panel(page,"Deck storage notice",MenuRect(new Rect(16,78,508,68),0),new Color(.19f,.05f,.06f,.98f),false);
            Label(notice,DeckStorageMessage(),new Rect(10,4,350,60),14,Color.white);
            var retry=GameButton(notice,GameText.Spanish?"REINTENTAR":"RETRY",new Rect(366,0,142,68),RetryDeckStorage,false,14);
            retry.name="Retry deck storage";
        }
        string DeckStorageMessage()
        {
            if(deckStorageError=="version")return GameText.Spanish?"Tus mazos requieren una versión más reciente. Se conservan los datos.":"Your decks require a newer game version. Saved data is preserved.";
            if(deckStorageError=="recovery")return GameText.Spanish?"Se muestra un respaldo. Tus mazos necesitan recuperación antes de guardar.":"A backup is shown. Decks need recovery before saving.";
            if(deckStorageError=="catalogue")return GameText.Spanish?"Faltan personajes del catálogo. Tus mazos originales se conservan.":"Some catalogue characters are unavailable. Original decks are preserved.";
            if(deckStorageError=="conflict")return GameText.Spanish?"Otra sesión actualizó tus mazos. Vuelve a elegir tu cambio.":"Another session updated your decks. Please make your selection again.";
            if(deckStorageError=="roster")return GameText.Spanish?"El Brief necesita un Monster desbloqueado y tus Whelps disponibles, hasta siete. Elige tu equipo para guardarlo.":"Your Brief needs an unlocked Monster and all available Whelps, up to seven. Choose your team to save it.";
            return GameText.Spanish?"No se pudo cargar o guardar el mazo. Tus datos anteriores se conservan.":"The deck could not be loaded or saved. Previous data is preserved.";
        }
    }
}
