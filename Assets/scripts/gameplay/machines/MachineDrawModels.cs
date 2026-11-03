using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Machines
{
    public enum MachineRewardKind { Character, Ability, Resource }
    public enum MachineRewardStatus { Available, Locked, Claimed }
    public enum MachineGuarantee { None, CharacterAtTen, FirstAbility }
    public enum MachineStatus
    {
        Ready, Drawn, AlreadyApplied, Conflict, Complete, InvalidCatalog,
        InvalidState, InvalidOwnership, InvalidRequest, InvalidSample, NeedsConfiguration
    }

    [Serializable] public sealed class MachineReward
    {
        public string RewardId;
        public MachineRewardKind Kind;
        public string AbilityId;
    }
    [Serializable] public sealed class MachineDefinition
    {
        public string MachineId, CharacterId;
        public int CatalogVersion;
        public MachineReward[] Rewards;
    }
    [Serializable] public sealed class MachineWeight
    {
        public string RewardId;
        public double Weight;
    }
    /// <summary>No default balance. Every caller must supply an explicit policy.</summary>
    [Serializable] public sealed class MachinePolicy
    {
        public string PolicyId;
        public int Version;
        public double[] CharacterChanceByDraw;
        public MachineWeight[] RewardWeights;
    }
    [Serializable] public sealed class MachineOwnership
    {
        public bool CharacterOwned;
        public string[] AbilityIds = Array.Empty<string>();
    }
    [Serializable] public sealed class MachineState
    {
        public string MachineId, CatalogSignature;
        public int CatalogVersion;
        public long Revision;
        public int DrawCount;
        // -1: no character. Otherwise the number of completed draws when it
        // became owned. External ownership does not consume an extraction.
        public int CharacterAcquiredAtDraw = -1;
        public List<string> ClaimedRewardIds = new List<string>();
        public List<MachineDrawReceipt> Receipts = new List<MachineDrawReceipt>();
    }
    [Serializable] public sealed class MachineDrawRequest
    {
        public string RequestId, MachineId;
        public int CatalogVersion;
        public long ExpectedRevision;
    }
    [Serializable] public sealed class MachineDrawReceipt
    {
        public string RequestId, RewardId, MachineId, PolicyId, CatalogSignature, PolicySignature;
        public int CatalogVersion, PolicyVersion, DrawNumber;
        public long ExpectedRevision;
        public MachineGuarantee Guarantee;
    }
    public sealed class MachineOutcomeProbability
    {
        public string RewardId;
        public double Probability;
    }
    public sealed class MachineRewardView
    {
        public string RewardId;
        public MachineRewardStatus Status;
        public double Probability;
    }
    public sealed class MachinePreview
    {
        public MachineStatus Status;
        public string Reason;
        public int NextDrawNumber;
        public bool FirstAbilityGuaranteed;
        public MachineRewardView[] Rewards = Array.Empty<MachineRewardView>();
        public MachineOutcomeProbability[] Outcomes = Array.Empty<MachineOutcomeProbability>();
    }
    public sealed class MachineDrawResult
    {
        public MachineStatus Status;
        public string Reason;
        public MachineState State;
        public MachineDrawReceipt Receipt;
        public MachinePreview Preview;
    }
}
