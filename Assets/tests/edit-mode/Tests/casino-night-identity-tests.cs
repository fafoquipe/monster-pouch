using System;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class CasinoNightIdentityTests
{
    static readonly string[] Existing={"classic","lounge","royal","naturalis","aquarion","geothar","volkanar"};

    [Test] public void AdditiveIdentitiesPreserveEveryExistingSerializedEnumValue()
    {
        for(int value=0;value<Existing.Length;value++)
        {
            Assert.AreEqual(Existing[value],CardTableTheme.MapId((TableStyle)value));
            Assert.AreEqual(value,(int)CardTableTheme.FromMapId(Existing[value]));
            Assert.IsTrue(MatchController.ValidOnlineMap(Existing[value]));
        }
        Assert.AreEqual(7,(int)TableStyle.CasinoNight);Assert.AreEqual(8,(int)TableStyle.Aireola);
        var expected=Existing.Concat(new[]{"casino-night","aireola"}).ToArray();
        CollectionAssert.AreEquivalent(expected,Enum.GetValues(typeof(TableStyle)).Cast<TableStyle>().Select(CardTableTheme.MapId));
        foreach(string id in expected){Assert.IsTrue(CardTableTheme.KnownMap(id));Assert.IsTrue(MatchController.ValidOnlineMap(id));}
    }

    [TestCase(null)] [TestCase("")] [TestCase("Casino-Night")] [TestCase("casino-night ")]
    [TestCase("casino_night")] [TestCase("casino-night-v2")] [TestCase("../casino-night")] [TestCase("Aireola")] [TestCase("aireola ")] [TestCase("../aireola")]
    public void UnknownOrMalformedMapIdsRemainRejected(string id)
    {
        Assert.IsFalse(CardTableTheme.KnownMap(id));Assert.IsFalse(MatchController.ValidOnlineMap(id));
    }

    [Test] public void ExistingPacketSerializationRetainsAllMapIdsWithoutAliases()
    {
        var packetType=typeof(OnlineRoom).GetNestedType("Packet",BindingFlags.NonPublic);
        Assert.IsNotNull(packetType);
        var mapField=packetType.GetField("map",BindingFlags.Public|BindingFlags.Instance);
        foreach(string id in Existing.Concat(new[]{"casino-night","aireola"}))
        {
            var packet=Activator.CreateInstance(packetType,true);mapField.SetValue(packet,id);
            var restored=JsonUtility.FromJson(JsonUtility.ToJson(packet),packetType);
            Assert.AreEqual(id,mapField.GetValue(restored));
        }
    }

    [Test] public void SavedSelectionIsIndependentAndTheFiveTrophyStagesStayUnchanged()
    {
        var keys=new[]{CardTableTheme.PreferenceKey,CardTableTheme.LobbyPreferenceKey};
        var had=keys.Select(PlayerPrefs.HasKey).ToArray();var saved=keys.Select(PlayerPrefs.GetString).ToArray();
        try
        {
            CardTableTheme.Select(TableStyle.GrandCasaHotel);CardTableTheme.SelectLobby(TableStyle.CasinoNight);
            Assert.AreEqual("lounge",PlayerPrefs.GetString(keys[0]));Assert.AreEqual("casino-night",PlayerPrefs.GetString(keys[1]));
            Assert.AreEqual(TableStyle.GrandCasaHotel,CardTableTheme.Current);Assert.AreEqual(TableStyle.CasinoNight,CardTableTheme.Lobby);
            CardTableTheme.Select(TableStyle.CasinoNight);CardTableTheme.SelectLobby(TableStyle.GrandCasaHotel);
            Assert.AreEqual(TableStyle.CasinoNight,CardTableTheme.Current);Assert.AreEqual(TableStyle.GrandCasaHotel,CardTableTheme.Lobby);
            CardTableTheme.Select(TableStyle.Aireola);CardTableTheme.SelectLobby(TableStyle.CampoReal);
            Assert.AreEqual("aireola",PlayerPrefs.GetString(keys[0]));Assert.AreEqual("royal",PlayerPrefs.GetString(keys[1]));
            Assert.AreEqual(TableStyle.Aireola,CardTableTheme.Current);Assert.AreEqual(TableStyle.CampoReal,CardTableTheme.Lobby);
            var stages=GuestTrophyLadder.CreatePolicy().Stages;
            CollectionAssert.AreEqual(new[]{"naturalis","aquarion","geothar","volkanar","royal"},stages.Select(s=>s.BoardId));
            CollectionAssert.AreEqual(new long[]{0,150,300,450,600},stages.Select(s=>s.MinimumTrophies));
        }
        finally{for(int i=0;i<keys.Length;i++)if(had[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);PlayerPrefs.Save();}
    }
}
