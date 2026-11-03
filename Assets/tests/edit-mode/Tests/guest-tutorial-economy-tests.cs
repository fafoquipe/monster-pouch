using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Machines;
using MonsterPouch.Gameplay.Profiles;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class GuestTutorialEconomyTests
{
    string directory;
    LocalGuestProfileStore store;
    MatchConfig catalogue;
    string Primary => Path.Combine(directory,"guest-profile.json");
    GuestPouchRoomConfiguration Table => new GuestPouchRoomConfiguration {
        ApprovalId="synthetic-tutorial-table",ResourceTableId="synthetic-equal-resource-table",Resources=new[] {
            new GuestPouchResourceReward{Currency=PermanentCurrency.Earthcoins,Amount=20,Chances=9500},
            new GuestPouchResourceReward{Currency=PermanentCurrency.Saturncoins,Amount=5,Chances=500} } };
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchTutorialEconomyTests",Guid.NewGuid().ToString("N"));
        store=new LocalGuestProfileStore(directory);
        catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        store.OpenOrCreate(false);store.ChooseStarter(catalogue,"bugaloo");
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(catalogue);
        string allowed=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchTutorialEconomyTests"))+Path.DirectorySeparatorChar;
        Assert.IsTrue(Path.GetFullPath(directory).StartsWith(allowed,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
    void ReachCollection()
    {foreach(string id in GuestTutorialModules.OrderedIds.Take(6))Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(id));}
    void VisitAll()
    {foreach(string visit in new[]{"deck","collection","ability","wallet"})store.RecordTutorialMenuVisit(visit);}
    void FinishCollection()
    {
        ReachCollection();store.ClaimTutorialPouchCore(catalogue,Table,_=>0);store.OpenTutorialPouch();VisitAll();
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.ProgressionCollection));
    }
    GuestWalletEvent Draw()
    {
        var config=Resources.Load<PlayerMachineCatalogue>("MonsterPouch/VendingCatalogue").Machines.First().Configuration;
        var preview=store.PreviewMachine(catalogue,config);
        var result=store.DrawMachineCore(catalogue,config,new MachineDrawRequest {RequestId=Guid.NewGuid().ToString("N"),
            MachineId=config.Definition.MachineId,CatalogVersion=config.Definition.CatalogVersion,ExpectedRevision=preview.MachineRevision},()=>.9);
        Assert.AreEqual(GuestVendingStatus.Committed,result.Status);return result.Receipt;
    }
    [Test] public void ModuleSevenRequiresActualPouchOpeningAndEveryRealMenuVisit()
    {
        Assert.Throws<InvalidOperationException>(()=>store.ClaimTutorialPouch(catalogue,Table));ReachCollection();
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.ObjectivePending,store.CompleteTutorialModule(GuestTutorialModules.ProgressionCollection));
        var claimed=store.ClaimTutorialPouchCore(catalogue,Table,_=>0);
        Assert.AreEqual(1,claimed.TutorialPouches.Count);Assert.IsEmpty(claimed.Pouches);Assert.IsEmpty(claimed.PouchVictories);
        Assert.AreEqual(20,claimed.FreeBalance(PermanentCurrency.Saturncoins));Assert.AreEqual(0,claimed.FreeBalance(PermanentCurrency.Earthcoins));
        VisitAll();Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.ObjectivePending,store.CompleteTutorialModule(GuestTutorialModules.ProgressionCollection));
        var opened=store.OpenTutorialPouch();Assert.AreEqual(20,opened.FreeBalance(PermanentCurrency.Earthcoins));Assert.IsTrue(opened.TutorialCollectionReady);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.ProgressionCollection));
        Assert.AreEqual(GuestTutorialModules.FirstMachine,store.Reload().NextTutorialModuleId);
    }
    [Test] public void ReopenReplayAndTableChangeNeverRerollOrRepeatTheGift()
    {
        ReachCollection();var original=store.ClaimTutorialPouchCore(catalogue,Table,_=>9999);
        var changed=Table;changed.Resources[0].Amount=500;
        var repeat=new LocalGuestProfileStore(directory).ClaimTutorialPouchCore(catalogue,changed,_=>{Assert.Fail("Already acquired pouch cannot reroll.");return 0;});
        Assert.AreEqual(original.TutorialPouches[0].ResourceRoll,repeat.TutorialPouches[0].ResourceRoll);
        store.OpenTutorialPouch();VisitAll();store.CompleteTutorialModule(GuestTutorialModules.ProgressionCollection);
        string before=File.ReadAllText(Primary);store.OpenTutorialPouch();store.ClaimTutorialPouch(catalogue,changed);VisitAll();
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(25,store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(2,store.Reload().WalletEvents.Count);Assert.IsEmpty(store.Reload().Pouches);
    }
    [Test,Platform("Win")] public void FailedAcquisitionOrOpeningIsAtomicAndTheCommittedRollSurvivesRetry()
    {
        ReachCollection();string before=File.ReadAllText(Primary);
        using(new FileStream(Primary,FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>store.ClaimTutorialPouchCore(catalogue,Table,_=>0));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsEmpty(store.Reload().TutorialPouches);
        store.ClaimTutorialPouchCore(catalogue,Table,_=>9999);before=File.ReadAllText(Primary);
        using(new FileStream(Primary,FileMode.Open,FileAccess.Read,FileShare.Read))Assert.Throws<IOException>(()=>store.OpenTutorialPouch());
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(20,store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        Assert.IsTrue(string.IsNullOrEmpty(store.Reload().TutorialPouches[0].OpenedUtc));
        Assert.AreEqual(25,new LocalGuestProfileStore(directory).OpenTutorialPouch().FreeBalance(PermanentCurrency.Saturncoins));
    }
    [Test] public void MachineLessonRequiresAConfirmedDrawAndNeverGrantsOrSpendsOnItsOwn()
    {
        FinishCollection();string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.ObjectivePending,store.CompleteTutorialModule(GuestTutorialModules.FirstMachine));
        Assert.Throws<ArgumentException>(()=>store.RecordTutorialMachineReward(store.Reload().WalletEvents[0].Id));
        Assert.AreEqual(before,File.ReadAllText(Primary));var receipt=Draw();
        var observed=store.RecordTutorialMachineReward(receipt.Id);Assert.IsTrue(observed.TutorialMachineReady);
        Assert.AreEqual(15,observed.FreeBalance(PermanentCurrency.Saturncoins));
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.FirstMachine));
        Assert.AreEqual(GuestTutorialModules.FirstBo5,store.Reload().NextTutorialModuleId);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.NotImplemented,store.CompleteTutorialModule(GuestTutorialModules.FirstBo5));
        before=File.ReadAllText(Primary);store.RecordTutorialMachineReward(receipt.Id);store.CompleteTutorialModule(GuestTutorialModules.FirstMachine);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void EarlySpenderCanReviewAnExistingDrawWithoutAForcedExtraPurchase()
    {
        var receipt=Draw();Draw();Draw();Draw();Assert.AreEqual(0,store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
        FinishCollection();store.RecordTutorialMachineReward(receipt.Id);
        Assert.AreEqual(LocalGuestProfileStore.TutorialStepResult.Completed,store.CompleteTutorialModule(GuestTutorialModules.FirstMachine));
        Assert.AreEqual(0,store.Reload().FreeBalance(PermanentCurrency.Saturncoins));
    }
    [TestCase("TutorialPouches",false)][TestCase("TutorialPouches",true)]
    [TestCase("TutorialMenuVisits",false)][TestCase("TutorialMenuVisits",true)]
    public void CurrentSchemaRequiresExplicitTutorialCollections(string property,bool nullValue)
    {
        var account=new LocalGuestProfileStore(Path.Combine(directory,"fresh"));account.OpenOrCreate(false);
        string primary=Path.Combine(directory,"fresh","guest-profile.json");
        var envelope=JObject.Parse(File.ReadAllText(primary));var payload=JObject.Parse((string)envelope["Payload"]);
        if(nullValue)payload[property]=JValue.CreateNull();else payload.Remove(property);
        WriteEnvelope(primary,envelope,payload);string corrupt=File.ReadAllText(primary);
        Assert.Throws<InvalidDataException>(()=>account.Reload());Assert.AreEqual(corrupt,File.ReadAllText(primary));
    }
    [Test] public void VersionEightMigratesWithoutGrantingPouchesAndReadingDoesNotRewrite()
    {
        var envelope=JObject.Parse(File.ReadAllText(Primary));var payload=JObject.Parse((string)envelope["Payload"]);
        payload["Version"]=8;payload.Remove("TrophyLadder");payload.Remove("TutorialPouches");payload.Remove("TutorialMenuVisits");payload.Remove("TutorialMachineReceiptId");
        WriteEnvelope(Primary,envelope,payload);string old=File.ReadAllText(Primary);
        var migrated=store.Reload();Assert.AreEqual(LocalGuestProfile.CurrentVersion,migrated.Version);Assert.IsEmpty(migrated.TutorialPouches);
        Assert.AreEqual(20,migrated.FreeBalance(PermanentCurrency.Saturncoins));Assert.AreEqual(old,File.ReadAllText(Primary));
    }
    [Test] public void AReservedTutorialReceiptCannotBeGrantedAsOrdinaryCurrency()
    {
        Assert.AreEqual(GuestVendingStatus.InvalidRequest,store.CreditFreeCurrency("tutorial-pouch:fake","other",PermanentCurrency.Earthcoins,50).Status);
        Assert.AreEqual(GuestVendingStatus.InvalidRequest,store.CreditFreeCurrency("fake","approved-tutorial-blue-pouch-2026-10-24",PermanentCurrency.Earthcoins,50).Status);
        Assert.AreEqual(0,store.Reload().FreeBalance(PermanentCurrency.Earthcoins));
    }
    [Test] public void SavedCompletionAndRewardMustMatchTheActualTutorialEvidence()
    {
        ReachCollection();var profile=store.Reload();
        profile.CompletedTutorialModules.Add(GuestTutorialModules.ProgressionCollection);
        Assert.Throws<InvalidDataException>(()=>profile.Validate(),"A fabricated completion cannot bypass real objectives.");
        store.ClaimTutorialPouchCore(catalogue,Table,_=>0);var opened=store.OpenTutorialPouch();
        opened.WalletEvents.Single(e=>e.Id==opened.TutorialPouches.Single().Id).Amounts[0].Amount++;
        Assert.Throws<InvalidDataException>(()=>opened.Validate(),"Opening and currency must agree exactly.");
        VisitAll();store.CompleteTutorialModule(GuestTutorialModules.ProgressionCollection);
        profile=store.Reload();profile.TutorialMachineReceiptId=profile.WalletEvents[0].Id;
        Assert.Throws<InvalidDataException>(()=>profile.Validate(),"The welcome credit is not a machine draw.");
    }
    static void WriteEnvelope(string path,JObject envelope,JObject payload)
    {
        string text=payload.ToString(Newtonsoft.Json.Formatting.None);envelope["Payload"]=text;
        using(var hash=SHA256.Create())envelope["Sha256"]=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");
        File.WriteAllText(path,envelope.ToString());
    }
}
