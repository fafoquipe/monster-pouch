using System;
using System.Linq;
using System.Globalization;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfileStore
    {
        public GuestProgressionResult ReadProgression(MatchConfig catalogue, GuestPouchRoomConfiguration resources, DateTime utc)
        {
            if(!RoadInputs(catalogue,resources,utc,out _,out var reason)) return RoadResult(GuestProgressionStatus.NeedsConfiguration,reason:reason);
            using(Lock()) {
                var profile=LoadExisting(); var status=RoadStatus(profile,catalogue);
                return RoadResult(status,profile);
            }
        }
        public GuestProgressionResult ClaimProgressionMilestone(MatchConfig catalogue, GuestPouchRoomConfiguration resources,
            string milestoneId, DateTime utc) => ClaimProgressionMilestoneCore(catalogue,resources,milestoneId,utc,PouchRoll);

        internal GuestProgressionResult ClaimProgressionMilestoneCore(MatchConfig catalogue, GuestPouchRoomConfiguration resources,
            string milestoneId, DateTime utc, Func<int,int> roll)
        {
            var milestone=GuestProgressionCatalogue.Milestone(milestoneId);
            if(milestone==null||roll==null) return RoadResult(GuestProgressionStatus.InvalidRequest);
            if(!RoadInputs(catalogue,resources,utc,out var approved,out var reason)) return RoadResult(GuestProgressionStatus.NeedsConfiguration,reason:reason);
            using(Lock()) {
                var profile=LoadExisting(); var status=RoadStatus(profile,catalogue);
                if(status!=GuestProgressionStatus.Ready) return RoadResult(status,profile);
                var prior=profile.ProgressionClaims.FirstOrDefault(c=>c.MilestoneId==milestoneId);
                if(prior!=null) return RoadResult(GuestProgressionStatus.AlreadyApplied,profile,claim:prior);
                if(profile.HighestTrophies<milestone.RequiredTrophies) return RoadResult(GuestProgressionStatus.Locked,profile);
                var next=ClonePouchProfile(profile);
                var claim=new GuestProgressionClaim {MilestoneId=milestoneId,TimestampUtc=utc.ToString("O",CultureInfo.InvariantCulture)};
                if(milestone.Reward.Kind==GuestRoadRewardKind.Currency) {
                    next.WalletEvents.Add(new GuestWalletEvent {Id=GuestProgressionCatalogue.ClaimId(milestoneId),
                        SourceId=GuestProgressionCatalogue.ApprovalId,TimestampUtc=claim.TimestampUtc,Kind=GuestWalletEventKind.FreeCredit,
                        Amounts=new[]{new PlayerMachineAmount {Currency=milestone.Reward.Currency,Amount=milestone.Reward.Amount}}});
                } else {
                    claim.PouchId="road-pouch-"+milestoneId;
                    var order=milestone.Reward.Kind==GuestRoadRewardKind.FactionPouch?
                        GuestProgressionCatalogue.GetWorld(milestone.WorldId).CharacterIds:Array.Empty<string>();
                    try {
                        // Fisher-Yates gives a uniform first eligible member after later ownership changes.
                        for(int i=order.Length-1;i>0;i--) { int j=CheckedPouchRoll(roll,i+1); var old=order[i];order[i]=order[j];order[j]=old; }
                        next.ProgressionPouches.Add(new GuestProgressionPouch {Id=claim.PouchId,MilestoneId=milestoneId,
                            WorldId=milestone.WorldId,ClaimedUtc=claim.TimestampUtc,Kind=milestone.Reward.Kind,CharacterOrder=order,
                            CharacterRoll=milestone.Reward.Kind==GuestRoadRewardKind.FactionPouch?CheckedPouchRoll(roll,10000):-1,
                            GroupRoll=CheckedPouchRoll(roll,10000),EronyaWeight=GuestProgressionCatalogue.NewCharacterGroupWeight,LegacyWeight=GuestProgressionCatalogue.LegacyCharacterGroupWeight,
                            ResourceRoll=CheckedPouchRoll(roll,10000),Configuration=approved});
                    } catch(ArgumentOutOfRangeException error) {return RoadResult(GuestProgressionStatus.InvalidRequest,profile,reason:error.Message);}
                }
                next.ProgressionClaims.Add(claim); CommitPouchProfile(next);
                return RoadResult(GuestProgressionStatus.Claimed,next,claim:claim);
            }
        }
        public GuestProgressionResult OpenProgressionPouch(MatchConfig catalogue, GuestPouchRoomConfiguration resources,
            string pouchId, DateTime utc)
        {
            if(!GuestPouchRules.Id(pouchId)) return RoadResult(GuestProgressionStatus.InvalidRequest);
            if(!RoadInputs(catalogue,resources,utc,out _,out var reason)) return RoadResult(GuestProgressionStatus.NeedsConfiguration,reason:reason);
            using(Lock()) {
                var profile=LoadExisting(); var status=RoadStatus(profile,catalogue);
                if(status!=GuestProgressionStatus.Ready) return RoadResult(status,profile);
                var prior=profile.ProgressionOpenings.FirstOrDefault(p=>p.PouchId==pouchId);
                if(prior!=null) return RoadResult(GuestProgressionStatus.AlreadyApplied,profile,opening:prior);
                var pouch=profile.ProgressionPouches.FirstOrDefault(p=>p.Id==pouchId);
                if(pouch==null) return RoadResult(GuestProgressionStatus.InvalidRequest,profile);
                GuestPouchRules.Timestamp(pouch.ClaimedUtc,out var acquired);
                if(utc<acquired) return RoadResult(GuestProgressionStatus.ClockRollback,profile);
                var next=ClonePouchProfile(profile);
                var eligible=pouch.CharacterOrder.Where(id=>!next.OwnsCharacter(id)).ToArray();
                var opening=new GuestProgressionOpening {PouchId=pouch.Id,TimestampUtc=utc.ToString("O",CultureInfo.InvariantCulture),
                    EligibleCharacterIds=eligible,RewardKind=GuestProgressionCatalogue.CharacterPrize(pouch.CharacterRoll,eligible.Length)?
                        GuestPouchRewardKind.Character:GuestPouchRewardKind.Resources};
                string grantId=GuestProgressionCatalogue.OpenId(pouchId);
                if(opening.RewardKind==GuestPouchRewardKind.Character) {
                    opening.CharacterId=GuestProgressionCatalogue.SelectCharacter(eligible,pouch.GroupRoll,pouch.EronyaWeight,pouch.LegacyWeight);
                    next.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt {ReceiptId=grantId,CharacterId=opening.CharacterId,
                        Source=GuestUnlockSource.NormalPouch,TimestampUtc=opening.TimestampUtc});
                } else {
                    var resource=pouch.Configuration.At(pouch.ResourceRoll);
                    opening.Amounts=new[]{new PlayerMachineAmount {Currency=resource.Currency,Amount=resource.Amount}};
                    next.WalletEvents.Add(new GuestWalletEvent {Id=grantId,SourceId=GuestProgressionCatalogue.ApprovalId,
                        TimestampUtc=opening.TimestampUtc,Kind=GuestWalletEventKind.FreeCredit,Amounts=opening.Amounts});
                }
                next.ProgressionOpenings.Add(opening); CommitPouchProfile(next);
                return RoadResult(GuestProgressionStatus.Opened,next,opening:opening);
            }
        }
        static bool RoadInputs(MatchConfig catalogue,GuestPouchRoomConfiguration resources,DateTime utc,
            out GuestPouchRoomConfiguration approved,out string reason)
        {
            if(!PouchInputs(catalogue,resources,utc,out approved,out _,out reason))return false;
            if(GuestProgressionCatalogue.NewCharacterGroupWeight<=0||GuestProgressionCatalogue.LegacyCharacterGroupWeight<=0||
                (long)GuestProgressionCatalogue.NewCharacterGroupWeight+GuestProgressionCatalogue.LegacyCharacterGroupWeight>int.MaxValue) {
                reason="Positive character group weights within Int32 are required.";return false;
            }
            var ids=GuestProgressionCatalogue.Worlds.SelectMany(w=>w.CharacterIds).ToArray();
            if(ids.Length!=44||ids.Distinct(StringComparer.Ordinal).Count()!=44||ids.Any(id=>catalogue.Get(id)==null)) {
                reason="The complete unique 44-character reward catalogue is required.";return false;
            }
            return true;
        }
        GuestProgressionStatus RoadStatus(LocalGuestProfile profile,MatchConfig catalogue)
        {
            if(NeedsRecovery)return GuestProgressionStatus.RecoveryRequired;
            if(profile==null)return GuestProgressionStatus.MissingProfile;
            if(profile.CollectionMode!=GuestCollectionMode.StarterInventory)return GuestProgressionStatus.LegacyUnavailable;
            if(profile.Starter==null)return GuestProgressionStatus.StarterRequired;
            return GuestAbilityCatalogue.KnownCollection(profile,catalogue)?GuestProgressionStatus.Ready:GuestProgressionStatus.NeedsConfiguration;
        }
        static GuestProgressionResult RoadResult(GuestProgressionStatus status,LocalGuestProfile profile=null,
            GuestProgressionClaim claim=null,GuestProgressionOpening opening=null,string reason=null) => new GuestProgressionResult {
                Status=status,Profile=profile,Snapshot=profile==null?null:GuestProgressionView.Create(profile),
                ClaimReceipt=claim,OpenReceipt=opening,Reason=reason
            };
    }
    public static class GuestProgressionView
    {
        public static GuestProgressionSnapshot Create(LocalGuestProfile profile)
        {
            if(profile==null)throw new ArgumentNullException(nameof(profile));
            var current=GuestProgressionCatalogue.CurrentWorld(profile.Trophies);
            var reached=GuestProgressionCatalogue.ReachedWorldIds(profile);
            return new GuestProgressionSnapshot {
                CurrentTrophies=profile.Trophies,HighestTrophies=profile.HighestTrophies,CurrentWorldId=current,
                CurrentStep=GuestProgressionCatalogue.CurrentStep(profile.Trophies),Completed=profile.HighestTrophies>=1500,IsAtFinalWorld=profile.Trophies>=1500,
                Worlds=GuestProgressionCatalogue.Worlds.Select(w=>{
                    bool available=w.CharacterIds.Any(id=>!profile.OwnsCharacter(id));
                    return new GuestProgressionWorldSnapshot {Id=w.Id,BoardId=w.BoardId,StartTrophies=w.StartTrophies,EndTrophies=w.EndTrophies,
                        Reached=reached.Contains(w.Id),IsCurrent=current==w.Id,CanReceiveNewCharacter=available,
                        CharacterChanceBasisPoints=available?2000:0,
                        Characters=w.CharacterIds.Select(id=>new GuestProgressionCharacterSnapshot {Id=id,Owned=profile.OwnsCharacter(id)}).ToArray()};
                }).ToArray(),
                Milestones=GuestProgressionCatalogue.Milestones.Select(m=>new GuestProgressionMilestoneSnapshot {Id=m.Id,WorldId=m.WorldId,
                    RequiredTrophies=m.RequiredTrophies,Reward=m.Reward,State=profile.ProgressionClaims.Any(c=>c.MilestoneId==m.Id)?GuestMilestoneState.Claimed:
                        profile.HighestTrophies>=m.RequiredTrophies?GuestMilestoneState.Claimable:GuestMilestoneState.Locked}).ToArray(),
                Pouches=profile.ProgressionPouches.Select(p=>{
                    bool opened=profile.ProgressionOpenings.Any(o=>o.PouchId==p.Id),available=p.CharacterOrder.Any(id=>!profile.OwnsCharacter(id));
                    return new GuestProgressionPouchSnapshot {Id=p.Id,WorldId=p.WorldId,Kind=p.Kind,Opened=opened,CanOpen=!opened,
                        CanReceiveNewCharacter=available,CharacterChanceBasisPoints=available?2000:0};
                }).ToArray()
            };
        }
    }
}
