using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Machines;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class GuestAbilityTests
{
    string directory;
    LocalGuestProfileStore store;
    MatchConfig catalogue;
    string Primary=>Path.Combine(directory,"guest-profile.json");
    string Backup=>Path.Combine(directory,"guest-profile.backup.json");
    string Ability(string character="atori",int index=0)=>catalogue.Get(character).Tricks[index].Id;
    [SetUp] public void Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchGuestAbilityTests",Guid.NewGuid().ToString("N"));
        store=new LocalGuestProfileStore(directory);
        catalogue=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
    }
    [TearDown] public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(catalogue);
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchGuestAbilityTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    [Test] public void NewGuestAndStarterCreateNoPermanentAbilityReceipts()
    {
        Assert.IsEmpty(store.OpenOrCreate(false).AbilityUnlocks);
        Choose();var profile=store.Reload();Assert.IsEmpty(profile.AbilityUnlocks);
        foreach(string id in profile.GetOwnedCharacterIds())Assert.IsEmpty(profile.GetOwnedAbilityIds(id));
        Assert.AreEqual(1,profile.Revision);Assert.AreEqual(LocalGuestProfile.CurrentVersion,profile.Version);
    }
    [Test] public void VersionFourMigratesInMemoryPreservingOwnershipIdentityReceiptsAndDeckBytes()
    {
        Choose();store.UnlockCharacter(catalogue,"character:one","flo",GuestUnlockSource.NormalPouch);
        store.CompleteTutorialModule(GuestTutorialModules.FirstContact);
        var before=store.Reload();string encoded=WriteV4(before);
        string deckPath=Path.Combine(directory,"decks.json");File.WriteAllText(deckPath,"existing customized decks");
        var migrated=new LocalGuestProfileStore(directory).Reload();
        Assert.AreEqual(LocalGuestProfile.CurrentVersion,migrated.Version);Assert.AreEqual(before.ProfileId,migrated.ProfileId);
        Assert.AreEqual(before.Revision,migrated.Revision);Assert.AreEqual(before.CollectionMode,migrated.CollectionMode);
        Assert.AreEqual(JsonUtility.ToJson(before.Starter),JsonUtility.ToJson(migrated.Starter));
        Assert.AreEqual(JsonUtility.ToJson(before.CharacterUnlocks[0]),JsonUtility.ToJson(migrated.CharacterUnlocks[0]));
        CollectionAssert.AreEqual(before.CompletedTutorialModules,migrated.CompletedTutorialModules);
        CollectionAssert.AreEqual(before.GetOwnedCharacterIds(),migrated.GetOwnedCharacterIds());
        Assert.IsEmpty(migrated.AbilityUnlocks);Assert.AreEqual(encoded,File.ReadAllText(Primary));
        Assert.AreEqual("existing customized decks",File.ReadAllText(deckPath));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,Unlock());
        var committed=store.Reload();Assert.AreEqual(before.Revision+1,committed.Revision);
        Assert.AreEqual(before.ProfileId,committed.ProfileId);CollectionAssert.AreEqual(before.GetOwnedCharacterIds(),committed.GetOwnedCharacterIds());
        Assert.AreEqual(encoded,File.ReadAllText(Backup));Assert.AreEqual("existing customized decks",File.ReadAllText(deckPath));
    }
    [Test] public void VersionFourLegacyPreservesCompatibilityWithoutInventingAbilities()
    {
        Choose();var old=store.Reload();old.CollectionMode=GuestCollectionMode.Legacy;
        string before=WriteV4(old);var migrated=store.Reload();
        Assert.AreEqual(GuestCollectionMode.Legacy,migrated.CollectionMode);Assert.IsEmpty(migrated.AbilityUnlocks);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.LegacyProfile,Unlock());
        Assert.AreEqual(GuestMachineOwnershipStatus.LegacyUnavailable,store.ReadMachineOwnership(catalogue,Machine(),out var ownership));
        Assert.IsNull(ownership);Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void OldSchemaCannotSmuggleNewAbilityReceiptsThroughMigration()
    {
        Choose();Unlock();var old=store.Reload();old.WalletEvents.Clear();old.Version=4;
        Assert.Throws<InvalidDataException>(()=>old.UpgradeSchema());
    }
    [Test] public void ExplicitUnlockPersistsStableIdAndSourceWithoutChangingCharacterCollection()
    {
        Choose();var before=store.Reload();Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,Unlock());
        var after=new LocalGuestProfileStore(directory).Reload();var receipt=after.AbilityUnlocks.Single();
        Assert.AreEqual("ability:one",receipt.ReceiptId);Assert.AreEqual("atori",receipt.CharacterId);
        Assert.AreEqual(Ability(),receipt.AbilityId);Assert.AreEqual(GuestUnlockSource.Mastery,receipt.Source);
        Assert.IsTrue(DateTime.TryParse(receipt.TimestampUtc,out _));Assert.AreEqual(before.Revision+1,after.Revision);
        CollectionAssert.AreEqual(before.GetOwnedCharacterIds(),after.GetOwnedCharacterIds());
        Assert.IsEmpty(after.CharacterUnlocks);Assert.IsEmpty(after.CompletedTutorialModules);
    }
    [Test] public void RetryAndConflictingPayloadsNeverRewriteAnAppliedUnlock()
    {
        Choose();Unlock();string before=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        var stale=new LocalGuestProfileStore(directory);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.AlreadyApplied,stale.UnlockAbility(catalogue,"ability:one","atori",Ability(),GuestUnlockSource.Mastery));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Conflict,store.UnlockAbility(catalogue,"ability:one","atori",Ability(index:1),GuestUnlockSource.Mastery));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Conflict,store.UnlockAbility(catalogue,"ability:one","bugui",Ability("bugui"),GuestUnlockSource.Mastery));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Conflict,store.UnlockAbility(catalogue,"ability:one","atori",Ability(),GuestUnlockSource.Machine));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.AlreadyOwned,store.UnlockAbility(catalogue,"ability:two","atori",Ability(),GuestUnlockSource.Tutorial));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void ReceiptIdCannotCrossBetweenCharacterAndAbilityOperations()
    {
        Choose();store.UnlockCharacter(catalogue,"character:one","flo",GuestUnlockSource.Machine);
        string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Conflict,store.UnlockAbility(catalogue,"character:one","atori",Ability(),GuestUnlockSource.Machine));
        Assert.AreEqual(before,File.ReadAllText(Primary));Unlock();before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Conflict,store.UnlockCharacter(catalogue,"ability:one","gochan",GuestUnlockSource.Mastery));
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void AbilityRequiresAnOwnedCharacterAndNeverUnlocksItsCharacterImplicitly()
    {
        Choose();string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.CharacterLocked,store.UnlockAbility(catalogue,"flo:ability","flo",Ability("flo"),GuestUnlockSource.NormalPouch));
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsFalse(store.Reload().OwnsCharacter("flo"));
        store.UnlockCharacter(catalogue,"flo:character","flo",GuestUnlockSource.NormalPouch);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,store.UnlockAbility(catalogue,"flo:ability","flo",Ability("flo"),GuestUnlockSource.NormalPouch));
    }
    [Test] public void MissingStarterOrIdentityCannotCreateAbilityOwnership()
    {
        Assert.Throws<InvalidOperationException>(()=>Unlock());Assert.IsFalse(File.Exists(Primary));
        store.OpenOrCreate(false);string before=File.ReadAllText(Primary);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.StarterRequired,Unlock());Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void InvalidIdsSourcesAndReservedReceiptsAreRejectedWithoutWrites()
    {
        Choose();string before=File.ReadAllText(Primary);
        foreach(string ability in new[]{null,"","missing-ability",Ability("bugui"),catalogue.Get("atori").Tricks[0].Name})
            Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest,store.UnlockAbility(catalogue,"ability:invalid","atori",ability,GuestUnlockSource.Mastery));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest,store.UnlockAbility(catalogue,"ability:invalid","atori",Ability(),(GuestUnlockSource)999));
        foreach(string receipt in new[]{null,"",new string('x',161),StarterRoster.CurrentReceiptId,StarterRoster.LegacyReceiptId})
            Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest,store.UnlockAbility(catalogue,receipt,"atori",Ability(),GuestUnlockSource.Mastery));
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void AmbiguousCatalogueIdsDoNotAuthorizeAnUnlock()
    {
        Choose();string before=File.ReadAllText(Primary);var units=catalogue.Units;
        catalogue.Units=units.Concat(new[]{catalogue.Get("atori")}).ToArray();
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest,Unlock());catalogue.Units=units;
        catalogue.Get("atori").Tricks[1].Id=Ability();
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidRequest,Unlock());Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void UnknownCommittedAbilityBlocksFurtherCollectionMutationsAndAdaptation()
    {
        Choose();Unlock();var broken=store.Reload();broken.AbilityUnlocks[0].AbilityId="removed-ability";
        string before=Encode(JsonUtility.ToJson(broken));File.WriteAllText(Primary,before);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidCatalogue,store.UnlockAbility(catalogue,"ability:two","atori",Ability(index:1),GuestUnlockSource.Mastery));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidCatalogue,store.UnlockCharacter(catalogue,"character:one","flo",GuestUnlockSource.Machine));
        Assert.AreEqual(GuestMachineOwnershipStatus.InvalidCatalogue,store.ReadMachineOwnership(catalogue,Machine(),out var ownership));
        Assert.IsNull(ownership);Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void IncompleteOwnedCharacterCatalogueCannotEraseOrExtendOwnership()
    {
        Choose();Unlock();string before=File.ReadAllText(Primary);
        catalogue.Units=catalogue.Units.Where(unit=>unit.Id!="bugui").ToArray();
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidCatalogue,store.UnlockAbility(catalogue,"ability:two","atori",Ability(index:1),GuestUnlockSource.Mastery));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.InvalidCatalogue,store.UnlockCharacter(catalogue,"character:one","flo",GuestUnlockSource.Machine));
        Assert.AreEqual(GuestMachineOwnershipStatus.InvalidCatalogue,store.ReadMachineOwnership(catalogue,Machine(),out var ownership));
        Assert.IsNull(ownership);Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void ConcurrentLockFailureCanRetryWithoutPublishingPartialOwnership()
    {
        Choose();string before=File.ReadAllText(Primary);
        using(new FileStream(Path.Combine(directory,"guest-profile.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            Assert.Throws<IOException>(()=>Unlock());
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsEmpty(store.Reload().AbilityUnlocks);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,Unlock());
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.AlreadyApplied,new LocalGuestProfileStore(directory).UnlockAbility(catalogue,"ability:one","atori",Ability(),GuestUnlockSource.Mastery));
    }
    [Test] public void StaleStoreReloadsLatestReceiptsBeforeCommittingAnotherAbility()
    {
        Choose();var other=new LocalGuestProfileStore(directory);long revision=other.Reload().Revision;
        Unlock();Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,other.UnlockAbility(catalogue,"ability:two","atori",Ability(index:1),GuestUnlockSource.Mastery));
        var profile=store.Reload();Assert.AreEqual(revision+2,profile.Revision);
        CollectionAssert.AreEqual(new[]{Ability(),Ability(index:1)},profile.GetOwnedAbilityIds("atori"));
    }
    [Test,Platform("Win")] public void ReplaceFailurePreservesOriginalFilesAndAllowsTheSameRequestToRetry()
    {
        Choose();string before=File.ReadAllText(Primary),backup=File.ReadAllText(Backup);
        using(new FileStream(Primary,FileMode.Open,FileAccess.Read,FileShare.Read))Assert.Throws<IOException>(()=>Unlock());
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
        Assert.IsEmpty(Directory.GetFiles(directory,"*.tmp"));Assert.IsEmpty(store.Reload().AbilityUnlocks);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.Unlocked,Unlock());
    }
    [Test] public void CorruptLatestGenerationBlocksReplayAndMachineOwnershipFromTheOlderBackup()
    {
        Choose();Unlock();string backup=File.ReadAllText(Backup);File.WriteAllText(Primary,"damaged");
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.RecoveryRequired,Unlock());
        Assert.AreEqual(GuestMachineOwnershipStatus.RecoveryRequired,store.ReadMachineOwnership(catalogue,Machine(),out var ownership));
        Assert.IsNull(ownership);Assert.AreEqual("damaged",File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [Test] public void UnsupportedFutureVersionDoesNotFallBackOrRewrite()
    {
        Choose();var future=store.Reload();future.Version=LocalGuestProfile.CurrentVersion+1;
        string before=Encode(JsonUtility.ToJson(future));File.WriteAllText(Primary,before);
        Assert.Throws<NotSupportedException>(()=>Unlock());
        Assert.Throws<NotSupportedException>(()=>store.ReadMachineOwnership(catalogue,Machine(),out _));
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase("missing")][TestCase("duplicate-ability")][TestCase("duplicate-receipt")][TestCase("unowned-character")][TestCase("cross-receipt")]
    public void InvalidAbilityRecordsAreRejectedStructurally(string mutation)
    {
        Choose();Unlock();var profile=store.Reload();
        if(mutation=="missing")profile.AbilityUnlocks=null;
        else if(mutation=="unowned-character")profile.AbilityUnlocks[0].CharacterId="flo";
        else if(mutation=="cross-receipt")profile.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt{
            ReceiptId="ability:one",CharacterId="flo",Source=GuestUnlockSource.Mastery,TimestampUtc=DateTime.UtcNow.ToString("O")});
        else profile.AbilityUnlocks.Add(new GuestAbilityUnlockReceipt{
            ReceiptId=mutation=="duplicate-receipt"?"ability:one":"ability:two",CharacterId="atori",
            AbilityId=mutation=="duplicate-receipt"?Ability(index:1):Ability(),Source=GuestUnlockSource.Mastery,TimestampUtc=DateTime.UtcNow.ToString("O")});
        Assert.Throws<InvalidDataException>(()=>profile.Validate());
    }
    [Test] public void MissingAbilityListInCurrentPayloadRequiresRecoveryInsteadOfInventingEmptyOwnership()
    {
        Choose();string before=Encode(CurrentPayloadWithoutAbilityList(store.Reload()));File.WriteAllText(Primary,before);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.RecoveryRequired,Unlock());Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase("null")][TestCase("object")][TestCase("string")][TestCase("nested")]
    [TestCase("escaped-text")][TestCase("wrong-case")][TestCase("duplicate")]
    public void InvalidOrNonRootAbilityListCannotMasqueradeAsAPresentCollection(string shape)
    {
        Choose();string json=CurrentPayloadWithoutAbilityList(store.Reload());
        string extra=shape=="null"?"\"AbilityUnlocks\":null":
            shape=="object"?"\"AbilityUnlocks\":{}":
            shape=="string"?"\"AbilityUnlocks\":\"[]\"":
            shape=="nested"?"\"Extension\":{\"AbilityUnlocks\":[]}":
            shape=="escaped-text"?"\"Extension\":\"\\\"AbilityUnlocks\\\": []\"":
            shape=="wrong-case"?"\"abilityUnlocks\":[]":"\"AbilityUnlocks\":[],\"AbilityUnlocks\":[]";
        string before=Encode(json.Substring(0,json.Length-1)+","+extra+"}");File.WriteAllText(Primary,before);
        string backup=File.ReadAllText(Backup);
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.RecoveryRequired,Unlock());
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.AreEqual(backup,File.ReadAllText(Backup));
    }
    [TestCase("whitespace-first")][TestCase("escaped-property")]
    public void PresentEmptyRootArrayIsAcceptedRegardlessOfFormattingOrPropertyOrder(string shape)
    {
        Choose();string json=CurrentPayloadWithoutAbilityList(store.Reload());
        string prefix=shape=="whitespace-first"?"{\n  \"AbilityUnlocks\"  : [  ] ,\n":"{\"\\u0041bilityUnlocks\":[],";
        string before=Encode(prefix+json.Substring(1));File.WriteAllText(Primary,before);
        var profile=store.Reload();Assert.IsFalse(store.NeedsRecovery);Assert.IsEmpty(profile.AbilityUnlocks);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void EscapedRootPropertyPreservesNonEmptyReceiptsAndIdempotentRetry()
    {
        Choose();Unlock();var original=store.Reload();string json=JsonUtility.ToJson(original);
        Assert.That(json.Contains("\"AbilityUnlocks\":"));
        string before=Encode(json.Replace("\"AbilityUnlocks\":","\"\\u0041bilityUnlocks\":"));
        File.WriteAllText(Primary,before);var loaded=store.Reload();
        Assert.IsFalse(store.NeedsRecovery);Assert.AreEqual(original.Revision,loaded.Revision);
        Assert.AreEqual(1,loaded.AbilityUnlocks.Count);
        Assert.AreEqual(JsonUtility.ToJson(original.AbilityUnlocks[0]),JsonUtility.ToJson(loaded.AbilityUnlocks[0]));
        Assert.AreEqual(LocalGuestProfileStore.UnlockResult.AlreadyApplied,Unlock());
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [TestCase("StarterSelections",false)][TestCase("StarterSelections",true)]
    [TestCase("CompletedTutorialModules",false)][TestCase("CompletedTutorialModules",true)]
    [TestCase("CharacterUnlocks",false)][TestCase("CharacterUnlocks",true)]
    [TestCase("WalletEvents",false)][TestCase("WalletEvents",true)]
    [TestCase("VendingMachines",false)][TestCase("VendingMachines",true)]
    public void OtherRequiredCurrentCollectionsCannotBecomeImplicitEmptyLists(string field,bool nullValue)
    {
        var profile=store.OpenOrCreate(false);string json=JsonUtility.ToJson(profile);
        var payload=JObject.Parse(json);
        Assert.IsInstanceOf<JArray>(payload[field],"The fresh profile explicitly stores every required collection, including its welcome receipt.");
        if(nullValue)payload[field]=JValue.CreateNull();else payload.Remove(field);
        json=payload.ToString(Newtonsoft.Json.Formatting.None);
        string before=Encode(json);File.WriteAllText(Primary,before);
        // No backup exists: a malformed first generation must not manufacture a
        // new empty identity or overwrite the supplied bytes.
        Assert.Throws<InvalidDataException>(()=>store.Reload());
        Assert.AreEqual(before,File.ReadAllText(Primary));Assert.IsFalse(File.Exists(Backup));
    }
    [TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)]
    public void HistoricalProfilesNeedOnlyTheCollectionsDefinedInTheirVersion(int version)
    {
        Choose();var profile=store.Reload();
        if(version<4)profile.StarterSelections[0]=new StarterSelection{ReceiptId=StarterRoster.LegacyReceiptId,
            TimestampUtc=profile.Starter.TimestampUtc,Deck=new SavedDeck{Monster="popow",Whelps=StarterRoster.LegacyWhelps.ToArray()}};
        var old=OldPayload(profile);old.Version=version;string json=JsonUtility.ToJson(old);
        if(version<4)json=json.Replace(",\"CharacterUnlocks\":[]","").Replace("\"CharacterUnlocks\":[],","");
        if(version==1)json=json.Replace(",\"CompletedTutorialModules\":[]","").Replace("\"CompletedTutorialModules\":[],","");
        string before=Encode(json);File.WriteAllText(Primary,before);var loaded=store.Reload();
        Assert.IsFalse(store.NeedsRecovery);Assert.AreEqual(profile.ProfileId,loaded.ProfileId);
        Assert.AreEqual(profile.Revision,loaded.Revision);Assert.IsEmpty(loaded.AbilityUnlocks);
        Assert.AreEqual(version<4?GuestCollectionMode.Legacy:GuestCollectionMode.StarterInventory,loaded.CollectionMode);
        CollectionAssert.AreEqual(profile.Starter.Deck.Whelps,loaded.Starter.Deck.Whelps);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void AdapterReadsOnlyTargetCharacterAndReturnsDetachedArraysWithoutWrites()
    {
        Choose();Unlock();store.UnlockAbility(catalogue,"bugui:ability","bugui",Ability("bugui"),GuestUnlockSource.Tutorial);
        string before=File.ReadAllText(Primary);var profile=store.Reload();string snapshot=JsonUtility.ToJson(profile);
        var definition=Machine();string machineBefore=JsonUtility.ToJson(definition);
        Assert.AreEqual(GuestMachineOwnershipStatus.Ready,GuestMachineOwnershipAdapter.TryCreate(profile,catalogue,definition,out var ownership));
        Assert.IsTrue(ownership.CharacterOwned);CollectionAssert.AreEqual(new[]{Ability()},ownership.AbilityIds);
        ownership.AbilityIds[0]="mutated";ownership.CharacterOwned=false;
        Assert.AreEqual(GuestMachineOwnershipStatus.Ready,store.ReadMachineOwnership(catalogue,definition,out var reloaded));
        Assert.IsTrue(reloaded.CharacterOwned);CollectionAssert.AreEqual(new[]{Ability()},reloaded.AbilityIds);
        Assert.AreEqual(snapshot,JsonUtility.ToJson(profile));Assert.AreEqual(machineBefore,JsonUtility.ToJson(definition));
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void AdapterDoesNotInferOwnershipOfAnUnselectedMonsterOrLockedCharacter()
    {
        Choose();foreach(string id in new[]{"bugaloo","flo"})
        {
            Assert.AreEqual(GuestMachineOwnershipStatus.Ready,store.ReadMachineOwnership(catalogue,Machine(id),out var ownership));
            Assert.IsFalse(ownership.CharacterOwned);Assert.IsEmpty(ownership.AbilityIds);
        }
    }
    [Test] public void EditingReturnedProfileAndAbilityListCannotAffectTheNextCommittedSnapshot()
    {
        Choose();Unlock();string before=File.ReadAllText(Primary);var profile=store.Reload();
        ((string[])profile.GetOwnedAbilityIds("atori"))[0]="mutated-copy";
        Assert.AreEqual(Ability(),profile.AbilityUnlocks[0].AbilityId);
        profile.AbilityUnlocks[0].AbilityId="mutated-profile";profile.Starter.Deck.Monster="bugaloo";
        Assert.AreEqual(GuestMachineOwnershipStatus.Ready,store.ReadMachineOwnership(catalogue,Machine(),out var ownership));
        CollectionAssert.AreEqual(new[]{Ability()},ownership.AbilityIds);Assert.AreEqual("popow",store.Reload().Starter.Deck.Monster);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    [Test] public void AdapterRejectsUnknownOrMismatchedMachineAbilities()
    {
        Choose();var machine=Machine();machine.Rewards[1].AbilityId=Ability("bugui");
        Assert.AreEqual(GuestMachineOwnershipStatus.InvalidMachine,store.ReadMachineOwnership(catalogue,machine,out var ownership));Assert.IsNull(ownership);
        machine=Machine();machine.CharacterId="missing-character";
        Assert.AreEqual(GuestMachineOwnershipStatus.InvalidCatalogue,store.ReadMachineOwnership(catalogue,machine,out ownership));
        machine=Machine();machine.Rewards[1].RewardId=machine.Rewards[0].RewardId;
        Assert.AreEqual(GuestMachineOwnershipStatus.InvalidMachine,store.ReadMachineOwnership(catalogue,machine,out ownership));Assert.IsNull(ownership);
    }
    [Test] public void AdapterUnavailableBeforeChoiceOrForAnInvalidProfile()
    {
        Assert.AreEqual(GuestMachineOwnershipStatus.MissingProfile,store.ReadMachineOwnership(catalogue,Machine(),out _));
        Assert.IsFalse(File.Exists(Primary));var profile=store.OpenOrCreate(false);
        Assert.AreEqual(GuestMachineOwnershipStatus.StarterRequired,GuestMachineOwnershipAdapter.TryCreate(profile,catalogue,Machine(),out _));
        profile.Version=999;
        Assert.AreEqual(GuestMachineOwnershipStatus.InvalidProfile,GuestMachineOwnershipAdapter.TryCreate(profile,catalogue,Machine(),out _));
    }
    [Test] public void ExplicitExternalAbilityIsExcludedFromTheExactMachinePreviewWithoutAConsumption()
    {
        Choose();Unlock();var definition=Machine();var state=MachineDrawEngine.CreateState(definition);
        Assert.AreEqual(GuestMachineOwnershipStatus.Ready,store.ReadMachineOwnership(catalogue,definition,out var ownership));
        var policy=new MachinePolicy{PolicyId="qa-ability-adapter-only",Version=1,
            CharacterChanceByDraw=new[]{.01,.01,.01,.01,.01,.02,.04,.08,.2,1.0},
            RewardWeights=definition.Rewards.Select(reward=>new MachineWeight{RewardId=reward.RewardId,Weight=1}).ToArray()};
        string before=File.ReadAllText(Primary);var preview=MachineDrawEngine.Preview(definition,state,ownership,policy);
        Assert.AreEqual(MachineStatus.Ready,preview.Status);Assert.IsTrue(preview.FirstAbilityGuaranteed);
        Assert.AreEqual(MachineRewardStatus.Claimed,preview.Rewards.Single(reward=>reward.RewardId=="qa-ability-0").Status);
        Assert.AreEqual(2,preview.Outcomes.Length);Assert.That(preview.Outcomes.All(outcome=>outcome.Probability==.5));
        Assert.AreEqual(0,state.DrawCount);Assert.IsEmpty(state.Receipts);Assert.IsEmpty(state.ClaimedRewardIds);
        Assert.AreEqual(before,File.ReadAllText(Primary));
    }
    LocalGuestProfileStore.UnlockResult Unlock()=>store.UnlockAbility(catalogue,"ability:one","atori",Ability(),GuestUnlockSource.Mastery);
    void Choose(){store.OpenOrCreate(false);Assert.AreEqual(LocalGuestProfileStore.SelectionResult.Selected,store.ChooseStarter(catalogue,"popow"));}
    MachineDefinition Machine(string character="atori")=>new MachineDefinition{MachineId="qa-machine-"+character,CharacterId=character,CatalogVersion=1,
        Rewards=new[]{new MachineReward{RewardId="qa-character",Kind=MachineRewardKind.Character}}
            .Concat(catalogue.Get(character).Tricks.Select((ability,index)=>new MachineReward{RewardId="qa-ability-"+index,AbilityId=ability.Id,Kind=MachineRewardKind.Ability}))
            .Concat(Enumerable.Range(0,10).Select(index=>new MachineReward{RewardId="qa-resource-"+index,Kind=MachineRewardKind.Resource})).ToArray()};
    string WriteV4(LocalGuestProfile profile)
    {string encoded=Encode(JsonUtility.ToJson(OldPayload(profile)));File.WriteAllText(Primary,encoded);return encoded;}
    static string CurrentPayloadWithoutAbilityList(LocalGuestProfile profile)
    {
        Assert.AreEqual(LocalGuestProfile.CurrentVersion,profile.Version);
        Assert.IsEmpty(profile.AbilityUnlocks);
        string json=JsonUtility.ToJson(profile),property="\"AbilityUnlocks\":[]";
        Assert.That(json.Contains(property),"Remove only AbilityUnlocks; retain every other required current-schema collection.");
        return json.Contains(property+",")?json.Replace(property+",",""):json.Replace(","+property,"");
    }
    static V4Profile OldPayload(LocalGuestProfile profile)=>new V4Profile{Version=4,ProfileId=profile.ProfileId,Revision=profile.Revision,
        HasLegacyAccess=profile.HasLegacyAccess,StarterSelections=profile.StarterSelections,OnboardingStage=profile.OnboardingStage,
        CompletedTutorialModules=profile.CompletedTutorialModules,CollectionMode=profile.CollectionMode,CharacterUnlocks=profile.CharacterUnlocks};
    [Serializable] sealed class V4Profile
    {
        public int Version;public string ProfileKind="local-guest",ProfileId;public long Revision;public bool HasLegacyAccess;
        public List<StarterSelection> StarterSelections;public GuestOnboardingStage OnboardingStage;
        public List<string> CompletedTutorialModules;public GuestCollectionMode CollectionMode;public List<GuestCharacterUnlockReceipt> CharacterUnlocks;
    }
    [Serializable] sealed class Envelope{public int Version=1;public string Payload,Sha256;}
    static string Encode(string payload)
    {
        string checksum;using(var hash=SHA256.Create())checksum=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-","");
        return JsonUtility.ToJson(new Envelope{Payload=payload,Sha256=checksum});
    }
}
