using System;
using System.Linq;
using MonsterPouch.Gameplay.Machines;

namespace MonsterPouch.Gameplay.Profiles
{
    public enum GuestProgressionStatus { Ready, Claimed, Opened, AlreadyApplied, Locked, InvalidRequest, NeedsConfiguration, RecoveryRequired, MissingProfile, StarterRequired, LegacyUnavailable, ClockRollback }
    public enum GuestMilestoneState { Locked, Claimable, Claimed }
    public enum GuestRoadRewardKind { Currency, BasicPouch, FactionPouch }
    [Serializable] public sealed class GuestRoadReward
    {
        public GuestRoadRewardKind Kind;
        public PermanentCurrency Currency;
        public long Amount;
    }
    [Serializable] public sealed class GuestProgressionWorld
    {
        public string Id, BoardId;
        public int StartTrophies, EndTrophies;
        public string[] CharacterIds;
    }
    [Serializable] public sealed class GuestProgressionMilestone
    {
        public string Id, WorldId;
        public int RequiredTrophies;
        public GuestRoadReward Reward;
    }
    [Serializable] public sealed class GuestProgressionClaim
    {
        public string MilestoneId, TimestampUtc, PouchId;
    }
    [Serializable] public sealed class GuestProgressionPouch
    {
        public string Id, MilestoneId, WorldId, ClaimedUtc;
        public GuestRoadRewardKind Kind;
        // Both random decisions and a uniform permutation are persisted at acquisition.
        // At opening the first still-unowned member is selected. Other acquisitions
        // cannot turn the 20% character branch into a duplicate prize.
        public int CharacterRoll, ResourceRoll;
        public int GroupRoll, EronyaWeight=3, LegacyWeight=1;
        public string[] CharacterOrder = Array.Empty<string>();
        public GuestPouchRoomConfiguration Configuration;
    }
    [Serializable] public sealed class GuestProgressionOpening
    {
        public string PouchId, TimestampUtc, CharacterId;
        public GuestPouchRewardKind RewardKind;
        public string[] EligibleCharacterIds = Array.Empty<string>();
        public PlayerMachineAmount[] Amounts = Array.Empty<PlayerMachineAmount>();
    }
    public sealed class GuestProgressionCharacterSnapshot { public string Id; public bool Owned; }
    public sealed class GuestProgressionWorldSnapshot
    {
        public string Id, BoardId;
        public int StartTrophies, EndTrophies, CharacterChanceBasisPoints;
        public bool Reached, IsCurrent, CanReceiveNewCharacter;
        public GuestProgressionCharacterSnapshot[] Characters;
    }
    public sealed class GuestProgressionMilestoneSnapshot
    {
        public string Id, WorldId;
        public int RequiredTrophies;
        public GuestRoadReward Reward;
        public GuestMilestoneState State;
    }
    public sealed class GuestProgressionPouchSnapshot
    {
        public string Id, WorldId;
        public GuestRoadRewardKind Kind;
        public bool Opened, CanOpen, CanReceiveNewCharacter;
        public int CharacterChanceBasisPoints;
    }
    public sealed class GuestProgressionSnapshot
    {
        public long CurrentTrophies, HighestTrophies;
        public string CurrentWorldId, FinalBoardId = "royal";
        public int CurrentStep;
        public bool Completed, IsAtFinalWorld;
        public GuestProgressionWorldSnapshot[] Worlds;
        public GuestProgressionMilestoneSnapshot[] Milestones;
        public GuestProgressionPouchSnapshot[] Pouches;
    }
    public sealed class GuestProgressionResult
    {
        public GuestProgressionStatus Status;
        public LocalGuestProfile Profile;
        public GuestProgressionSnapshot Snapshot;
        public GuestProgressionClaim ClaimReceipt;
        public GuestProgressionOpening OpenReceipt;
        public string Reason;
    }
    /// <summary>Reward themes only. Never changes IsMonster, battle faction or abilities.</summary>
    public static class GuestProgressionCatalogue
    {
        public const int WorldLength = 300, StepLength = 50, FinalTrophies = 1500, CharacterChanceBasisPoints = 2000;
        public const string ApprovalId = "eronya-trophy-road-v1", ReceiptPrefix = "road-v1:";
        public static int NewCharacterGroupWeight {get;set;}=3;
        public static int LegacyCharacterGroupWeight {get;set;}=1;
        public static bool IsEronya(string id) => new[]{"cael","guarda-veterano","arboreo","antidota","drayven","titanica","jabalinero","maestro-de-las-mareas",
            "korran","golem-tectonico","la-minera","protectora-del-mausoleo","serya","legionario","dagata","canonero-del-orvek",
            "alveyr","zephalontropo","alboguera","palista"}.Contains(id);
        internal static string SelectCharacter(string[] eligible,int groupRoll,int eronyaWeight,int legacyWeight)
        {
            if(eligible.Length==0)return null;
            var modern=eligible.Where(IsEronya).ToArray();var legacy=eligible.Where(id=>!IsEronya(id)).ToArray();
            if(modern.Length==0)return legacy[0];if(legacy.Length==0)return modern[0];
            return (long)groupRoll*(eronyaWeight+legacyWeight)<(long)eronyaWeight*10000?modern[0]:legacy[0];
        }
        // Detached arrays prevent UI callers from changing the reward catalogue.
        public static GuestProgressionWorld[] Worlds => new[] {
            World("naturalis", 0, "anuik,bugaloo,tauris,flo,rosie,kayon,kazuru,cael,guarda-veterano,arboreo,antidota"),
            World("aquarion", 300, "suki,pokrak,tokoro,tsu,hymay,drayven,titanica,jabalinero,maestro-de-las-mareas"),
            World("geothar", 600, "trimol,blotan,atong,korran,golem-tectonico,la-minera,protectora-del-mausoleo"),
            World("volkanar", 900, "gochan,jazar,atori,serya,legionario,dagata,canonero-del-orvek"),
            World("aireola", 1200, "sepora,stein,aky,bugui,martio,popow,alveyr,zephalontropo,alboguera,palista")
        };
        static GuestProgressionWorld World(string id, int start, string ids) => new GuestProgressionWorld {
            Id=id, BoardId=id, StartTrophies=start, EndTrophies=start+WorldLength,
            CharacterIds=ids.Split(',').OrderBy(x=>x,StringComparer.Ordinal).ToArray()
        };
        public static GuestProgressionMilestone[] Milestones => Worlds.SelectMany(w=>Enumerable.Range(1,6).Select(step=>
            new GuestProgressionMilestone {
                Id="road-v1-"+(w.StartTrophies+step*StepLength).ToString("D4",System.Globalization.CultureInfo.InvariantCulture),
                WorldId=w.Id, RequiredTrophies=w.StartTrophies+step*StepLength,
                // Existing 20-coin daily denominations; two faction pouches per world.
                Reward=step==3||step==6 ? new GuestRoadReward {Kind=GuestRoadRewardKind.FactionPouch} :
                    step==1||step==4 ? new GuestRoadReward {Kind=GuestRoadRewardKind.BasicPouch} :
                    new GuestRoadReward {Kind=GuestRoadRewardKind.Currency,
                        Currency=step==2?PermanentCurrency.Earthcoins:PermanentCurrency.Suncoins,Amount=20}
            })).ToArray();
        public static string CurrentWorld(long trophies) => trophies>=FinalTrophies?"royal":Worlds.Last(w=>w.StartTrophies<=Math.Max(0,trophies)).Id;
        public static int CurrentStep(long trophies) => (int)(Math.Min(FinalTrophies,Math.Max(0,trophies))/StepLength);
        public static GuestProgressionMilestone Milestone(string id) => Milestones.FirstOrDefault(m=>m.Id==id);
        public static GuestProgressionWorld GetWorld(string id) => Worlds.FirstOrDefault(w=>w.Id==id);
        internal static string ClaimId(string milestone) => ReceiptPrefix+"claim:"+milestone;
        internal static string OpenId(string pouch) => ReceiptPrefix+"open:"+pouch;
        internal static bool Reserved(string id) => id!=null&&id.StartsWith(ReceiptPrefix,StringComparison.Ordinal);
        internal static bool CharacterPrize(int roll,int eligibleCount) => eligibleCount>0&&roll<CharacterChanceBasisPoints;
        public static string[] ReachedWorldIds(LocalGuestProfile profile) => GuestTrophyLadder.ReachedBoardIds(profile.TrophyLadder)
            .Where(id=>GetWorld(id)!=null).Distinct(StringComparer.Ordinal).ToArray();
    }
}

