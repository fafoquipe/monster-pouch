using System;
using System.Collections.Generic;

namespace MonsterPouch.Gameplay.Machines.QA
{
    [Serializable] public sealed class QAMachineResourceAmount
    {
        public string ResourceId;
        public long Amount;
    }
    [Serializable] public sealed class QAMachineResourcePackage
    {
        public string RewardId;
        public QAMachineResourceAmount[] Resources;
    }
    /// <summary>Explicit QA inputs only; no price, balance or package defaults.</summary>
    [Serializable] public sealed class QAMachineConfiguration
    {
        public MachineDefinition Definition;
        public MachinePolicy Policy;
        public long InitialQACredits=-1;
        public long DrawCost=-1;
        public MachineOwnership InitialOwnership;
        public QAMachineResourcePackage[] ResourcePackages;
    }
    [Serializable] public sealed class QAMachineTransactionReceipt
    {
        public string RequestId, ConfigurationSignature;
        public long ExpectedRevision, Cost, CreditsBefore, CreditsAfter;
        public MachineDrawReceipt Draw;
        public MachineRewardKind Kind;
        public string CharacterId, AbilityId;
        public QAMachineResourceAmount[] Resources;
    }
    [Serializable] public sealed class QAMachineSandbox
    {
        public const int CurrentVersion=1;
        public const string RequiredScope="InternalQA.MachineSandbox";
        public int Version=CurrentVersion;
        public string Scope=RequiredScope;
        public string SandboxId, ConfigurationSignature;
        public long Revision, QACredits;
        public bool CharacterOwned;
        public List<string> AbilityIds=new List<string>();
        public List<QAMachineResourceAmount> Resources=new List<QAMachineResourceAmount>();
        public MachineState Machine;
        public List<QAMachineTransactionReceipt> Transactions=new List<QAMachineTransactionReceipt>();
    }
    public enum QAMachineTransactionStatus
    {
        Ready, Initialized, Committed, AlreadyApplied, Conflict, Disabled,
        RecoveryRequired, InsufficientFunds, Rejected, WriteFailed
    }
    public sealed class QAMachineTransactionResult
    {
        public QAMachineTransactionStatus Status;
        public string Reason;
        public MachineStatus EngineStatus;
        public QAMachineSandbox Snapshot;
        public QAMachineTransactionReceipt Receipt;
        public MachinePreview Preview;
    }
}
