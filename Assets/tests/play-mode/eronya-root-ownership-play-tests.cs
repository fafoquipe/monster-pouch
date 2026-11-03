using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class EronyaRootOwnershipPlayTests
{
    LocalGameUI ui;
    CombatStatusEffects effects;
    BattleUnit actor;
    object owner;
    bool shown;

    [UnitySetUp] public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity"); yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); ui.Match.enabled = false;
        Assert.IsTrue(ui.Match.StartMatch("anuik", new[] { "atong", "flo" })); ui.Match.Ready();
        effects = ui.GetComponent<CombatStatusEffects>();
        actor = ui.Match.Actors[ui.Match.Player.Monster]; owner = new object(); shown = false;
        SetRoot(true); Invoke("EnsureStatus", actor, "root");
        Assert.IsTrue(LegacyRootShown());
    }

    [UnityTearDown] public IEnumerator Cleanup()
    { ui.Match.Pause(false); ui.Match.Menu(); yield return null; }

    void SetRoot(bool value) => typeof(BattleUnit).GetProperty("IsRooted").SetValue(actor, value);
    void Invoke(string method, params object[] args) => typeof(CombatStatusEffects)
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(effects, args);
    bool LegacyRootShown() => ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(x => x.name == "fx-root");
    bool Claim() => effects.ClaimAuthoredRoot(actor, actor.CombatResetVersion, owner, () => shown = false);

    [UnityTest] public IEnumerator ClaimIsAtomicAndDifferentOwnerCannotReleaseOrDuplicate()
    {
        Assert.IsTrue(Claim()); Assert.IsFalse(LegacyRootShown()); shown = true;
        Assert.IsFalse(effects.ClaimAuthoredRoot(actor, actor.CombatResetVersion, new object(), () => { }));
        Assert.IsFalse(effects.ReleaseAuthoredRoot(actor, actor.CombatResetVersion, new object()));
        Invoke("EnsureStatus", actor, "root"); Assert.IsFalse(LegacyRootShown()); Assert.IsTrue(shown);
        Assert.IsTrue(effects.ReleaseAuthoredRoot(actor, actor.CombatResetVersion, owner));
        Assert.IsFalse(shown); Invoke("EnsureStatus", actor, "root"); Assert.IsTrue(LegacyRootShown());
        yield return null;
    }

    [UnityTest] public IEnumerator SameTickClearDuringPauseHidesAuthoredRoot()
    {
        Assert.IsTrue(Claim()); shown = true; ui.Match.Pause(true);
        SetRoot(false); Invoke("LateUpdate");
        Assert.IsFalse(shown); Assert.IsFalse(LegacyRootShown());
        SetRoot(true); Assert.IsTrue(Claim()); shown = true;
        Assert.IsFalse(LegacyRootShown());
        yield return null;
    }

    [UnityTest] public IEnumerator OldGenerationCannotSuppressOrReleaseNewRoot()
    {
        int oldVersion = actor.CombatResetVersion;
        Assert.IsTrue(Claim()); shown = true; actor.ResetForCombat();
        effects.ReconcileAuthoredRootOwners(); Assert.IsFalse(shown);
        SetRoot(true); Invoke("EnsureStatus", actor, "root"); Assert.IsTrue(LegacyRootShown());
        Assert.IsFalse(effects.ClaimAuthoredRoot(actor, oldVersion, owner, () => shown = false));
        Assert.IsTrue(Claim()); shown = true;
        Assert.IsFalse(effects.ReleaseAuthoredRoot(actor, oldVersion, owner)); Assert.IsTrue(shown);
        yield return null;
    }

    [UnityTest] public IEnumerator DisablingEffectsHidesBothWorldRepresentations()
    {
        Assert.IsTrue(Claim()); shown = true; effects.enabled = false;
        Assert.IsFalse(shown); Assert.IsFalse(LegacyRootShown());
        effects.enabled = true; Invoke("EnsureStatus", actor, "root"); Assert.IsTrue(LegacyRootShown());
        yield return null;
    }
}
