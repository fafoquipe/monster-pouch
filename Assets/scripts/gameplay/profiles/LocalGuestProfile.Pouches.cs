using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfile
    {
        public string PouchRoomDay = string.Empty;
        public List<GuestPouchVictoryReceipt> PouchVictories = new List<GuestPouchVictoryReceipt>();
        public List<GuestPouchInstance> Pouches = new List<GuestPouchInstance>();
        public List<GuestPouchOpenReceipt> PouchOpenings = new List<GuestPouchOpenReceipt>();

        void ValidatePouchRoom()
        {
            if (PouchRoomDay == null || PouchRoomDay.Length > 0 && !GuestPouchRules.ValidDay(PouchRoomDay) ||
                PouchVictories == null || Pouches == null || PouchOpenings == null)
                throw new InvalidDataException("Missing or invalid pouch-room records.");
            if ((CollectionMode != GuestCollectionMode.StarterInventory || Starter == null || PouchRoomDay.Length == 0) &&
                (PouchVictories.Count != 0 || Pouches.Count != 0 || PouchOpenings.Count != 0))
                throw new InvalidDataException("Pouch inventory requires an initialized starter account and cycle.");
            var victories = new Dictionary<string, GuestPouchVictoryReceipt>(StringComparer.Ordinal);
            foreach (var victory in PouchVictories)
            {
                if (victory == null || !GuestPouchRules.Id(victory.MatchId) || !GuestPouchRules.Id(victory.SourceModeId, 64) ||
                    victories.ContainsKey(victory.MatchId) || !GuestPouchRules.Timestamp(victory.CompletedUtc, out var completed) ||
                    !GuestPouchRules.Timestamp(victory.RecordedUtc, out var recorded) || completed > recorded ||
                    victory.Day != GuestPouchRules.Day(completed) || string.CompareOrdinal(GuestPouchRules.Day(recorded), PouchRoomDay) > 0 ||
                    victory.Status != GuestPouchRoomStatus.Granted && victory.Status != GuestPouchRoomStatus.DailyLimitReached &&
                    victory.Status != GuestPouchRoomStatus.Expired)
                    throw new InvalidDataException("Invalid or repeated completed-victory receipt.");
                if (victory.Status == GuestPouchRoomStatus.Granted && (!GuestPouchRules.Id(victory.PouchId) || victory.Day != GuestPouchRules.Day(recorded)) ||
                    victory.Status != GuestPouchRoomStatus.Granted && !string.IsNullOrEmpty(victory.PouchId) ||
                    victory.Status == GuestPouchRoomStatus.DailyLimitReached && victory.Day != GuestPouchRules.Day(recorded) ||
                    victory.Status == GuestPouchRoomStatus.Expired && string.CompareOrdinal(victory.Day, GuestPouchRules.Day(recorded)) >= 0)
                    throw new InvalidDataException("Victory status does not match its cycle or pouch.");
                victories.Add(victory.MatchId, victory);
            }
            var instances = new Dictionary<string, GuestPouchInstance>(StringComparer.Ordinal);
            foreach (var pouch in Pouches)
            {
                if (pouch == null || !GuestPouchRules.Id(pouch.Id) || instances.ContainsKey(pouch.Id) ||
                    !GuestPouchRules.ValidDay(pouch.Day) || string.CompareOrdinal(pouch.Day, PouchRoomDay) > 0 ||
                    pouch.SlotIndex < 0 ||
                    pouch.Kind != GuestPouchKind.Blue && pouch.Kind != GuestPouchKind.Character && pouch.Kind!=GuestPouchKind.Faction ||
                    pouch.ResourceRoll < 0 || pouch.ResourceRoll >= 10000 || pouch.Configuration == null ||
                    pouch.MatchId == null || !victories.TryGetValue(pouch.MatchId, out var victory) ||
                    victory.Status != GuestPouchRoomStatus.Granted || victory.PouchId != pouch.Id || victory.Day != pouch.Day)
                    throw new InvalidDataException("Invalid pouch acquisition or victory link.");
                if (pouch.Kind == GuestPouchKind.Blue && (!string.IsNullOrEmpty(pouch.CharacterId) || pouch.CharacterRoll != -1) ||
                    pouch.Kind == GuestPouchKind.Character && (pouch.SlotIndex % GuestPouchRules.CycleLength < GuestPouchRules.InitialBlue ||
                        !GuestPouchRules.Id(pouch.CharacterId) || pouch.CharacterRoll < 0 || pouch.CharacterRoll >= 10000))
                    throw new InvalidDataException("Invalid pouch kind, character or committed roll.");
                if(pouch.Kind==GuestPouchKind.Faction) {
                    var world=GuestProgressionCatalogue.GetWorld(pouch.WorldId);
                    if(world==null||!GuestProgressionCatalogue.ReachedWorldIds(this).Contains(world.Id)||!string.IsNullOrEmpty(pouch.CharacterId)||
                        pouch.CharacterOrder==null||!pouch.CharacterOrder.OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(world.CharacterIds)||
                        pouch.CharacterRoll<0||pouch.CharacterRoll>=10000||pouch.GroupRoll<0||pouch.GroupRoll>=10000||
                        pouch.EronyaWeight<=0||pouch.LegacyWeight<=0||(long)pouch.EronyaWeight+pouch.LegacyWeight>int.MaxValue)
                        throw new InvalidDataException("Invalid faction case pool or committed random decisions.");
                }
                try { pouch.Configuration.Validate(); }
                catch (ArgumentException error) { throw new InvalidDataException("Invalid saved pouch configuration.", error); }
                instances.Add(pouch.Id, pouch);
            }
            foreach (var day in Pouches.GroupBy(p => p.Day))
                if (!day.Select(p => p.SlotIndex).OrderBy(i => i).SequenceEqual(Enumerable.Range(0, day.Count())))
                    throw new InvalidDataException("Daily pouch opportunities are repeated or out of order.");
            foreach (var victory in victories.Values)
                if (victory.Status == GuestPouchRoomStatus.Granted && !instances.ContainsKey(victory.PouchId) ||
                    victory.Status == GuestPouchRoomStatus.DailyLimitReached && Pouches.Count(p => p.Day == victory.Day) < GuestPouchRules.CycleLength)
                    throw new InvalidDataException("Victory receipt is missing its granted inventory or daily limit.");

            var requests = new HashSet<string>(StringComparer.Ordinal);
            var opened = new HashSet<string>(StringComparer.Ordinal);
            var resourceReceipts = new HashSet<string>(StringComparer.Ordinal);
            var characterReceipts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var receipt in PouchOpenings)
            {
                if (receipt == null || !GuestPouchRules.Id(receipt.RequestId, GuestPouchRules.OpenRequestIdMaximum) || !requests.Add(receipt.RequestId) ||
                    receipt.PouchId == null || !opened.Add(receipt.PouchId) || !instances.TryGetValue(receipt.PouchId, out var pouch) ||
                    !GuestPouchRules.Timestamp(receipt.TimestampUtc, out var openedUtc) || GuestPouchRules.Day(openedUtc) != pouch.Day ||
                    !Enum.IsDefined(typeof(GuestPouchRewardKind), receipt.RewardKind) || receipt.Amounts == null ||
                    receipt.RewardRulesVersion < 0 || receipt.RewardRulesVersion > GuestPouchRules.CurrentRewardRulesVersion)
                    throw new InvalidDataException("Invalid or repeated pouch opening.");
                string unlockId = GuestPouchRules.UnlockId(receipt.RequestId);
                bool characterPrize = pouch.Kind == GuestPouchKind.Character && !receipt.CharacterOwnedBefore &&
                    pouch.CharacterRoll < GuestPouchRules.CharacterChancesFor(receipt);
                string expectedCharacter=pouch.CharacterId;
                if(pouch.Kind==GuestPouchKind.Faction) {
                    var eligible=receipt.EligibleCharacterIds;
                    if(receipt.RewardRulesVersion!=2||receipt.CharacterOwnedBefore||eligible==null||
                        !pouch.CharacterOrder.Where(eligible.Contains).SequenceEqual(eligible)||pouch.CharacterOrder.Except(eligible).Any(id=>!OwnsCharacter(id)))
                        throw new InvalidDataException("Invalid faction case opening inventory.");
                    characterPrize=GuestProgressionCatalogue.CharacterPrize(pouch.CharacterRoll,eligible.Length);
                    expectedCharacter=GuestProgressionCatalogue.SelectCharacter(eligible,pouch.GroupRoll,pouch.EronyaWeight,pouch.LegacyWeight);
                }
                if (pouch.Kind == GuestPouchKind.Blue && receipt.CharacterOwnedBefore ||
                    receipt.CharacterOwnedBefore && !OwnsCharacter(pouch.CharacterId) ||
                    characterPrize != (receipt.RewardKind == GuestPouchRewardKind.Character))
                    throw new InvalidDataException("Pouch outcome differs from committed rolls and ownership.");
                if (characterPrize)
                {
                    var grant = CharacterUnlocks.SingleOrDefault(r => r.ReceiptId == unlockId);
                    if (receipt.CharacterId != expectedCharacter || receipt.Amounts.Length != 0 || grant == null ||
                        grant.CharacterId != expectedCharacter || grant.Source != GuestUnlockSource.NormalPouch || grant.TimestampUtc != receipt.TimestampUtc ||
                        WalletEvents.Any(e => e.Id == unlockId))
                        throw new InvalidDataException("Pouch character grant is missing from the same transaction.");
                    characterReceipts.Add(unlockId);
                }
                else
                {
                    var expected = pouch.Configuration.At(pouch.ResourceRoll);
                    var credit = WalletEvents.SingleOrDefault(e => e.Id == unlockId);
                    if (!string.IsNullOrEmpty(receipt.CharacterId) || receipt.Amounts.Length != 1 || receipt.Amounts[0] == null ||
                        receipt.Amounts[0].Currency != expected.Currency || receipt.Amounts[0].Amount != expected.Amount ||
                        credit == null || credit.Kind != GuestWalletEventKind.FreeCredit || credit.SourceId != pouch.Configuration.ApprovalId ||
                        credit.TimestampUtc != receipt.TimestampUtc || !SameAmounts(credit.Amounts, receipt.Amounts) ||
                        CharacterUnlocks.Any(r => r.ReceiptId == unlockId))
                        throw new InvalidDataException("Pouch currency grant is missing from the same transaction.");
                    resourceReceipts.Add(unlockId);
                }
            }
            if (WalletEvents.Any(e => e.Id.StartsWith("pouch-open:", StringComparison.Ordinal) && !resourceReceipts.Contains(e.Id)) ||
                CharacterUnlocks.Any(r => r.ReceiptId.StartsWith("pouch-open:", StringComparison.Ordinal) && !characterReceipts.Contains(r.ReceiptId)) ||
                AbilityUnlocks.Any(r => r.ReceiptId.StartsWith("pouch-open:", StringComparison.Ordinal)))
                throw new InvalidDataException("An orphan pouch grant has no consumed inventory receipt.");
        }
    }
}
