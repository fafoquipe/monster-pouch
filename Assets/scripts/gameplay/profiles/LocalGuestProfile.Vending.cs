using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Machines;
using UnityEngine;

namespace MonsterPouch.Gameplay.Profiles
{
    public enum GuestWalletEventKind { FreeCredit = 0, MachineDraw = 1, AbilityPurchase = 2 }
    [Serializable] public sealed class GuestWalletEvent
    {
        public string Id,SourceId,TimestampUtc;
        public GuestWalletEventKind Kind;
        public PlayerMachineAmount[] Amounts;
        public string MachineId;
        public string CharacterId,AbilityId;
        public int PermanentPolicyVersion;
        public MachineOwnership OwnershipBefore;
        public MachineDrawReceipt Draw;
    }
    [Serializable] public sealed class GuestVendingMachine
    {
        public PlayerMachineConfiguration Configuration;
        public MachineState State;
    }
    public sealed partial class LocalGuestProfile
    {
        // Earned/local-test credits only. No paid or QA-origin import path exists.
        public List<GuestWalletEvent> WalletEvents=new List<GuestWalletEvent>();
        public List<GuestVendingMachine> VendingMachines=new List<GuestVendingMachine>();
        public long FreeBalance(PermanentCurrency currency)=>WalletEvents
            .SelectMany(e=>e.Amounts).Where(a=>a.Currency==currency).Aggregate(0L,(sum,a)=>checked(sum+a.Amount));

        internal static string VendingUnlockId(string requestId)=>"vending:"+requestId;
        internal static PlayerMachineAmount[] VendingAmounts(PlayerMachineConfiguration configuration,MachineReward reward)
        {
            var amounts=new Dictionary<PermanentCurrency,long>{{configuration.Cost.Currency,-configuration.Cost.Amount}};
            if(reward.Kind==MachineRewardKind.Resource)
                foreach(var value in configuration.Packages.Single(p=>p.RewardId==reward.RewardId).Amounts)
                {amounts.TryGetValue(value.Currency,out long existing);amounts[value.Currency]=checked(existing+value.Amount);}
            // Zero net Saturncoin grants remain explicit; spending was still required before the draw.
            return amounts.OrderBy(a=>a.Key).Select(a=>new PlayerMachineAmount{Currency=a.Key,Amount=a.Value}).ToArray();
        }
        void ValidateVending()
        {
            if(WalletEvents==null||VendingMachines==null)throw new InvalidDataException("Missing local wallet or vending records.");
            var machines=new Dictionary<string,GuestVendingMachine>(StringComparer.Ordinal);
            var contexts=new Dictionary<string,PreparedMachineContext>(StringComparer.Ordinal);
            var states=new Dictionary<string,MachineState>(StringComparer.Ordinal);
            foreach(var machine in VendingMachines)
            {
                if(machine?.Configuration==null||machine.State==null)throw new InvalidDataException("Incomplete vending snapshot.");
                try{machine.Configuration.Validate();}
                catch(ArgumentException error){throw new InvalidDataException("Invalid saved vending configuration.",error);}
                string id=machine.Configuration.Definition.MachineId;
                if(machines.ContainsKey(id))throw new InvalidDataException("Repeated vending machine.");
                machines.Add(id,machine);
                var context=MachineDrawEngine.Prepare(machine.Configuration.Definition,machine.Configuration.Policy);
                contexts.Add(id,context);states.Add(id,context.CreateState());
            }
            var ids=new HashSet<string>(StringComparer.Ordinal);
            var balances=new Dictionary<PermanentCurrency,long>();
            foreach(var entry in WalletEvents)
            {
                if(entry==null||string.IsNullOrWhiteSpace(entry.Id)||entry.Id.Length>120||!ids.Add(entry.Id)||
                    string.IsNullOrWhiteSpace(entry.SourceId)||entry.SourceId.Length>160||!Enum.IsDefined(typeof(GuestWalletEventKind),entry.Kind)||
                    !DateTime.TryParse(entry.TimestampUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out _)||
                    entry.Amounts==null||entry.Amounts.Length==0||entry.Amounts.Any(a=>a==null||!Enum.IsDefined(typeof(PermanentCurrency),a.Currency))||
                    entry.Amounts.Select(a=>a.Currency).Distinct().Count()!=entry.Amounts.Length)
                    throw new InvalidDataException("Invalid local wallet receipt.");
                if(entry.Kind==GuestWalletEventKind.FreeCredit)
                {
                    if(entry.Amounts.Any(a=>a.Amount<=0)||HasMachinePayload(entry)||
                        !string.IsNullOrEmpty(entry.CharacterId)||!string.IsNullOrEmpty(entry.AbilityId))
                        throw new InvalidDataException("Free credits must be positive and cannot describe machine draws.");
                }
                else if(entry.Kind==GuestWalletEventKind.AbilityPurchase)
                    ValidateAbilityPurchase(entry,balances);
                else if(entry.Kind==GuestWalletEventKind.MachineDraw)
                {
                    if(!string.IsNullOrEmpty(entry.CharacterId)||!string.IsNullOrEmpty(entry.AbilityId)||entry.PermanentPolicyVersion!=0)
                        throw new InvalidDataException("Machine draws cannot contain permanent-shop metadata.");
                    if(CollectionMode!=GuestCollectionMode.StarterInventory||Starter==null||entry.MachineId==null||
                        !machines.TryGetValue(entry.MachineId,out var machine)||entry.Draw==null||entry.OwnershipBefore==null||
                        entry.OwnershipBefore.AbilityIds==null||entry.SourceId!=machine.Configuration.ApprovalId||entry.Draw.RequestId!=entry.Id)
                        throw new InvalidDataException("Machine receipt does not belong to this account or configuration.");
                    var config=machine.Configuration;var context=contexts[entry.MachineId];var state=states[entry.MachineId];
                    balances.TryGetValue(config.Cost.Currency,out long available);
                    if(available<config.Cost.Amount)throw new InvalidDataException("Machine draw did not have enough earned credits.");
                    var preview=context.Preview(state,entry.OwnershipBefore);
                    double sample=0;bool found=false;
                    foreach(var outcome in preview.Outcomes)
                    {if(outcome.RewardId==entry.Draw.RewardId){found=true;break;}sample+=outcome.Probability;}
                    if(!found)throw new InvalidDataException("Saved machine prize was not eligible.");
                    var replay=context.TryDraw(state,entry.OwnershipBefore,new MachineDrawRequest{
                        RequestId=entry.Id,MachineId=entry.MachineId,CatalogVersion=config.Definition.CatalogVersion,ExpectedRevision=state.Revision
                    },sample);
                    if(replay.Status!=MachineStatus.Drawn||JsonUtility.ToJson(replay.Receipt)!=JsonUtility.ToJson(entry.Draw))
                        throw new InvalidDataException("Machine receipt differs from its confirmed result.");
                    states[entry.MachineId]=replay.State;
                    var reward=config.Definition.Rewards.Single(r=>r.RewardId==entry.Draw.RewardId);
                    var expected=VendingAmounts(config,reward);
                    if(!SameAmounts(expected,entry.Amounts))throw new InvalidDataException("Machine resource grant differs from the configured package.");
                    string character=config.Definition.CharacterId;
                    if(entry.OwnershipBefore.CharacterOwned&&!OwnsCharacter(character)||
                        entry.OwnershipBefore.AbilityIds.Any(id=>!OwnsAbility(character,id)))
                        throw new InvalidDataException("Machine reconciliation references missing permanent ownership.");
                    string unlockId=VendingUnlockId(entry.Id);
                    if(reward.Kind==MachineRewardKind.Character)
                    {
                        var receipt=CharacterUnlocks.SingleOrDefault(r=>r.ReceiptId==unlockId);
                        if(receipt==null||receipt.CharacterId!=character||receipt.Source!=GuestUnlockSource.Machine||receipt.TimestampUtc!=entry.TimestampUtc)
                            throw new InvalidDataException("Machine character grant is missing from the same saved transaction.");
                    }
                    else if(reward.Kind==MachineRewardKind.Ability)
                    {
                        var receipt=AbilityUnlocks.SingleOrDefault(r=>r.ReceiptId==unlockId);
                        if(receipt==null||receipt.CharacterId!=character||receipt.AbilityId!=reward.AbilityId||receipt.Source!=GuestUnlockSource.Machine||receipt.TimestampUtc!=entry.TimestampUtc)
                            throw new InvalidDataException("Machine ability grant is missing from the same saved transaction.");
                    }
                }
                foreach(var amount in entry.Amounts)
                {
                    balances.TryGetValue(amount.Currency,out long before);long after;
                    try{after=checked(before+amount.Amount);}
                    catch(OverflowException error){throw new InvalidDataException("Local balance overflow.",error);}
                    if(after<0)throw new InvalidDataException("Negative local currency balance.");balances[amount.Currency]=after;
                }
            }
            ValidatePermanentEconomyReceipts();
            foreach(var pair in machines)
                if(states[pair.Key].DrawCount==0||JsonUtility.ToJson(states[pair.Key])!=JsonUtility.ToJson(pair.Value.State))
                    throw new InvalidDataException("Machine state differs from its confirmed wallet transactions.");
        }
        internal static bool SameAmounts(PlayerMachineAmount[] left,PlayerMachineAmount[] right)=>
            left.Length==right.Length&&left.All(a=>right.Any(b=>b.Currency==a.Currency&&b.Amount==a.Amount));
    }
}
