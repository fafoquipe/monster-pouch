using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;

public sealed class GuestCollectionTests
{
    string directory;
    LocalGuestProfileStore store;
    MatchConfig catalogue;
    string Primary=>Path.Combine(directory,"guest-profile.json");
    string Backup=>Path.Combine(directory,"guest-profile.backup.json");
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchGuestCollectionTests",Guid.NewGuid().ToString("N"));
        store=new LocalGuestProfileStore(directory);
        catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(catalogue);
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchGuestCollectionTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [TestCase("bugaloo")]
    [TestCase("popow")]
    public void NewGuestOwnsOnlyTheChosenMonsterAndTheThreeApprovedWhelps(string monster)
    {
        var empty=store.OpenOrCreate(false);
        Assert.AreEqual(GuestCollectionMode.StarterInventory,empty.CollectionMode);
        Assert.IsFalse(empty.HasAuthoritativeWhelpRoster);Assert.IsEmpty(empty.GetOwnedCharacterIds());
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,store.ChooseStarter(catalogue,monster));
        var chosen=store.OpenOrCreate(false);Assert.AreEqual(LocalGuestProfile.CurrentVersion,chosen.Version);
        Assert.AreEqual(StarterRoster.CurrentReceiptId,chosen.Starter.ReceiptId);
        CollectionAssert.AreEqual(new[]{"atori","bugui","blotan"},chosen.Starter.Deck.Whelps);
        CollectionAssert.AreEqual(new[]{monster,"atori","bugui","blotan"},chosen.GetOwnedCharacterIds());
        CollectionAssert.AreEqual(new[]{"atori","bugui","blotan"},chosen.GetAvailableWhelpIds(catalogue));
        Assert.IsTrue(chosen.HasAuthoritativeWhelpRoster);Assert.IsFalse(chosen.OwnsCharacter("flo"));
        Assert.IsFalse(chosen.OwnsCharacter(monster=="popow"?"bugaloo":"popow"));
        Assert.IsEmpty(chosen.CharacterUnlocks);Assert.IsEmpty(chosen.CompletedTutorialModules);
        string before=File.ReadAllText(Primary);store.OpenOrCreate(true);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void OldGuestKeepsItsSevenReceiptAndUnrestrictedCompatibilityWithoutARewrite(int version)
    {
        string id=store.OpenOrCreate(false).ProfileId;
        var old=new OldGuest{Version=version,ProfileId=id,Revision=5,OnboardingStage=GuestOnboardingStage.Tutorial,
            StarterSelections=new List<StarterSelection>{LegacySelection()},
            CompletedTutorialModules=version==1?new List<string>():new List<string>{GuestTutorialModules.FirstContact}};
        string before=Encode(JsonUtility.ToJson(old));File.WriteAllText(Primary,before);
        var migrated=store.OpenOrCreate(false);
        Assert.AreEqual(LocalGuestProfile.CurrentVersion,migrated.Version);Assert.AreEqual(id,migrated.ProfileId);Assert.AreEqual(5,migrated.Revision);
        Assert.AreEqual(GuestCollectionMode.Legacy,migrated.CollectionMode);Assert.IsFalse(migrated.HasAuthoritativeWhelpRoster);
        Assert.AreEqual(version==1?GuestOnboardingStage.None:GuestOnboardingStage.Tutorial,migrated.OnboardingStage);
        CollectionAssert.AreEqual(StarterRoster.LegacyWhelps,migrated.Starter.Deck.Whelps);
        Assert.AreEqual(StarterRoster.LegacyReceiptId,migrated.Starter.ReceiptId);
        Assert.AreEqual(LocalGuestProfileStore.SelectionResult.AlreadySelected,store.ChooseStarter(catalogue,"popow"));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.LegacyProfile,store.UnlockCharacter(catalogue,"award:old","stein",GuestUnlockSource.NormalPouch));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsEmpty(migrated.CharacterUnlocks);
    }
    [Test] public void UnlocksGrowThreeToSevenInReceiptOrderWithoutQAOrAutomaticPrizes()
    {
        Choose();long initial=store.OpenOrCreate(false).Revision;
        string[] added={"flo","gochan","trimol","atong"};
        for(int i=0;i<added.Length;i++)
            Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,store.UnlockCharacter(catalogue,"approved:unlock:"+i,added[i],GuestUnlockSource.NormalPouch));
        var reopened=new LocalGuestProfileStore(directory).OpenOrCreate(false);
        CollectionAssert.AreEqual(new[]{"atori","bugui","blotan"}.Concat(added),reopened.GetAvailableWhelpIds(catalogue));
        Assert.AreEqual(initial+4,reopened.Revision);Assert.AreEqual(4,reopened.CharacterUnlocks.Count);
        Assert.IsEmpty(reopened.CompletedTutorialModules);Assert.AreEqual(GuestOnboardingStage.Tutorial,reopened.OnboardingStage);
        // A Monster unlock remains owned, but cannot count toward seven Whelps.
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,store.UnlockCharacter(catalogue,"approved:monster","bugaloo",GuestUnlockSource.NormalPouch));
        reopened=store.OpenOrCreate(false);Assert.IsTrue(reopened.OwnsCharacter("bugaloo"));
        Assert.AreEqual(7,reopened.GetAvailableWhelpIds(catalogue).Count);
    }
    [Test] public void DuplicateAndConflictingUnlockReceiptsNeverRewriteOrGrantAgain()
    {
        Choose();Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,store.UnlockCharacter(catalogue,"award:one","flo",GuestUnlockSource.Mastery));
        string before=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        var stale=new LocalGuestProfileStore(directory);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.AlreadyApplied,stale.UnlockCharacter(catalogue,"award:one","flo",GuestUnlockSource.Mastery));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Conflict,stale.UnlockCharacter(catalogue,"award:one","gochan",GuestUnlockSource.Mastery));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Conflict,stale.UnlockCharacter(catalogue,"award:one","flo",GuestUnlockSource.Tutorial));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.AlreadyOwned,stale.UnlockCharacter(catalogue,"award:two","flo",GuestUnlockSource.Tutorial));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.AlreadyOwned,stale.UnlockCharacter(catalogue,"award:three","atori",GuestUnlockSource.Tutorial));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void LockedUnlockCanRetryWithoutPublishingPartialOwnership()
    {
        Choose();string before=File.ReadAllText(Primary);
        using(new FileStream(Path.Combine(directory,"guest-profile.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Assert.Throws<IOException>(()=>store.UnlockCharacter(catalogue,"award:one","flo",GuestUnlockSource.Machine));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsFalse(store.OpenOrCreate(false).OwnsCharacter("flo"));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,store.UnlockCharacter(catalogue,"award:one","flo",GuestUnlockSource.Machine));
        Assert.IsTrue(store.OpenOrCreate(false).OwnsCharacter("flo"));
    }
    [Test] public void CorruptUnlockGenerationCannotReplayFromTheOlderBackup()
    {
        Choose();store.UnlockCharacter(catalogue,"award:one","flo",GuestUnlockSource.NormalPouch);
        string backup=File.ReadAllText(Backup);File.WriteAllText(Primary,"damaged");
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.RecoveryRequired,store.UnlockCharacter(catalogue,"award:one","flo",GuestUnlockSource.NormalPouch));
        Assert.AreEqual("damaged",File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void MissingChoiceAndInvalidUnlockRequestsCannotCreateOwnership()
    {
        store.OpenOrCreate(false);string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.StarterRequired,store.UnlockCharacter(catalogue,"award:one","flo",GuestUnlockSource.Tutorial));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest,store.UnlockCharacter(catalogue,"award:one","not-a-character",GuestUnlockSource.Tutorial));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest,store.UnlockCharacter(catalogue,"award:one","flo",(GuestUnlockSource)100));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest,store.UnlockCharacter(catalogue,StarterRoster.CurrentReceiptId,"flo",GuestUnlockSource.Tutorial));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsEmpty(store.OpenOrCreate(false).GetOwnedCharacterIds());
    }
    [Test] public void HistoricalReceiptVersionMustMatchItsExactApprovedComposition()
    {
        var old=LegacySelection();Assert.IsTrue(StarterRoster.IsValidSelection(old));
        Assert.IsTrue(StarterRoster.TryBuildReceipt(old,catalogue,out var preserved));
        CollectionAssert.AreEqual(StarterRoster.LegacyWhelps,preserved.Whelps);
        old.Deck.Whelps=StarterRoster.Whelps.ToArray();Assert.IsFalse(StarterRoster.IsValidSelection(old));
        old.ReceiptId=StarterRoster.CurrentReceiptId;Assert.IsTrue(StarterRoster.IsValidSelection(old));
        old.Deck.Whelps=new[]{"atori","blotan","bugui"};Assert.IsFalse(StarterRoster.IsValidSelection(old));
        old.Deck.Whelps=new[]{"atori","bugui","stein"};Assert.IsFalse(StarterRoster.IsValidSelection(old));
        old.Deck.Whelps=StarterRoster.LegacyWhelps.ToArray();Assert.IsFalse(StarterRoster.IsValidSelection(old));
    }
    [Test] public void V3CannotSmuggleANewRosterOrV4UnlocksThroughMigration()
    {
        Choose();var old=store.OpenOrCreate(false);old.WalletEvents.Clear();old.Version=3;
        Assert.Throws<InvalidDataException>(()=>old.UpgradeSchema());
        old=store.OpenOrCreate(false);old.WalletEvents.Clear();old.Version=3;old.StarterSelections=new List<StarterSelection>{LegacySelection()};
        old.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt{ReceiptId="award:one",CharacterId="stein",Source=GuestUnlockSource.NormalPouch,TimestampUtc=DateTime.UtcNow.ToString("O")});
        Assert.Throws<InvalidDataException>(()=>old.UpgradeSchema());
    }
    [Test] public void QaHistoricalOwnershipSurvivesTheSharedStarterSchemaChange()
    {
        var qa=LocalEconomyProfile.CreateQA("old XP");qa.Version=3;
        qa.StarterSelections.Add(LegacySelection());
        qa.OwnedCharacterIds.AddRange(new[]{"popow"}.Concat(StarterRoster.LegacyWhelps));qa.OwnedCharacterIds.Add("stein");
        var qaDirectory=Path.Combine(directory,"QA");Directory.CreateDirectory(qaDirectory);
        string path=Path.Combine(qaDirectory,"qa-profile.json"),before=Encode(JsonUtility.ToJson(qa));File.WriteAllText(path,before);
        var restored=new LocalQAProfileStore(qaDirectory).OpenOrCreate();
        Assert.AreEqual(4,restored.Version);CollectionAssert.AreEqual(qa.OwnedCharacterIds,restored.OwnedCharacterIds);
        CollectionAssert.AreEqual(StarterRoster.LegacyWhelps,restored.Starter.Deck.Whelps);
        Assert.AreEqual(before,File.ReadAllText(path));Assert.IsEmpty(restored.Transactions);Assert.IsEmpty(restored.Balances);
    }
    [Test] public void ReloadRefreshesCommittedOwnershipWithoutCreatingOrRewritingIdentity()
    {
        Assert.Throws<InvalidOperationException>(()=>store.Reload());
        Assert.IsFalse(File.Exists(Primary));Assert.IsFalse(File.Exists(Backup));
        Choose();var observer=new LocalGuestProfileStore(directory);string id=observer.Reload().ProfileId;
        store.UnlockCharacter(catalogue,"award:one","flo",GuestUnlockSource.NormalPouch);
        string before=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        var reloaded=observer.Reload();Assert.AreEqual(id,reloaded.ProfileId);Assert.IsTrue(reloaded.OwnsCharacter("flo"));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
        File.Delete(Primary);File.Delete(Backup);
        Assert.Throws<InvalidOperationException>(()=>observer.Reload());
        Assert.IsFalse(File.Exists(Primary));Assert.IsFalse(File.Exists(Backup));
    }
    [Test] public void CurrentSchemaRejectsMissingCollectionAndDuplicateOwnership()
    {
        Choose();var chosen=store.Reload();chosen.CharacterUnlocks=null;
        Assert.Throws<InvalidDataException>(()=>chosen.Validate());
        chosen=store.Reload();chosen.CollectionMode=(GuestCollectionMode)(-1);
        Assert.Throws<InvalidDataException>(()=>chosen.Validate());
        chosen=store.Reload();chosen.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt{
            ReceiptId="award:duplicate-starter",CharacterId="atori",Source=GuestUnlockSource.Tutorial,TimestampUtc=DateTime.UtcNow.ToString("O")});
        Assert.Throws<InvalidDataException>(()=>chosen.Validate());
        chosen=store.Reload();chosen.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt{
            ReceiptId="award:one",CharacterId="flo",Source=GuestUnlockSource.NormalPouch,TimestampUtc=DateTime.UtcNow.ToString("O")});
        chosen.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt{
            ReceiptId="award:one",CharacterId="gochan",Source=GuestUnlockSource.Machine,TimestampUtc=DateTime.UtcNow.ToString("O")});
        Assert.Throws<InvalidDataException>(()=>chosen.Validate());
    }
    void Choose(){store.OpenOrCreate(false);Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,store.ChooseStarter(catalogue,"popow"));}
    static StarterSelection LegacySelection()=>new StarterSelection{ReceiptId=StarterRoster.LegacyReceiptId,TimestampUtc=DateTime.UtcNow.ToString("O"),
        Deck=new SavedDeck{Monster="popow",Whelps=StarterRoster.LegacyWhelps.ToArray()}};
    [Serializable] sealed class OldGuest
    {
        public int Version;public string ProfileKind="local-guest",ProfileId;public long Revision;
        public bool HasLegacyAccess;public List<StarterSelection> StarterSelections;
        public GuestOnboardingStage OnboardingStage;public List<string> CompletedTutorialModules;
    }
    [Serializable] sealed class Envelope{public int Version=1;public string Payload,Sha256;}
    static string Encode(string payload)
    {
        string checksum;using(var hash=SHA256.Create())checksum=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");
        return JsonUtility.ToJson(new Envelope{Payload=payload,Sha256=checksum});
    }
}
