using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonsterPouch.Gameplay.Profiles
{
    // The source is a receipt label. It neither activates a reward system nor
    // authorizes a payment, and every grant below remains inside an InternalQA profile.
    public enum InventoryRewardSource { InternalQA, Machine, NormalPouch, Mastery, Tutorial }

    [Serializable] public sealed class OwnedAbility
    {
        public string CharacterId, AbilityId;
    }

    [Serializable] public sealed class InventoryUnlockReceipt
    {
        public string Id, CharacterId, AbilityId, TimestampUtc;
        public InventoryRewardSource Source;
    }

    public sealed partial class LocalEconomyProfile
    {
        public List<OwnedAbility> OwnedAbilities = new List<OwnedAbility>();
        public List<InventoryUnlockReceipt> InventoryReceipts = new List<InventoryUnlockReceipt>();

        public bool OwnsCharacter(string characterId) =>
            !string.IsNullOrWhiteSpace(characterId) && OwnedCharacterIds != null &&
            OwnedCharacterIds.Contains(characterId, StringComparer.Ordinal);

        public bool OwnsAbility(string characterId, string abilityId) =>
            !string.IsNullOrWhiteSpace(characterId) && !string.IsNullOrWhiteSpace(abilityId) &&
            OwnedAbilities != null && OwnedAbilities.Any(a => a != null &&
                a.CharacterId == characterId && a.AbilityId == abilityId);

        // Only old schemas receive absent fields. A malformed current schema
        // must fail validation instead of silently discarding owned content.
        public void UpgradeSchema()
        {
            if (Version < 1 || Version > CurrentVersion)
                throw new NotSupportedException("Unsupported profile schema; original save is preserved.");
            if (Version == 1 && StarterSelections == null)
                StarterSelections = new List<StarterSelection>();
            if (Version < 3)
            {
                if ((OwnedAbilities != null && OwnedAbilities.Count != 0) ||
                    (InventoryReceipts != null && InventoryReceipts.Count != 0))
                    throw new InvalidDataException("An older profile cannot contain v3 inventory records.");
                OwnedAbilities = new List<OwnedAbility>();
                InventoryReceipts = new List<InventoryUnlockReceipt>();
            }
            Validate();
            Version = CurrentVersion;
        }

        void ValidateInventory()
        {
            if (OwnedAbilities == null || InventoryReceipts == null)
                throw new InvalidDataException("Missing inventory records.");
            if (Version < 3 && (OwnedAbilities.Count != 0 || InventoryReceipts.Count != 0))
                throw new InvalidDataException("An older profile cannot contain v3 inventory records.");
            var abilities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ability in OwnedAbilities)
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.AbilityId) ||
                    !OwnsCharacter(ability.CharacterId) ||
                    !abilities.Add(AbilityKey(ability.CharacterId, ability.AbilityId)))
                    throw new InvalidDataException("Invalid or repeated owned ability.");
            }
            var receipts = new HashSet<string>(StringComparer.Ordinal);
            var grantedCharacters = new HashSet<string>(StringComparer.Ordinal);
            var grantedAbilities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var receipt in InventoryReceipts)
            {
                if (receipt == null || string.IsNullOrWhiteSpace(receipt.Id) || receipt.Id.Length > 160 ||
                    !receipts.Add(receipt.Id) || !OwnsCharacter(receipt.CharacterId) ||
                    !Enum.IsDefined(typeof(InventoryRewardSource), receipt.Source) ||
                    !DateTime.TryParse(receipt.TimestampUtc, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out _))
                    throw new InvalidDataException("Invalid inventory receipt.");
                if (!string.IsNullOrEmpty(receipt.AbilityId))
                {
                    string key = AbilityKey(receipt.CharacterId, receipt.AbilityId);
                    if (!abilities.Contains(key) || !grantedAbilities.Add(key))
                        throw new InvalidDataException("An ability receipt is missing ownership or grants it twice.");
                }
                else if (!grantedCharacters.Add(receipt.CharacterId))
                    throw new InvalidDataException("A character has more than one grant receipt.");
            }
            if (abilities.Any(key => !grantedAbilities.Contains(key)))
                throw new InvalidDataException("An owned ability is missing its grant receipt.");
        }

        static string AbilityKey(string characterId, string abilityId) =>
            characterId.Length + ":" + characterId + abilityId;
    }
}
