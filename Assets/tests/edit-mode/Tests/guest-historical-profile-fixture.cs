using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;

// A real pre-welcome schema for tests whose subject requires an earned wallet
// starting at zero. Never subtract a production grant or expose a runtime bypass.
internal static class GuestHistoricalProfileFixture
{
    [Serializable] sealed class Envelope { public int Version = 1; public string Payload, Sha256; }
    internal static LocalGuestProfileStore CreateVersionSeven(string directory, MatchConfig catalogue, string monster = "bugaloo")
    {
        Directory.CreateDirectory(directory);
        var historical = new LocalGuestProfile {
            Version = 7, ProfileId = Guid.NewGuid().ToString("N"),
            CollectionMode = GuestCollectionMode.StarterInventory, OnboardingStage = GuestOnboardingStage.ChooseStarter
        };
        string payload = WithoutTrophies(JsonUtility.ToJson(historical));
        using (var hash = SHA256.Create()) File.WriteAllText(Path.Combine(directory, "guest-profile.json"), JsonUtility.ToJson(new Envelope {
            Payload = payload, Sha256 = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "")
        }));
        var store = new LocalGuestProfileStore(directory);
        if (monster != null) store.ChooseStarter(catalogue, monster);
        return store;
    }
    internal static string WithoutTrophies(string payload)
    {
        using(var reader=new Newtonsoft.Json.JsonTextReader(new StringReader(payload)) {DateParseHandling=Newtonsoft.Json.DateParseHandling.None})
        {
            var root=Newtonsoft.Json.Linq.JObject.Load(reader);root.Remove("TrophyLadder");
            return root.ToString(Newtonsoft.Json.Formatting.None);
        }
    }
}
