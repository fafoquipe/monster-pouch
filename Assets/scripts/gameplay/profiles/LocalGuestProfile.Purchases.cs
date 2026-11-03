using System;
using MonsterPouch.Gameplay.Machines;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfile
    {
        internal static PlayerMachineAmount[] AbilityPurchaseAmounts() => new[] {
            new PlayerMachineAmount { Currency = PermanentCurrency.Earthcoins, Amount = -GuestPermanentEconomy.AbilityV1EarthCost },
            new PlayerMachineAmount { Currency = PermanentCurrency.Suncoins, Amount = -GuestPermanentEconomy.AbilityV1SunCost }
        };

        void ValidateAbilityPurchase(GuestWalletEvent entry, Dictionary<PermanentCurrency,long> balances)
        {
            if (CollectionMode != GuestCollectionMode.StarterInventory || HasLegacyAccess || Starter == null ||
                entry.SourceId != GuestPermanentEconomy.AbilityV1ApprovalId || entry.PermanentPolicyVersion != 1 ||
                !entry.Id.StartsWith(GuestPermanentEconomy.PurchasePrefix, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(entry.Id.Substring(GuestPermanentEconomy.PurchasePrefix.Length)) ||
                HasMachinePayload(entry) ||
                string.IsNullOrWhiteSpace(entry.CharacterId) || string.IsNullOrWhiteSpace(entry.AbilityId) ||
                !SameAmounts(entry.Amounts, AbilityPurchaseAmounts()))
                throw new InvalidDataException("Invalid permanent ability purchase.");
            balances.TryGetValue(PermanentCurrency.Earthcoins, out long earth);
            balances.TryGetValue(PermanentCurrency.Suncoins, out long sun);
            if (earth < GuestPermanentEconomy.AbilityV1EarthCost || sun < GuestPermanentEconomy.AbilityV1SunCost)
                throw new InvalidDataException("An ability purchase requires both earned currency balances.");
            var unlock = AbilityUnlocks.SingleOrDefault(r => r.ReceiptId == entry.Id);
            if (unlock == null || unlock.Source != GuestUnlockSource.Purchase ||
                unlock.CharacterId != entry.CharacterId || unlock.AbilityId != entry.AbilityId ||
                unlock.TimestampUtc != entry.TimestampUtc)
                throw new InvalidDataException("Ability purchase and ownership must share one committed receipt.");
        }

        // JsonUtility can materialize a serialized null inline class as an empty
        // object. Accept only that empty representation, never actual draw data.
        static bool HasMachinePayload(GuestWalletEvent entry)
        {
            if (!string.IsNullOrEmpty(entry.MachineId) || entry.OwnershipBefore != null &&
                (entry.OwnershipBefore.CharacterOwned || entry.OwnershipBefore.AbilityIds != null && entry.OwnershipBefore.AbilityIds.Length != 0))
                return true;
            var draw = entry.Draw;
            return draw != null && (!string.IsNullOrEmpty(draw.RequestId) || !string.IsNullOrEmpty(draw.RewardId) ||
                !string.IsNullOrEmpty(draw.MachineId) || !string.IsNullOrEmpty(draw.PolicyId) ||
                !string.IsNullOrEmpty(draw.CatalogSignature) || !string.IsNullOrEmpty(draw.PolicySignature) ||
                draw.CatalogVersion != 0 || draw.PolicyVersion != 0 || draw.DrawNumber != 0 ||
                draw.ExpectedRevision != 0 || draw.Guarantee != default(MachineGuarantee));
        }

        void ValidatePermanentEconomyReceipts()
        {
            foreach (var entry in WalletEvents)
            {
                bool welcome = entry.Id.StartsWith(GuestPermanentEconomy.WelcomePrefix, StringComparison.Ordinal) ||
                    entry.SourceId == GuestPermanentEconomy.WelcomeV1ApprovalId;
                if (welcome && (entry.Id != GuestPermanentEconomy.WelcomeId(ProfileId) ||
                    entry.SourceId != GuestPermanentEconomy.WelcomeV1ApprovalId ||
                    entry.Kind != GuestWalletEventKind.FreeCredit || entry.PermanentPolicyVersion != 1 || CollectionMode != GuestCollectionMode.StarterInventory ||
                    HasLegacyAccess || !SameAmounts(entry.Amounts, new[] {
                        new PlayerMachineAmount { Currency = PermanentCurrency.Saturncoins, Amount = GuestPermanentEconomy.WelcomeV1Saturnkens }
                    }))) throw new InvalidDataException("Invalid new-account welcome receipt.");
                if (!welcome && entry.Kind == GuestWalletEventKind.FreeCredit && entry.PermanentPolicyVersion != 0)
                    throw new InvalidDataException("Ordinary credits cannot carry new-account policy metadata.");
                bool purchase = entry.Id.StartsWith(GuestPermanentEconomy.PurchasePrefix, StringComparison.Ordinal) ||
                    entry.SourceId == GuestPermanentEconomy.AbilityV1ApprovalId;
                if (purchase && entry.Kind != GuestWalletEventKind.AbilityPurchase)
                    throw new InvalidDataException("A purchase identifier cannot be used as a free award or machine draw.");
            }
            foreach (var receipt in AbilityUnlocks)
            {
                if (receipt.Source == GuestUnlockSource.Purchase || GuestPermanentEconomy.ReservedId(receipt.ReceiptId))
                {
                    if (receipt.Source != GuestUnlockSource.Purchase ||
                        !WalletEvents.Any(e => e.Kind == GuestWalletEventKind.AbilityPurchase && e.Id == receipt.ReceiptId &&
                            e.CharacterId == receipt.CharacterId && e.AbilityId == receipt.AbilityId && e.TimestampUtc == receipt.TimestampUtc))
                        throw new InvalidDataException("Orphaned permanent ability purchase receipt.");
                }
            }
            if (CharacterUnlocks.Any(r => r.Source == GuestUnlockSource.Purchase || GuestPermanentEconomy.ReservedId(r.ReceiptId)))
                throw new InvalidDataException("Ability purchases and welcome credits cannot unlock a character.");
        }
    }
}
