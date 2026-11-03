using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfileStore
    {
        /// <summary>An explicit authorized award. No caller is wired to issue one automatically.</summary>
        public UnlockResult UnlockAbility(MatchConfig catalogue,string receiptId,string characterId,
            string abilityId,GuestUnlockSource source)
        {
            if(!GuestAbilityCatalogue.KnownAbility(catalogue,characterId,abilityId)||
                string.IsNullOrWhiteSpace(receiptId)||receiptId.Length>160||
                receiptId==StarterRoster.LegacyReceiptId||receiptId==StarterRoster.CurrentReceiptId||
                (!Enum.IsDefined(typeof(GuestUnlockSource),source)||source==GuestUnlockSource.Purchase||GuestPermanentEconomy.ReservedId(receiptId)))return UnlockResult.InvalidRequest;
            using(Lock())
            {
                var profile=LoadExisting();
                if(profile==null)throw new InvalidOperationException("Open the guest profile before recording an ability unlock.");
                if(NeedsRecovery)return UnlockResult.RecoveryRequired;
                if(profile.CollectionMode!=GuestCollectionMode.StarterInventory)return UnlockResult.LegacyProfile;
                if(profile.Starter==null)return UnlockResult.StarterRequired;
                if(!GuestAbilityCatalogue.KnownCollection(profile,catalogue))return UnlockResult.InvalidCatalogue;
                if(profile.CharacterUnlocks.Any(receipt=>receipt.ReceiptId==receiptId))return UnlockResult.Conflict;
                var previous=profile.AbilityUnlocks.FirstOrDefault(receipt=>receipt.ReceiptId==receiptId);
                if(previous!=null)return previous.CharacterId==characterId&&previous.AbilityId==abilityId&&previous.Source==source?
                    UnlockResult.AlreadyApplied:UnlockResult.Conflict;
                if(!profile.OwnsCharacter(characterId))return UnlockResult.CharacterLocked;
                if(profile.OwnsAbility(characterId,abilityId))return UnlockResult.AlreadyOwned;
                profile.AbilityUnlocks.Add(new GuestAbilityUnlockReceipt{
                    ReceiptId=receiptId,CharacterId=characterId,AbilityId=abilityId,Source=source,TimestampUtc=DateTime.UtcNow.ToString("O")
                });
                profile.Revision=checked(profile.Revision+1);
                Save(profile);return UnlockResult.Unlocked;
            }
        }

        /// <summary>Read committed ownership. Recovery never supplies an authoritative snapshot.</summary>
        public GuestMachineOwnershipStatus ReadMachineOwnership(MatchConfig catalogue,MachineDefinition machine,
            out MachineOwnership ownership)
        {
            ownership=null;
            using(Lock())
            {
                var profile=LoadExisting();
                if(NeedsRecovery)return GuestMachineOwnershipStatus.RecoveryRequired;
                return GuestMachineOwnershipAdapter.TryCreate(profile,catalogue,machine,out ownership);
            }
        }
    }
}
