using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalQAProfileStore
    {
        public enum InventoryResult
        {
            Applied, AlreadyApplied, AlreadyOwned, Conflict, Disabled,
            RecoveryRequired, InvalidReward, CharacterLocked
        }

        /// <summary>
        /// Explicit internal QA grant of a catalogue character or one of its three
        /// existing upgrades. A null/empty ability ID denotes the character itself.
        /// Receipt IDs are scoped to inventory; no currency is charged or granted.
        /// </summary>
        public InventoryResult GrantInventory(EconomyDevelopmentConfig config, MatchConfig catalogue,
            string receiptId, InventoryRewardSource source, string characterId, string abilityId = null)
        {
            if (!Allowed(config)) return InventoryResult.Disabled;
            if (string.IsNullOrWhiteSpace(receiptId) || receiptId.Length > 160 ||
                !Enum.IsDefined(typeof(InventoryRewardSource), source) ||
                !KnownReward(catalogue, characterId, abilityId))
                return InventoryResult.InvalidReward;
            abilityId = abilityId ?? "";
            using (Lock())
            {
                var profile = LoadExisting();
                if (profile == null)
                    throw new InvalidOperationException("Open the QA profile before granting inventory.");
                if (NeedsRecovery) return InventoryResult.RecoveryRequired;
                if (profile.Environment != ProfileEnvironment.InternalQA) return InventoryResult.Disabled;
                var previous = profile.InventoryReceipts.FirstOrDefault(r => r.Id == receiptId);
                if (previous != null)
                    return previous.CharacterId == characterId && previous.AbilityId == abilityId &&
                        previous.Source == source ? InventoryResult.AlreadyApplied : InventoryResult.Conflict;
                if (abilityId.Length == 0)
                {
                    if (profile.OwnsCharacter(characterId)) return InventoryResult.AlreadyOwned;
                    profile.OwnedCharacterIds.Add(characterId);
                }
                else
                {
                    if (!profile.OwnsCharacter(characterId)) return InventoryResult.CharacterLocked;
                    if (profile.OwnsAbility(characterId, abilityId)) return InventoryResult.AlreadyOwned;
                    profile.OwnedAbilities.Add(new OwnedAbility { CharacterId = characterId, AbilityId = abilityId });
                }
                profile.InventoryReceipts.Add(new InventoryUnlockReceipt {
                    Id = receiptId, Source = source, CharacterId = characterId, AbilityId = abilityId,
                    TimestampUtc = DateTime.UtcNow.ToString("O")
                });
                profile.Revision = checked(profile.Revision + 1);
                Save(profile);
                return InventoryResult.Applied;
            }
        }

        static bool KnownReward(MatchConfig catalogue, string characterId, string abilityId)
        {
            if (catalogue == null || catalogue.Units == null || string.IsNullOrWhiteSpace(characterId))
                return false;
            var matches = catalogue.Units.Where(u => u != null && u.Id == characterId).ToArray();
            if (matches.Length != 1) return false;
            if (string.IsNullOrEmpty(abilityId)) return true;
            // Base ability and effects are not unlock IDs. Use the catalogue's
            // persistent upgrade IDs, never display names, slot positions or EffectId.
            var upgrades = matches[0].Tricks;
            return upgrades != null && upgrades.Count(t => t != null && t.Id == abilityId) == 1;
        }
    }
}
