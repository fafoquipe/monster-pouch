using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;

namespace MonsterPouch.Gameplay.Machines
{
    /// <summary>
    /// Shared, explicitly approved prize economics. This resource has no artwork,
    /// character-to-machine bindings, wallet grants or automatic draw behavior.
    /// </summary>
    [CreateAssetMenu(menuName = "Monster Pouch/Approved vending economy")]
    public sealed class ApprovedPlayerMachineEconomy : ScriptableObject
    {
        [SerializeField] string approvalId;
        [SerializeField] string policyId;
        [SerializeField] int policyVersion;
        [SerializeField] PlayerMachineAmount cost;
        [SerializeField] double[] characterChanceByDraw;
        [SerializeField] double rewardWeight;
        [SerializeField] PlayerMachinePackage[] packages;

        /// <summary>
        /// Builds an independent configuration for an explicitly supplied binding.
        /// Pass the current MatchConfig.CreateRuntimeConfig() result, as with the
        /// profile store. Artwork approval remains the PlayerMachineCatalogue gate.
        /// </summary>
        public PlayerMachineConfiguration Create(MatchConfig characters, string machineId,
            string characterId, int catalogVersion = 1)
        {
            var character = GuestAbilityCatalogue.Character(characters, characterId);
            if (character?.Tricks == null || character.Tricks.Length != 3 ||
                character.Tricks.Any(t => t == null || string.IsNullOrWhiteSpace(t.Id)) ||
                character.Tricks.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() != 3)
                throw new ArgumentException("The current character and its three stable ability IDs are required.");
            if (packages == null || packages.Any(p => p == null))
                throw new ArgumentException("An approved resource package table is required.");

            var rewards = new[] { new MachineReward {
                RewardId = "character", Kind = MachineRewardKind.Character, AbilityId = string.Empty
            } }
                .Concat(character.Tricks.Select(t => new MachineReward {
                    RewardId = "ability:" + t.Id, Kind = MachineRewardKind.Ability, AbilityId = t.Id
                }))
                .Concat(packages.Select(p => new MachineReward {
                    RewardId = p.RewardId, Kind = MachineRewardKind.Resource, AbilityId = string.Empty
                }))
                .ToArray();
            var configuration = new PlayerMachineConfiguration {
                ApprovalId = approvalId,
                EnabledForLocalPlay = true,
                Cost = CopyAmount(cost),
                Definition = new MachineDefinition {
                    MachineId = machineId, CharacterId = character.Id, CatalogVersion = catalogVersion, Rewards = rewards
                },
                Policy = new MachinePolicy {
                    PolicyId = policyId, Version = policyVersion,
                    CharacterChanceByDraw = characterChanceByDraw?.ToArray(),
                    RewardWeights = rewards.Where(r => r.Kind != MachineRewardKind.Character)
                        .Select(r => new MachineWeight { RewardId = r.RewardId, Weight = rewardWeight }).ToArray()
                },
                Packages = packages.Select(p => new PlayerMachinePackage {
                    RewardId = p.RewardId, Amounts = p.Amounts?.Select(CopyAmount).ToArray()
                }).ToArray()
            };
            configuration.Validate(characters);
            return configuration;
        }

        static PlayerMachineAmount CopyAmount(PlayerMachineAmount amount) => amount == null ? null :
            new PlayerMachineAmount { Currency = amount.Currency, Amount = amount.Amount };
    }
}
