using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;

namespace MonsterPouch.Gameplay.Profiles
{
    public enum GuestCollectionMode { Legacy, StarterInventory }
    // Source labels for an explicitly authorized local unlock. This model does
    // not implement or automatically issue any machine, pouch or tutorial prize.
    public enum GuestUnlockSource { Tutorial = 0, NormalPouch = 1, Machine = 2, Mastery = 3
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        , DeveloperBuild = 4
#endif
        , Purchase = 5
    }
    [Serializable] public sealed class GuestCharacterUnlockReceipt
    {
        public string ReceiptId,CharacterId,TimestampUtc;
        public GuestUnlockSource Source;
    }
    public sealed partial class LocalGuestProfile
    {
        public GuestCollectionMode CollectionMode;
        public List<GuestCharacterUnlockReceipt> CharacterUnlocks=new List<GuestCharacterUnlockReceipt>();
        public bool HasAuthoritativeWhelpRoster=>CollectionMode==GuestCollectionMode.StarterInventory&&Starter!=null;

        public IReadOnlyList<string> GetOwnedCharacterIds()
        {
            var result=new List<string>();
            if(Starter!=null)
            {
                result.Add(Starter.Deck.Monster);
                result.AddRange(Starter.Deck.Whelps);
            }
            if(CharacterUnlocks!=null)result.AddRange(CharacterUnlocks.Select(receipt=>receipt.CharacterId));
            return result.AsReadOnly();
        }
        public bool OwnsCharacter(string characterId)=>!string.IsNullOrWhiteSpace(characterId)&&
            GetOwnedCharacterIds().Contains(characterId,StringComparer.Ordinal);

        public IReadOnlyList<string> GetAvailableWhelpIds(MatchConfig catalogue)=>catalogue==null?
            Array.Empty<string>():GetOwnedCharacterIds().Where(id=>catalogue.Get(id)?.IsMonster==false).ToArray();

        void ValidateCollection()
        {
            if(!Enum.IsDefined(typeof(GuestCollectionMode),CollectionMode)||CharacterUnlocks==null)
                throw new InvalidDataException("Missing guest collection records.");
            if(CollectionMode==GuestCollectionMode.Legacy&&CharacterUnlocks.Count!=0||
                CollectionMode==GuestCollectionMode.StarterInventory&&(HasLegacyAccess||OnboardingStage==GuestOnboardingStage.None)||
                Starter==null&&CharacterUnlocks.Count!=0)
                throw new InvalidDataException("Collection authority does not match this guest.");
            var receiptIds=new HashSet<string>(StringComparer.Ordinal);
            var characterIds=new HashSet<string>(Starter==null?Array.Empty<string>():
                new[]{Starter.Deck.Monster}.Concat(Starter.Deck.Whelps),StringComparer.Ordinal);
            foreach(var receipt in CharacterUnlocks)
            {
                if(receipt==null||string.IsNullOrWhiteSpace(receipt.ReceiptId)||receipt.ReceiptId.Length>160||
                    receipt.ReceiptId==StarterRoster.LegacyReceiptId||receipt.ReceiptId==StarterRoster.CurrentReceiptId||
                    !receiptIds.Add(receipt.ReceiptId)||string.IsNullOrWhiteSpace(receipt.CharacterId)||
                    !characterIds.Add(receipt.CharacterId)||!Enum.IsDefined(typeof(GuestUnlockSource),receipt.Source)||
                    !DateTime.TryParse(receipt.TimestampUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out _))
                    throw new InvalidDataException("Invalid or duplicate guest character unlock.");
            }
        }
    }
}
