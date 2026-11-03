using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class AbilityRecoveryPresentationPlayTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly MethodInfo Advance = typeof(UnitPresentation).GetMethod("AdvancePresentation", Hidden);
    readonly List<GameObject> objects = new List<GameObject>();
    CharacterDefinition[] characters;

    [SetUp] public void Setup()
    {
        var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        Assert.IsNotNull(config);
        characters = config.Characters;
        Assert.IsNotEmpty(characters, "Use the current asset catalogue, not the online runtime-only config.");
    }
    [TearDown] public void Cleanup()
    {
        foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
        objects.Clear();
    }
    UnitPresentation Create(string id, out BattleUnit actor, out UnitArt art, BoardWorldMapper mapper = null)
    {
        var character = characters.Single(c => c.Id == id);
        var definition = character.CreateRuntimeDefinition();
        var go = new GameObject("ability-recovery-" + id); objects.Add(go);
        actor = definition.IsMonster ? (BattleUnit)go.AddComponent<MonsterUnit>() : go.AddComponent<WhelpUnit>();
        actor.Initialize(id, BoardSide.Blue, UnitStats.FromDefinition(definition, new OwnedUnit(definition)));
        art = character.Visuals;
        var view = go.AddComponent<UnitPresentation>(); view.enabled = false;
        view.Configure(actor, mapper, art);
        return view;
    }
    static void Sample(UnitPresentation view, float seconds) => Advance.Invoke(view, new object[] { seconds });
    static void Flag(BattleUnit actor, string name, bool value) => typeof(BattleUnit).GetProperty(name).SetValue(actor, value);
    static void Call(BattleUnit actor, string name, params object[] values) => typeof(BattleUnit).GetMethod(name, Hidden).Invoke(actor, values);
    static bool IdleSprite(UnitPresentation view, UnitArt art) => art.Animation(view.Facing).Idle.Contains(view.Renderer.sprite);

    [UnityTest] public IEnumerator CurrentSpecialsReturnToIdleAtTheirExactEndInEveryDirection()
    {
        string[] ids = { "bugaloo", "popow", "sepora", "tauris", "suki", "pokrak", "kazuru", "rosie", "flo", "kayon", "stein", "tokoro", "gochan", "hymay", "trimol", "blotan" };
        foreach (string id in ids)
        {
            var view = Create(id, out var actor, out var art);
            string artBefore = JsonUtility.ToJson(art), statsBefore = JsonUtility.ToJson(actor.BaseStats);
            string kind = id == "trimol" ? "trimol-boulder" : id == "blotan" ? "blotan-income" : id + "-super";
            for (int direction = 0; direction < 8; direction++)
            {
                view.Configure(actor, null, art);
                float angle = direction * Mathf.PI / 4;
                view.Face(Mathf.RoundToInt(Mathf.Sin(angle)), -Mathf.RoundToInt(Mathf.Cos(angle)));
                float duration = view.GetAbilityDuration(kind);
                Assert.Greater(duration, 0, id + " must have a visible authored action.");
                if (id == "flo") view.PrepareFlower(AbilityTiming.Cast(actor.BaseStats, kind));
                else view.UseAbility(kind);
                Assert.IsTrue(view.IsUsingAbility, id);
                Sample(view, duration * .5f);
                Assert.IsTrue(view.IsUsingAbility, id + " ended during its configured sequence.");
                Sample(view, duration * .5f + .000001f);
                Assert.IsFalse(view.IsUsingAbility, id + " retained the action past its configured end.");
                Assert.IsTrue(IdleSprite(view, art), id + " must display idle in this render, without needing another Advance.");
                Assert.IsFalse(view.IsAttacking, id + " created an unrelated basic attack.");
                Assert.AreEqual(Vector3.zero, actor.transform.position, id + " moved its simulation anchor.");
            }
            Assert.AreEqual(artBefore, JsonUtility.ToJson(art), id + " modified authored art.");
            Assert.AreEqual(statsBefore, JsonUtility.ToJson(actor.BaseStats), id + " modified balance.");
            yield return null;
        }
    }

    [UnityTest] public IEnumerator DashAndBackflipFinishTheirExistingMotionBeforeResting()
    {
        var root = new GameObject("ability-recovery-board"); objects.Add(root);
        var board = root.AddComponent<BoardManager>(); board.BuildBoard();
        var mapper = root.AddComponent<BoardWorldMapper>(); mapper.Configure(board, root.transform, Vector2.one, Vector2.zero);
        var dash = Create("popow", out var popow, out var popowArt, mapper);
        Assert.IsTrue(board.TryOccupyCell(popow, 1, 6));
        Vector3 start = mapper.GetWorldPosition(popow.CurrentCell), target = mapper.GetWorldPosition(board.GetCell(1, 4));
        dash.Dash(start, mapper.GetWorldPosition(board.GetCell(1, 5)), target, .6f);
        Sample(dash, .3f); Assert.IsTrue(dash.IsDashing);
        Sample(dash, .3f); Assert.IsFalse(dash.IsDashing); Assert.IsFalse(dash.IsMoving);
        Assert.AreEqual(target, popow.transform.position);
        Assert.IsTrue(IdleSprite(dash, popowArt), "The last dash render must return to idle.");

        var flip = Create("hymay", out var hymay, out var hymayArt, mapper);
        Assert.IsTrue(board.TryOccupyCell(hymay, 3, 6));
        start = mapper.GetWorldPosition(hymay.CurrentCell); target = mapper.GetWorldPosition(board.GetCell(3, 7));
        Call(hymay, "BeginFlight", .7f);
        Assert.IsTrue(board.TryRepositionUnit(hymay, board.GetCell(3, 7)));
        flip.Move(start, target, .7f);
        Sample(flip, .35f); Assert.IsTrue(flip.IsBackflipping);
        Call(hymay, "EndFlight"); Sample(flip, 0);
        var landing = typeof(UnitPresentation).GetField("backflipLanding", Hidden);
        Assert.AreEqual(AbilityTiming.BackflipLandingDuration, (float)landing.GetValue(flip));
        Sample(flip, AbilityTiming.BackflipLandingDuration * .5f);
        Assert.Greater((float)landing.GetValue(flip), 0);
        Sample(flip, AbilityTiming.BackflipLandingDuration * .5f + .000001f);
        Assert.AreEqual(0, (float)landing.GetValue(flip));
        Assert.IsFalse(flip.IsBackflipping); Assert.IsFalse(flip.IsMoving);
        Assert.AreEqual(target, hymay.transform.position);
        Assert.IsTrue(IdleSprite(flip, hymayArt));
        Assert.AreEqual(0, Quaternion.Angle(Quaternion.identity, flip.VisualRoot.localRotation), .001f);
        var flight = Create("aky", out var aky, out var akyArt, mapper);
        Assert.IsTrue(board.TryOccupyCell(aky, 4, 6));
        start = mapper.GetWorldPosition(aky.CurrentCell); target = mapper.GetWorldPosition(board.GetCell(4, 4));
        Call(aky, "BeginFlight", .7f); Assert.IsTrue(board.TryRepositionUnit(aky, board.GetCell(4, 4)));
        flight.Move(start, target, .7f); Sample(flight, .35f);
        Assert.IsFalse(flight.IsBackflipping);
        Call(aky, "EndFlight"); Sample(flight, 0);
        Assert.AreEqual(0, (float)landing.GetValue(flight), "Aky's flight must not acquire Hymay's landing recovery.");
        Assert.IsFalse(flight.IsMoving); Assert.AreEqual(target, aky.transform.position); Assert.IsTrue(IdleSprite(flight, akyArt));
        yield return null;
    }

    [UnityTest] public IEnumerator TimedGuardAndCastPauseKeepTheirOwnLifecycleWithoutRestartingBasicPreparation()
    {
        var view = Create("stein", out var stein, out var art);
        float duration = view.GetAbilityDuration("stein-super");
        view.UseAbility("stein-super"); Sample(view, duration * .5f);
        var sprite = view.Renderer.sprite; var pose = view.VisualRoot.localPosition;
        Sample(view, 0); Assert.AreSame(sprite, view.Renderer.sprite); Assert.AreEqual(pose, view.VisualRoot.localPosition);
        Assert.IsTrue(view.IsUsingAbility, "A paused cast must not consume its recovery.");
        Call(stein, "InterruptAttack"); Sample(view, 0);
        Assert.IsFalse(view.IsUsingAbility); Assert.IsFalse(view.IsAttacking); Assert.IsTrue(IdleSprite(view, art));

        var guard = Create("bugaloo", out var bugaloo, out var guardArt);
        guard.UseAbility("bugaloo-super"); Flag(bugaloo, "IsEnergyLocked", true); Flag(bugaloo, "IsReflecting", true);
        Sample(guard, guard.GetAbilityDuration("bugaloo-super") + 1);
        Assert.IsTrue(guard.IsUsingAbility, "An active reflection window must not end with its charge animation.");
        Assert.IsTrue(IdleSprite(guard, guardArt), "Bugaloo keeps the existing calm guard drawing.");
        Flag(bugaloo, "IsReflecting", false); Flag(bugaloo, "IsEnergyLocked", false); Sample(guard, 0);
        Assert.IsFalse(guard.IsUsingAbility); Assert.IsFalse(guard.IsAttacking); Assert.IsTrue(IdleSprite(guard, guardArt));
        float windup = CombatSimulation.GetAttackWindup(bugaloo);
        guard.Attack(bugaloo.transform.position + Vector3.up, false, windup);
        Assert.That(guard.AttackContactDelay, Is.EqualTo(windup).Within(.00001f), "Starting the next basic must retain exactly its one configured preparation.");
        if (windup > .001f) { Sample(guard, windup - .001f); Assert.IsFalse(guard.AttackContactReached); Sample(guard, .001f); }
        else Sample(guard, 0);
        Assert.IsTrue(guard.AttackContactReached);
        yield return null;
    }
}
