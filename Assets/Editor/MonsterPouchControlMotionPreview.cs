using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using UnityEngine;

// Runtime-only fixtures. Definitions are deep copies; no assets or preferences are saved.
public static class MonsterPouchControlMotionPreview
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static LocalGameUI owner;
    static Coroutine recording;
    static float originalTimeScale = 1;
    static float originalCaptureDeltaTime;
    static Report report;
    static string originalConfig;
    static string DirectoryPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/local-validation"));

    [Serializable] public sealed class ActorSample
    {
        public string id;
        public int health, x, y;
        public bool stunned, pulled, flying, reviving;
        public float pullDuration, pullRemaining, flightDuration, flightRemaining;
        public Vector3 visualPosition;
        public float visualRotation;
    }
    [Serializable] public sealed class Sample
    {
        public string label, screenshot;
        public float elapsed, simulationTime, flightFraction;
        public ActorSample[] actors;
    }
    [Serializable] public sealed class StunChange
    {
        public string id;
        public bool stunned;
        public float elapsed, simulationTime;
    }
    [Serializable] public sealed class Report
    {
        public string kind, status, trick, jsonPath;
        public bool configUnchanged;
        public float previewTimeScale, previewDashDuration;
        public int airborneProbeDamage = -1;
        public List<Sample> samples = new List<Sample>();
        public List<StunChange> stunChanges = new List<StunChange>();
        public List<string> warnings = new List<string>();
    }

    public static string Preview(string kind)
    {
        kind = (kind ?? "").ToLowerInvariant();
        if (!new[] { "hymay", "popow", "anuik" }.Contains(kind))
            throw new ArgumentException("Use hymay, popow or anuik.");
        var ui = UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        if (ui == null || !Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        StopRecording();
        owner = ui;
        originalTimeScale = Time.timeScale;
        originalCaptureDeltaTime = Time.captureDeltaTime;
        var match = ui.Match;
        originalConfig = JsonUtility.ToJson(match.Config);
        report = new Report { kind = kind, status = "recording", trick = kind == "hymay" ? "hymay-backstep" :
            kind == "popow" ? "popow-dash-stun" : "anuik-revive-impact", previewTimeScale = .5f,
            previewDashDuration = kind == "popow" ? 1.05f : 0,
            jsonPath = Path.Combine(DirectoryPath, "control-motion-" + kind + ".json") };
        try
        {
            match.enabled = false;
            ui.CloseModal();
            if (!match.StartMatch(kind == "hymay" ? "popow" : kind,
                new[] { "gochan", "flo", "atong", "hymay", "bugui", "tokoro", "trimol" }, 260926))
                throw new InvalidOperationException(match.Notice);
            typeof(MatchController).GetMethod("ClearActors", Hidden).Invoke(match, null);
            var blue = Roster(match.Player);
            var red = Roster(match.Bot);
            blue.Clear(); red.Clear();
            var owned = Add(blue, match, kind, 2, kind == "popow" ? 7 : 6, false);
            int trick = Array.FindIndex(owned.Definition.Tricks, t => t != null && t.EffectId == report.trick);
            if (trick < 0) throw new InvalidOperationException("Missing trick " + report.trick);
            owned.Tricks[trick] = true;
            if (kind == "popow") SetParameter(owned.Definition.BaseAbility, "travelDuration", 1.05f);
            if (kind == "hymay") Add(red, match, "tokoro", 4, 4, true);
            else if (kind == "popow")
            {
                Add(red, match, "tokoro", 2, 6, true);
                Add(red, match, "flo", 2, 4, true);
                Add(red, match, "atong", 2, 2, true);
            }
            else
            {
                Add(red, match, "tokoro", 2, 5, true);
                Add(red, match, "flo", 1, 6, true);
                Add(red, match, "atong", 3, 6, true);
            }
            typeof(MatchController).GetMethod("SyncActors", Hidden).Invoke(match, null);
            match.Ready(); ui.RenderPage();
            if (match.Simulation == null) throw new InvalidOperationException("Combat did not start.");
            Time.timeScale = report.previewTimeScale;
            // PNG encoding must not cause a large next-frame jump past a requested pose.
            Time.captureDeltaTime = 1f / 60f;
            recording = ui.StartCoroutine(Record(ui, kind));
            return "Recording " + kind + "; Status() returns progress. JSON: " + report.jsonPath;
        }
        catch
        {
            report.status = "failed";
            Time.timeScale = originalTimeScale;
            Time.captureDeltaTime = originalCaptureDeltaTime;
            match.Menu(); match.enabled = true; ui.RenderPage();
            throw;
        }
    }

    static Dictionary<string, OwnedUnit> Roster(PouchState state) =>
        (Dictionary<string, OwnedUnit>)typeof(PouchState).GetField("owned", Hidden).GetValue(state);

    static OwnedUnit Add(Dictionary<string, OwnedUnit> roster, MatchController match, string id, int x, int y, bool enemy)
    {
        // Clone the full nested parameter/trick graph before tuning any field.
        var definition = JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(match.Config.Get(id)));
        definition.MaxHealth = 1000;
        definition.Damage = 1;
        definition.AttackInterval = definition.MoveInterval = 1000;
        definition.EnergyPerAttack = definition.EnergyOnDamage = definition.EnergyPerSecond = 0;
        if (enemy) { definition.AttackRange = 99; definition.EnergyMax = 0; }
        var owned = new OwnedUnit(definition);
        typeof(OwnedUnit).GetProperty("Location").SetValue(owned, UnitLocation.Field);
        typeof(OwnedUnit).GetProperty("Deployment").SetValue(owned, new Vector2Int(x, y));
        roster.Add(id, owned);
        return owned;
    }

    static void SetParameter(TrickDefinition definition, string key, float value)
    {
        var parameters = (definition.Parameters ?? Array.Empty<AbilityParameter>()).ToList();
        var parameter = parameters.FirstOrDefault(p => p.Key == key);
        if (parameter == null) { parameter = new AbilityParameter { Key = key }; parameters.Add(parameter); }
        parameter.Value = value; definition.Parameters = parameters.ToArray();
    }

    static IEnumerator Record(LocalGameUI ui, string kind)
    {
        var match = ui.Match;
        var simulation = match.Simulation;
        var units = match.Actors.Values.OrderBy(a => a.UnitId).ToArray();
        var source = units.First(a => a.Side == BoardSide.Blue);
        var enemies = units.Where(a => a.Side == BoardSide.Red).ToArray();
        var stunStates = enemies.ToDictionary(a => a, a => false);
        bool pullSeen = false, revived = false, wasReviving = false, wasFlying = false, landed = false;
        float elapsed = 0, flightStart = -1, lastTick = -1;
        float[] fractions = { .2f, .45f, .7f, .95f };
        int pose = 0;
        float duration = kind == "anuik" ? Mathf.Max(6, source.BaseStats.ReviveDelay + 3.8f) : 4.2f;
        Directory.CreateDirectory(Path.Combine(DirectoryPath, "screenshots"));
        yield return new WaitForEndOfFrame();
        Capture("start", elapsed, simulation, units, 0);
        if (kind == "anuik") source.ApplyDamage(source.CurrentHealth);
        else source.AddEnergy(1000);
        try
        {
            while (elapsed < duration && match.Simulation == simulation)
            {
                yield return null;
                float dt = Time.deltaTime;
                match.Advance(dt); elapsed += dt;
                yield return new WaitForEndOfFrame();
                foreach (var enemy in enemies)
                {
                    if (enemy.IsStunned == stunStates[enemy]) continue;
                    stunStates[enemy] = enemy.IsStunned;
                    report.stunChanges.Add(new StunChange { id = enemy.UnitId, stunned = enemy.IsStunned,
                        elapsed = elapsed, simulationTime = simulation.Elapsed });
                    Capture((enemy.IsStunned ? "stun-on-" : "stun-off-") + enemy.UnitId, elapsed, simulation, units, 0);
                }
                if (kind == "hymay")
                {
                    if (!pullSeen && enemies.Any(e => e.IsBeingPulled))
                    { pullSeen = true; Capture("pull", elapsed, simulation, units, 0); }
                    if (source.IsFlying && !wasFlying && flightStart < 0)
                    {
                        flightStart = elapsed;
                        report.airborneProbeDamage = source.ApplyDamage(7);
                        Capture("arrival-flight-start", elapsed, simulation, units, 0);
                    }
                    float fraction = flightStart < 0 ? 0 : Mathf.Clamp01((elapsed - flightStart) / source.FlightDuration);
                    if (source.IsFlying && pose < fractions.Length && fraction >= fractions[pose])
                    {
                        Capture("leap-" + Mathf.RoundToInt(fractions[pose] * 100), elapsed, simulation, units, fraction);
                        pose++;
                    }
                    if (wasFlying && !source.IsFlying)
                    { landed = true; Capture("landing", elapsed, simulation, units, 1); }
                    wasFlying = source.IsFlying;
                }
                if (kind == "anuik")
                {
                    if (wasReviving && !source.IsReviving && source.IsAlive)
                    { revived = true; Capture("after-revive", elapsed, simulation, units, 0); }
                    wasReviving = source.IsReviving;
                }
                if (simulation.Elapsed > lastTick)
                { lastTick = simulation.Elapsed; SampleState("tick", elapsed, simulation, units, 0); }
            }
            if (kind == "hymay" && (!pullSeen || !landed || pose != fractions.Length))
                report.warnings.Add("Missing pull, landing or leap pose: pull=" + pullSeen + ", landing=" + landed + ", poses=" + pose);
            if (kind == "hymay" && report.airborneProbeDamage != 0) report.warnings.Add("Airborne damage probe was not blocked.");
            if (kind == "anuik" && !revived) report.warnings.Add("Revival was not observed.");
            if (report.stunChanges.Where(s => s.stunned).Select(s => s.id).Distinct().Count() != enemies.Length)
                report.warnings.Add("Not every target had a recorded stun onset.");
            report.status = match.Simulation == simulation ? "complete" : "interrupted";
            Capture("end", elapsed, simulation, units, 0);
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            Time.captureDeltaTime = originalCaptureDeltaTime;
            report.configUnchanged = originalConfig == JsonUtility.ToJson(match.Config);
            if (!report.configUnchanged) report.warnings.Add("Config serialization changed during preview.");
            File.WriteAllText(report.jsonPath, JsonUtility.ToJson(report, true));
            Debug.Log("Control-motion QA " + report.kind + ": " + JsonUtility.ToJson(report));
            recording = null;
        }
    }

    static Sample SampleState(string label, float elapsed, CombatSimulation simulation, BattleUnit[] units, float fraction)
    {
        var sample = new Sample { label = label, elapsed = elapsed, simulationTime = simulation.Elapsed,
            flightFraction = fraction, actors = units.Select(unit =>
            {
                var presentation = unit.GetComponent<UnitPresentation>();
                var visual = presentation != null && presentation.Renderer != null ? presentation.Renderer.transform : unit.transform;
                return new ActorSample { id = unit.UnitId, health = unit.CurrentHealth,
                    x = unit.CurrentCell != null ? unit.CurrentCell.X : -1, y = unit.CurrentCell != null ? unit.CurrentCell.Y : -1,
                    stunned = unit.IsStunned, pulled = unit.IsBeingPulled, flying = unit.IsFlying, reviving = unit.IsReviving,
                    pullDuration = unit.PullDuration, pullRemaining = unit.PullRemaining,
                    flightDuration = unit.FlightDuration, flightRemaining = unit.FlightRemaining,
                    visualPosition = visual.position, visualRotation = visual.eulerAngles.z };
            }).ToArray() };
        report.samples.Add(sample); return sample;
    }

    // Called only at end of frame so the saved screenshot exists when its JSON path is reported.
    static void Capture(string label, float elapsed, CombatSimulation simulation, BattleUnit[] units, float fraction)
    {
        var sample = SampleState(label, elapsed, simulation, units, fraction);
        sample.screenshot = Path.Combine(DirectoryPath, "screenshots", "control-" + report.kind + "-" + label + ".png");
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        try { File.WriteAllBytes(sample.screenshot, image.EncodeToPNG()); }
        finally { UnityEngine.Object.Destroy(image); }
    }

    public static string Status() => report == null ? "No control-motion preview started." : JsonUtility.ToJson(report, true);

    static void StopRecording()
    {
        if (recording == null) return;
        if (report != null) report.status = "interrupted";
        if (owner != null) owner.StopCoroutine(recording);
        recording = null; Time.timeScale = originalTimeScale;
        Time.captureDeltaTime = originalCaptureDeltaTime;
    }

    public static string Restore()
    {
        StopRecording();
        var ui = UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        if (ui == null) return "No live LocalGameUI.";
        ui.Match.Menu(); ui.Match.enabled = true; ui.RenderPage();
        return "Returned to menu; definitions and preferences were not saved.";
    }
}
