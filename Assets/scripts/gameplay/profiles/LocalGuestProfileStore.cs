using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MonsterPouch.Gameplay.Profiles
{
    /// <summary>Device-local identity, collection and earned-currency receipts. No purchases or QA imports.</summary>
    public sealed partial class LocalGuestProfileStore
    {
        [Serializable] sealed class Envelope { public int Version; public string Payload,Sha256; }
        [Serializable] sealed class PayloadVersion { public int Version=0; }
        public enum SelectionResult { Selected, AlreadySelected, Conflict, InvalidStarter, InvalidCatalogue, RecoveryRequired, NotRequired }
        public enum TutorialStepResult { Completed, AlreadyCompleted, OutOfOrder, InvalidStep, NotInTutorial, RecoveryRequired, NotImplemented, ObjectivePending }
        readonly string directory,primary,backup;
        public bool NeedsRecovery { get; private set; }

        public LocalGuestProfileStore(string profileDirectory)
        {
            directory=Path.GetFullPath(profileDirectory);
            primary=Path.Combine(directory,"guest-profile.json");
            backup=Path.Combine(directory,"guest-profile.backup.json");
        }
        public LocalGuestProfile OpenOrCreate(bool hadExistingPlayerData)
        {
            using(Lock())
            {
                var profile=LoadExisting();
                if(profile!=null)return profile;
                profile=new LocalGuestProfile{ProfileId=Guid.NewGuid().ToString("N"),HasLegacyAccess=hadExistingPlayerData,
                    CollectionMode=hadExistingPlayerData?GuestCollectionMode.Legacy:GuestCollectionMode.StarterInventory,
                    OnboardingStage=hadExistingPlayerData?GuestOnboardingStage.None:GuestOnboardingStage.ChooseStarter};
                profile.InitializeTrophyLadder();
                if(!hadExistingPlayerData)AddNewAccountWelcome(profile);
                Save(profile);return profile;
            }
        }
        /// <summary>Refreshes existing identity and receipts without creating or rewriting a profile.</summary>
        public LocalGuestProfile Reload()
        {
            using(Lock())
                return LoadExisting()??throw new InvalidOperationException("No saved guest profile exists.");
        }
        public SelectionResult ChooseStarter(MatchConfig catalogue,string monsterId)
        {
            if(!StarterRoster.IsChoice(monsterId))return SelectionResult.InvalidStarter;
            if(!StarterRoster.TryBuild(monsterId,catalogue,out var deck))return SelectionResult.InvalidCatalogue;
            using(Lock())
            {
                var profile=LoadExisting();
                if(profile==null)throw new InvalidOperationException("Open the guest profile before selecting a starter.");
                if(NeedsRecovery)return SelectionResult.RecoveryRequired;
                if(profile.Starter!=null)return profile.Starter.Deck.Monster==monsterId?SelectionResult.AlreadySelected:SelectionResult.Conflict;
                if(profile.OnboardingStage!=GuestOnboardingStage.ChooseStarter)return SelectionResult.NotRequired;
                profile.StarterSelections.Add(new StarterSelection{
                    ReceiptId=StarterRoster.CurrentReceiptId,TimestampUtc=DateTime.UtcNow.ToString("O"),Deck=deck
                });
                profile.OnboardingStage=GuestOnboardingStage.Tutorial;
                profile.Revision=checked(profile.Revision+1);
                Save(profile);return SelectionResult.Selected;
            }
        }
        public TutorialStepResult CompleteTutorialModule(string moduleId)
        {
            if(!GuestTutorialModules.IsKnown(moduleId))return TutorialStepResult.InvalidStep;
            using(Lock())
            {
                var profile=LoadExisting();
                if(profile==null)throw new InvalidOperationException("Open the guest profile before recording a tutorial objective.");
                if(NeedsRecovery)return TutorialStepResult.RecoveryRequired;
                if(profile.CompletedTutorialModules.Contains(moduleId))return TutorialStepResult.AlreadyCompleted;
                if(profile.OnboardingStage!=GuestOnboardingStage.Tutorial)return TutorialStepResult.NotInTutorial;
                if(profile.NextTutorialModuleId!=moduleId)return TutorialStepResult.OutOfOrder;
                if(!GuestTutorialModules.IsImplemented(moduleId))return TutorialStepResult.NotImplemented;
                if(moduleId==GuestTutorialModules.ProgressionCollection&&!profile.TutorialCollectionReady||
                    moduleId==GuestTutorialModules.FirstMachine&&!profile.TutorialMachineReady)return TutorialStepResult.ObjectivePending;
                profile.CompletedTutorialModules.Add(moduleId);
                // Unimplemented modules remain pending. Recording one objective
                // never grants rewards or completes the entire tutorial.
                profile.Revision=checked(profile.Revision+1);
                Save(profile);return TutorialStepResult.Completed;
            }
        }
        FileStream Lock()
        {
            Directory.CreateDirectory(directory);
            return new FileStream(Path.Combine(directory,"guest-profile.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }
        LocalGuestProfile LoadExisting()
        {
            NeedsRecovery=false;
            if(!File.Exists(primary)&&!File.Exists(backup))return null;
            try{return Read(primary);}
            catch(NotSupportedException){throw;}
            catch(Exception error) when(error is IOException||error is InvalidDataException||error is ArgumentException)
            {
                if(!File.Exists(backup))throw new InvalidDataException("Guest profile could not be read. Original data was preserved.",error);
                var recovered=Read(backup);
                // The previous generation may lack the choice receipt. It must
                // not allow choosing the other Monster after a damaged write.
                NeedsRecovery=true;return recovered;
            }
        }
        static LocalGuestProfile Read(string path)
        {
            var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(path,Encoding.UTF8));
            if(envelope==null||envelope.Version<1)throw new InvalidDataException("Missing guest profile envelope.");
            if(envelope.Version!=1)throw new NotSupportedException("Unsupported guest profile envelope.");
            if(string.IsNullOrEmpty(envelope.Payload)||envelope.Sha256!=Hash(envelope.Payload))
                throw new InvalidDataException("Guest profile checksum mismatch.");
            var profile=new LocalGuestProfile{Version=0,ProfileKind=null,ProfileId=null,Revision=-1,StarterSelections=null,
                OnboardingStage=(GuestOnboardingStage)(-1),CompletedTutorialModules=null,
                CollectionMode=(GuestCollectionMode)(-1),CharacterUnlocks=null,AbilityUnlocks=null,WalletEvents=null,VendingMachines=null,
                PouchRoomDay=null,PouchVictories=null,Pouches=null,PouchOpenings=null,TutorialPouches=null,TutorialMenuVisits=null};
            int version=JsonUtility.FromJson<PayloadVersion>(envelope.Payload)?.Version??0;
            string payload=ValidatePayloadCollections(envelope.Payload,version);
            JsonUtility.FromJsonOverwrite(payload,profile);
            // The exact old JSON was checked above. Unity can materialize an
            // absent inline class; it is not a historical trophy receipt.
            if(version>=1&&version<=9)profile.TrophyLadder=null;
            if(profile.TrophyLadder!=null&&profile.TrophyLadder.Version<3)
                profile.TrophyLadder.HistoricalSegments=Array.Empty<MonsterPouch.Gameplay.Progression.TrophyPolicySegment>();
            profile.UpgradeSchema();return profile;
        }
        static string ValidatePayloadCollections(string payload,int version)
        {
            // Let the version validator preserve unsupported-version behavior.
            if(version<1||version>LocalGuestProfile.CurrentVersion)return payload;
            JObject root;
            try
            {
                using(var reader=new JsonTextReader(new StringReader(payload)){DateParseHandling=DateParseHandling.None})
                {
                    root=JObject.Load(reader,new JsonLoadSettings{
                        DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error
                    });
                    if(reader.Read())throw new InvalidDataException("Unexpected content after guest profile JSON.");
                }
            }
            catch(JsonException error){throw new InvalidDataException("Invalid guest profile JSON structure.",error);}
            // JsonUtility may materialize an omitted or null collection as an
            // empty list. Inspect exact root properties before trusting that DTO.
            RequireArray(root,"StarterSelections");
            if(version>=2)RequireArray(root,"CompletedTutorialModules");
            if(version>=4)RequireArray(root,"CharacterUnlocks");
            if(version>=5)RequireArray(root,"AbilityUnlocks");
            if(version>=6){RequireArray(root,"WalletEvents");RequireArray(root,"VendingMachines");}
            if(version>=7){RequireArray(root,"PouchVictories");RequireArray(root,"Pouches");RequireArray(root,"PouchOpenings");}
            if(version>=9){RequireArray(root,"TutorialPouches");RequireArray(root,"TutorialMenuVisits");}
            if(version>=11){RequireArray(root,"ProgressionClaims");RequireArray(root,"ProgressionPouches");RequireArray(root,"ProgressionOpenings");}
            else if(new[]{"ProgressionClaims","ProgressionPouches","ProgressionOpenings"}.Any(name=>root[name]!=null&&root[name].Type!=JTokenType.Null&&(!(root[name] is JArray array)||array.Count!=0))||!string.IsNullOrEmpty((string)root["ProgressionTutorialVisitedUtc"]))
                throw new InvalidDataException("An older guest cannot contain trophy-road rewards.");
            ValidateTrophyPayload(root,version);
            // Keep dates as strings so receipt timestamps are preserved exactly.
            return root.ToString(Formatting.None);
        }
        static void RequireArray(JObject root,string name)
        {
            if(!root.TryGetValue(name,StringComparison.Ordinal,out var value)||value.Type!=JTokenType.Array)
                throw new InvalidDataException("Missing or invalid guest collection: "+name+".");
        }
        void Save(LocalGuestProfile profile)
        {
            profile.Validate();
            string payload=JsonUtility.ToJson(profile);
            string encoded=JsonUtility.ToJson(new Envelope{Version=1,Payload=payload,Sha256=Hash(payload)},true);
            string temporary=Path.Combine(directory,"guest-profile."+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    byte[] bytes=Encoding.UTF8.GetBytes(encoded);stream.Write(bytes,0,bytes.Length);stream.Flush(true);
                }
                if(File.Exists(primary))File.Replace(temporary,primary,backup);
                else File.Move(temporary,primary);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        static string Hash(string value)
        {
            using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");
        }
    }
}


