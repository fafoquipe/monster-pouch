using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;

public sealed class OnlineVerifiedResultTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Digest = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    readonly List<VerifiedOnlineMatchResult> received = new List<VerifiedOnlineMatchResult>();
    MatchConfig config;
    MatchController match;
    OnlineRoom room;
    string sharedId;

    [SetUp] public void Setup()
    {
        received.Clear();
        config = ScriptableObject.CreateInstance<MatchConfig>();
        config.Units = DocumentedRoster.CreateAll();
        var go = new GameObject("Verified result test - no transport");
        var board = go.AddComponent<BoardManager>(); board.BuildBoard();
        var mapper = go.AddComponent<BoardWorldMapper>();
        mapper.Configure(board, go.transform, new Vector2(1, -1), Vector2.zero);
        match = go.AddComponent<MatchController>(); match.Configure(config, board, mapper);
        match.enabled = false;
        Assert.IsTrue(match.StartOnlineMatch(BoardSide.Blue, "bugaloo", new[] { "atori" }, "popow", new[] { "atori" }, 251025));
        room = go.AddComponent<OnlineRoom>(); room.Configure(match); room.enabled = false;
        room.MatchCompletedVerified += received.Add;
        sharedId = Guid.NewGuid().ToString("N");
        Field(room, "sharedMatchId", sharedId); Field(room, "peer", 42UL);
        Property(match, "Phase", MatchPhase.MatchResult);
        Property(match, "PlayerWins", 3); Property(match, "BotWins", 1);
        Field(room, "localDigest", Digest); Field(room, "peerDigest", Digest);
    }

    [TearDown] public void Cleanup()
    {
        if (room != null) room.MatchCompletedVerified -= received.Add;
        if (match != null) { match.Menu(); UnityEngine.Object.DestroyImmediate(match.gameObject); }
        if (config != null) UnityEngine.Object.DestroyImmediate(config);
    }

    [Test] public void FinalResultWaitsForBothDigestsAndPublishesOnlyOnce()
    {
        Field(room, "peerDigest", null); Compare();
        Assert.IsEmpty(received);
        Field(room, "peerDigest", Digest); Compare(); Compare();
        Assert.AreEqual(1, received.Count);
        Assert.AreEqual(sharedId, received[0].MatchId);
        Assert.AreEqual(match.SessionId, received[0].LocalSessionId);
        Assert.AreEqual(Digest, received[0].Digest);
        Assert.AreEqual(3, received[0].PlayerWins);
        Assert.AreEqual(1, received[0].OpponentWins);
        Assert.IsFalse(string.IsNullOrWhiteSpace(received[0].OpponentId));
        Assert.AreEqual(match.Round, room.LastVerifiedRound);
        Assert.AreEqual(Digest, room.LastVerifiedDigest);
    }

    [Test] public void PeerDigestAloneDoesNotPublish()
    { Field(room, "localDigest", null); Compare(); Assert.IsEmpty(received); }

    [Test] public void MismatchStopsMatchWithoutPublishing()
    {
        Field(room, "peerDigest", new string('B', 64)); Compare();
        Assert.IsEmpty(received); Assert.IsFalse(match.IsOnline);
        Assert.IsTrue(room.StatusIsError);
    }

    [TestCase(MatchPhase.Menu)] [TestCase(MatchPhase.Preparation)]
    [TestCase(MatchPhase.Combat)] [TestCase(MatchPhase.RoundResult)]
    public void NonFinalPhasesNeverPublish(MatchPhase phase)
    { Property(match, "Phase", phase); Compare(); Assert.IsEmpty(received); }

    [Test] public void BotMatchNeverPublishesEvenWithMatchingFinalDigests()
    { Property(match, "IsOnline", false); Compare(); Assert.IsEmpty(received); }

    [Test] public void TrainingNeverPublishesEvenWithMatchingFinalDigests()
    { Property(match, "IsTraining", true); Compare(); Assert.IsEmpty(received); }

    [TestCase(1)] [TestCase(2)] [TestCase(4)] [TestCase(5)]
    public void FormatsOtherThanBO5NeverPublish(int requiredWins)
    {
        config.RoundUI.maxRoundsToWin = requiredWins;
        Property(match, "PlayerWins", requiredWins); Property(match, "BotWins", 0);
        Compare(); Assert.IsEmpty(received);
    }

    [TestCase(3, 0)] [TestCase(3, 1)] [TestCase(3, 2)]
    [TestCase(0, 3)] [TestCase(1, 3)] [TestCase(2, 3)]
    public void CompletedBO5PreservesLocalWinAndLossPerspective(int local, int rival)
    {
        Property(match, "PlayerWins", local); Property(match, "BotWins", rival); Compare();
        Assert.AreEqual(1, received.Count);
        Assert.AreEqual(local, received[0].PlayerWins); Assert.AreEqual(rival, received[0].OpponentWins);
    }

    [TestCase(0, 0)] [TestCase(2, 1)] [TestCase(3, 3)]
    [TestCase(4, 1)] [TestCase(1, 4)] [TestCase(-1, 3)] [TestCase(3, -1)]
    public void ImpossibleOrIncompleteScoresNeverPublish(int local, int rival)
    {
        Property(match, "PlayerWins", local); Property(match, "BotWins", rival);
        Compare(); Assert.IsEmpty(received);
    }

    [TestCase(null)] [TestCase("")] [TestCase("not-a-match-id")]
    public void MissingOrInvalidSharedIdentityNeverPublishes(string id)
    { Field(room, "sharedMatchId", id); Compare(); Assert.IsEmpty(received); }

    [TestCase("")] [TestCase("ABC")]
    [TestCase("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public void InvalidMatchingDigestsNeverPublish(string digest)
    {
        Field(room, "localDigest", digest); Field(room, "peerDigest", digest);
        Compare(); Assert.IsEmpty(received);
    }

    [Test] public void ChangingEitherBO5ScoreChangesTheConfirmedDigest()
    {
        string original = match.OnlineDigest();
        Property(match, "PlayerWins", 2);
        Assert.AreNotEqual(original, match.OnlineDigest(), "The digest must bind the local score.");
        Property(match, "PlayerWins", 3); Property(match, "BotWins", 2);
        Assert.AreNotEqual(original, match.OnlineDigest(), "The digest must bind the opponent score.");
    }

    [Test] public void MirroredBlueAndRedViewsConfirmTheSameBO5Digest()
    {
        var go = new GameObject("Mirrored peer - no transport");
        MatchController guest = null;
        try
        {
            var board = go.AddComponent<BoardManager>(); board.BuildBoard();
            var mapper = go.AddComponent<BoardWorldMapper>();
            mapper.Configure(board, go.transform, new Vector2(1, -1), Vector2.zero);
            guest = go.AddComponent<MatchController>(); guest.Configure(config, board, mapper); guest.enabled = false;
            Assert.IsTrue(guest.StartOnlineMatch(BoardSide.Red, "popow", new[] { "atori" }, "bugaloo", new[] { "atori" }, 251025));
            // Preparation intentionally creates only local actors. Reveal and start
            // real combat on both peers before comparing a post-preparation digest.
            Property(match, "Phase", MatchPhase.Preparation);
            Property(match, "PlayerWins", 0); Property(match, "BotWins", 0);
            Field(room, "localDigest", null); Field(room, "peerDigest", null);
            Assert.AreEqual(1, match.Actors.Count); Assert.AreEqual(1, guest.Actors.Count);
            match.Ready(); guest.Ready(); match.SetOpponentReady(); guest.SetOpponentReady();
            Assert.IsTrue(match.RevealOpponent(guest.PreparationCommands, match.Round));
            Assert.IsTrue(guest.RevealOpponent(match.PreparationCommands, guest.Round));
            Assert.AreEqual(match.OnlineDigest(), guest.OnlineDigest(), "Revealed preparation must be canonical on both clients.");
            Assert.IsTrue(match.StartOnlineCombat(match.Round));
            Assert.IsTrue(guest.StartOnlineCombat(guest.Round));
            Assert.AreEqual(2, match.Actors.Count); Assert.AreEqual(2, guest.Actors.Count);
            CollectionAssert.AreEquivalent(match.Actors.Values.Select(a => a.UnitId), guest.Actors.Values.Select(a => a.UnitId));
            Assert.AreEqual(match.OnlineDigest(), guest.OnlineDigest(), "Both peers must start the same real combat state.");
            for (int tick = 0; tick < 500 && (match.Phase == MatchPhase.Combat || guest.Phase == MatchPhase.Combat); tick++)
            { match.Advance(.1f); guest.Advance(.1f); }
            Assert.AreEqual(MatchPhase.RoundResult, match.Phase); Assert.AreEqual(MatchPhase.RoundResult, guest.Phase);
            Assert.AreEqual(match.OnlineDigest(), guest.OnlineDigest(), "Both peers must finish the same real round before the BO5 score assertion.");
            Property(match, "Phase", MatchPhase.MatchResult);
            Property(match, "PlayerWins", 3); Property(match, "BotWins", 1);
            Property(guest, "Phase", MatchPhase.MatchResult);
            Property(guest, "PlayerWins", 1); Property(guest, "BotWins", 3);
            Assert.AreEqual(match.OnlineDigest(), guest.OnlineDigest(), "Scores must use canonical blue/red order, not local/opponent order.");
            Property(guest, "PlayerWins", 2);
            Assert.AreNotEqual(match.OnlineDigest(), guest.OnlineDigest(), "A score disagreement between clients must change the digest.");
        }
        finally
        {
            if (guest != null) guest.Menu();
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    void Compare() => typeof(OnlineRoom).GetMethod("CompareResults", Private).Invoke(room, null);
    static void Field(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name, Private);
        Assert.IsNotNull(field, name); field.SetValue(owner, value);
    }
    static void Property(object owner, string name, object value)
    {
        var property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(property, name); property.GetSetMethod(true).Invoke(owner, new[] { value });
    }
}
