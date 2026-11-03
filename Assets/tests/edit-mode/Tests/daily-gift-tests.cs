using System;
using System.IO;
using System.Linq;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class DailyGiftTests
{
    string root,id;LocalDailyGiftStore store;
    readonly DateTime day=new DateTime(2026,10,22,12,0,0,DateTimeKind.Utc);
    DailyGiftTable Table=>JsonUtility.FromJson<DailyGiftTable>(Resources.Load<TextAsset>("MonsterPouch/UI/Lobby/Header/DailyGifts").text);
    [SetUp] public void Setup(){root=Path.Combine(Path.GetTempPath(),"MonsterPouchDailyGiftTests",Guid.NewGuid().ToString("N"));id=Guid.NewGuid().ToString("N");store=new LocalDailyGiftStore(root);}
    [TearDown] public void Cleanup()
    {
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchDailyGiftTests"))+Path.DirectorySeparatorChar;
        Assert.IsTrue(Path.GetFullPath(root).StartsWith(prefix,StringComparison.OrdinalIgnoreCase));if(Directory.Exists(root))Directory.Delete(root,true);
    }
    [Test] public void ApprovedGiftTableUsesExactAmountsAndFivePercentSaturnkens()
    {
        var table=Table;table.Validate();
        var outcomes=Enumerable.Range(0,10000).Select(table.At).ToArray();
        foreach(var currency in new[]{DailyGiftCurrency.Earthkens,DailyGiftCurrency.Sunkens})
        {
            int[] amounts={10,20,30,50},expected={1000,1500,1500,750};
            for(int i=0;i<amounts.Length;i++)
                Assert.AreEqual(expected[i],outcomes.Count(r=>r.Currency==currency&&r.Amount==amounts[i]),currency+" "+amounts[i]);
            Assert.AreEqual(4750,outcomes.Count(r=>r.Currency==currency));
        }
        var rare=outcomes.Where(r=>r.Currency==DailyGiftCurrency.Saturnkens).ToArray();
        Assert.AreEqual(500,rare.Length);Assert.IsTrue(rare.All(r=>r.Amount==5));
        Assert.AreEqual(9,table.Rewards.Length);Assert.AreEqual("small-daily-gift-v2",table.Id);
    }
    [Test] public void UpdatedTablePreservesEarlierReceiptsAndCannotReopenTheSameDay()
    {
        var previous=new DailyGiftTable{Id="small-daily-gift-v1",Rewards=new[]{
            new DailyGiftReward{Currency=DailyGiftCurrency.Earthkens,Amount=10,Chances=8000},
            new DailyGiftReward{Currency=DailyGiftCurrency.Sunkens,Amount=2,Chances=1900},
            new DailyGiftReward{Currency=DailyGiftCurrency.Saturnkens,Amount=5,Chances=100}}};
        store.ClaimCore(id,day,previous,()=>8000,out _);
        store=new LocalDailyGiftStore(root);
        Assert.AreEqual(LocalDailyGiftStore.ClaimResult.AlreadyClaimed,store.ClaimCore(id,day,Table,()=>throw new Exception("Must not reroll"),out var unchanged));
        Assert.AreEqual(1,unchanged.Receipts.Count);Assert.AreEqual(2,unchanged.Balance(DailyGiftCurrency.Sunkens));
        Assert.AreEqual(previous.Id,unchanged.Receipts[0].TableId);
        Assert.AreEqual(LocalDailyGiftStore.ClaimResult.Claimed,store.ClaimCore(id,day.AddDays(1),Table,()=>4750,out var next));
        Assert.AreEqual(12,next.Balance(DailyGiftCurrency.Sunkens));Assert.AreEqual(2,next.Receipts.Count);
        Assert.AreEqual(previous.Id,next.Receipts[0].TableId);Assert.AreEqual(Table.Id,next.Receipts[1].TableId);
    }
    [Test] public void SameDayRetryAndRestartNeverRerollOrGrantTwice()
    {
        int rolls=0;
        Assert.AreEqual(LocalDailyGiftStore.ClaimResult.Claimed,store.ClaimCore(id,day,Table,()=>{rolls++;return 0;},out var first));
        store=new LocalDailyGiftStore(root);
        Assert.AreEqual(LocalDailyGiftStore.ClaimResult.AlreadyClaimed,store.ClaimCore(id,day.AddHours(2),Table,()=>{rolls++;return 9999;},out var second));
        Assert.AreEqual(1,rolls);Assert.AreEqual(1,second.Receipts.Count);Assert.AreEqual(10,second.Balance(DailyGiftCurrency.Earthkens));
    }
    [Test] public void NextUtcDayGrantsOnceAndBackwardClockDoesNotGrant()
    {
        store.ClaimCore(id,day,Table,()=>0,out _);
        Assert.AreEqual(LocalDailyGiftStore.ClaimResult.Claimed,store.ClaimCore(id,day.Date.AddDays(1),Table,()=>9999,out var next));
        Assert.AreEqual(5,next.Balance(DailyGiftCurrency.Saturnkens));
        Assert.AreEqual(LocalDailyGiftStore.ClaimResult.AlreadyClaimed,store.ClaimCore(id,day.AddDays(-1),Table,()=>throw new Exception("Must not roll"),out _));
    }
    [Test] public void ProfileBalancesAreIsolatedAndReadingDoesNotClaim()
    {
        store.ClaimCore(id,day,Table,()=>0,out _);string other=Guid.NewGuid().ToString("N");
        var fresh=store.Read(other);Assert.AreEqual(0,fresh.Receipts.Count);Assert.AreEqual(0,fresh.Balance(DailyGiftCurrency.Earthkens));
        Assert.IsFalse(File.Exists(Path.Combine(root,other+".json")));
        Assert.Throws<ArgumentException>(()=>store.Read("../qa-profile"));
    }
    [Test] public void CorruptPrimaryWithBackupNeverGrantsFromAnOlderReceiptSet()
    {
        store.ClaimCore(id,day,Table,()=>0,out _);store.ClaimCore(id,day.AddDays(1),Table,()=>9999,out _);
        string path=Path.Combine(root,id+".json");File.WriteAllText(path,"corrupt");
        Assert.AreEqual(LocalDailyGiftStore.ClaimResult.RecoveryRequired,store.ClaimCore(id,day.AddDays(2),Table,()=>throw new Exception("Must not roll"),out _));
        Assert.IsTrue(store.NeedsRecovery);Assert.AreEqual("corrupt",File.ReadAllText(path));
    }
    [Test] public void UnknownEnvelopeAndBadTablePreserveExistingData()
    {
        store.ClaimCore(id,day,Table,()=>0,out _);string path=Path.Combine(root,id+".json");
        string saved=File.ReadAllText(path);var invalid=Table;invalid.Rewards[0].Chances=1;
        Assert.Throws<InvalidDataException>(()=>store.Claim(id,day.AddDays(1),invalid,out _));Assert.AreEqual(saved,File.ReadAllText(path));
        string future=saved.Replace("\"Version\":1","\"Version\":99");File.WriteAllText(path,future);
        Assert.Throws<NotSupportedException>(()=>store.Read(id));Assert.AreEqual(future,File.ReadAllText(path));
    }
    [Test] public void CompetingWriterCannotClaimAndLeavesNoPartialBalance()
    {
        Directory.CreateDirectory(root);
        using(new FileStream(Path.Combine(root,id+".json.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
            Assert.Throws<IOException>(()=>store.Claim(id,day,Table,out _));
        Assert.IsFalse(File.Exists(Path.Combine(root,id+".json")));
    }
    [Test] public void PresentationUsesZeroForEmptyAmountsWithoutChangingAccountData()
    {
        var summary=new LobbyAccountSummary(id,0,10,null);
        Assert.AreEqual("0",LobbyAccountSummary.Number(summary.Earthkens));Assert.AreEqual("0",LobbyAccountSummary.Number(summary.Trophies));Assert.IsNull(summary.Trophies);
        Assert.AreEqual("18 140",LobbyAccountSummary.Number(18140));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new LobbyAccountSummary(id,-1,0,0));
    }
}
