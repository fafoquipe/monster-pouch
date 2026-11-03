using System;
using System.Collections;
using System.Linq;
using System.Text;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;

// Runtime-only bridge fixtures. Never saves scenes, configuration or player preferences.
public static class MonsterPouchRoundUIPreview
{
    static LocalGameUI owner;
    static Coroutine recording;
    static string lastReport = "No round UI preview recorded.";
    static EditorWindow previewWindow;
    static bool wasMaximized;

    public static string ExpandView()
    {
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (type == null) return "Game view type unavailable.";
        previewWindow = EditorWindow.GetWindow(type);
        wasMaximized = previewWindow.maximized;
        previewWindow.maximized = true;
        previewWindow.Focus();
        return "Game view expanded for visual inspection.";
    }

    // Bridge argument: all, combat, enemy, player, final or nextprep.
    public static string Preview(string kind)
    {
        kind = (kind ?? "all").Trim().ToLowerInvariant();
        if (!new[] { "all", "combat", "enemy", "player", "final", "nextprep" }.Contains(kind))
            throw new ArgumentException("Use all, combat, enemy, player, final or nextprep.", nameof(kind));
        var ui = UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        if (!Application.isPlaying || ui == null || ui.Match == null)
            throw new InvalidOperationException("Enter Play Mode with the local game scene first.");
        StopRecording();
        owner = ui;
        ui.CloseModal();
        ui.Match.enabled = false;
        lastReport = "Recording round UI: " + kind;
        recording = ui.StartCoroutine(Record(ui, kind));
        return lastReport + ". Screenshots: Logs/local-validation/screenshots/round-ui-*.png. Call Report for state or Restore to return to menu.";
    }

    static void Start(LocalGameUI ui)
    {
        // StartMatch changes only the live session. The fixed roster/seed makes repeated captures comparable.
        if (!ui.Match.StartMatch("popow", new[] { "flo", "atong", "hymay", "bugui", "tokoro", "trimol", "atori" }, 260926))
            throw new InvalidOperationException("Could not start the round UI fixture: " + ui.Match.Notice);
        ui.RenderPage();
    }

    static IEnumerator Record(LocalGameUI ui, string kind)
    {
        var match = ui.Match;
        Start(ui);
        match.Ready();
        if (kind == "combat" || kind == "all")
            yield return Capture(ui, "combat-hud");
        if (kind == "combat") { recording = null; yield break; }

        BoardSide winner = kind == "enemy" ? BoardSide.Red : BoardSide.Blue;
        yield return Resolve(match, winner);
        if (kind != "final")
        {
            if (kind == "player" || kind == "enemy" || kind == "all")
            {
                yield return Capture(ui, winner == BoardSide.Blue ? "player-before-stamp" : "enemy-before-stamp");
                AdvanceResultTo(match, match.RoundStampStartTime + match.Config.RoundUI.StampDuration * .5f);
                yield return Capture(ui, winner == BoardSide.Blue ? "player-stamp" : "enemy-stamp");
            }
            AdvanceResultTo(match, match.RoundStampStartTime + match.Config.RoundUI.StampDuration);
            if (kind != "nextprep")
                yield return Capture(ui, winner == BoardSide.Blue ? "player-win" : "enemy-win");
        }
        if (kind == "nextprep" || kind == "all")
        {
            AdvanceResultTo(match, match.RoundTransitionDuration + .01f);
            if (match.Phase != MatchPhase.Preparation)
                throw new InvalidOperationException("The round did not advance automatically to preparation.");
            // Let the Brief open while the match clock remains frozen.
            float deadline = Time.realtimeSinceStartup + 3;
            while (!ui.BriefReady && Time.realtimeSinceStartup < deadline) yield return null;
            yield return Capture(ui, "next-preparation");
        }
        if (kind == "all")
        {
            Start(ui); match.Ready();
            yield return Resolve(match, BoardSide.Red);
            AdvanceResultTo(match, match.RoundStampStartTime + match.Config.RoundUI.StampDuration);
            yield return Capture(ui, "enemy-win");
            Start(ui); match.Ready();
            yield return Resolve(match, BoardSide.Blue);
        }
        if (kind == "final" || kind == "all")
        {
            while (match.Phase == MatchPhase.RoundResult)
            {
                AdvanceResultTo(match, match.RoundTransitionDuration + .01f);
                match.Ready();
                yield return Resolve(match, BoardSide.Blue);
            }
            if (match.Phase != MatchPhase.MatchResult || match.PlayerWins != match.WinsToWin)
                throw new InvalidOperationException("The final preview did not reach the configured winning score.");
            AdvanceResultTo(match, match.RoundTransitionDuration + .01f);
            yield return Capture(ui, "final-flags");
        }
        recording = null;
    }

    static IEnumerator Resolve(MatchController match, BoardSide winner)
    {
        // Repeatedly use real damage/ticks: randomized rivals may revive, summon or temporarily fly.
        for (int tick = 0; tick < 150 && match.Phase == MatchPhase.Combat; tick++)
        {
            foreach (var actor in match.Actors.Values.Concat(match.Simulation.CombatOnlyUnits)
                .Where(actor => actor != null && actor.Side != winner && actor.IsAlive).Distinct().ToArray())
                actor.ApplyDamage(actor.CurrentHealth);
            match.Advance(.1f);
            yield return null;
        }
        if ((match.Phase != MatchPhase.RoundResult && match.Phase != MatchPhase.MatchResult) || match.LastRoundWinner != winner)
            throw new InvalidOperationException("The damage fixture failed to resolve the expected round winner.");
    }

    static void AdvanceResultTo(MatchController match, float elapsed)
    {
        match.Advance(Mathf.Max(0, elapsed - match.RoundResultElapsed));
    }

    static IEnumerator Capture(LocalGameUI ui, string name)
    {
        // LocalGameUI.Update consumes the fixed controller clock, then Canvas and capture settle.
        yield return null;
        yield return null;
        MonsterPouchPlayInspection.Capture("round-ui-" + name);
        yield return null;
        yield return null;
        lastReport = "Captured round-ui-" + name + ".png\n" + Describe(ui);
        Debug.Log(lastReport);
    }

    public static string Report(string unused = "")
    {
        var ui = UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        return lastReport + (ui != null && ui.Match != null ? "\nCurrent state:\n" + Describe(ui) : "");
    }

    static string Describe(LocalGameUI ui)
    {
        var match = ui.Match;
        var report = new StringBuilder();
        report.AppendLine("phase=" + match.Phase + "; round=" + match.Round + "; score=" + match.PlayerWins + ":" + match.BotWins +
            "; winner=" + match.LastRoundWinner + "; resultElapsed=" + match.RoundResultElapsed.ToString("F3") +
            "; autoUpdate=" + match.enabled + "; transitionAlpha=" + ui.RoundTransitionAlpha.ToString("F2") +
            "; stamp=" + ui.RoundStampProgress.ToString("F2") + "; finalActions=" + ui.RoundFinalActionsVisible);
        foreach (string name in new[] { "Enemy round HUD", "Player round HUD", "Enemy round card", "Local round card" })
        {
            var panel = ui.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.name == name && rect.gameObject.activeInHierarchy);
            var flags = panel != null ? panel.GetComponentsInChildren<RoundFlagIcon>(true) : new RoundFlagIcon[0];
            report.AppendLine(name + ": active=" + (panel != null) + "; flags=" + flags.Length +
                "; won=" + flags.Count(flag => flag.Won));
        }
        return report.ToString();
    }

    public static string Restore(string unused = "")
    {
        StopRecording();
        if(previewWindow != null)previewWindow.maximized = wasMaximized;
        previewWindow = null;
        var ui = UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        if (ui == null || ui.Match == null) return "No active local game.";
        ui.CloseModal(); ui.Match.Menu(); ui.Match.enabled = true; ui.RenderPage();
        return "Returned to menu; automatic match updates restored.";
    }

    static void StopRecording()
    {
        if (owner != null && recording != null) owner.StopCoroutine(recording);
        owner = null; recording = null;
    }
}
