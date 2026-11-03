using System;
using System.Globalization;
using System.Linq;
using MonsterPouch.Gameplay.Machines;
using UnityEngine;

namespace MonsterPouch.Gameplay.Profiles
{
    public enum GuestPouchKind { Empty, Blue, Character, Faction }
    public enum GuestPouchRewardKind { Resources, Character }
    public enum GuestPouchRoomStatus
    {
        Ready, Granted, Opened, AlreadyApplied, DailyLimitReached, Expired, Ineligible, Conflict,
        InvalidRequest, NeedsConfiguration, RecoveryRequired, MissingProfile, StarterRequired,
        LegacyUnavailable, ClockRollback
    }
    [Serializable] public sealed class GuestPouchResourceReward
    {
        public PermanentCurrency Currency;
        public long Amount;
        public int Chances;
    }
    /// <summary>Explicit resource table supplied by the approved daily-gift adapter.</summary>
    [Serializable] public sealed class GuestPouchRoomConfiguration
    {
        public int Version = 1;
        public string ApprovalId, ResourceTableId;
        public GuestPouchResourceReward[] Resources;
        public void Validate()
        {
            if (Version != 1 || !GuestPouchRules.Id(ApprovalId, 160) || !GuestPouchRules.Id(ResourceTableId, 160) ||
                Resources == null || Resources.Length == 0 || Resources.Any(r => r == null || r.Amount <= 0 ||
                    r.Chances <= 0 || !Enum.IsDefined(typeof(PermanentCurrency), r.Currency)) ||
                Resources.Sum(r => (long)r.Chances) != 10000)
                throw new ArgumentException("An explicit approved pouch resource table totaling 10000 is required.");
        }
        public GuestPouchRoomConfiguration Snapshot()
        {
            Validate();
            return JsonUtility.FromJson<GuestPouchRoomConfiguration>(JsonUtility.ToJson(this));
        }
        internal GuestPouchResourceReward At(int roll)
        {
            int end = 0;
            foreach (var reward in Resources) { end += reward.Chances; if (roll < end) return reward; }
            throw new ArgumentException("Invalid pouch resource roll.");
        }
    }
    public sealed class GuestPouchVictoryRequest
    {
        public string MatchId, SourceModeId;
        public bool Completed, Won;
        public DateTime CompletedUtc;
    }
    [Serializable] public sealed class GuestPouchVictoryReceipt
    {
        public string MatchId, SourceModeId, CompletedUtc, RecordedUtc, Day, PouchId;
        public GuestPouchRoomStatus Status;
    }
    [Serializable] public sealed class GuestPouchInstance
    {
        public string Id, MatchId, Day, CharacterId;
        public int SlotIndex;
        public GuestPouchKind Kind;
        // Persisted with acquisition, so a failed opening or different request ID
        // cannot reroll this consumable. Ownership is still checked when opened.
        public int CharacterRoll = -1, ResourceRoll;
        public string WorldId;
        public string[] CharacterOrder = Array.Empty<string>();
        public int GroupRoll, EronyaWeight=3, LegacyWeight=1;
        public GuestPouchRoomConfiguration Configuration;
    }
    [Serializable] public sealed class GuestPouchOpenReceipt
    {
        public string RequestId, PouchId, TimestampUtc, CharacterId;
        // Missing/zero preserves already committed historical openings. New openings use rule 1.
        public int RewardRulesVersion;
        public GuestPouchRewardKind RewardKind;
        public bool CharacterOwnedBefore;
        public string[] EligibleCharacterIds = Array.Empty<string>();
        public PlayerMachineAmount[] Amounts = Array.Empty<PlayerMachineAmount>();
    }
    public sealed class GuestPouchSlotSnapshot
    {
        public int Index;
        public string PouchId, CharacterId;
        public GuestPouchKind Kind;
        public bool Opened, CanOpen;
        public string WorldId;
        public bool CanReceiveNewCharacter;
        public int CharacterChanceBasisPoints;
    }
    public sealed class GuestPouchRoomSnapshot
    {
        public string Day;
        public int EarnedCount;
        public GuestPouchSlotSnapshot[] Slots = Array.Empty<GuestPouchSlotSnapshot>();
    }
    public sealed class GuestPouchRoomResult
    {
        public GuestPouchRoomStatus Status;
        public LocalGuestProfile Profile;
        public GuestPouchRoomSnapshot Snapshot;
        public GuestPouchVictoryReceipt VictoryReceipt;
        public GuestPouchOpenReceipt OpenReceipt;
        public string Reason;
    }
    internal static class GuestPouchRules
    {
        internal const int CycleLength = 5, InitialBlue = 3, BlueChances = 7500;
        internal const int CurrentRewardRulesVersion = 2, CharacterChances = 3000, LegacyCharacterChances = 50;
        internal static int CharacterChancesFor(GuestPouchOpenReceipt receipt) =>
            receipt.RewardRulesVersion == 0 ? LegacyCharacterChances : CharacterChances;
        internal const int OpenRequestIdMaximum = 109;
        internal static bool Id(string value, int maximum = 120) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;
        internal static string Day(DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC time is required.");
            return utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        internal static bool ValidDay(string day) => DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out _);
        internal static bool Timestamp(string value, out DateTime utc) => DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out utc) && utc.Kind == DateTimeKind.Utc;
        internal static string UnlockId(string requestId) => "pouch-open:" + requestId;
    }
}
