using System.Linq;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class LobbyAvatarTests
{
    [Test] public void InventoryFacesUnlockWithTheirCharacterAndCannotSelectOtherCharacters()
    {
        var profile=new LocalGuestProfile{CollectionMode=GuestCollectionMode.StarterInventory};
        string[] catalogue={"popow","pokrak","rosie"};
        Assert.IsEmpty(LobbyAvatarSelection.Available(profile,catalogue));
        profile.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt{CharacterId="popow"});
        var available=LobbyAvatarSelection.Available(profile,catalogue);
        CollectionAssert.AreEqual(new[]{"popow"},available);
        Assert.AreEqual("popow",LobbyAvatarSelection.Resolve("rosie","pokrak",available));
        profile.CharacterUnlocks.Add(new GuestCharacterUnlockReceipt{CharacterId="rosie"});
        Assert.AreEqual("rosie",LobbyAvatarSelection.Resolve("rosie","popow",LobbyAvatarSelection.Available(profile,catalogue)));
        Assert.AreEqual(2,profile.CharacterUnlocks.Count);
    }
    [Test] public void LegacyCosmeticCompatibilityDoesNotGrantCharacterOwnership()
    {
        var profile=new LocalGuestProfile{CollectionMode=GuestCollectionMode.Legacy,HasLegacyAccess=true};
        string before=JsonUtility.ToJson(profile);
        CollectionAssert.AreEqual(new[]{"pokrak","rosie"},LobbyAvatarSelection.Available(profile,new[]{"pokrak","rosie","pokrak"}));
        Assert.IsFalse(profile.OwnsCharacter("pokrak"));Assert.AreEqual(before,JsonUtility.ToJson(profile));
    }
    [Test] public void MissingProfileNeverGrantsFacesAndPreferencesAreScopedToIdentity()
    {
        Assert.IsEmpty(LobbyAvatarSelection.Available(null,new[]{"kazuru"}));
        Assert.IsNull(LobbyAvatarSelection.Resolve("unknown","kazuru",new string[0]));
        Assert.AreNotEqual(LobbyAvatarSelection.PreferenceKey("player-a"),LobbyAvatarSelection.PreferenceKey("player-b"));
    }
    [Test] public void EveryCatalogueCharacterHasAnExistingSquareFaceCrop()
    {
        var config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        try
        {
            var data=JsonUtility.FromJson<PlayerAvatarManifest>(Resources.Load<TextAsset>("MonsterPouch/UI/PlayerAvatars").text);
            foreach(var unit in config.Units.Where(u=>u!=null))
            {
                var entry=data.entries.Single(e=>e.id==unit.Id);var texture=Resources.Load<Texture2D>(entry.resource);
                Assert.IsNotNull(texture,unit.Id);Assert.AreEqual(4,entry.face.Length);
                Assert.Greater(entry.face[2],0);Assert.Greater(entry.face[3],0);
                Assert.GreaterOrEqual(entry.face[0],0);Assert.GreaterOrEqual(entry.face[1],0);
                Assert.LessOrEqual(entry.face[0]+entry.face[2],1.001f);Assert.LessOrEqual(entry.face[1]+entry.face[3],1.001f);
                Assert.That(texture.width*entry.face[2],Is.EqualTo(texture.height*entry.face[3]).Within(.1f),unit.Id);
            }
        }
        finally{Object.DestroyImmediate(config);}
    }
}
