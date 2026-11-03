using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
public sealed class LobbyThemeSelectionPlayTests
{
    [UnityTest] public IEnumerator AllNineCleanScenesAreSharedAcrossMenusAndPedestalsRequireCharacters()
    {
        using(var account=new EronyaMapTestAccount())
        {
            CardTableTheme.Select(TableStyle.GrandCasaHotel);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
            var ui=Object.FindFirstObjectByType<LocalGameUI>();yield return WaitForLobbyReady(ui);
            foreach(string id in LobbyThemeIdentity.Ids)
            {
                LobbyThemeSelection.Select(id);ui.CloseModal();ui.OpenHome();ui.RenderPage();
                yield return new WaitForSecondsRealtime(.4f);
                var background=ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Painted lobby artwork");
                var expectedSize=background.rectTransform.sizeDelta;
                var expectedPosition=background.rectTransform.anchoredPosition;
                foreach(int tab in new[]{2,1,0,4,3})
                {
                    ui.CloseModal();ui.NavigateMenu(tab);yield return new WaitForSecondsRealtime(.4f);
                    AssertSharedLobbyBackground(ui,id);
                    background=ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Painted lobby artwork");
                    Assert.AreEqual(expectedSize,background.rectTransform.sizeDelta,id);
                    Assert.AreEqual(expectedPosition,background.rectTransform.anchoredPosition,id);
                    var images=ui.CanvasRoot.GetComponentsInChildren<Image>();
                    var pick=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().SingleOrDefault(r=>r.name=="Choose featured Monster");
                    var pedestal=images.SingleOrDefault(i=>i.name=="Home character pedestal");
                    bool hasHero=pick!=null&&pick.GetComponentInChildren<LobbyPersonality>()!=null;
                    Assert.AreEqual(hasHero,pedestal!=null,id+" tab "+tab);
                    if(pedestal!=null)
                    {
                        var art=LobbyArtwork.Find(id);Assert.AreSame(art.Pedestal,pedestal.sprite);
                        Assert.IsTrue(pedestal.preserveAspect);Assert.IsFalse(pedestal.raycastTarget);
                        var corners=new Vector3[4];pedestal.rectTransform.GetWorldCorners(corners);
                        var foot=Vector3.Lerp(corners[1],corners[2],art.PedestalFootAnchor.x);
                        foot.y=Mathf.Lerp(corners[1].y,corners[0].y,art.PedestalFootAnchor.y);
                        pick.GetWorldCorners(corners);
                        Assert.Less(Vector2.Distance(foot,(corners[0]+corners[3])*.5f),1f,id);
                    }
                    if(tab==1)
                    {
                        foreach(var slot in ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Where(r=>r.name=="deck-monster"||r.name.StartsWith("deck-slot-")))
                        {
                            string name=slot.name=="deck-monster"?"Monster wooden pedestal":"Whelp wooden pedestal "+slot.name.Substring(10);
                            Assert.AreEqual(slot.GetComponentInChildren<LobbyPersonality>()!=null,images.Any(i=>i.name==name),name);
                        }
                    }
                    Assert.AreEqual(TableStyle.GrandCasaHotel,CardTableTheme.Current);
                }
            }
        }
    }

    [UnityTest] public IEnumerator AllNineLobbyButtonsShowMapMiniaturesDuringTheIrisAndKeepNormalLobbyBackgrounds()
    {
        using(var account=new EronyaMapTestAccount())
        {
            CardTableTheme.Select(TableStyle.GrandCasaHotel);CardTableTheme.SelectLobby(TableStyle.Default);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
            var ui=Object.FindFirstObjectByType<LocalGameUI>();
            yield return WaitForLobbyReady(ui);
            string savedMap=PlayerPrefs.GetString(CardTableTheme.PreferenceKey);
            // Exercise the actual LOBBY buttons, starting with the route in the user's screenshot.
            foreach(var choice in new[]{TableStyle.CasinoNight,TableStyle.Default,TableStyle.GrandCasaHotel,
                TableStyle.CampoReal,TableStyle.Naturalis,TableStyle.Aquarion,TableStyle.Geothar,TableStyle.Volkanar,TableStyle.Aireola})
            {
                string id=CardTableTheme.MapId(choice);
                ui.ShowLobbySkins();yield return null;
                var gallery=ui.CanvasRoot.GetComponentInChildren<LobbySkinScrollView>();
                var scroll=gallery.GetComponent<ScrollRect>();
                int index=LobbyThemeIdentity.Ids.ToList().IndexOf(id);
                scroll.content.anchoredPosition=new Vector2(0,Mathf.Clamp(index*LobbySkinScrollView.RowStride,0,
                    scroll.content.rect.height-scroll.viewport.rect.height));
                scroll.onValueChanged.Invoke(Vector2.zero);
                for(int frame=0;frame<7;frame++)yield return null;
                string buttonName=choice==TableStyle.Aireola?"use-lobby-aireola":"use-style-"+choice;
                var use=ui.CanvasRoot.GetComponentsInChildren<Button>().SingleOrDefault(b=>b.name==buttonName);
                var card=use==null?null:use.transform.parent;
                var thumbnail=card==null?null:card.GetComponentsInChildren<Image>().SingleOrDefault(i=>i.name=="Lobby preview");
                Assert.IsNotNull(thumbnail,id);Assert.IsNotNull(thumbnail.sprite,id);
                Assert.AreEqual("lobby-preview-"+id,thumbnail.sprite.name);
                Assert.IsTrue(thumbnail.preserveAspect);Assert.LessOrEqual(gallery.ResidentPreviewCount,gallery.PooledCellCount);
                Assert.IsNotNull(use,id);Assert.IsTrue(use.IsInteractable(),id);
                var expected=CardTableTheme.MiniWorld(choice);
                var background=LobbyArtwork.Find(id).Background;
                Assert.IsNotNull(expected,id);Assert.IsNotNull(background,id);
                Assert.AreEqual(id+"-miniature",expected.name);
                Assert.AreNotSame(background.texture,expected.texture,"The supplied map and normal lobby are distinct assets.");
                use.onClick.Invoke();
                Assert.IsTrue(ui.MapTransitionActive,"The real lobby callback must start the iris for "+id);
                var transition=ui.GetComponentsInChildren<MapIrisTransition>(true).Single();
                var preview=transition.GetComponentsInChildren<Image>(true).Single(i=>i.name=="Destination mini-world");
                Assert.AreEqual(choice,transition.TargetStyle);
                Assert.AreSame(expected,preview.sprite,"Lobby selection must use its map miniature, not its normal background: "+id);
                Assert.IsTrue(preview.preserveAspect);Assert.IsFalse(preview.raycastTarget);
                Assert.AreEqual(new Vector2(260,260),preview.rectTransform.sizeDelta);
                Assert.AreEqual(Vector2.one*.5f,preview.rectTransform.pivot);
                Assert.AreEqual(Vector2.zero,preview.rectTransform.anchoredPosition);
                float deadline=Time.realtimeSinceStartup+MapIrisTransition.Duration+2;
                while(transition.IsRunning&&!transition.IsCovered&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsTrue(transition.IsCovered,"Observe the actual covered interval for "+id);
                Assert.AreSame(expected,preview.sprite);Assert.That(preview.color.a,Is.EqualTo(1).Within(.001f));
                Assert.AreEqual(preview.rectTransform.localScale.x,preview.rectTransform.localScale.y);
                Assert.AreEqual(0,transition.GetComponentInChildren<IrisCurtain>().Radius);
                while(transition.IsRunning&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsFalse(transition.IsRunning,"The iris must complete normally for "+id);
                Assert.AreEqual(choice,CardTableTheme.Lobby);
                Assert.AreEqual(id,PlayerPrefs.GetString(CardTableTheme.LobbyPreferenceKey));
                Assert.AreEqual(savedMap,PlayerPrefs.GetString(CardTableTheme.PreferenceKey),"Lobby selection must preserve the match map.");
                ui.CloseModal();ui.OpenDecks();yield return new WaitForSecondsRealtime(.4f);
                var normal=ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Painted lobby artwork");
                Assert.AreSame(background,normal.sprite,"The normal brief/lobby must retain its background after the transition.");
                Assert.IsTrue(normal.preserveAspect);
            }
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
            ui=Object.FindFirstObjectByType<LocalGameUI>();yield return WaitForLobbyReady(ui);
            Assert.AreEqual(TableStyle.Aireola,CardTableTheme.Lobby);
            Assert.AreEqual(savedMap,PlayerPrefs.GetString(CardTableTheme.PreferenceKey));
        }
    }

    [UnityTest] public IEnumerator LobbyButtonsChangeBackgroundAndPaletteWithoutChangingTheMatchMap()
    {
        // Use the existing disposable 600-trophy fixture, never the user's account.
        using(var account=new EronyaMapTestAccount())
        {
            CardTableTheme.Select(TableStyle.GrandCasaHotel);CardTableTheme.SelectLobby(TableStyle.Default);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
            var ui=Object.FindFirstObjectByType<LocalGameUI>();
            yield return WaitForLobbyReady(ui);
            foreach(var choice in new[]{TableStyle.CampoReal,TableStyle.GrandCasaHotel,TableStyle.Default})
            {
                ui.ShowLobbySkins();yield return null;
                Assert.IsTrue(ui.CanSelectTrophyBoard(choice),"The isolated account must unlock "+choice+" before selection.");
                var use=ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="use-style-"+choice);
                Assert.IsTrue(use.IsInteractable(),"An unavailable style must not be invoked programmatically.");
                use.onClick.Invoke();
                Assert.IsTrue(ui.MapTransitionActive,"Lobby style should retain the iris transition.");
                yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.25f);
                Assert.AreEqual(choice,CardTableTheme.Lobby);
                Assert.AreEqual(TableStyle.GrandCasaHotel,CardTableTheme.Current);
                ui.CloseModal();ui.OpenDecks();yield return new WaitForSecondsRealtime(.4f);
                var style=EnvironmentStyle.Find(CardTableTheme.MapId(choice));
                AssertSharedLobbyBackground(ui,CardTableTheme.MapId(choice));
                Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<RetroButtonFace>().Any(f=>f.color==style.Button));
                var post=ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Cabinet left post");
                var furniture=Resources.Load<LobbyFurnitureDefinition>("MonsterPouch/Styles/Furniture/"+style.Id);
                Assert.IsNotNull(furniture,"Each lobby theme must provide its cabinet pieces and palette.");
                Assert.AreSame(furniture.Post,post.sprite);
                Assert.AreEqual(Color.white,post.color);
                Assert.AreEqual(furniture.Tint,post.material.GetColor("_Color"));
                Assert.AreEqual(furniture.Saturation,post.material.GetFloat("_Saturation"),.001f);
                Assert.IsNull(post.GetComponent<Shadow>(),"Structural artwork already includes its shadow; an extra UI shadow creates dark seams.");
                ui.OpenHome();yield return new WaitForSecondsRealtime(.4f);
                var painted=LobbyArtwork.Find(CardTableTheme.MapId(choice));
                if(painted!=null&&painted.Background!=null)
                {
                    Assert.AreSame(painted.Background,ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Painted lobby artwork").sprite);
                    Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Image>().Any(i=>i.name=="Home wooden pedestal"||i.name=="Figure platform"));
                }
                else
                {
                    Assert.AreSame(CardTableTheme.NationArtwork(choice)?.LobbyBackground??style.LobbyBackground,ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Square scenery").sprite);
                    var pedestal=ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Home wooden pedestal");
                    Assert.AreSame(furniture.Pedestal,pedestal.sprite);
                    Assert.AreEqual(furniture.Tint,pedestal.material.GetColor("_Color"));
                }
            }
            // The same selection survives every main menu route and modal overlay.
            foreach(int tab in new[]{0,1,2,3,4})
            {
                ui.NavigateMenu(tab);yield return new WaitForSecondsRealtime(.4f);
                AssertSharedLobbyBackground(ui,"classic");
                Assert.AreEqual(TableStyle.GrandCasaHotel,CardTableTheme.Current);
            }
            ui.CloseModal();ui.OpenHome();yield return new WaitForSecondsRealtime(.4f);
            ui.SelectLobbyStyle(TableStyle.CampoReal);yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.25f);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
            Assert.AreEqual(TableStyle.CampoReal,CardTableTheme.Lobby);Assert.AreEqual(TableStyle.GrandCasaHotel,CardTableTheme.Current);
            ui=Object.FindFirstObjectByType<LocalGameUI>();
            yield return WaitForLobbyReady(ui);
            var restored=LobbyArtwork.Find("royal");
            var expected=restored?.Background??CardTableTheme.NationArtwork(TableStyle.CampoReal)?.LobbyBackground??EnvironmentStyle.Find("royal").LobbyBackground;
            Assert.AreSame(expected,ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name==(restored?.Background!=null?"Painted lobby artwork":"Square scenery")).sprite);
        }
    }

    static void AssertSharedLobbyBackground(LocalGameUI ui,string id)
    {
        var image=ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Painted lobby artwork");
        Assert.AreSame(LobbyArtwork.Find(id).Background,image.sprite);
        Assert.IsTrue(image.preserveAspect);
        Assert.IsFalse(image.raycastTarget);
        Assert.AreEqual(image.sprite.rect.width/image.sprite.rect.height,image.rectTransform.rect.width/image.rectTransform.rect.height,.00001f);
        Assert.AreEqual(Color.black,image.transform.parent.GetComponent<Image>().color);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Image>().Any(i=>i.name=="Square scenery"));
    }

    static IEnumerator WaitForLobbyReady(LocalGameUI ui)
    {
        Assert.IsNotNull(ui);
        ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+15;
        while((LoadingArtworkScreen.IsBlocking||ui.MapTransitionActive)&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking,"Finish the shipped loading screen before selecting a lobby.");
        Assert.IsFalse(ui.MapTransitionActive,"The previous transition must be finished before selecting a lobby.");
        Assert.AreEqual(MonsterPouch.Gameplay.Match.MatchPhase.Menu,ui.Match.Phase);
        Assert.AreEqual(600L,ui.AccountTrophies,"Load the isolated map account before exercising Royal access.");
    }
}
