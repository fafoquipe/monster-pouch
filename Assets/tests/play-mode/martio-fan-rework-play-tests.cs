using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class MartioFanReworkPlayTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly PropertyInfo[] Overrides = new[] { "GuestProfileDirectoryOverride", "ProgressionDirectoryOverride", "DeckDirectoryOverride" }
        .Select(n => typeof(LocalGameUI).GetProperty(n, BindingFlags.Static | BindingFlags.NonPublic)).ToArray();
    readonly string[] keys = { "monster-pouch.language", "monster-pouch.progress.v1", "monster-pouch.decks.v1", "monster-pouch.monster", "monster-pouch.whelps" };
    string[] oldPaths, saved; bool[] existed; bool spanish;
    LocalGameUI ui; InteractionCaptureGameView view;
    [UnitySetUp] public IEnumerator Setup()
    {
        string root = Path.GetFullPath("Logs/local-validation/martio-fan-rework/isolated-profile-" + Guid.NewGuid().ToString("N"));
        oldPaths = Overrides.Select(p => (string)p.GetValue(null)).ToArray();
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, Path.Combine(root, "store-" + i));
        saved = keys.Select(PlayerPrefs.GetString).ToArray(); existed = keys.Select(PlayerPrefs.HasKey).ToArray(); spanish = GameText.Spanish;
        foreach (string key in keys) PlayerPrefs.DeleteKey(key); GameText.SetLanguage(true);
        new LocalGuestProfileStore(Path.Combine(root, "store-0")).OpenOrCreate(true);
        view = new InteractionCaptureGameView(); yield return view.Open();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity"); yield return null; yield return null;
        ui = Object.FindFirstObjectByType<LocalGameUI>(); Assert.NotNull(ui); ui.Match.enabled = false;
        float until = Time.realtimeSinceStartup + 15;
        while (LoadingArtworkScreen.IsBlocking && Time.realtimeSinceStartup < until) yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking); ui.Match.Menu(); ui.CloseModal(); ui.RenderPage(); yield return null;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (ui != null) { ui.Match.Pause(false); ui.Match.Menu(); ui.gameObject.SetActive(false); Object.Destroy(ui.gameObject); }
        yield return null;
        for (int i = 0; i < Overrides.Length; i++) Overrides[i].SetValue(null, oldPaths[i]);
        GameText.SetLanguage(spanish);
        for (int i = 0; i < keys.Length; i++) { if (existed[i]) PlayerPrefs.SetString(keys[i], saved[i]); else PlayerPrefs.DeleteKey(keys[i]); }
        PlayerPrefs.Save(); view.RestoreSelection(); yield return view.WaitForRestoration();
    }
    object Actor(BattleUnit unit) => ((IEnumerable)typeof(CombatSimulation).GetField("actors", Private).GetValue(ui.Match.Simulation)).Cast<object>()
        .Single(a => a.GetType().GetField("Unit").GetValue(a) == (object)unit);


    [UnityTest] public IEnumerator NineProjectilesTerminalBurstsPauseAndReset()
    {
        var match=ui.Match;
        Assert.IsTrue(match.SetLocalAbilityAccess(AbilityAccessSnapshot.Unrestricted));
        Assert.IsTrue(match.StartMatch("bugaloo",new[]{"martio"},20261028));
        int slot=Enumerable.Range(0,match.Player.Offers.Count).First(i=>match.Player.Offers[i]?.UnitId=="martio");
        Assert.IsTrue(match.BuyAndPlace(slot,match.Player.Offers[slot].Token,match.Board.GetCell(2,8)));
        match.Player.Owned["martio"].Tricks[1]=true;
        match.Player.Owned["martio"].Tricks[2]=true;
        ui.RenderPage();yield return null;yield return null;
        match.Ready();ui.RenderPage();yield return null;yield return null;
        var martio=match.Actors.Values.Single(a=>a.BaseStats.DefinitionId=="martio"&&a.Side==match.Player.Side);
        var enemy=match.Actors.Values.First(a=>a.Side!=martio.Side);
        var units=match.Actors.Values.ToArray();foreach(var unit in units)match.Board.ReleaseUnit(unit);
        int parking=0;
        foreach(var unit in units) {
            var cell=unit==martio?match.Board.GetCell(2,8):unit==enemy?match.Board.GetCell(2,5):match.Board.GetCell(parking++%6,0);
            Assert.IsTrue(match.Board.TryOccupyCell(unit,cell));unit.transform.position=match.Mapper.GetWorldPosition(cell);
        }
        foreach(var actor in ((IEnumerable)typeof(CombatSimulation).GetField("actors",Private).GetValue(match.Simulation))) {
            actor.GetType().GetField("NextMove").SetValue(actor,99999);actor.GetType().GetField("NextAttack").SetValue(actor,99999);
        }
        match.Advance(match.Config.OpeningAbilityWindowSeconds);
        typeof(CombatSimulation).GetMethod("QueueBasicAttack",Private).Invoke(match.Simulation,new object[]{Actor(martio),enemy});
        match.Advance(CombatSimulation.GetAttackWindup(martio));
        Assert.AreEqual(3,match.Simulation.MartioVolleys.Count);
        var sides=match.Simulation.MartioVolleys.Where(v=>v.FanSide!=0).ToArray();
        var central=match.Simulation.MartioVolleys.Single(v=>v.FanSide==0);
        while(central.Flight.Progress<1.4)match.Advance(CombatSimulation.TickDuration);
        yield return null;yield return null;
        var effects=Object.FindFirstObjectByType<DocumentedBattleEffects>();Assert.NotNull(effects);
        match.Pause(true);
        typeof(DocumentedBattleEffects).GetField("martioBetweenTicks",Private).SetValue(effects,CombatSimulation.TickDuration);
        typeof(DocumentedBattleEffects).GetMethod("DrawMartioFlights",Private).Invoke(effects,null);
        var images=ui.CanvasRoot.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("Martio projectile ")&&i.enabled).ToArray();
        Assert.AreEqual(9,images.Length);Assert.IsTrue(images.All(i=>i.sprite!=null&&!i.raycastTarget));
        foreach(string name in new[]{"Martio projectile 0","Martio projectile 1","Martio projectile 2"}) {
            var widths=images.Where(i=>i.name==name).Select(i=>i.rectTransform.sizeDelta.x).ToArray();
            Assert.AreEqual(3,widths.Length);Assert.IsTrue(widths.All(w=>Mathf.Abs(w-widths[0])<.01f));
        }
        Capture("nine-projectiles");
        double progress=central.Flight.Progress;var positions=images.Select(i=>i.rectTransform.anchoredPosition).ToArray();
        yield return new WaitForSecondsRealtime(.08f);
        Assert.AreEqual(progress,central.Flight.Progress);CollectionAssert.AreEqual(positions,images.Select(i=>i.rectTransform.anchoredPosition).ToArray());
        match.Pause(false);
        int guard=0;while(sides.Any(v=>!v.Flight.Complete)&&guard++<100)match.Advance(CombatSimulation.TickDuration);
        Assert.IsTrue(sides.All(v=>v.Flight.Complete));
        var terminal=match.Simulation.MartioContacts.Where(c=>sides.Any(v=>v.Id==c.VolleyId)&&c.StopsProjectile).ToArray();
        Assert.AreEqual(6,terminal.Length);Assert.IsTrue(terminal.All(c=>!c.Explodes));
        yield return null;yield return null;match.Pause(true);
        typeof(DocumentedBattleEffects).GetField("martioBetweenTicks",Private).SetValue(effects,0f);
        typeof(DocumentedBattleEffects).GetMethod("DrawMartioFlights",Private).Invoke(effects,null);
        foreach(var contact in terminal) {
            var root=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Martio impact "+contact.Id);
            Assert.NotNull(root.GetComponent<RectMask2D>());
            var impact=root.GetComponentsInChildren<Image>().Single(i=>i.name=="Martio direct impact");
            Assert.IsTrue(impact.enabled&&impact.sprite!=null);
            Assert.IsFalse(root.GetComponentsInChildren<Image>().Single(i=>i.name=="Martio shockwave").enabled);
        }
        Capture("side-terminal-bursts");
        var sim=match.Simulation;match.Pause(false);match.Menu();yield return null;yield return null;
        Assert.IsEmpty(sim.MartioVolleys);Assert.IsEmpty(sim.MartioContacts);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Image>(true).Any(i=>i.name.StartsWith("Martio projectile ")||i.name=="Martio direct impact"));
    }
    void Capture(string name)
    {
        var camera = ui.GameCamera; var canvas = ui.CanvasRoot.GetComponentInParent<Canvas>();
        var mode = canvas.renderMode; var oldCamera = canvas.worldCamera; var order = canvas.sortingOrder; var distance = canvas.planeDistance;
        var oldTarget = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(Screen.width, Screen.height, 24); var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
        try {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingOrder = short.MaxValue;
            camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); image.Apply();
            string folder = Path.GetFullPath("Logs/local-validation/martio-fan-rework"); Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        }
        finally {
            camera.targetTexture = oldTarget; RenderTexture.active = active; canvas.renderMode = mode; canvas.worldCamera = oldCamera;
            canvas.planeDistance = distance; canvas.sortingOrder = order; Object.Destroy(image); Object.Destroy(target);
        }
    }
}
