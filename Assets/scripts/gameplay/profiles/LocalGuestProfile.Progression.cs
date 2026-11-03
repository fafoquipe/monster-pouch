using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfile
    {
        public List<GuestProgressionClaim> ProgressionClaims=new List<GuestProgressionClaim>();
        public List<GuestProgressionPouch> ProgressionPouches=new List<GuestProgressionPouch>();
        public List<GuestProgressionOpening> ProgressionOpenings=new List<GuestProgressionOpening>();
        public string ProgressionTutorialVisitedUtc;

        void ValidateProgressionRewards()
        {
            if(ProgressionClaims==null||ProgressionPouches==null||ProgressionOpenings==null)
                throw new InvalidDataException("Missing trophy-road collections.");
            if((ProgressionClaims.Count>0||ProgressionPouches.Count>0||ProgressionOpenings.Count>0||!string.IsNullOrEmpty(ProgressionTutorialVisitedUtc))&&
                (CollectionMode!=GuestCollectionMode.StarterInventory||Starter==null))
                throw new InvalidDataException("Trophy rewards require an authoritative starter inventory.");
            if(!string.IsNullOrEmpty(ProgressionTutorialVisitedUtc)&&(!GuestPouchRules.Timestamp(ProgressionTutorialVisitedUtc,out _)||
                !CompletedTutorialModules.Contains(GuestTutorialModules.ProgressionCollection)))
                throw new InvalidDataException("Invalid progression tutorial visit.");
            var claimIds=new HashSet<string>(StringComparer.Ordinal);
            var pouchIds=new HashSet<string>(StringComparer.Ordinal);
            var grants=new HashSet<string>(StringComparer.Ordinal);
            foreach(var claim in ProgressionClaims) {
                var milestone=GuestProgressionCatalogue.Milestone(claim?.MilestoneId);
                if(milestone==null||!claimIds.Add(claim.MilestoneId)||HighestTrophies<milestone.RequiredTrophies||
                    !GuestPouchRules.Timestamp(claim.TimestampUtc,out _))
                    throw new InvalidDataException("Invalid or repeated trophy milestone claim.");
                if(milestone.Reward.Kind==GuestRoadRewardKind.Currency) {
                    if(!string.IsNullOrEmpty(claim.PouchId))throw new InvalidDataException("A currency milestone cannot issue a pouch.");
                    string grant=GuestProgressionCatalogue.ClaimId(claim.MilestoneId);
                    RequireRoadCredit(grant,claim.TimestampUtc,milestone.Reward.Currency,milestone.Reward.Amount);grants.Add(grant);
                } else if(claim.PouchId!="road-pouch-"+claim.MilestoneId||!pouchIds.Add(claim.PouchId))
                    throw new InvalidDataException("Invalid milestone pouch identity.");
            }
            var acquired=new HashSet<string>(StringComparer.Ordinal);
            foreach(var pouch in ProgressionPouches) {
                if(pouch==null||!pouchIds.Contains(pouch.Id)||!acquired.Add(pouch.Id))throw new InvalidDataException("Orphan or duplicate road pouch.");
                var milestone=GuestProgressionCatalogue.Milestone(pouch.MilestoneId);
                var claim=ProgressionClaims.SingleOrDefault(c=>c.PouchId==pouch.Id);
                if(claim==null||claim.MilestoneId!=pouch.MilestoneId||pouch.ClaimedUtc!=claim.TimestampUtc||
                    pouch.Kind!=milestone.Reward.Kind||pouch.WorldId!=milestone.WorldId||pouch.Configuration==null||
                    pouch.ResourceRoll<0||pouch.ResourceRoll>=10000||pouch.CharacterOrder==null||pouch.GroupRoll<0||pouch.GroupRoll>=10000||
                    pouch.EronyaWeight<=0||pouch.LegacyWeight<=0||(long)pouch.EronyaWeight+pouch.LegacyWeight>int.MaxValue)
                    throw new InvalidDataException("Invalid road pouch acquisition.");
                try {pouch.Configuration.Validate();}catch(ArgumentException ex){throw new InvalidDataException("Invalid road resource table.",ex);}
                var pool=GuestProgressionCatalogue.GetWorld(pouch.WorldId).CharacterIds;
                if(pouch.Kind==GuestRoadRewardKind.BasicPouch ? pouch.CharacterRoll!=-1||pouch.CharacterOrder.Length!=0 :
                    pouch.CharacterRoll<0||pouch.CharacterRoll>=10000||!pouch.CharacterOrder.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(pool))
                    throw new InvalidDataException("Invalid committed road chance or foreign/duplicate character pool.");
            }
            if(!acquired.SetEquals(pouchIds))throw new InvalidDataException("A milestone is missing its pouch.");
            var opened=new HashSet<string>(StringComparer.Ordinal);
            foreach(var receipt in ProgressionOpenings) {
                if(receipt==null||!opened.Add(receipt.PouchId)||!acquired.Contains(receipt.PouchId)||receipt.EligibleCharacterIds==null||
                    receipt.Amounts==null||!GuestPouchRules.Timestamp(receipt.TimestampUtc,out var openedUtc))
                    throw new InvalidDataException("Invalid road opening receipt.");
                var pouch=ProgressionPouches.Single(p=>p.Id==receipt.PouchId);
                GuestPouchRules.Timestamp(pouch.ClaimedUtc,out var acquiredUtc);
                var eligible=receipt.EligibleCharacterIds;
                if(openedUtc<acquiredUtc||!pouch.CharacterOrder.Where(eligible.Contains).SequenceEqual(eligible)||
                    pouch.CharacterOrder.Except(eligible).Any(id=>!OwnsCharacter(id)))
                    throw new InvalidDataException("Invalid opening time or eligible character order.");
                bool character=GuestProgressionCatalogue.CharacterPrize(pouch.CharacterRoll,eligible.Length);
                string grantId=GuestProgressionCatalogue.OpenId(pouch.Id);
                if(character) {
                    var grant=CharacterUnlocks.SingleOrDefault(c=>c.ReceiptId==grantId);
                    if(receipt.RewardKind!=GuestPouchRewardKind.Character||receipt.CharacterId!=GuestProgressionCatalogue.SelectCharacter(eligible,pouch.GroupRoll,pouch.EronyaWeight,pouch.LegacyWeight)||receipt.Amounts.Length!=0||grant==null||
                        grant.CharacterId!=receipt.CharacterId||grant.TimestampUtc!=receipt.TimestampUtc||grant.Source!=GuestUnlockSource.NormalPouch||
                        WalletEvents.Any(e=>e.Id==grantId))throw new InvalidDataException("Road character prize did not commit with the opening.");
                } else {
                    var reward=pouch.Configuration.At(pouch.ResourceRoll);
                    if(receipt.RewardKind!=GuestPouchRewardKind.Resources||!string.IsNullOrEmpty(receipt.CharacterId)||receipt.Amounts.Length!=1||
                        receipt.Amounts[0]==null||receipt.Amounts[0].Currency!=reward.Currency||receipt.Amounts[0].Amount!=reward.Amount)
                        throw new InvalidDataException("Road resource result differs from the committed roll.");
                    RequireRoadCredit(grantId,receipt.TimestampUtc,reward.Currency,reward.Amount);
                }
                grants.Add(grantId);
            }
            if(WalletEvents.Any(e=>(GuestProgressionCatalogue.Reserved(e.Id)||e.SourceId==GuestProgressionCatalogue.ApprovalId)&&!grants.Contains(e.Id))||
                CharacterUnlocks.Any(c=>GuestProgressionCatalogue.Reserved(c.ReceiptId)&&!grants.Contains(c.ReceiptId))||
                AbilityUnlocks.Any(c=>GuestProgressionCatalogue.Reserved(c.ReceiptId)))
                throw new InvalidDataException("Orphan trophy-road grant.");
        }
        void RequireRoadCredit(string id,string timestamp,PermanentCurrency currency,long amount)
        {
            var credit=WalletEvents.SingleOrDefault(e=>e.Id==id);
            if(credit==null||credit.Kind!=GuestWalletEventKind.FreeCredit||credit.SourceId!=GuestProgressionCatalogue.ApprovalId||
                credit.TimestampUtc!=timestamp||credit.Amounts.Length!=1||credit.Amounts[0].Currency!=currency||credit.Amounts[0].Amount!=amount||
                CharacterUnlocks.Any(c=>c.ReceiptId==id))throw new InvalidDataException("Road credit did not commit with its receipt.");
        }
    }
}
