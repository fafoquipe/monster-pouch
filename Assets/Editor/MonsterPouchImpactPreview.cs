using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using UnityEngine;

// Runtime-only QA fixtures. Never saves scenes, player preferences or balance assets.
public static class MonsterPouchImpactPreview
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string Preview(string kind)
    {
        var ui = UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        if (ui == null || !Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var match = ui.Match;
        match.enabled = false;
        match.StartMatch("popow", new[] { "flo", "atong", "hymay", "bugui", "tokoro", "trimol", "atori" }, 260926);
        typeof(MatchController).GetMethod("ClearActors", Hidden).Invoke(match, null);
        var blue = (Dictionary<string, OwnedUnit>)typeof(PouchState).GetField("owned", Hidden).GetValue(match.Player);
        var red = (Dictionary<string, OwnedUnit>)typeof(PouchState).GetField("owned", Hidden).GetValue(match.Bot);
        blue.Clear(); red.Clear();
        string fighter = kind == "punch" ? "popow" : kind;
        if (!new[] { "popow", "atong", "flo", "hymay" }.Contains(fighter)) throw new ArgumentException("Unknown impact preview.");
        Add(blue, match, fighter, 2, 7);
        if (kind == "popow")
        {
            Add(red, match, "atori", 2, 5);
            Add(red, match, "tokoro", 2, 2);
        }
        else Add(red, match, "tokoro", fighter == "hymay" ? 4 : fighter == "atong" ? 3 : 2, fighter == "hymay" ? 5 : fighter == "atong" ? 7 : 6);
        typeof(MatchController).GetMethod("SyncActors", Hidden).Invoke(match, null);
        match.Ready(); ui.RenderPage();
        foreach (var actor in match.Actors.Values)
        {
            // Stats are per-combat copies; keep enemies alive and still for the short inspection.
            typeof(UnitStats).GetField("maxHealth", Hidden).SetValue(actor.BaseStats, 500);
            actor.Heal(500);
            if (actor.Side == BoardSide.Red)
            {
                typeof(UnitStats).GetField("attack", Hidden).SetValue(actor.BaseStats, 0);
                typeof(UnitStats).GetField("moveSpeed", Hidden).SetValue(actor.BaseStats, .05f);
                typeof(UnitStats).GetField("attackSpeed", Hidden).SetValue(actor.BaseStats, .05f);
            }
        }
        var source = match.Actors.Values.First(a => a.Side == BoardSide.Blue);
        if (kind == "popow" || kind == "flo") source.AddEnergy(1000);
        ui.StartCoroutine(Record(ui, match, kind));
        return "Started " + kind + " visual preview.";
    }

    static void Add(Dictionary<string, OwnedUnit> roster, MatchController match, string id, int x, int y)
    {
        var owned = new OwnedUnit(match.Config.Get(id));
        typeof(OwnedUnit).GetProperty("Location").SetValue(owned, UnitLocation.Field);
        typeof(OwnedUnit).GetProperty("Deployment").SetValue(owned, new Vector2Int(x, y));
        roster.Add(id, owned);
    }

    static IEnumerator Record(LocalGameUI ui, MatchController match, string kind)
    {
        var simulation = match.Simulation;
        float elapsed = 0;
        int capture = 0;
        var source = match.Actors.Values.First(a => a.Side == BoardSide.Blue);
        var target = match.Actors.Values.First(a => a.Side == BoardSide.Red);
        float windup = CombatSimulation.GetAttackWindup(source);
        float contact = .1f + CombatSimulation.GetImpactDelay(source, target);
        float[] times = kind == "popow" || kind == "flo" ? new[] { .18f, .32f, .48f, .66f, 1.2f } :
            new[] { .1f + windup * .7f, .1f + windup * .94f, contact + .03f, contact + .12f, contact + .24f };
        while (elapsed < times[times.Length-1] + .15f && match.Simulation == simulation)
        {
            float dt = Time.deltaTime;
            match.Advance(dt); elapsed += dt;
            if (capture < times.Length && elapsed >= times[capture])
            {
                MonsterPouchPlayInspection.Capture("impact-" + kind + "-" + capture);
                capture++;
            }
            yield return null;
        }
    }

    public static string Restore()
    {
        var ui = UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        ui.Match.Menu(); ui.Match.enabled = true; ui.RenderPage();
        return "Returned to menu.";
    }
}
