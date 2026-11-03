using System.Collections;
using System.Reflection;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class RuntimeHotloopPlayTests
{
    LocalGameUI ui;
    EronyaMapTestAccount account;
    [UnitySetUp] public IEnumerator LoadIsolatedMenu()
    {
        account = new EronyaMapTestAccount(0);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); Assert.IsNotNull(ui); ui.Match.enabled = false;
        float deadline = Time.realtimeSinceStartup + 15;
        while ((LoadingArtworkScreen.IsBlocking || ui.MapTransitionActive) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking); Assert.IsFalse(ui.MapTransitionActive);
    }
    [UnityTearDown] public IEnumerator RestoreIsolatedMenu()
    {
        if (ui != null) { ui.CloseModal(); ui.Match.Menu(); ui.gameObject.SetActive(false); Object.Destroy(ui.gameObject); }
        yield return null;
        account?.Dispose(); account = null;
    }
    [UnityTest] public IEnumerator EmptyMartioPresentationDoesNotAcquireCharacterArt()
    {
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[] { "blotan" },20261102));
        yield return null; yield return null;
        ui.Match.Ready(); yield return null; yield return null;
        Assert.AreEqual(MonsterPouch.Gameplay.Match.MatchPhase.Combat,ui.Match.Phase);
        Assert.IsNotNull(ui.Match.Simulation);
        Assert.IsEmpty(ui.Match.Simulation.MartioVolleys); Assert.IsEmpty(ui.Match.Simulation.MartioContacts);
        var effects = Object.FindFirstObjectByType<DocumentedBattleEffects>(); Assert.IsNotNull(effects);
        Assert.IsTrue(effects.isActiveAndEnabled);
        typeof(DocumentedBattleEffects).GetMethod("DrawMartioFlights",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effects,null);
        var field = typeof(DocumentedBattleEffects).GetField("martioArt",BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNull(field.GetValue(effects),"A renderer with no Martio effect must not acquire the character's art.");
        yield return null; yield return null;
        Assert.IsNull(field.GetValue(effects));
        ui.Match.Menu(); yield return null;
        Assert.IsNull(field.GetValue(effects));
    }
}
