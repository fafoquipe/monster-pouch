using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;
using UnityEngine;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfileStore
    {
        public GuestPouchRoomResult ReadPouchRoom(MatchConfig catalogue, GuestPouchRoomConfiguration configuration, DateTime utc)
        {
            if (!PouchInputs(catalogue, configuration, utc, out _, out string day, out string error))
                return PouchResult(GuestPouchRoomStatus.NeedsConfiguration, reason: error);
            using (Lock())
            {
                var profile = LoadExisting();
                var status = PouchProfileStatus(profile, catalogue, day);
                if (status != GuestPouchRoomStatus.Ready) return PouchResult(status);
                if (AdvancePouchDay(profile, day)) CommitPouchProfile(profile);
                return PouchResult(GuestPouchRoomStatus.Ready, profile);
            }
        }

        public GuestPouchRoomResult RecordPouchVictory(MatchConfig catalogue, GuestPouchRoomConfiguration configuration,
            GuestPouchVictoryRequest request, DateTime utc) => RecordPouchVictoryCore(catalogue, configuration, request, utc, PouchRoll);

        internal GuestPouchRoomResult RecordPouchVictoryCore(MatchConfig catalogue, GuestPouchRoomConfiguration configuration,
            GuestPouchVictoryRequest request, DateTime utc, Func<int, int> roll)
        {
            if (request == null || !GuestPouchRules.Id(request.MatchId) || !GuestPouchRules.Id(request.SourceModeId, 64) ||
                request.CompletedUtc.Kind != DateTimeKind.Utc || utc.Kind != DateTimeKind.Utc || request.CompletedUtc > utc || roll == null)
                return PouchResult(GuestPouchRoomStatus.InvalidRequest);
            if (!request.Completed || !request.Won) return PouchResult(GuestPouchRoomStatus.Ineligible);
            if (!PouchInputs(catalogue, configuration, utc, out var approved, out string day, out string error))
                return PouchResult(GuestPouchRoomStatus.NeedsConfiguration, reason: error);
            using (Lock())
            {
                var profile = LoadExisting();
                var status = PouchProfileStatus(profile, catalogue, day);
                if (status != GuestPouchRoomStatus.Ready) return PouchResult(status);
                string completed = request.CompletedUtc.ToString("O", CultureInfo.InvariantCulture);
                var prior = profile.PouchVictories.FirstOrDefault(r => r.MatchId == request.MatchId);
                if (prior != null)
                {
                    if (prior.SourceModeId != request.SourceModeId || prior.CompletedUtc != completed)
                        return PouchResult(GuestPouchRoomStatus.Conflict, profile);
                    if (AdvancePouchDay(profile, day)) CommitPouchProfile(profile);
                    return PouchResult(GuestPouchRoomStatus.AlreadyApplied, profile, victory: prior);
                }
                var next = ClonePouchProfile(profile);
                AdvancePouchDay(next, day);
                int slot = next.Pouches.Count(p => p.Day == day);
                string completedDay = GuestPouchRules.Day(request.CompletedUtc);
                var receipt = new GuestPouchVictoryReceipt {
                    MatchId = request.MatchId, SourceModeId = request.SourceModeId, CompletedUtc = completed,
                    RecordedUtc = utc.ToString("O", CultureInfo.InvariantCulture), Day = completedDay,
                    Status = completedDay != day ? GuestPouchRoomStatus.Expired : GuestPouchRoomStatus.Granted
                };
                if (receipt.Status == GuestPouchRoomStatus.Granted)
                {
                    GuestPouchInstance pouch;
                    try { pouch = CreatePouch(catalogue, next, approved, request.MatchId, day, slot, roll); }
                    catch (ArgumentOutOfRangeException invalidRoll) { return PouchResult(GuestPouchRoomStatus.InvalidRequest, reason: invalidRoll.Message); }
                    receipt.PouchId = pouch.Id;
                    next.Pouches.Add(pouch);
                }
                // Late victories keep a receipt too: a retry on another day
                // must never convert an already handled match into a new opportunity.
                next.PouchVictories.Add(receipt);
                CommitPouchProfile(next);
                return PouchResult(receipt.Status, next, victory: receipt);
            }
        }

        public GuestPouchRoomResult OpenPouch(MatchConfig catalogue, GuestPouchRoomConfiguration configuration,
            string pouchId, string requestId, DateTime utc)
        {
            if (!GuestPouchRules.Id(pouchId) || !GuestPouchRules.Id(requestId, GuestPouchRules.OpenRequestIdMaximum))
                return PouchResult(GuestPouchRoomStatus.InvalidRequest);
            if (!PouchInputs(catalogue, configuration, utc, out _, out string day, out string error))
                return PouchResult(GuestPouchRoomStatus.NeedsConfiguration, reason: error);
            using (Lock())
            {
                var profile = LoadExisting();
                var status = PouchProfileStatus(profile, catalogue, day);
                if (status != GuestPouchRoomStatus.Ready) return PouchResult(status);
                var previousRequest = profile.PouchOpenings.FirstOrDefault(r => r.RequestId == requestId);
                if (previousRequest != null && previousRequest.PouchId != pouchId)
                    return PouchResult(GuestPouchRoomStatus.Conflict, profile);
                var pouch = profile.Pouches.FirstOrDefault(p => p.Id == pouchId);
                if (pouch == null) return PouchResult(GuestPouchRoomStatus.InvalidRequest, profile);
                var previous = profile.PouchOpenings.FirstOrDefault(r => r.PouchId == pouchId);
                if (previous != null)
                {
                    if (AdvancePouchDay(profile, day)) CommitPouchProfile(profile);
                    return PouchResult(GuestPouchRoomStatus.AlreadyApplied, profile, opening: previous);
                }
                var next = ClonePouchProfile(profile);
                bool advanced = AdvancePouchDay(next, day);
                if (pouch.Day != day)
                {
                    if (advanced) CommitPouchProfile(next);
                    return PouchResult(GuestPouchRoomStatus.Expired, next);
                }
                string unlockId = GuestPouchRules.UnlockId(requestId);
                if (next.WalletEvents.Any(e => e.Id == unlockId) || next.CharacterUnlocks.Any(r => r.ReceiptId == unlockId) ||
                    next.AbilityUnlocks.Any(r => r.ReceiptId == unlockId))
                    return PouchResult(GuestPouchRoomStatus.Conflict, profile);
                bool owned = pouch.Kind == GuestPouchKind.Character && next.OwnsCharacter(pouch.CharacterId);
                var eligible=pouch.Kind==GuestPouchKind.Faction?pouch.CharacterOrder.Where(id=>!next.OwnsCharacter(id)).ToArray():Array.Empty<string>();
                var receipt = new GuestPouchOpenReceipt {
                    RequestId = requestId, PouchId = pouchId, TimestampUtc = utc.ToString("O", CultureInfo.InvariantCulture),
                    CharacterOwnedBefore = owned, RewardRulesVersion = GuestPouchRules.CurrentRewardRulesVersion,
                    EligibleCharacterIds=eligible,
                    RewardKind = (pouch.Kind == GuestPouchKind.Character && !owned && pouch.CharacterRoll < GuestPouchRules.CharacterChances)||
                        (pouch.Kind==GuestPouchKind.Faction&&GuestProgressionCatalogue.CharacterPrize(pouch.CharacterRoll,eligible.Length)) ?
                        GuestPouchRewardKind.Character : GuestPouchRewardKind.Resources
                };
                if (receipt.RewardKind == GuestPouchRewardKind.Character)
                {
                    receipt.CharacterId = pouch.Kind==GuestPouchKind.Faction?
                        GuestProgressionCatalogue.SelectCharacter(eligible,pouch.GroupRoll,pouch.EronyaWeight,pouch.LegacyWeight):pouch.CharacterId;
                    next.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt {
                        ReceiptId = unlockId, CharacterId = receipt.CharacterId, Source = GuestUnlockSource.NormalPouch, TimestampUtc = receipt.TimestampUtc
                    });
                }
                else
                {
                    // Each acquired pouch owns its immutable prize table snapshot.
                    // Live table updates cannot alter an already earned consumable.
                    var resource = pouch.Configuration.At(pouch.ResourceRoll);
                    receipt.Amounts = new[] { new PlayerMachineAmount { Currency = resource.Currency, Amount = resource.Amount } };
                    next.WalletEvents.Add(new GuestWalletEvent {
                        Id = unlockId, SourceId = pouch.Configuration.ApprovalId, TimestampUtc = receipt.TimestampUtc,
                        Kind = GuestWalletEventKind.FreeCredit,
                        Amounts = new[] { new PlayerMachineAmount { Currency = resource.Currency, Amount = resource.Amount } }
                    });
                }
                next.PouchOpenings.Add(receipt);
                CommitPouchProfile(next);
                return PouchResult(GuestPouchRoomStatus.Opened, next, opening: receipt);
            }
        }

        static GuestPouchInstance CreatePouch(MatchConfig catalogue, LocalGuestProfile profile,
            GuestPouchRoomConfiguration configuration, string matchId, string day, int slot, Func<int, int> roll)
        {
            var kind = CheckedPouchRoll(roll,10000)<7000?GuestPouchKind.Blue:GuestPouchKind.Faction;
            string worldId=null;var order=Array.Empty<string>();
            if(kind==GuestPouchKind.Faction) {
                var reached=GuestProgressionCatalogue.ReachedWorldIds(profile);
                var worlds=GuestProgressionCatalogue.Worlds.Where(w=>reached.Contains(w.Id)).ToArray();
                var world=worlds[CheckedPouchRoll(roll,worlds.Length)];worldId=world.Id;order=world.CharacterIds;
                for(int i=order.Length-1;i>0;i--){int j=CheckedPouchRoll(roll,i+1);var old=order[i];order[i]=order[j];order[j]=old;}
            }
            return new GuestPouchInstance {
                Id = Guid.NewGuid().ToString("N"), MatchId = matchId, Day = day, SlotIndex = slot, Kind = kind, CharacterId = string.Empty,
                WorldId=worldId,CharacterOrder=order,GroupRoll=CheckedPouchRoll(roll,10000),
                EronyaWeight=GuestProgressionCatalogue.NewCharacterGroupWeight,LegacyWeight=GuestProgressionCatalogue.LegacyCharacterGroupWeight,
                CharacterRoll = kind == GuestPouchKind.Faction ? CheckedPouchRoll(roll, 10000) : -1,
                ResourceRoll = CheckedPouchRoll(roll, 10000), Configuration = configuration.Snapshot()
            };
        }
        static int CheckedPouchRoll(Func<int, int> roll, int maximum)
        {
            int sample = roll(maximum);
            if (sample < 0 || sample >= maximum) throw new ArgumentOutOfRangeException(nameof(roll));
            return sample;
        }
        static int PouchRoll(int maximum)
        {
            using (var random = RandomNumberGenerator.Create())
            {
                var bytes = new byte[4]; uint sample;
                ulong range = 1UL << 32, limit = range - range % (uint)maximum;
                do { random.GetBytes(bytes); sample = BitConverter.ToUInt32(bytes, 0); } while ((ulong)sample >= limit);
                return (int)(sample % maximum);
            }
        }
        static bool PouchInputs(MatchConfig catalogue, GuestPouchRoomConfiguration input, DateTime utc,
            out GuestPouchRoomConfiguration configuration, out string day, out string reason)
        {
            configuration = null; day = null; reason = null;
            try
            {
                day = GuestPouchRules.Day(utc);
                if (input == null || catalogue?.Units == null || catalogue.Units.Length == 0 ||
                    catalogue.Units.Any(u => u == null || !GuestPouchRules.Id(u.Id)) ||
                    catalogue.Units.Select(u => u.Id).Distinct(StringComparer.Ordinal).Count() != catalogue.Units.Length)
                    throw new ArgumentException("The approved table and current unique character catalogue are required.");
                configuration = input.Snapshot(); return true;
            }
            catch (ArgumentException error) { reason = error.Message; return false; }
        }
        GuestPouchRoomStatus PouchProfileStatus(LocalGuestProfile profile, MatchConfig catalogue, string day)
        {
            if (NeedsRecovery) return GuestPouchRoomStatus.RecoveryRequired;
            if (profile == null) return GuestPouchRoomStatus.MissingProfile;
            if (profile.CollectionMode != GuestCollectionMode.StarterInventory) return GuestPouchRoomStatus.LegacyUnavailable;
            if (profile.Starter == null) return GuestPouchRoomStatus.StarterRequired;
            if (!GuestAbilityCatalogue.KnownCollection(profile, catalogue) ||
                GuestProgressionCatalogue.Worlds.SelectMany(w=>w.CharacterIds).Any(id=>catalogue.Get(id)==null)||
                GuestProgressionCatalogue.NewCharacterGroupWeight<=0||GuestProgressionCatalogue.LegacyCharacterGroupWeight<=0||
                (long)GuestProgressionCatalogue.NewCharacterGroupWeight+GuestProgressionCatalogue.LegacyCharacterGroupWeight>int.MaxValue||
                profile.Pouches.Any(p => p.Kind == GuestPouchKind.Character && !GuestAbilityCatalogue.KnownCharacter(catalogue, p.CharacterId)))
                return GuestPouchRoomStatus.NeedsConfiguration;
            if (string.CompareOrdinal(day, profile.PouchRoomDay) < 0) return GuestPouchRoomStatus.ClockRollback;
            return GuestPouchRoomStatus.Ready;
        }
        static bool AdvancePouchDay(LocalGuestProfile profile, string day)
        {
            if (profile.PouchRoomDay == day) return false;
            // Previous acquisitions remain audit receipts, but can never be
            // opened or occupy the new day's opportunities after this boundary.
            profile.PouchRoomDay = day; return true;
        }
        void CommitPouchProfile(LocalGuestProfile profile)
        {
            profile.Revision = checked(profile.Revision + 1); Save(profile);
        }
        static LocalGuestProfile ClonePouchProfile(LocalGuestProfile profile) =>
            JsonUtility.FromJson<LocalGuestProfile>(JsonUtility.ToJson(profile));
        static GuestPouchRoomResult PouchResult(GuestPouchRoomStatus status, LocalGuestProfile profile = null,
            GuestPouchVictoryReceipt victory = null, GuestPouchOpenReceipt opening = null, string reason = null)
        {
            var result = new GuestPouchRoomResult {
                Status = status, Profile = profile, VictoryReceipt = victory, OpenReceipt = opening, Reason = reason
            };
            if (profile == null) return result;
            var today = profile.Pouches.Where(p => p.Day == profile.PouchRoomDay).ToDictionary(p => p.SlotIndex);
            var openedIds = new HashSet<string>(profile.PouchOpenings.Select(r => r.PouchId), StringComparer.Ordinal);
            int slotCount = Math.Max(GuestPouchRules.CycleLength,
                ((today.Count + GuestPouchRules.CycleLength - 1) / GuestPouchRules.CycleLength) * GuestPouchRules.CycleLength);
            result.Snapshot = new GuestPouchRoomSnapshot {
                Day = profile.PouchRoomDay, EarnedCount = today.Count,
                Slots = Enumerable.Range(0, slotCount).Select(index => {
                    today.TryGetValue(index, out var pouch);
                    bool opened = pouch != null && openedIds.Contains(pouch.Id);
                    return new GuestPouchSlotSnapshot {
                        Index = index, PouchId = pouch?.Id, CharacterId = pouch?.CharacterId,
                        Kind = pouch?.Kind ?? GuestPouchKind.Empty, Opened = opened, CanOpen = pouch != null && !opened
                        ,WorldId=pouch?.WorldId,CanReceiveNewCharacter=pouch!=null&&pouch.Kind==GuestPouchKind.Faction&&pouch.CharacterOrder.Any(id=>!profile.OwnsCharacter(id)),
                        CharacterChanceBasisPoints=pouch!=null&&pouch.Kind==GuestPouchKind.Faction&&pouch.CharacterOrder.Any(id=>!profile.OwnsCharacter(id))?2000:0
                    };
                }).ToArray()
            };
            return result;
        }
    }
}
