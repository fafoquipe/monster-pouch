using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class MatchBottomMessagesPlayTests
{
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(name => typeof(LocalGameUI).GetProperty(name, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
    string directory;
    string[] oldDirectories;
    LocalGameUI ui;
    MatchController Match => ui.Match;

    [UnitySetUp] public IEnumerator Setup()
    {
        directory = Path.Combine(Path.GetTempPath(), "MonsterPouchBottomMessagesTests", Guid.NewGuid().ToString("N"));
        oldDirectories = Overrides.Select(p => (string)p.GetValue(null)).ToArray();
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, Path.Combine(directory, "store-" + i));
        new LocalGuestProfileStore(Path.Combine(directory, "store-0")).OpenOrCreate(true);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>();
        Assert.IsNotNull(ui);
        Match.enabled = false;
        ui.CloseModal();
        Assert.IsTrue(Match.StartMatch("popow", new[] { "atori" }, 20261022));
        yield return null; yield return null;
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        try
        {
            if (ui != null) { ui.CloseModal(); Match.Menu(); }
            yield return null;
        }
        finally
        {
            if (oldDirectories != null)
                for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, oldDirectories[i]);
            if (directory != null)
            {
                string prefix = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MonsterPouchBottomMessagesTests")) + Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(directory);
                Assert.IsTrue(target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (Directory.Exists(target)) Directory.Delete(target, true);
            }
        }
    }

    [UnityTest] public IEnumerator EliminationAndFinalVictoryKeepCardsAndScoresWithoutTheDiagnosticFooter()
    {
        int targetWins = Match.WinsToWin;
        for (int win = 1; win <= targetWins; win++)
        {
            StartPassiveCombat();
            foreach (var actor in Match.Actors.Values.Where(a => a.Side == Match.RivalSide)) actor.ApplyDamage(actor.CurrentHealth);
            Match.Advance(.2f);
            yield return null; yield return null;
            Assert.That(Match.Phase, Is.EqualTo(win == targetWins ? MatchPhase.MatchResult : MatchPhase.RoundResult));
            Assert.AreEqual(win, Match.PlayerWins);
            Assert.AreEqual(0, Match.BotWins);
            Assert.AreEqual(Match.LocalSide, Match.LastRoundWinner);
            Assert.IsFalse(Match.UsesHealthTiebreaker);
            Match.Advance(Match.RoundStampStartTime + Match.Config.RoundUI.StampDuration * .5f);
            yield return null; yield return null;
            Assert.Greater(ui.RoundTransitionAlpha, .9f);
            AssertSummaryHasNoReason();
            if (win == targetWins)
            {
                Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t => t.text == GameText.Resolve("VICTORYYY")));
                Assert.IsNotNull(Find("Match actions"));
            }
            else
            {
                Match.Advance(Match.RoundTransitionDuration - Match.RoundResultElapsed + .1f);
                yield return null; yield return null;
                Assert.AreEqual(MatchPhase.Preparation, Match.Phase);
            }
        }
    }

    [UnityTest] public IEnumerator TimeoutKeepsTheHealthComparisonAndNecessaryRejectionNoticeWithoutTheFooter()
    {
        StartPassiveCombat();
        Match.Advance(40);
        yield return null; yield return null;
        Assert.AreEqual(MatchPhase.RoundResult, Match.Phase);
        Assert.IsTrue(Match.UsesHealthTiebreaker);
        Assert.IsNotNull(Find("Timeout banner"));
        Assert.IsNotNull(Find("Round health comparison"));
        Assert.IsNotNull(Find("Blue health total"));
        Assert.IsNotNull(Find("Red health total"));
        AssertSummaryHasNoReason();
        Match.Advance(Match.RoundStampStartTime + Match.Config.RoundUI.StampDuration * .5f);
        yield return null; yield return null;
        Assert.Greater(ui.RoundTransitionAlpha, .9f);
        AssertSummaryHasNoReason();

        // The removed diagnostic footer must not disable the separate error overlay.
        const string error = "No tienes suficientes Moonkens.";
        Assert.IsFalse(Match.Reject(error));
        yield return new WaitForSecondsRealtime(.25f);
        var notice = ui.CanvasRoot.GetComponentInChildren<TransientNoticeView>();
        Assert.IsNotNull(notice);
        Assert.That(notice.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(.9f));
        Assert.AreEqual(GameText.Resolve(error), notice.GetComponentsInChildren<Text>().Single(t => t.name == "Error message").text);
        Assert.IsFalse(notice.GetComponent<CanvasGroup>().blocksRaycasts);
    }

    void StartPassiveCombat()
    {
        Match.Ready();
        Assert.AreEqual(MatchPhase.Combat, Match.Phase);
        // Change only this fixture's actor stats: no attacks, effects or revivals
        // can race the requested elimination/timeout and no asset is changed.
        foreach (var actor in Match.Actors.Values)
            actor.Initialize(actor.UnitId, actor.Side, new UnitStats(101, 0, 99, 100, 100));
        Match.Simulation.Begin(Match.Actors.Values.ToArray());
    }

    void AssertSummaryHasNoReason()
    {
        string reason = Match.Simulation.Reason;
        Assert.IsNotEmpty(reason, "The underlying result keeps its diagnostic explanation.");
        Assert.That(Match.Result, Does.Contain(reason));
        Assert.IsNotNull(Find("Enemy round card"));
        Assert.IsNotNull(Find("Local round card"));
        var cards = Find("Round cards");
        Assert.IsFalse(cards.GetComponentsInChildren<Text>(true).Any(text => text.text == GameText.Resolve(reason)),
            "The auxiliary result explanation must not be drawn below the cards.");
        Assert.AreEqual(1, cards.Cast<Transform>().Count(child => child.GetComponent<Text>() != null),
            "Only the result title belongs directly to the summary; no replacement footer is added.");
    }
    RectTransform Find(string name) => ui.CanvasRoot.GetComponentsInChildren<RectTransform>(true).SingleOrDefault(r => r.name == name && r.gameObject.activeInHierarchy);
}
