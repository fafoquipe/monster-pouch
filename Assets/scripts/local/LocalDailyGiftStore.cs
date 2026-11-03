using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("MonsterPouch.Gameplay.Tests.EditMode")]

namespace MonsterPouch.Local
{
    public enum DailyGiftCurrency { Earthkens, Sunkens, Saturnkens }
    [Serializable] public sealed class DailyGiftReward
    {
        public DailyGiftCurrency Currency;
        public int Amount;
        public int Chances;
    }
    [Serializable] public sealed class DailyGiftTable
    {
        public string Id;
        public DailyGiftReward[] Rewards;
        public void Validate()
        {
            if(string.IsNullOrWhiteSpace(Id)||Rewards==null||Rewards.Length==0||Rewards.Any(r=>r==null||r.Amount<=0||r.Chances<=0||!Enum.IsDefined(typeof(DailyGiftCurrency),r.Currency))||Rewards.Sum(r=>(long)r.Chances)!=10000)
                throw new InvalidDataException("Daily gift weights must total 10000.");
        }
        public DailyGiftReward At(int roll)
        {
            Validate();if(roll<0||roll>=10000)throw new ArgumentOutOfRangeException(nameof(roll));
            int end=0;foreach(var item in Rewards){end+=item.Chances;if(roll<end)return item;}
            throw new InvalidDataException("Invalid gift table.");
        }
    }
    [Serializable] public sealed class DailyGiftReceipt
    {
        public string Day,TableId;
        public DailyGiftCurrency Currency;
        public int Amount;
    }
    [Serializable] public sealed class DailyGiftAccount
    {
        public int Version=1;
        public string Kind="local-player-daily-gifts",ProfileId;
        public List<DailyGiftReceipt> Receipts=new List<DailyGiftReceipt>();
        public long Balance(DailyGiftCurrency currency)=>Receipts.Where(r=>r.Currency==currency).Sum(r=>(long)r.Amount);
        public bool CanClaim(DateTime utc)=>Receipts.Count==0||string.CompareOrdinal(Receipts[Receipts.Count-1].Day,LocalDailyGiftStore.Day(utc))<0;
    }
    // Earned, device-local gifts only. No QA import, spending, purchased currency
    // or machine entitlement. UTC/time and checksums are not a server authority.
    public sealed class LocalDailyGiftStore
    {
        [Serializable] sealed class Envelope { public int Version=1;public string Payload,Sha256; }
        public enum ClaimResult { Claimed,AlreadyClaimed,RecoveryRequired }
        readonly string directory;
        public bool NeedsRecovery { get; private set; }
        public LocalDailyGiftStore(string directory){this.directory=Path.GetFullPath(directory);}
        public static string Day(DateTime utc)
        {
            if(utc.Kind!=DateTimeKind.Utc)throw new ArgumentException("UTC time is required.");
            return utc.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        }
        string FileFor(string id)
        {
            if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Invalid guest identity.");
            return Path.Combine(directory,id+".json");
        }
        FileStream Lock(string path)
        {
            Directory.CreateDirectory(directory);
            return new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        }
        public DailyGiftAccount Read(string profileId)
        {
            string path=FileFor(profileId);using(Lock(path))return Load(path,profileId);
        }
        public ClaimResult Claim(string profileId,DateTime utc,DailyGiftTable table,out DailyGiftAccount account)
            =>ClaimCore(profileId,utc,table,RandomRoll,out account);
        internal ClaimResult ClaimCore(string profileId,DateTime utc,DailyGiftTable table,Func<int> roll,out DailyGiftAccount account)
        {
            string day=Day(utc);table.Validate();string path=FileFor(profileId);
            using(Lock(path))
            {
                account=Load(path,profileId);
                if(NeedsRecovery)return ClaimResult.RecoveryRequired;
                if(!account.CanClaim(utc))return ClaimResult.AlreadyClaimed;
                var gift=table.At(roll());
                var next=JsonUtility.FromJson<DailyGiftAccount>(JsonUtility.ToJson(account));
                next.Receipts.Add(new DailyGiftReceipt{Day=day,TableId=table.Id,Currency=gift.Currency,Amount=gift.Amount});
                Save(path,next);account=next;return ClaimResult.Claimed;
            }
        }
        static int RandomRoll()
        {
            // Rejection avoids modulo bias: each of 10000 outcomes is equally likely.
            using(var rng=RandomNumberGenerator.Create())
            {
                var bytes=new byte[4];uint n;const uint limit=4294960000;
                do{rng.GetBytes(bytes);n=BitConverter.ToUInt32(bytes,0);}while(n>=limit);
                return (int)(n%10000);
            }
        }
        DailyGiftAccount Load(string path,string id)
        {
            NeedsRecovery=false;
            if(!File.Exists(path)&&!File.Exists(path+".backup"))return new DailyGiftAccount{ProfileId=id};
            try{return ReadFile(path,id);}
            catch(NotSupportedException){throw;}
            catch(Exception error) when(error is IOException||error is InvalidDataException||error is ArgumentException)
            {
                if(!File.Exists(path+".backup"))throw new InvalidDataException("Daily gifts cannot be read. Data preserved.",error);
                var backup=ReadFile(path+".backup",id);NeedsRecovery=true;return backup;
            }
        }
        static DailyGiftAccount ReadFile(string path,string id)
        {
            var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(path,Encoding.UTF8));
            if(envelope==null||envelope.Version<1)throw new InvalidDataException("Missing gift envelope.");
            if(envelope.Version!=1)throw new NotSupportedException("Unsupported gift envelope.");
            if(string.IsNullOrEmpty(envelope.Payload)||envelope.Sha256!=Hash(envelope.Payload))throw new InvalidDataException("Gift checksum mismatch.");
            var account=new DailyGiftAccount{Version=0,Kind=null,Receipts=null};JsonUtility.FromJsonOverwrite(envelope.Payload,account);
            if(account.Version>1)throw new NotSupportedException("Unsupported gifts version.");
            if(account.Version!=1||account.Kind!="local-player-daily-gifts"||account.ProfileId!=id||account.Receipts==null)throw new InvalidDataException("Invalid gift identity.");
            string previous="";
            foreach(var receipt in account.Receipts)
            {
                if(receipt==null||!DateTime.TryParseExact(receipt.Day,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _)||
                    string.CompareOrdinal(previous,receipt.Day)>=0||string.IsNullOrWhiteSpace(receipt.TableId)||receipt.Amount<=0||!Enum.IsDefined(typeof(DailyGiftCurrency),receipt.Currency))throw new InvalidDataException("Invalid gift receipt.");
                previous=receipt.Day;
            }
            return account;
        }
        static void Save(string path,DailyGiftAccount account)
        {
            string payload=JsonUtility.ToJson(account);string json=JsonUtility.ToJson(new Envelope{Payload=payload,Sha256=Hash(payload)});
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                byte[] bytes=Encoding.UTF8.GetBytes(json);
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
                if(File.Exists(path))File.Replace(temporary,path,path+".backup");else File.Move(temporary,path);
            }
            finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
        static string Hash(string text)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
    }
}
