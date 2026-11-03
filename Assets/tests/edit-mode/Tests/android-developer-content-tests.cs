using System;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AndroidDeveloperContentTests
{
    [Test] public void EditorSupportsDevelopmentAccountsButOrdinaryProfilesKeepOnboarding()
    {
        Assert.IsTrue(AndroidDeveloperContent.Enabled);
        var profile=new LocalGuestProfile { OnboardingStage=GuestOnboardingStage.ChooseStarter };
        Assert.IsTrue(profile.OnboardingRequired);
    }

    [Test] public void DeveloperGrantUsesEveryCurrentCatalogueIdAndIsIdempotentWithoutChangingCombat()
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        void Checkpoint(string phase)
        {
            string message="DeveloperGrant phase="+phase+" elapsedMs="+timer.ElapsedMilliseconds;
            TestContext.Out.WriteLine(message);TestContext.Progress.WriteLine(message);
        }
        Checkpoint("start");
        var source=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        Assert.IsNotNull(source);Checkpoint("resource-loaded");
        // The serialized Units array is the 24-unit migration fallback. Resolve the same
        // authored 44-character catalogue used by startup once, outside all assertion loops.
        var catalogue=source.CreateRuntimeConfig();
        try
        {
            Checkpoint("runtime-created");
            var units=catalogue.Units.Where(u=>u!=null).ToArray();
            var characterIds=units.Select(u=>u.Id).ToArray();
            var abilityIds=units.ToDictionary(u=>u.Id,u=>(u.Tricks??Array.Empty<TrickDefinition>()).Where(a=>a!=null).Select(a=>a.Id).ToArray(),StringComparer.Ordinal);
            Assert.AreEqual(44,units.Length);Assert.AreEqual(44,characterIds.Distinct(StringComparer.Ordinal).Count());
            string sourceOriginal=JsonUtility.ToJson(source);Checkpoint("source-snapshotted");
            string runtimeOriginal=JsonUtility.ToJson(catalogue);Checkpoint("runtime-snapshotted");
            var profile=new LocalGuestProfile { ProfileId=Guid.NewGuid().ToString("N"),
                CollectionMode=GuestCollectionMode.StarterInventory,OnboardingStage=GuestOnboardingStage.ChooseStarter };
            profile.TrophyLadder=GuestTrophyLadder.CreateEngine().CreateState(profile.ProfileId);
            var wallet=profile.WalletEvents.Select(e=>JsonUtility.ToJson(e)).ToArray();
            var balances=Enum.GetValues(typeof(PermanentCurrency)).Cast<PermanentCurrency>().ToDictionary(c=>c,c=>profile.FreeBalance(c));
            Checkpoint("first-grant-start");
            Assert.IsTrue(AndroidDeveloperContent.GrantCatalogue(profile,catalogue));Checkpoint("first-grant-done");
            profile.Validate();
            CollectionAssert.AreEquivalent(characterIds,profile.GetOwnedCharacterIds());
            foreach(var unit in units)
            {
                CollectionAssert.AreEquivalent(abilityIds[unit.Id],profile.GetOwnedAbilityIds(unit.Id),unit.Id);
                var owned=new OwnedUnit(unit);
                Assert.IsTrue(owned.Tricks.All(active=>!active),"Permanent access does not buy match upgrades: "+unit.Id);
                Assert.AreEqual(0,owned.Paid,"The grant cannot buy combat currency/upgrades: "+unit.Id);
            }
            CollectionAssert.AreEqual(wallet,profile.WalletEvents.Select(e=>JsonUtility.ToJson(e)).ToArray());
            foreach(var currency in balances.Keys)Assert.AreEqual(balances[currency],profile.FreeBalance(currency),currency.ToString());
            Assert.IsEmpty(profile.CompletedTutorialModules,"Dev access must not forge completed lessons.");
            Checkpoint("44-characters-and-abilities-checked");
            string granted=JsonUtility.ToJson(profile);
            Assert.IsFalse(AndroidDeveloperContent.GrantCatalogue(profile,catalogue));Checkpoint("second-grant-done");
            Assert.AreEqual(granted,JsonUtility.ToJson(profile));Checkpoint("idempotency-checked");
            Assert.AreEqual(runtimeOriginal,JsonUtility.ToJson(catalogue),"No runtime costs or effects change.");Checkpoint("runtime-unchanged");
            Assert.AreEqual(sourceOriginal,JsonUtility.ToJson(source),"The authored source asset remains unchanged.");Checkpoint("source-unchanged");
        }
        finally { Object.DestroyImmediate(catalogue);Checkpoint("runtime-destroyed"); }
    }

    [Test] public void DeveloperGrantPreservesExistingIdentityAndStarter()
    {
        var catalogue=MatchConfig.CreateDocumented();
        try
        {
            Assert.IsTrue(StarterRoster.TryBuild("popow",catalogue,out var deck));
            var profile=new LocalGuestProfile { ProfileId=Guid.NewGuid().ToString("N"),Revision=9,
                CollectionMode=GuestCollectionMode.StarterInventory,OnboardingStage=GuestOnboardingStage.Tutorial };
            profile.TrophyLadder=GuestTrophyLadder.CreateEngine().CreateState(profile.ProfileId);
            var starter=new StarterSelection { ReceiptId=StarterRoster.CurrentReceiptId,Deck=deck,TimestampUtc=DateTime.UtcNow.ToString("O") };
            profile.StarterSelections.Add(starter);
            profile.CompletedTutorialModules.Add(GuestTutorialModules.FirstContact);
            string identity=profile.ProfileId;
            AndroidDeveloperContent.GrantCatalogue(profile,catalogue);
            Assert.AreEqual(identity,profile.ProfileId);Assert.AreSame(starter,profile.Starter);Assert.AreEqual(9,profile.Revision);
            CollectionAssert.AreEqual(new[]{GuestTutorialModules.FirstContact},profile.CompletedTutorialModules);
        }
        finally { Object.DestroyImmediate(catalogue); }
    }
}
