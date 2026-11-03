using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;

namespace MonsterPouch.Gameplay.Machines
{
    public enum GuestMachineOwnershipStatus
    {
        Ready, MissingProfile, InvalidProfile, LegacyUnavailable, StarterRequired,
        InvalidCatalogue, InvalidMachine, RecoveryRequired
    }

    /// <summary>
    /// Converts explicit permanent guest receipts, never QA or playable legacy
    /// access. It neither changes inventory nor settles a machine transaction.
    /// Store-backed callers must use ReadMachineOwnership to check recovery.
    /// </summary>
    public static class GuestMachineOwnershipAdapter
    {
        public static GuestMachineOwnershipStatus TryCreate(LocalGuestProfile profile,MatchConfig catalogue,
            MachineDefinition machine,out MachineOwnership ownership)
        {
            ownership=null;
            if(profile==null)return GuestMachineOwnershipStatus.MissingProfile;
            try{profile.Validate();}
            catch(Exception error) when(error is InvalidDataException||error is NotSupportedException)
            {return GuestMachineOwnershipStatus.InvalidProfile;}
            if(profile.CollectionMode==GuestCollectionMode.Legacy)return GuestMachineOwnershipStatus.LegacyUnavailable;
            if(profile.Starter==null)return GuestMachineOwnershipStatus.StarterRequired;
            if(catalogue==null||!GuestAbilityCatalogue.KnownCollection(profile,catalogue))
                return GuestMachineOwnershipStatus.InvalidCatalogue;
            try{MachineDrawEngine.CreateState(machine);}
            catch(ArgumentException){return GuestMachineOwnershipStatus.InvalidMachine;}
            var character=GuestAbilityCatalogue.Character(catalogue,machine.CharacterId);
            if(character?.Tricks==null||character.Tricks.Length!=3||
                character.Tricks.Any(ability=>ability==null||string.IsNullOrWhiteSpace(ability.Id))||
                character.Tricks.Select(ability=>ability.Id).Distinct(StringComparer.Ordinal).Count()!=3)
                return GuestMachineOwnershipStatus.InvalidCatalogue;
            var machineAbilities=machine.Rewards.Where(reward=>reward.Kind==MachineRewardKind.Ability)
                .Select(reward=>reward.AbilityId).OrderBy(id=>id,StringComparer.Ordinal);
            if(!machineAbilities.SequenceEqual(character.Tricks.Select(ability=>ability.Id).OrderBy(id=>id,StringComparer.Ordinal),StringComparer.Ordinal))
                return GuestMachineOwnershipStatus.InvalidMachine;
            ownership=new MachineOwnership{
                CharacterOwned=profile.OwnsCharacter(machine.CharacterId),
                AbilityIds=profile.GetOwnedAbilityIds(machine.CharacterId).ToArray()
            };
            return GuestMachineOwnershipStatus.Ready;
        }
    }
}
