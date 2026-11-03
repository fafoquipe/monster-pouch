using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Profiles
{
    public static partial class AndroidDeveloperContent
    {
        public const string Define = "MONSTER_POUCH_ANDROID_DEVBUILD";
        public const string IdentifierSuffix = ".devbuild";
        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return false;
#endif
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // The isolated development account opts in; ordinary profiles keep normal onboarding.
        // No wallet, match currency, purchased match upgrades or asset is modified here.
        public static bool GrantCatalogue(LocalGuestProfile profile, MatchConfig catalogue)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var units = catalogue?.Units;
            if (units == null) throw new ArgumentNullException(nameof(catalogue));
            profile.Validate();
            bool changed = profile.DevelopmentMode != DevelopmentAccountMode.AllContent;
            profile.DevelopmentMode = DevelopmentAccountMode.AllContent;
            string now = DateTime.UtcNow.ToString("O");
            if (profile.Starter == null)
            {
                string starter = StarterRoster.Monsters.FirstOrDefault(id => catalogue.Get(id)?.IsMonster == true);
                if (!StarterRoster.TryBuild(starter, catalogue, out var deck))
                    throw new ArgumentException("El catálogo no contiene el equipo inicial de desarrollo.");
                profile.StarterSelections.Add(new StarterSelection {
                    ReceiptId = StarterRoster.CurrentReceiptId, TimestampUtc = now, Deck = deck
                });
                changed = true;
            }
            if (profile.CollectionMode != GuestCollectionMode.StarterInventory || profile.HasLegacyAccess ||
                profile.OnboardingStage == GuestOnboardingStage.None || profile.OnboardingStage == GuestOnboardingStage.ChooseStarter)
            {
                profile.CollectionMode = GuestCollectionMode.StarterInventory;
                profile.HasLegacyAccess = false;
                // Skip the startup gate in the dev player without inventing completed tutorial receipts.
                profile.OnboardingStage = GuestOnboardingStage.Tutorial;
                changed = true;
            }
            // Resolve ownership once per grant, including the starter just selected above.
            // Do not rebuild the owned-character/ability lists for every catalogue entry.
            var ownedCharacters = new HashSet<string>(profile.GetOwnedCharacterIds(), StringComparer.Ordinal);
            var ownedAbilities = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var receipt in profile.AbilityUnlocks)
            {
                if (!ownedAbilities.TryGetValue(receipt.CharacterId, out var ids))
                    ownedAbilities.Add(receipt.CharacterId, ids = new HashSet<string>(StringComparer.Ordinal));
                ids.Add(receipt.AbilityId);
            }
            foreach (var unit in units.Where(unit => unit != null))
            {
                if (ownedCharacters.Add(unit.Id))
                {
                    profile.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt {
                        ReceiptId = "devbuild:character:" + unit.Id, CharacterId = unit.Id,
                        Source = GuestUnlockSource.DeveloperBuild, TimestampUtc = now
                    });
                    changed = true;
                }
                if (!ownedAbilities.TryGetValue(unit.Id, out var abilityIds))
                    ownedAbilities.Add(unit.Id, abilityIds = new HashSet<string>(StringComparer.Ordinal));
                foreach (var ability in (unit.Tricks ?? Array.Empty<TrickDefinition>()).Where(ability => ability != null))
                {
                    if (!abilityIds.Add(ability.Id)) continue;
                    profile.AbilityUnlocks.Add(new GuestAbilityUnlockReceipt {
                        ReceiptId = "devbuild:ability:" + unit.Id + ":" + ability.Id,
                        CharacterId = unit.Id, AbilityId = ability.Id,
                        Source = GuestUnlockSource.DeveloperBuild, TimestampUtc = now
                    });
                    changed = true;
                }
            }
            profile.Validate();
            return changed;
        }
#endif
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public sealed partial class LocalGuestProfileStore
    {
        internal LocalGuestProfile LoadDeveloperContent(MatchConfig catalogue)
        {
            using (Lock())
            {
                var profile = LoadExisting() ?? throw new InvalidOperationException("Abre primero el perfil DEV.");
                if (NeedsRecovery) throw new InvalidOperationException("The development profile needs recovery.");
                if (profile.DevelopmentMode == DevelopmentAccountMode.None)
                    throw new InvalidOperationException("Only an isolated development profile can enable catalogue access.");
                if (AndroidDeveloperContent.GrantCatalogue(profile, catalogue))
                {
                    profile.Revision = checked(profile.Revision + 1);
                    Save(profile);
                }
                return profile;
            }
        }
    }
#endif
}
