using System;
using MonsterPouch.Gameplay.Machines;
using System.Linq;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfileStore
    {
        // Called only in the same transaction that creates a genuinely new identity.
        static void AddNewAccountWelcome(LocalGuestProfile profile)
        {
            profile.WalletEvents.Add(new GuestWalletEvent {
                Id = GuestPermanentEconomy.WelcomeId(profile.ProfileId), SourceId = GuestPermanentEconomy.WelcomeApprovalId,
                TimestampUtc = DateTime.UtcNow.ToString("O"), Kind = GuestWalletEventKind.FreeCredit, PermanentPolicyVersion = GuestPermanentEconomy.CurrentPolicyVersion,
                Amounts = new[] { new PlayerMachineAmount { Currency = PermanentCurrency.Saturncoins, Amount = GuestPermanentEconomy.WelcomeSaturnkens } }
            });
        }

        public GuestAbilityPurchaseResult PreviewAbilityPurchase(MatchConfig catalogue, string characterId, string abilityId)
        {
            if (!GuestAbilityCatalogue.KnownAbility(catalogue, characterId, abilityId))
                return AbilityPurchaseResult(GuestAbilityPurchaseStatus.InvalidRequest);
            using (Lock())
            {
                var profile = LoadExisting();
                return AbilityPurchasePreview(profile, catalogue, characterId, abilityId);
            }
        }

        public GuestAbilityPurchaseResult PurchaseAbility(MatchConfig catalogue, string requestId, string characterId, string abilityId)
        {
            if (!VendingId(requestId, 120 - GuestPermanentEconomy.PurchasePrefix.Length) ||
                GuestPermanentEconomy.ReservedId(requestId) ||
                !GuestAbilityCatalogue.KnownAbility(catalogue, characterId, abilityId))
                return AbilityPurchaseResult(GuestAbilityPurchaseStatus.InvalidRequest);
            using (Lock())
            {
                var profile = LoadExisting();
                var blocked = AbilityPurchaseProfileStatus(profile, catalogue);
                if (blocked != GuestAbilityPurchaseStatus.Ready) return AbilityPurchaseResult(blocked, profile);
                string id = GuestPermanentEconomy.PurchaseId(requestId);
                var previous = profile.WalletEvents.FirstOrDefault(e => e.Id == id);
                if (previous != null)
                    return previous.Kind == GuestWalletEventKind.AbilityPurchase && previous.CharacterId == characterId && previous.AbilityId == abilityId ?
                        AbilityPurchaseResult(GuestAbilityPurchaseStatus.AlreadyApplied, profile, previous,
                            profile.AbilityUnlocks.Single(r => r.ReceiptId == id)) :
                        AbilityPurchaseResult(GuestAbilityPurchaseStatus.Conflict, profile);
                // Raw request IDs already used by another operation are not reusable authorization.
                if (profile.WalletEvents.Any(e => e.Id == requestId) ||
                    profile.CharacterUnlocks.Any(r => r.ReceiptId == id || r.ReceiptId == requestId) ||
                    profile.AbilityUnlocks.Any(r => r.ReceiptId == id || r.ReceiptId == requestId))
                    return AbilityPurchaseResult(GuestAbilityPurchaseStatus.Conflict, profile);
                var preview = AbilityPurchasePreview(profile, catalogue, characterId, abilityId);
                if (preview.Status != GuestAbilityPurchaseStatus.Ready) return preview;
                var next = CloneVending(profile);
                string timestamp = DateTime.UtcNow.ToString("O");
                var wallet = new GuestWalletEvent {
                    Id = id, SourceId = GuestPermanentEconomy.AbilityPurchaseApprovalId, TimestampUtc = timestamp,
                    Kind = GuestWalletEventKind.AbilityPurchase, PermanentPolicyVersion = GuestPermanentEconomy.CurrentPolicyVersion, CharacterId = characterId, AbilityId = abilityId,
                    Amounts = LocalGuestProfile.AbilityPurchaseAmounts()
                };
                var unlock = new GuestAbilityUnlockReceipt {
                    ReceiptId = id, CharacterId = characterId, AbilityId = abilityId,
                    Source = GuestUnlockSource.Purchase, TimestampUtc = timestamp
                };
                next.WalletEvents.Add(wallet); next.AbilityUnlocks.Add(unlock);
                next.Revision = checked(next.Revision + 1);
                Save(next);
                return AbilityPurchaseResult(GuestAbilityPurchaseStatus.Purchased, next, wallet, unlock);
            }
        }

        GuestAbilityPurchaseStatus AbilityPurchaseProfileStatus(LocalGuestProfile profile, MatchConfig catalogue)
        {
            if (NeedsRecovery) return GuestAbilityPurchaseStatus.RecoveryRequired;
            if (profile == null) return GuestAbilityPurchaseStatus.MissingProfile;
            if (profile.CollectionMode != GuestCollectionMode.StarterInventory || profile.HasLegacyAccess)
                return GuestAbilityPurchaseStatus.LegacyUnavailable;
            if (profile.Starter == null) return GuestAbilityPurchaseStatus.StarterRequired;
            if (!GuestAbilityCatalogue.KnownCollection(profile, catalogue)) return GuestAbilityPurchaseStatus.InvalidCatalogue;
            return GuestAbilityPurchaseStatus.Ready;
        }

        GuestAbilityPurchaseResult AbilityPurchasePreview(LocalGuestProfile profile, MatchConfig catalogue, string characterId, string abilityId)
        {
            var blocked = AbilityPurchaseProfileStatus(profile, catalogue);
            if (blocked != GuestAbilityPurchaseStatus.Ready) return AbilityPurchaseResult(blocked, profile);
            if (!profile.OwnsCharacter(characterId)) return AbilityPurchaseResult(GuestAbilityPurchaseStatus.CharacterLocked, profile);
            var existing = profile.AbilityUnlocks.FirstOrDefault(r => r.CharacterId == characterId && r.AbilityId == abilityId);
            if (existing != null) return AbilityPurchaseResult(GuestAbilityPurchaseStatus.AlreadyOwned, profile, unlock: existing);
            return AbilityPurchaseResult(profile.FreeBalance(PermanentCurrency.Earthcoins) >= GuestPermanentEconomy.AbilityEarthCost &&
                profile.FreeBalance(PermanentCurrency.Suncoins) >= GuestPermanentEconomy.AbilitySunCost ?
                GuestAbilityPurchaseStatus.Ready : GuestAbilityPurchaseStatus.InsufficientFunds, profile);
        }

        static GuestAbilityPurchaseResult AbilityPurchaseResult(GuestAbilityPurchaseStatus status, LocalGuestProfile profile = null,
            GuestWalletEvent wallet = null, GuestAbilityUnlockReceipt unlock = null) =>
            new GuestAbilityPurchaseResult { Status = status, Profile = profile, WalletReceipt = wallet, UnlockReceipt = unlock };
    }
}
