using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfileStore
    {
        public enum UnlockResult { Unlocked, AlreadyApplied, AlreadyOwned, Conflict, InvalidRequest, RecoveryRequired, LegacyProfile, StarterRequired, CharacterLocked, InvalidCatalogue }

        // The caller must supply an approved award's stable receipt ID. Merely
        // opening a collection, match, or shop never invokes this operation.
        public UnlockResult UnlockCharacter(MatchConfig catalogue,string receiptId,string characterId,GuestUnlockSource source)
        {
            if(!GuestAbilityCatalogue.KnownCharacter(catalogue,characterId)||
                string.IsNullOrWhiteSpace(receiptId)||receiptId.Length>160||
                receiptId==StarterRoster.LegacyReceiptId||receiptId==StarterRoster.CurrentReceiptId||
                (!Enum.IsDefined(typeof(GuestUnlockSource),source)||source==GuestUnlockSource.Purchase||GuestPermanentEconomy.ReservedId(receiptId)))return UnlockResult.InvalidRequest;
            using(Lock())
            {
                var profile=LoadExisting();
                if(profile==null)throw new InvalidOperationException("Open the guest profile before recording a character unlock.");
                if(NeedsRecovery)return UnlockResult.RecoveryRequired;
                if(profile.CollectionMode!=GuestCollectionMode.StarterInventory)return UnlockResult.LegacyProfile;
                if(profile.Starter==null)return UnlockResult.StarterRequired;
                if(!GuestAbilityCatalogue.KnownCollection(profile,catalogue))return UnlockResult.InvalidCatalogue;
                if(profile.AbilityUnlocks.Any(receipt=>receipt.ReceiptId==receiptId))return UnlockResult.Conflict;
                var previous=profile.CharacterUnlocks.FirstOrDefault(receipt=>receipt.ReceiptId==receiptId);
                if(previous!=null)return previous.CharacterId==characterId&&previous.Source==source?
                    UnlockResult.AlreadyApplied:UnlockResult.Conflict;
                if(profile.OwnsCharacter(characterId))return UnlockResult.AlreadyOwned;
                profile.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt{
                    ReceiptId=receiptId,CharacterId=characterId,Source=source,TimestampUtc=DateTime.UtcNow.ToString("O")
                });
                profile.Revision=checked(profile.Revision+1);
                Save(profile);return UnlockResult.Unlocked;
            }
        }
    }
}
