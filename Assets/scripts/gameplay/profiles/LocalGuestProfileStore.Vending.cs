using System;
using System.Linq;
using System.Security.Cryptography;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;
using UnityEngine;

namespace MonsterPouch.Gameplay.Profiles
{
    public enum GuestVendingStatus
    {
        Ready, Committed, AlreadyApplied, Conflict, InsufficientFunds, NeedsConfiguration,
        RecoveryRequired, MissingProfile, StarterRequired, LegacyUnavailable, InvalidRequest, Complete, Retired
    }
    public sealed class GuestVendingResult
    {
        public GuestVendingStatus Status;
        public LocalGuestProfile Profile;
        public MachinePreview Preview;
        public GuestWalletEvent Receipt;
        public long MachineRevision;
        public string Reason;
    }
    public sealed partial class LocalGuestProfileStore
    {
        /// <summary>Explicit earned grant only; callers must supply an approved, stable receipt.</summary>
        public GuestVendingResult CreditFreeCurrency(string receiptId,string approvedSourceId,PermanentCurrency currency,long amount)
        {
            if(!VendingId(receiptId,120)||!VendingId(approvedSourceId,160)||amount<=0||!Enum.IsDefined(typeof(PermanentCurrency),currency)||
                GuestPermanentEconomy.ReservedId(receiptId)||GuestProgressionCatalogue.Reserved(receiptId)||approvedSourceId==GuestProgressionCatalogue.ApprovalId||approvedSourceId==GuestPermanentEconomy.WelcomeApprovalId||
                approvedSourceId==GuestPermanentEconomy.AbilityPurchaseApprovalId||receiptId.StartsWith("tutorial-pouch:",StringComparison.Ordinal)||approvedSourceId==LocalGuestProfile.TutorialPouchApproval)
                return VendingResult(GuestVendingStatus.InvalidRequest);
            using(Lock())
            {
                var profile=LoadExisting();
                if(NeedsRecovery)return VendingResult(GuestVendingStatus.RecoveryRequired);
                if(profile==null)return VendingResult(GuestVendingStatus.MissingProfile);
                var previous=profile.WalletEvents.FirstOrDefault(e=>e.Id==receiptId);
                if(previous!=null)return VendingResult(previous.Kind==GuestWalletEventKind.FreeCredit&&previous.SourceId==approvedSourceId&&
                    previous.Amounts.Length==1&&previous.Amounts[0].Currency==currency&&previous.Amounts[0].Amount==amount?
                    GuestVendingStatus.AlreadyApplied:GuestVendingStatus.Conflict,profile,previous);
                var next=CloneVending(profile);var receipt=new GuestWalletEvent{
                    Id=receiptId,SourceId=approvedSourceId,TimestampUtc=DateTime.UtcNow.ToString("O"),Kind=GuestWalletEventKind.FreeCredit,
                    Amounts=new[]{new PlayerMachineAmount{Currency=currency,Amount=amount}}
                };
                next.WalletEvents.Add(receipt);next.Revision=checked(next.Revision+1);
                Save(next);return VendingResult(GuestVendingStatus.Committed,next,receipt);
            }
        }
        public GuestVendingResult PreviewMachine(MatchConfig catalogue,PlayerMachineConfiguration input)
        {
            return VendingResult(GuestVendingStatus.Retired,reason:"Character machines were replaced by the trophy road.");
        }
        /// <summary>Decides and persists debit, prize, ownership and machine state before returning a result.</summary>
        public GuestVendingResult DrawMachine(MatchConfig catalogue,PlayerMachineConfiguration configuration,MachineDrawRequest request)
            =>DrawMachineCore(catalogue,configuration,request,VendingSample);

        internal GuestVendingResult DrawMachineCore(MatchConfig catalogue,PlayerMachineConfiguration input,MachineDrawRequest request,Func<double> sample)
        {
            if(request==null||!VendingId(request.RequestId,120)||sample==null||GuestPermanentEconomy.ReservedId(request.RequestId))
                return VendingResult(GuestVendingStatus.InvalidRequest);
            if(!TryMachineConfiguration(catalogue,input,out var configuration,out var reason))
                return VendingResult(GuestVendingStatus.NeedsConfiguration,reason:reason);
            if(request.MachineId!=configuration.Definition.MachineId||request.CatalogVersion!=configuration.Definition.CatalogVersion||request.ExpectedRevision<0)
                return VendingResult(GuestVendingStatus.Conflict);
            using(Lock())
            {
                var profile=LoadExisting();var blocked=VendingProfileStatus(profile);
                if(blocked!=GuestVendingStatus.Ready)return VendingResult(blocked);
                var machine=profile.VendingMachines.FirstOrDefault(m=>m.Configuration.Definition.MachineId==request.MachineId);
                if(machine!=null&&machine.Configuration.Signature()!=configuration.Signature())
                    return VendingResult(GuestVendingStatus.Conflict,reason:"Saved machine configuration needs an explicit migration.");
                var previous=profile.WalletEvents.FirstOrDefault(e=>e.Id==request.RequestId);
                if(previous!=null)
                    return VendingResult(previous.Kind==GuestWalletEventKind.MachineDraw&&previous.MachineId==request.MachineId&&
                        previous.Draw.ExpectedRevision==request.ExpectedRevision?GuestVendingStatus.AlreadyApplied:GuestVendingStatus.Conflict,profile,previous);
                return VendingResult(GuestVendingStatus.Retired,profile,reason:"Character machines were replaced by the trophy road.");
            }
        }
        GuestVendingStatus VendingProfileStatus(LocalGuestProfile profile)
        {
            if(NeedsRecovery)return GuestVendingStatus.RecoveryRequired;
            if(profile==null)return GuestVendingStatus.MissingProfile;
            if(profile.CollectionMode==GuestCollectionMode.Legacy)return GuestVendingStatus.LegacyUnavailable;
            if(profile.Starter==null)return GuestVendingStatus.StarterRequired;
            return GuestVendingStatus.Ready;
        }
        static GuestVendingResult PrepareMachinePreview(LocalGuestProfile profile,MatchConfig catalogue,PlayerMachineConfiguration configuration)
        {
            var machine=profile.VendingMachines.FirstOrDefault(m=>m.Configuration.Definition.MachineId==configuration.Definition.MachineId);
            if(machine!=null&&machine.Configuration.Signature()!=configuration.Signature())return VendingResult(GuestVendingStatus.Conflict);
            if(GuestMachineOwnershipAdapter.TryCreate(profile,catalogue,configuration.Definition,out var ownership)!=GuestMachineOwnershipStatus.Ready)
                return VendingResult(GuestVendingStatus.NeedsConfiguration);
            var context=MachineDrawEngine.Prepare(configuration.Definition,configuration.Policy);
            var state=machine?.State??context.CreateState();var preview=context.Preview(state,ownership);
            return new GuestVendingResult{Status=preview.Status==MachineStatus.Ready?GuestVendingStatus.Ready:
                preview.Status==MachineStatus.Complete?GuestVendingStatus.Complete:GuestVendingStatus.NeedsConfiguration,
                Profile=profile,Preview=preview,MachineRevision=state.Revision,Reason=preview.Reason};
        }
        static bool TryMachineConfiguration(MatchConfig catalogue,PlayerMachineConfiguration input,out PlayerMachineConfiguration configuration,out string reason)
        {
            configuration=null;reason=null;
            try
            {
                if(input==null)throw new ArgumentException("No approved machine configuration is available.");
                input.Validate(catalogue);configuration=input.Snapshot();return true;
            }
            catch(ArgumentException error){reason=error.Message;return false;}
        }
        static bool VendingId(string value,int maximum)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=maximum;
        static LocalGuestProfile CloneVending(LocalGuestProfile value)=>JsonUtility.FromJson<LocalGuestProfile>(JsonUtility.ToJson(value));
        static GuestVendingResult VendingResult(GuestVendingStatus status,LocalGuestProfile profile=null,GuestWalletEvent receipt=null,string reason=null)=>
            new GuestVendingResult{Status=status,Profile=profile,Receipt=receipt,Reason=reason};
        static double VendingSample()
        {
            using(var random=RandomNumberGenerator.Create())
            {var bytes=new byte[8];random.GetBytes(bytes);return (BitConverter.ToUInt64(bytes,0)>>11)*(1.0/9007199254740992.0);}
        }
    }
}


