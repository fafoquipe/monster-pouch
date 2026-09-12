using System;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEngine;

/// <summary>Editor-only visual review of the actual shipped runtime. Never saves a scene or modifies balance.</summary>
public static class MonsterPouchRosterPreview
{
    static LocalGameUI UI()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode to review the roster.");
        return UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
    }
    public static void Menu()
    {
        var ui = UI(); ui.Match.Menu(); ui.Match.enabled = false; ui.RenderPage();
    }
    public static void AnuikHeadstand()
    {
        var ui = UI(); var match = ui.Match;
        match.StartMatch("anuik", new[] { "kayon", "stein", "bugui" });
        match.enabled = false;
        match.Ready();
        var actor = match.Actors[match.Player.Monster];
        var view = actor.GetComponent<UnitPresentation>();
        view.Face(0,1);
        actor.ApplyDamage(actor.CurrentHealth);
        match.Advance(.1f);
        view.SendMessage("AdvancePresentation", .35f);
        match.Pause(true);
        ui.RenderPage();
    }
    public static void AnuikRecovered()
    {
        var ui = UI(); var match = ui.Match;
        match.Pause(false);
        match.Advance(.8f);
        match.Pause(true);
        ui.RenderPage();
    }
    public static string TaurisBite()
    {
        var ui = UI(); var match = ui.Match;
        match.StartMatch("tauris", new[] { "dummy" });
        match.enabled = false;
        match.Ready();
        var actor = match.Actors[match.Player.Monster];
        MonsterPouch.Gameplay.Units.BattleUnit bitten = null;
        bool fired = false;
        Action<MonsterPouch.Gameplay.Units.BattleUnit, MonsterPouch.Gameplay.Units.BattleUnit, bool> observe =
            (source, target, projectile) => { if (source == actor) { bitten = target; fired = projectile; } };
        match.Attacked += observe;
        try
        {
            for (int step = 0; step < 250 && bitten == null; step++)
            {
                match.Advance(.1f);
                foreach(var other in match.Actors.Values.ToArray())
                    other.GetComponent<UnitPresentation>()?.SendMessage("AdvancePresentation", .1f);
            }
            if (bitten == null) throw new InvalidOperationException("Tauris did not reach an opponent.");
            var view = actor.GetComponent<UnitPresentation>();
            view.SnapToCell();
            bitten.GetComponent<UnitPresentation>().SnapToCell();
            view.Attack(bitten.transform.position, fired, MonsterPouch.Gameplay.Match.CombatSimulation.GetImpactDelay(actor,bitten));
            view.SendMessage("AdvancePresentation", MonsterPouch.Gameplay.Match.CombatSimulation.GetAttackWindup(actor));
            match.Pause(true);
            ui.RenderPage();
            int distance = Math.Abs(actor.CurrentCell.X-bitten.CurrentCell.X)+Math.Abs(actor.CurrentCell.Y-bitten.CurrentCell.Y);
            return $"Tauris range={actor.BaseStats.AttackRange}; distance={distance}; projectile={fired}; pose={view.Renderer.sprite.name}; target={bitten.UnitId}";
        }
        finally { match.Attacked -= observe; }
    }
}
