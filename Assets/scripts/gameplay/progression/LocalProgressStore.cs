using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MonsterPouch.Gameplay.Progression
{
    /// <summary>
    /// Local progress only. This is not an account, a commercial wallet or a
    /// trusted server. The checksum detects accidental corruption, not cheating.
    /// </summary>
    public sealed class LocalProgressStore
    {
        [Serializable] sealed class Envelope
        {
            public int Version;
            public string Payload, Sha256;
        }
        [Serializable] sealed class SaveData
        {
            public int Version;
            public long Revision;
            public string ProgressJson, OriginalLegacyJson;
        }
        public enum AwardResult { Applied, AlreadyApplied, InvalidRequest, RecoveryRequired }

        readonly string directory, primary, backup;
        public bool NeedsRecovery { get; private set; }
        public LocalProgressStore(string progressDirectory)
        {
            directory=Path.GetFullPath(progressDirectory);
            primary=Path.Combine(directory,"progress.json");
            backup=Path.Combine(directory,"progress.backup.json");
        }

        public PlayerProgress LoadOrImport(string legacyJson)
        {
            using(Lock())
            {
                var saved=LoadExisting();
                if(saved!=null)return ReadProgress(saved.ProgressJson);
                var progress=string.IsNullOrWhiteSpace(legacyJson)?new PlayerProgress():ReadProgress(legacyJson);
                Save(new SaveData{Version=1,Revision=0,ProgressJson=progress.Serialize(),OriginalLegacyJson=legacyJson??""});
                return progress;
            }
        }

        /// <summary>Read committed progress without importing, creating or awarding it.</summary>
        public PlayerProgress ReadExisting()
        {
            using(Lock())
            {
                var saved=LoadExisting();
                return saved==null?null:ReadProgress(saved.ProgressJson);
            }
        }

        public AwardResult Award(ProgressionConfig config,string matchId,bool won,out PlayerProgress progress)
        {
            progress=null;
            if(config==null||string.IsNullOrWhiteSpace(matchId))return AwardResult.InvalidRequest;
            using(Lock())
            {
                var saved=LoadExisting();
                if(saved==null)throw new InvalidOperationException("Load progress before awarding a match.");
                var current=ReadProgress(saved.ProgressJson);
                if(NeedsRecovery){progress=current;return AwardResult.RecoveryRequired;}
                if(!current.Award(config,matchId,won)){progress=current;return AwardResult.AlreadyApplied;}
                saved.ProgressJson=current.Serialize();saved.Revision=checked(saved.Revision+1);
                Save(saved);
                // Publish the new state only after the receipt and XP are committed together.
                progress=current;
                return AwardResult.Applied;
            }
        }

        FileStream Lock()
        {
            Directory.CreateDirectory(directory);
            return new FileStream(Path.Combine(directory,"progress.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }
        SaveData LoadExisting()
        {
            NeedsRecovery=false;
            if(!File.Exists(primary)&&!File.Exists(backup))return null;
            try{return Read(primary);}
            catch(NotSupportedException){throw;}
            catch(Exception error) when(error is IOException||error is InvalidDataException||error is ArgumentException)
            {
                if(!File.Exists(backup))throw new InvalidDataException("Progress could not be read. Original data was preserved.",error);
                var recovered=Read(backup);
                // A previous generation may lack a committed match receipt. Do not
                // automatically roll it back and award that match a second time.
                NeedsRecovery=true;
                return recovered;
            }
        }
        static SaveData Read(string path)
        {
            var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(path,Encoding.UTF8));
            if(envelope==null||envelope.Version<1)throw new InvalidDataException("Missing progress envelope.");
            if(envelope.Version!=1)throw new NotSupportedException("Unsupported progress envelope version.");
            if(string.IsNullOrEmpty(envelope.Payload)||envelope.Sha256!=Hash(envelope.Payload))
                throw new InvalidDataException("Progress checksum mismatch.");
            var saved=JsonUtility.FromJson<SaveData>(envelope.Payload);
            if(saved==null||saved.Version<1||saved.Revision<0)throw new InvalidDataException("Invalid progress record.");
            if(saved.Version!=1)throw new NotSupportedException("Unsupported progress record version.");
            ReadProgress(saved.ProgressJson);
            return saved;
        }
        static PlayerProgress ReadProgress(string json)
        {
            if(string.IsNullOrWhiteSpace(json))throw new InvalidDataException("Missing progress data.");
            // Explicit sentinels distinguish a damaged/missing field from a new
            // player. Restore's legacy fallback must not overwrite a broken save.
            var progress=new PlayerProgress{Version=0,Level=0,Experience=-1,Unlocks=null,CompletedMatches=null};
            JsonUtility.FromJsonOverwrite(json,progress);
            if(progress.Version>1)throw new NotSupportedException("Unsupported player progress version.");
            if(progress.Version!=1||progress.Level<1||progress.Level>10000||progress.Experience<0||
                progress.Unlocks==null||progress.CompletedMatches==null||
                progress.Unlocks.Any(string.IsNullOrWhiteSpace)||progress.CompletedMatches.Any(string.IsNullOrWhiteSpace)||
                progress.Unlocks.Distinct(StringComparer.Ordinal).Count()!=progress.Unlocks.Count||
                progress.CompletedMatches.Distinct(StringComparer.Ordinal).Count()!=progress.CompletedMatches.Count)
                throw new InvalidDataException("Invalid player progress fields.");
            return progress;
        }
        void Save(SaveData saved)
        {
            ReadProgress(saved.ProgressJson);
            string payload=JsonUtility.ToJson(saved);
            string json=JsonUtility.ToJson(new Envelope{Version=1,Payload=payload,Sha256=Hash(payload)},true);
            string temporary=Path.Combine(directory,"progress."+Guid.NewGuid().ToString("N")+".tmp");
            try
            {
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    byte[] bytes=Encoding.UTF8.GetBytes(json);stream.Write(bytes,0,bytes.Length);stream.Flush(true);
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
