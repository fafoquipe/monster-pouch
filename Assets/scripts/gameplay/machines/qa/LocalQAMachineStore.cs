using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;

namespace MonsterPouch.Gameplay.Machines.QA
{
    /// <summary>
    /// Isolated QA aggregate. No Guest/Player/LocalQAProfileStore wallet is read
    /// or written. Credits and opaque resources cannot leave this sandbox.
    /// Checksums detect damage, not cheating; this is not server authority.
    /// </summary>
    public sealed class LocalQAMachineStore
    {
        [Serializable] sealed class Envelope
        {
            public int Version=1;
            public string Payload, Sha256;
        }
        public const string SandboxDirectory="machine-sandbox";
        public const string FileName="qa-machine-sandbox.json";
        public const string BackupFileName="qa-machine-sandbox.backup.json";
        readonly string directory, primary, backup, configurationSignature;
        readonly QAMachineConfiguration configuration;
        readonly PreparedMachineContext context;
        readonly Dictionary<string,MachineReward> rewards;
        readonly Dictionary<string,QAMachineResourcePackage> packages;
        readonly Action beforeCommit;
        public bool NeedsRecovery {get;private set;}

        // The optional hook is a QA fault-injection point after durable temporary
        // write and before replacement. Normal callers leave it null.
        public LocalQAMachineStore(string qaDirectory,QAMachineConfiguration configuration,Action beforeCommit=null)
        {
            if(string.IsNullOrWhiteSpace(qaDirectory))throw new ArgumentException("A QA directory is required.",nameof(qaDirectory));
            ValidateInputStructure(configuration);
            this.configuration=CopyConfiguration(configuration);
            context=MachineDrawEngine.Prepare(this.configuration.Definition,this.configuration.Policy);
            rewards=this.configuration.Definition.Rewards.ToDictionary(r=>r.RewardId,StringComparer.Ordinal);
            ValidateConfiguration();
            packages=this.configuration.ResourcePackages.ToDictionary(p=>p.RewardId,StringComparer.Ordinal);
            configurationSignature=ConfigurationSignature(this.configuration);
            directory=Path.Combine(Path.GetFullPath(qaDirectory),SandboxDirectory);
            primary=Path.Combine(directory,FileName);backup=Path.Combine(directory,BackupFileName);
            this.beforeCommit=beforeCommit;
        }

        public QAMachineTransactionResult OpenOrCreate(EconomyDevelopmentConfig gate)
        {
            if(!Allowed(gate))return Result(QAMachineTransactionStatus.Disabled);
            using(Lock())
            {
                var current=LoadExisting();
                if(current!=null)return LoadedResult(current);
                current=new QAMachineSandbox {
                    SandboxId=Guid.NewGuid().ToString("N"),ConfigurationSignature=configurationSignature,
                    QACredits=configuration.InitialQACredits,Machine=context.CreateState(),
                    CharacterOwned=configuration.InitialOwnership.CharacterOwned,
                    AbilityIds=configuration.InitialOwnership.AbilityIds.ToList()
                };
                try{Save(current);}
                catch(Exception error) when(WriteError(error)){return Result(QAMachineTransactionStatus.WriteFailed,error.Message);}
                return WithSnapshot(QAMachineTransactionStatus.Initialized,current);
            }
        }

        public QAMachineTransactionResult Preview(EconomyDevelopmentConfig gate)
        {
            if(!Allowed(gate))return Result(QAMachineTransactionStatus.Disabled);
            using(Lock())
            {
                var current=LoadExisting();var loaded=LoadedResult(current);
                if(loaded.Status!=QAMachineTransactionStatus.Ready)return loaded;
                loaded.Preview=context.Preview(current.Machine,Ownership(current));
                loaded.EngineStatus=loaded.Preview.Status;
                return loaded;
            }
        }

        public QAMachineTransactionResult TryDraw(EconomyDevelopmentConfig gate,MachineDrawRequest request,double sample01)
        {
            if(!Allowed(gate))return Result(QAMachineTransactionStatus.Disabled);
            // Detach a caller-owned request before entering the transaction.
            request=Clone(request);
            using(Lock())
            {
                var current=LoadExisting();var loaded=LoadedResult(current);
                if(loaded.Status!=QAMachineTransactionStatus.Ready)return loaded;
                var drawn=context.TryDraw(current.Machine,Ownership(current),request,sample01);
                if(drawn.Status==MachineStatus.AlreadyApplied)
                {
                    var receipt=current.Transactions.Single(t=>t.RequestId==request.RequestId);
                    return WithSnapshot(QAMachineTransactionStatus.AlreadyApplied,current,receipt,drawn);
                }
                if(drawn.Status!=MachineStatus.Drawn)
                    return EngineFailure(drawn);
                if(current.QACredits<configuration.DrawCost)return Result(QAMachineTransactionStatus.InsufficientFunds);
                var candidate=Clone(current);
                var transaction=MakeReceipt(drawn.Receipt,current.QACredits);
                try
                {
                    ApplyGrant(candidate,transaction);
                    candidate.QACredits=transaction.CreditsAfter;
                    candidate.Machine=drawn.State;
                    candidate.Revision=checked(candidate.Revision+1);
                    candidate.Transactions.Add(transaction);
                    Save(candidate); // The only commit: reward, debit, state and receipt together.
                }
                catch(OverflowException){return Result(QAMachineTransactionStatus.Rejected,"Configured QA quantities exceed the supported range.");}
                catch(Exception error) when(WriteError(error)){return Result(QAMachineTransactionStatus.WriteFailed,error.Message);}
                return WithSnapshot(QAMachineTransactionStatus.Committed,candidate,transaction,drawn);
            }
        }

        QAMachineTransactionResult LoadedResult(QAMachineSandbox current)
        {
            if(current==null)return Result(QAMachineTransactionStatus.Rejected,"Open the QA sandbox before drawing.");
            if(current.ConfigurationSignature!=configurationSignature)
                return Result(QAMachineTransactionStatus.Conflict,"The sandbox belongs to another explicit QA configuration.");
            ValidateAggregate(current);
            return WithSnapshot(NeedsRecovery?QAMachineTransactionStatus.RecoveryRequired:QAMachineTransactionStatus.Ready,current);
        }

        void ValidateConfiguration()
        {
            if(configuration.InitialQACredits<0||configuration.DrawCost<=0||configuration.InitialOwnership==null)
                throw new ArgumentException("QA credit seed, positive cost and ownership must be explicit.");
            var preview=context.Preview(context.CreateState(),configuration.InitialOwnership);
            if(preview.Status==MachineStatus.InvalidOwnership)throw new ArgumentException(preview.Reason);
            var configured=configuration.ResourcePackages;
            if(configured==null||configured.Length!=10)throw new ArgumentException("All ten opaque QA resource packages must be configured.");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var package in configured)
            {
                if(package==null||!ValidId(package.RewardId)||!ids.Add(package.RewardId)||
                    !rewards.TryGetValue(package.RewardId,out var reward)||reward.Kind!=MachineRewardKind.Resource||
                    package.Resources==null||package.Resources.Length==0)
                    throw new ArgumentException("A QA package is missing, repeated or not a resource reward.");
                var resources=new HashSet<string>(StringComparer.Ordinal);
                foreach(var amount in package.Resources)
                    if(amount==null||!ValidId(amount.ResourceId)||amount.Amount<=0||!resources.Add(amount.ResourceId))
                        throw new ArgumentException("Explicit unique resource IDs and positive QA amounts are required.");
            }
        }

        static void ValidateInputStructure(QAMachineConfiguration input)
        {
            // JsonUtility may turn inline null objects into default instances.
            // Reject missing input before any serialization can erase that fact.
            if(input==null||input.Definition==null||input.Policy==null||input.InitialOwnership==null||
                input.InitialOwnership.AbilityIds==null||input.Definition.Rewards==null||
                input.Definition.Rewards.Any(r=>r==null)||input.Policy.CharacterChanceByDraw==null||
                input.Policy.RewardWeights==null||input.Policy.RewardWeights.Any(w=>w==null)||
                input.ResourcePackages==null||input.ResourcePackages.Any(p=>p==null||p.Resources==null||p.Resources.Any(r=>r==null)))
                throw new ArgumentException("All QA configuration objects and arrays must be explicit.",nameof(input));
        }

        void ValidateAggregate(QAMachineSandbox saved)
        {
            ValidateHeader(saved);
            if(saved.ConfigurationSignature!=configurationSignature)throw new InvalidDataException("Configuration mismatch.");
            if(saved.Machine==null||saved.Transactions==null||saved.AbilityIds==null||saved.Resources==null||
                saved.Revision!=saved.Transactions.Count||saved.Revision!=saved.Machine.Revision)
                throw new InvalidDataException("Incomplete atomic QA aggregate.");
            // At most fourteen transactions: replay derives every balance, grant,
            // receipt and state from the immutable QA configuration and history.
            var expected=new QAMachineSandbox {
                SandboxId=saved.SandboxId,ConfigurationSignature=configurationSignature,
                QACredits=configuration.InitialQACredits,Machine=context.CreateState(),
                CharacterOwned=configuration.InitialOwnership.CharacterOwned,
                AbilityIds=configuration.InitialOwnership.AbilityIds.ToList()
            };
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var transaction in saved.Transactions)
            {
                if(transaction==null||!ValidId(transaction.RequestId)||!ids.Add(transaction.RequestId)||transaction.Draw==null||
                    transaction.ConfigurationSignature!=configurationSignature||transaction.ExpectedRevision!=expected.Revision)
                    throw new InvalidDataException("Missing or repeated QA transaction identity.");
                var preview=context.Preview(expected.Machine,Ownership(expected));
                double sample=0;bool found=false;
                foreach(var outcome in preview.Outcomes)
                {
                    // The lower boundary is inclusive in the engine. A midpoint
                    // can round up to 1 for a valid sub-ULP final probability.
                    if(outcome.RewardId==transaction.Draw.RewardId){found=true;break;}
                    sample+=outcome.Probability;
                }
                if(!found)throw new InvalidDataException("A persisted QA prize was not eligible.");
                var request=new MachineDrawRequest {RequestId=transaction.RequestId,MachineId=context.MachineId,
                    CatalogVersion=context.CatalogVersion,ExpectedRevision=expected.Revision};
                var draw=context.TryDraw(expected.Machine,Ownership(expected),request,sample);
                if(draw.Status!=MachineStatus.Drawn||expected.QACredits<configuration.DrawCost)
                    throw new InvalidDataException("Invalid persisted QA extraction.");
                var receipt=MakeReceipt(draw.Receipt,expected.QACredits);
                if(JsonUtility.ToJson(receipt)!=JsonUtility.ToJson(transaction))throw new InvalidDataException("QA receipt content changed.");
                ApplyGrant(expected,receipt);expected.QACredits=receipt.CreditsAfter;
                expected.Machine=draw.State;expected.Revision++;expected.Transactions.Add(receipt);
            }
            if(JsonUtility.ToJson(expected)!=JsonUtility.ToJson(saved))throw new InvalidDataException("QA balances, ownership or machine state differ from the atomic receipts.");
        }

        QAMachineTransactionReceipt MakeReceipt(MachineDrawReceipt draw,long before)
        {
            var reward=rewards[draw.RewardId];
            return new QAMachineTransactionReceipt {
                RequestId=draw.RequestId,ConfigurationSignature=configurationSignature,ExpectedRevision=draw.ExpectedRevision,
                Cost=configuration.DrawCost,CreditsBefore=before,CreditsAfter=checked(before-configuration.DrawCost),Draw=Clone(draw),
                Kind=reward.Kind,CharacterId=reward.Kind==MachineRewardKind.Resource?"":configuration.Definition.CharacterId,
                AbilityId=reward.Kind==MachineRewardKind.Ability?reward.AbilityId:"",
                Resources=reward.Kind==MachineRewardKind.Resource?packages[reward.RewardId].Resources.Select(Clone).ToArray():Array.Empty<QAMachineResourceAmount>()
            };
        }
        static void ApplyGrant(QAMachineSandbox target,QAMachineTransactionReceipt receipt)
        {
            if(receipt.Kind==MachineRewardKind.Character)target.CharacterOwned=true;
            else if(receipt.Kind==MachineRewardKind.Ability)
            {
                if(!target.CharacterOwned||target.AbilityIds.Contains(receipt.AbilityId))throw new InvalidDataException("Invalid QA ability grant.");
                target.AbilityIds.Add(receipt.AbilityId);
            }
            else foreach(var amount in receipt.Resources)
            {
                var balance=target.Resources.SingleOrDefault(r=>r.ResourceId==amount.ResourceId);
                if(balance==null){balance=new QAMachineResourceAmount {ResourceId=amount.ResourceId};target.Resources.Add(balance);}
                balance.Amount=checked(balance.Amount+amount.Amount);
            }
        }

        QAMachineSandbox LoadExisting()
        {
            NeedsRecovery=false;
            if(!File.Exists(primary)&&!File.Exists(backup))return null;
            try{return Read(primary);}
            catch(NotSupportedException){throw;}
            catch(Exception error) when(ReadError(error))
            {
                if(!File.Exists(backup))throw new InvalidDataException("Unreadable QA sandbox; original files were preserved.",error);
                var recovered=Read(backup);NeedsRecovery=true;return recovered;
            }
        }
        QAMachineSandbox Read(string path)
        {
            var envelope=new Envelope {Version=0};
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path,Encoding.UTF8),envelope);
            if(envelope.Version==0)throw new InvalidDataException("Missing QA envelope version.");
            if(envelope.Version!=1)throw new NotSupportedException("Unsupported QA envelope version.");
            if(string.IsNullOrEmpty(envelope.Payload)||envelope.Sha256!=Hash(envelope.Payload))throw new InvalidDataException("QA checksum mismatch.");
            var value=new QAMachineSandbox {Version=0,Scope=null,Revision=-1,QACredits=-1,
                Machine=null,AbilityIds=null,Resources=null,Transactions=null};
            JsonUtility.FromJsonOverwrite(envelope.Payload,value);ValidateHeader(value);
            if(value.ConfigurationSignature==configurationSignature)ValidateAggregate(value);
            return value;
        }
        void Save(QAMachineSandbox value)
        {
            ValidateAggregate(value);
            string payload=JsonUtility.ToJson(value);
            string encoded=JsonUtility.ToJson(new Envelope{Payload=payload,Sha256=Hash(payload)},true);
            string temporary=Path.Combine(directory,"qa-machine-sandbox."+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {var bytes=Encoding.UTF8.GetBytes(encoded);file.Write(bytes,0,bytes.Length);file.Flush(true);}
                beforeCommit?.Invoke();
                if(File.Exists(primary))File.Replace(temporary,primary,backup);
                else File.Move(temporary,primary);
            }
            finally
            {
                if(File.Exists(temporary))
                    try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}
            }
        }
        FileStream Lock()
        {
            Directory.CreateDirectory(directory);
            return new FileStream(Path.Combine(directory,"qa-machine-sandbox.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }
        static void ValidateHeader(QAMachineSandbox value)
        {
            if(value==null)throw new InvalidDataException("Missing QA sandbox.");
            if(value.Version==0)throw new InvalidDataException("Missing QA sandbox version.");
            if(value.Version!=QAMachineSandbox.CurrentVersion)throw new NotSupportedException("Unsupported QA sandbox version.");
            if(value.Scope!=QAMachineSandbox.RequiredScope||!Guid.TryParseExact(value.SandboxId,"N",out _)||
                value.Revision<0||value.Revision>14||value.QACredits<0||!ValidId(value.ConfigurationSignature))
                throw new InvalidDataException("Not a valid isolated QA sandbox.");
        }
        static MachineOwnership Ownership(QAMachineSandbox value)=>new MachineOwnership {CharacterOwned=value.CharacterOwned,AbilityIds=value.AbilityIds.ToArray()};
        static bool Allowed(EconomyDevelopmentConfig gate)=>gate!=null&&gate.EnableLocalQA&&(Application.isEditor||Debug.isDebugBuild);
        static bool ValidId(string id)=>!string.IsNullOrWhiteSpace(id)&&id.Length<=160;
        static bool ReadError(Exception error)=>error is IOException||error is ArgumentException||error is InvalidDataException||error is OverflowException;
        static bool WriteError(Exception error)=>error is IOException||error is UnauthorizedAccessException;
        // Unity's JSON formatter can round double probabilities. Configuration
        // must never pass through it: copy every nested object and array directly.
        static QAMachineConfiguration CopyConfiguration(QAMachineConfiguration source)=>new QAMachineConfiguration {
            Definition=new MachineDefinition {
                MachineId=source.Definition.MachineId,CharacterId=source.Definition.CharacterId,CatalogVersion=source.Definition.CatalogVersion,
                Rewards=source.Definition.Rewards.Select(r=>new MachineReward {RewardId=r.RewardId,Kind=r.Kind,AbilityId=r.AbilityId}).ToArray()
            },
            Policy=new MachinePolicy {
                PolicyId=source.Policy.PolicyId,Version=source.Policy.Version,
                CharacterChanceByDraw=(double[])source.Policy.CharacterChanceByDraw.Clone(),
                RewardWeights=source.Policy.RewardWeights.Select(w=>new MachineWeight {RewardId=w.RewardId,Weight=w.Weight}).ToArray()
            },
            InitialQACredits=source.InitialQACredits,DrawCost=source.DrawCost,
            InitialOwnership=new MachineOwnership {CharacterOwned=source.InitialOwnership.CharacterOwned,
                AbilityIds=(string[])source.InitialOwnership.AbilityIds.Clone()},
            ResourcePackages=source.ResourcePackages.Select(p=>new QAMachineResourcePackage {
                RewardId=p.RewardId,Resources=p.Resources.Select(r=>new QAMachineResourceAmount {ResourceId=r.ResourceId,Amount=r.Amount}).ToArray()
            }).ToArray()
        };
        static string ConfigurationSignature(QAMachineConfiguration value)
        {
            // Versioned deterministic encoding: counts delimit arrays, strings
            // retain exact UTF-16 code units and doubles retain all IEEE-754 bits.
            // No culture, JSON formatter or floating-point arithmetic intervenes.
            using(var bytes=new MemoryStream())
            using(var writer=new BinaryWriter(bytes))
            {
                WriteString(writer,"InternalQA.MachineConfiguration.ExactBits.v1");
                WriteString(writer,value.Definition.MachineId);WriteString(writer,value.Definition.CharacterId);
                writer.Write(value.Definition.CatalogVersion);writer.Write(value.Definition.Rewards.Length);
                foreach(var reward in value.Definition.Rewards)
                {WriteString(writer,reward.RewardId);writer.Write((int)reward.Kind);WriteString(writer,reward.AbilityId);}
                WriteString(writer,value.Policy.PolicyId);writer.Write(value.Policy.Version);
                writer.Write(value.Policy.CharacterChanceByDraw.Length);
                foreach(double chance in value.Policy.CharacterChanceByDraw)writer.Write(BitConverter.DoubleToInt64Bits(chance));
                writer.Write(value.Policy.RewardWeights.Length);
                foreach(var weight in value.Policy.RewardWeights)
                {WriteString(writer,weight.RewardId);writer.Write(BitConverter.DoubleToInt64Bits(weight.Weight));}
                writer.Write(value.InitialQACredits);writer.Write(value.DrawCost);writer.Write(value.InitialOwnership.CharacterOwned);
                writer.Write(value.InitialOwnership.AbilityIds.Length);
                foreach(string ability in value.InitialOwnership.AbilityIds)WriteString(writer,ability);
                writer.Write(value.ResourcePackages.Length);
                foreach(var package in value.ResourcePackages)
                {
                    WriteString(writer,package.RewardId);writer.Write(package.Resources.Length);
                    foreach(var resource in package.Resources){WriteString(writer,resource.ResourceId);writer.Write(resource.Amount);}
                }
                writer.Flush();return Hash(bytes.ToArray());
            }
        }
        static void WriteString(BinaryWriter writer,string value)
        {
            writer.Write(value==null?-1:value.Length);
            if(value!=null)foreach(char codeUnit in value)writer.Write((ushort)codeUnit);
        }
        // Persistent state/requests/receipts contain no double fields.
        static T Clone<T>(T value) where T:class=>value==null?null:JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static string Hash(string text)=>Hash(Encoding.UTF8.GetBytes(text));
        static string Hash(byte[] bytes)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
        static QAMachineTransactionResult Result(QAMachineTransactionStatus status,string reason=null)=>new QAMachineTransactionResult {Status=status,Reason=reason};
        static QAMachineTransactionResult WithSnapshot(QAMachineTransactionStatus status,QAMachineSandbox snapshot,QAMachineTransactionReceipt receipt=null,MachineDrawResult drawn=null)=>
            new QAMachineTransactionResult {Status=status,Snapshot=Clone(snapshot),Receipt=Clone(receipt),EngineStatus=drawn?.Status??MachineStatus.Ready,Preview=drawn?.Preview};
        static QAMachineTransactionResult EngineFailure(MachineDrawResult draw)=>new QAMachineTransactionResult {
            Status=draw.Status==MachineStatus.Conflict?QAMachineTransactionStatus.Conflict:QAMachineTransactionStatus.Rejected,
            EngineStatus=draw.Status,Reason=draw.Reason,Preview=draw.Preview
        };
    }
}
