using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class MapMiniatureResourcePlayTests
{
    [UnityTest] public IEnumerator AllNineDestinationsUseTheirSuppliedMiniatureAndPreserveAspect()
    {
        var host=new GameObject("Miniature transition fixture");
        var transition=MapIrisTransition.Create(host.transform);
        try
        {
            foreach(var id in new[]{"classic","lounge","royal","naturalis","aquarion","geothar","volkanar","casino-night","aireola"})
            {
                var style=CardTableTheme.FromMapId(id);
                var art=CardTableTheme.NationArtwork(style);var sprite=CardTableTheme.MiniWorld(style);
                Assert.IsNotNull(sprite,id);Assert.AreSame(art.Miniature,sprite);
                Assert.AreEqual(id+"-miniature",sprite.name);
                Assert.AreNotSame(art.CombatPanorama.texture,sprite.texture);
                transition.Play(style,()=>{});
                var preview=transition.GetComponentsInChildren<Image>(true).Single(x=>x.name=="Destination mini-world");
                Assert.AreSame(sprite,preview.sprite,id);Assert.IsTrue(preview.preserveAspect);
                Assert.AreEqual(preview.rectTransform.localScale.x,preview.rectTransform.localScale.y);
                transition.Cancel();
            }
        }
        finally {Object.Destroy(host);}
        yield return null;
    }
}

public sealed class MapMiniatureGalleryPlayTests
{
    EronyaMapTestAccount account;
    LocalGameUI ui;
    static readonly TableStyle[] Styles={(TableStyle)0,(TableStyle)1,(TableStyle)2,(TableStyle)3,(TableStyle)4,(TableStyle)5,(TableStyle)6,(TableStyle)7,(TableStyle)8};

    [UnitySetUp] public IEnumerator Load()
    {
        account=new EronyaMapTestAccount(0);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        float until=Time.realtimeSinceStartup+15;
        while((LoadingArtworkScreen.IsBlocking||ui.MapTransitionActive)&&Time.realtimeSinceStartup<until)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);Assert.AreEqual(0L,ui.AccountTrophies);
    }
    [UnityTearDown] public IEnumerator Restore()
    {
        if(ui!=null)ui.CloseModal();
        account?.Dispose();yield return null;
    }

    [UnityTest] public IEnumerator AvailableThenLockedGalleryUsesRealRequirementsAndRejectsLockedClicks()
    {
        var before=Styles.Select(ui.CanSelectTrophyBoard).ToArray();
        string lobby=PlayerPrefs.GetString(CardTableTheme.LobbyPreferenceKey);
        var initial=CardTableTheme.Current;
        ui.ShowMapSkins();yield return null;
        var content=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Map gallery content");
        var available=content.Find("Map gallery available") as RectTransform;
        var locked=content.Find("Map gallery locked") as RectTransform;
        Assert.IsNotNull(available);Assert.IsNotNull(locked);
        Assert.Greater(available.anchoredPosition.y,locked.anchoredPosition.y);
        Assert.AreEqual(9,content.GetComponentsInChildren<Image>(true).Count(i=>i.name.StartsWith("map-miniature-")));
        foreach(var style in Styles)
        {
            string id=CardTableTheme.MapId(style);bool allowed=ui.CanSelectTrophyBoard(style);
            var card=(allowed?available:locked).Find("Skin "+id);Assert.IsNotNull(card,id);
            var art=card.GetComponentsInChildren<Image>().Single(i=>i.name=="map-miniature-"+id);
            Assert.AreSame(CardTableTheme.MiniWorld(style),art.sprite);Assert.IsTrue(art.preserveAspect);
            Assert.AreEqual(!allowed,art.GetComponent<AbilityAvailabilityVisual>().IsGrayscale);
            if(!allowed)Assert.AreEqual("MonsterPouch/UI/Ability Grayscale",art.material.shader.name);
            Assert.AreEqual(allowed,card.GetComponentsInChildren<Button>().Single(b=>b.name=="use-style-"+style).interactable);
        }
        foreach(var pair in new[]{("aquarion",150L),("geothar",300L),("volkanar",450L),("royal",600L),("aireola",600L)})
        {
            var label=content.GetComponentsInChildren<Text>().Single(t=>t.name=="map-requirement-"+pair.Item1);
            Assert.AreEqual(LobbyAccountSummary.Number(pair.Item2)+(GameText.Spanish?" COPAS":" TROPHIES"),label.text);
        }
        var royal=content.GetComponentsInChildren<Button>().Single(b=>b.name=="use-style-CampoReal");
        Assert.IsFalse(royal.interactable);royal.onClick.Invoke();yield return null;
        Assert.AreEqual(initial,CardTableTheme.Current,"Even invoking a locked callback directly must not equip it.");
        CollectionAssert.AreEqual(before,Styles.Select(ui.CanSelectTrophyBoard).ToArray(),"Rendering does not grant any access, including casinos.");
        Assert.AreEqual(lobby,PlayerPrefs.GetString(CardTableTheme.LobbyPreferenceKey));
        var requirement=typeof(LocalGameUI).GetMethod("MapGalleryRequirement",BindingFlags.NonPublic|BindingFlags.Instance);
        foreach(var casino in new[]{TableStyle.GrandCasaHotel,TableStyle.CasinoNight})
            Assert.AreEqual(GameText.Spanish?"Requisitos por definir":"Requirements to be defined",requirement.Invoke(ui,new object[]{casino,false}));
        var columns=typeof(LocalGameUI).GetMethod("MapGalleryColumns",BindingFlags.NonPublic|BindingFlags.Static);
        Assert.AreEqual(3,columns.Invoke(null,new object[]{508f,393f/540f}));
        Assert.AreEqual(2,columns.Invoke(null,new object[]{508f,320f/540f}));
    }

    [UnityTest] public IEnumerator AvailableSelectionShowsSelectedAndSurvivesSceneReloadWithoutChangingLobby()
    {
        string lobby=PlayerPrefs.GetString(CardTableTheme.LobbyPreferenceKey);
        ui.ShowMapSkins();yield return null;
        var use=ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="use-style-Default");
        Assert.IsTrue(use.interactable);use.onClick.Invoke();
        yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.2f);
        Assert.AreEqual("classic",PlayerPrefs.GetString(CardTableTheme.PreferenceKey));
        var selected=ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="use-style-Default");
        Assert.IsTrue(selected.GetComponentInChildren<Text>().text.Contains(GameText.Spanish?"EN USO":"SELECTED"));
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        Assert.AreEqual(TableStyle.Default,CardTableTheme.Current);
        Assert.AreEqual(lobby,PlayerPrefs.GetString(CardTableTheme.LobbyPreferenceKey));
    }

    [UnityTest] public IEnumerator UnlockedAireolaRemainsVisibleWhenItsSelectionReopensTheGallery()
    {
        account.Dispose();account=new EronyaMapTestAccount();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        float until=Time.realtimeSinceStartup+15;
        while((LoadingArtworkScreen.IsBlocking||ui.MapTransitionActive)&&Time.realtimeSinceStartup<until)yield return null;
        Assert.AreEqual(600L,ui.AccountTrophies);ui.ShowMapSkins();yield return null;
        var choose=ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="use-style-Aireola");
        Assert.IsTrue(choose.interactable);choose.onClick.Invoke();
        yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.2f);
        Canvas.ForceUpdateCanvases();
        var scroll=ui.CanvasRoot.GetComponentsInChildren<ScrollRect>().Single(s=>s.name=="Map gallery viewport");
        var card=scroll.content.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Skin aireola");
        var corners=new Vector3[4];card.GetWorldCorners(corners);
        foreach(var corner in corners)
        {
            var local=scroll.viewport.InverseTransformPoint(corner);
            Assert.That(local.x,Is.InRange(scroll.viewport.rect.xMin-.1f,scroll.viewport.rect.xMax+.1f));
            Assert.That(local.y,Is.InRange(scroll.viewport.rect.yMin-.1f,scroll.viewport.rect.yMax+.1f));
        }
        Assert.AreEqual(TableStyle.Aireola,CardTableTheme.Current);
        Assert.IsFalse(scroll.content.GetComponentsInChildren<AbilityAvailabilityVisual>().Any(x=>x.IsGrayscale));
    }
}
