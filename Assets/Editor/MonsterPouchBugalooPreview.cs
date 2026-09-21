using System;
using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;

/// <summary>Play Mode visual inspection of runtime combat; never edits or saves assets.</summary>
public static class MonsterPouchBugalooPreview
{
    public static void Preview() => ProtectedReflection();
    public static void Reflection() => Prepare(false);
    public static void ProtectedReflection() => Prepare(true);

    static void Prepare(bool protection)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode before previewing Bugaloo.");
        var ui = UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        if (ui == null || ui.Match == null) throw new InvalidOperationException("No active LocalGameUI runtime.");
        var match = ui.Match;
        var whelps = match.Config.Units.Where(unit => unit != null && !unit.IsMonster && !string.IsNullOrWhiteSpace(unit.Id))
            .Select(unit => unit.Id).Distinct().Take(match.Config.SelectionLimit).ToArray();
        if (!match.StartMatch("bugaloo", whelps)) throw new InvalidOperationException(match.Notice);
        match.enabled = false;
        foreach (int x in new[] { 2, 3 })
        {
            var cell = match.Board.GetCell(x, 5);
            if (cell != null && !cell.IsBlocked && cell.OccupiedBy == null && match.Config.IsDeploymentLegal(match.Player.Side, cell.Coordinates)
                && match.Place("bugaloo", cell)) break;
        }
        bool purchased = protection && match.UpgradeMonster(0);
        match.Ready();
        var actor = match.Actors[match.Player.Monster];
        if (protection && !purchased)
        {
            // A custom balance may make the upgrade unaffordable. Clone its current
            // definition and add the owned ability solely to this runtime actor.
            var definition = JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(match.Player.Monster.Definition));
            var owned = new OwnedUnit(definition); owned.Tricks[0] = true;
            actor.Initialize(actor.UnitId, actor.Side, UnitStats.FromDefinition(definition, owned));
        }
        var view = actor.GetComponent<UnitPresentation>();
        view?.SnapToCell(); view?.Face(0, 1);
        actor.AddEnergy(actor.BaseStats.EnergyMax);
        for (int tick = 0; tick < 20 && match.Phase == MatchPhase.Combat && !actor.IsReflecting; tick++) match.Advance(.1f);
        if (!actor.IsReflecting || (protection && !actor.IsProtected))
            throw new InvalidOperationException("Bugaloo's runtime super did not activate.");
        ui.RenderPage();
        ui.StartCoroutine(FreezeAndCapture(ui, actor, protection ? "bugaloo-protected-reflection" : "bugaloo-reflection"));
    }

    static IEnumerator FreezeAndCapture(LocalGameUI ui, BattleUnit actor, string screenshot)
    {
        // Let the normal Brief close, special animation, and status LateUpdate render.
        // The disabled MatchController keeps the authoritative super timer frozen.
        yield return new WaitForSecondsRealtime(.45f);
        yield return new WaitForEndOfFrame();
        if (ui == null || actor == null || !actor.IsReflecting) yield break;
        ui.Match.Pause(true);
        ui.RenderPage();
        Debug.Log("Bugaloo preview: reflecting=" + actor.IsReflecting + "; protected=" + actor.IsProtected +
            "; energyLocked=" + actor.IsEnergyLocked + "; energy=" + actor.Energy + "; screenshot=" + screenshot);
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying && ui != null && actor != null && actor.IsReflecting)
                MonsterPouchPlayInspection.Capture(screenshot);
        };
    }
}
