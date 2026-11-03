using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Profiles
{
    [Serializable] public sealed class GuestAbilityUnlockReceipt
    {
        public string ReceiptId,CharacterId,AbilityId,TimestampUtc;
        public GuestUnlockSource Source;
    }

    public sealed partial class LocalGuestProfile
    {
        // Permanent account receipts only. This does not alter in-match Tricks,
        // define initial ability ownership or grant a reward on profile creation.
        public List<GuestAbilityUnlockReceipt> AbilityUnlocks=new List<GuestAbilityUnlockReceipt>();

        public IReadOnlyList<string> GetOwnedAbilityIds(string characterId)=>
            (AbilityUnlocks??new List<GuestAbilityUnlockReceipt>())
                .Where(receipt=>receipt.CharacterId==characterId).Select(receipt=>receipt.AbilityId).ToArray();
        public bool OwnsAbility(string characterId,string abilityId)=>!string.IsNullOrWhiteSpace(abilityId)&&
            GetOwnedAbilityIds(characterId).Contains(abilityId,StringComparer.Ordinal);

        void ValidateAbilities()
        {
            if(AbilityUnlocks==null||
                AbilityUnlocks.Count!=0&&(CollectionMode!=GuestCollectionMode.StarterInventory||Starter==null))
                throw new InvalidDataException("Missing or unauthorized guest ability receipts.");
            var receipts=new HashSet<string>(CharacterUnlocks.Select(receipt=>receipt.ReceiptId),StringComparer.Ordinal);
            var abilities=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
            foreach(var receipt in AbilityUnlocks)
            {
                if(receipt==null||string.IsNullOrWhiteSpace(receipt.ReceiptId)||receipt.ReceiptId.Length>160||
                    receipt.ReceiptId==StarterRoster.LegacyReceiptId||receipt.ReceiptId==StarterRoster.CurrentReceiptId||
                    !receipts.Add(receipt.ReceiptId)||!OwnsCharacter(receipt.CharacterId)||
                    string.IsNullOrWhiteSpace(receipt.AbilityId)||!Enum.IsDefined(typeof(GuestUnlockSource),receipt.Source)||
                    !DateTime.TryParse(receipt.TimestampUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out _))
                    throw new InvalidDataException("Invalid or duplicate guest ability receipt.");
                if(!abilities.TryGetValue(receipt.CharacterId,out var ids))
                    abilities.Add(receipt.CharacterId,ids=new HashSet<string>(StringComparer.Ordinal));
                if(!ids.Add(receipt.AbilityId))throw new InvalidDataException("Duplicate permanent ability ownership.");
            }
        }
    }

    internal static class GuestAbilityCatalogue
    {
        internal static UnitDefinition Character(MatchConfig catalogue,string characterId)
        {
            if(catalogue?.Units==null||string.IsNullOrWhiteSpace(characterId))return null;
            var matches=catalogue.Units.Where(unit=>unit!=null&&unit.Id==characterId).Take(2).ToArray();
            return matches.Length==1?matches[0]:null;
        }
        internal static bool KnownCharacter(MatchConfig catalogue,string characterId)=>Character(catalogue,characterId)!=null;
        internal static bool KnownAbility(MatchConfig catalogue,string characterId,string abilityId)
        {
            var character=Character(catalogue,characterId);
            // Name, slot and EffectId are not persistent unlock identifiers.
            return !string.IsNullOrWhiteSpace(abilityId)&&character?.Tricks!=null&&
                character.Tricks.Count(ability=>ability!=null&&ability.Id==abilityId)==1;
        }
        internal static bool KnownCollection(LocalGuestProfile profile,MatchConfig catalogue)=>
            profile.GetOwnedCharacterIds().All(id=>KnownCharacter(catalogue,id))&&
            profile.AbilityUnlocks.All(receipt=>KnownAbility(catalogue,receipt.CharacterId,receipt.AbilityId));
    }
}
