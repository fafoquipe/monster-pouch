using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;

namespace MonsterPouch.Gameplay.Machines
{
    [Serializable] public sealed class PlayerMachineAmount
    {
        public PermanentCurrency Currency;
        public long Amount;
    }
    [Serializable] public sealed class PlayerMachinePackage
    {
        public string RewardId;
        public PlayerMachineAmount[] Amounts;
    }
    /// <summary>Explicit local-play configuration. No default prizes or commercial policy.</summary>
    [Serializable] public sealed class PlayerMachineConfiguration
    {
        public string ApprovalId;
        public bool EnabledForLocalPlay;
        public PlayerMachineAmount Cost;
        public MachineDefinition Definition;
        public MachinePolicy Policy;
        public PlayerMachinePackage[] Packages;

        public void Validate()
        {
            if(!EnabledForLocalPlay||string.IsNullOrWhiteSpace(ApprovalId)||ApprovalId.Length>160)
                throw new ArgumentException("This local machine configuration has not been enabled explicitly.");
            if(Cost==null||Cost.Amount<=0||!Enum.IsDefined(typeof(PermanentCurrency),Cost.Currency))
                throw new ArgumentException("An explicit approved currency and draw cost are required.");
            MachineDrawEngine.Prepare(Definition,Policy);
            if(Packages==null||Packages.Length!=10||Packages.Any(p=>p==null||string.IsNullOrWhiteSpace(p.RewardId)||
                p.Amounts==null||p.Amounts.Length==0||p.Amounts.Any(a=>a==null||a.Amount<=0||!Enum.IsDefined(typeof(PermanentCurrency),a.Currency))||
                p.Amounts.Select(a=>a.Currency).Distinct().Count()!=p.Amounts.Length)||
                Packages.Select(p=>p.RewardId).Distinct(StringComparer.Ordinal).Count()!=10)
                throw new ArgumentException("Ten explicit resource packages with positive approved amounts are required.");
            var resources=Definition.Rewards.Where(r=>r.Kind==MachineRewardKind.Resource).Select(r=>r.RewardId).OrderBy(id=>id,StringComparer.Ordinal);
            if(!resources.SequenceEqual(Packages.Select(p=>p.RewardId).OrderBy(id=>id,StringComparer.Ordinal),StringComparer.Ordinal))
                throw new ArgumentException("Resource packages must match the machine's ten resource rewards exactly.");
            if(Packages.Select(p=>string.Join(";",p.Amounts.OrderBy(a=>a.Currency).Select(a=>(int)a.Currency+":"+a.Amount)))
                .Distinct(StringComparer.Ordinal).Count()!=10)
                throw new ArgumentException("The ten resource packages must have distinct contents.");
            // Missing/zero weights must not create an unfinishable machine when
            // only that reward remains. No fallback weights are manufactured.
            foreach(var reward in Definition.Rewards.Where(r=>r.Kind!=MachineRewardKind.Character))
                if(Policy.RewardWeights.SingleOrDefault(w=>w.RewardId==reward.RewardId)?.Weight<=0||
                    !Policy.RewardWeights.Any(w=>w.RewardId==reward.RewardId))
                    throw new ArgumentException("Every resource and ability requires an explicit positive weight.");
        }
        public void Validate(MatchConfig catalogue)
        {
            Validate();var character=GuestAbilityCatalogue.Character(catalogue,Definition.CharacterId);
            if(character?.Tricks==null||character.Tricks.Length!=3||character.Tricks.Any(t=>t==null||string.IsNullOrWhiteSpace(t.Id))||
                character.Tricks.Select(t=>t.Id).Distinct(StringComparer.Ordinal).Count()!=3||
                !character.Tricks.Select(t=>t.Id).OrderBy(id=>id,StringComparer.Ordinal).SequenceEqual(
                    Definition.Rewards.Where(r=>r.Kind==MachineRewardKind.Ability).Select(r=>r.AbilityId).OrderBy(id=>id,StringComparer.Ordinal),StringComparer.Ordinal))
                throw new ArgumentException("Machine character and ability IDs must match the current character catalogue.");
        }
        public PlayerMachineConfiguration Snapshot()
        {
            Validate();return JsonUtility.FromJson<PlayerMachineConfiguration>(JsonUtility.ToJson(this));
        }
        public string Signature()
        {
            Validate();var stable=Snapshot();
            stable.Packages=stable.Packages.OrderBy(p=>p.RewardId,StringComparer.Ordinal).ToArray();
            foreach(var package in stable.Packages)package.Amounts=package.Amounts.OrderBy(a=>a.Currency).ToArray();
            stable.Policy.RewardWeights=stable.Policy.RewardWeights.OrderBy(w=>w.RewardId,StringComparer.Ordinal).ToArray();
            using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(stable)))).Replace("-","");
        }
    }
    [Serializable] public sealed class PlayerMachineEntry
    {
        public PlayerMachineConfiguration Configuration;
        public Sprite Artwork;
    }
}
