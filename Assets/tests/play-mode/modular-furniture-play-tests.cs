using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
public sealed class ModularFurniturePlayTests
{
    [UnityTest] public IEnumerator SwipeKeepsCabinetMaterialsAliveAcrossEveryLobbyStyle()
    {
        string previous=PlayerPrefs.GetString(CardTableTheme.LobbyPreferenceKey,"classic");
        try
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
            var ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
            foreach(var style in new[]{TableStyle.Default,TableStyle.CampoReal,TableStyle.GrandCasaHotel})
            {
                CardTableTheme.SelectLobby(style);ui.OpenHome();yield return new WaitForSecondsRealtime(.4f);
                var delta=new Vector2(Screen.width*.65f,0);
                Assert.IsTrue(ui.BeginMenuSwipe(delta));
                var preview=ui.CanvasRoot.Find("Menu swipe preview");Assert.IsNotNull(preview);
                var art=CabinetArt(preview);var materials=art.Select(g=>g.material).Distinct().ToArray();
                Assert.GreaterOrEqual(art.Length,17,"Shelves, posts, podiums and lining must all be present.");
                Assert.AreEqual(2,materials.Length);
                ui.EndMenuSwipe(delta);yield return new WaitForSecondsRealtime(.45f);yield return null;
                Assert.IsTrue(preview==null,"The temporary preview should be gone after promotion.");
                AssertCabinetAlive(art,materials);
            }
        }
        finally{PlayerPrefs.SetString(CardTableTheme.LobbyPreferenceKey,previous);PlayerPrefs.Save();}
    }
    [UnityTest] public IEnumerator CancelledAndInterruptedSwipesPreserveOnlyTheVisibleCabinetMaterials()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        var ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;ui.OpenHome();yield return new WaitForSecondsRealtime(.4f);
        var shortDrag=new Vector2(Screen.width*.25f,0);
        Assert.IsTrue(ui.BeginMenuSwipe(shortDrag));
        var discarded=CabinetArt(ui.CanvasRoot.Find("Menu swipe preview")).Select(g=>g.material).Distinct().ToArray();
        ui.EndMenuSwipe(shortDrag);yield return new WaitForSecondsRealtime(.45f);yield return null;
        Assert.IsTrue(discarded.All(m=>m==null),"A cancelled preview must release its own materials.");
        var complete=new Vector2(Screen.width*.65f,0);
        Assert.IsTrue(ui.BeginMenuSwipe(complete));
        var art=CabinetArt(ui.CanvasRoot.Find("Menu swipe preview"));var materials=art.Select(g=>g.material).Distinct().ToArray();
        Assert.GreaterOrEqual(art.Length,17);
        ui.EndMenuSwipe(complete);
        // Starting another gesture immediately promotes the Brief before its animation finishes.
        Assert.IsTrue(ui.BeginMenuSwipe(-shortDrag));ui.CancelMenuSwipe();
        yield return null;yield return null;
        AssertCabinetAlive(art,materials);
        ui.SwipeMenu(-complete);yield return new WaitForSecondsRealtime(.45f);yield return null;
        Assert.IsTrue(materials.All(m=>m==null),"Leaving the Brief must release the removed cabinet's materials.");
    }
    static Graphic[] CabinetArt(Transform root)=>root.GetComponentsInChildren<Graphic>().Where(g=>g.material!=null&&
        (g.material.name=="Brief furniture accents"||g.material.name=="Brief velvet lining")).ToArray();
    static void AssertCabinetAlive(Graphic[] art,Material[] materials)
    {
        foreach(var material in materials)Assert.IsTrue(material!=null,"Promoting a preview must preserve the cabinet's rendering materials.");
        foreach(var graphic in art)
        {
            Assert.IsTrue(graphic!=null&&graphic.isActiveAndEnabled, "Every cabinet part must stay active.");
            Assert.Contains(graphic.material,materials,"Furniture must keep its original palette after the preview is destroyed.");
        }
    }
    [UnityTest] public IEnumerator ShelvesHaveIndependentPartsAndStyleButtonsStayInsideTheirCards()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        var ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;ui.OpenDecks();yield return new WaitForSecondsRealtime(.5f);
        var images=ui.CanvasRoot.GetComponentsInChildren<Image>();
        Assert.AreEqual(7,images.Count(i=>i.name.StartsWith("Whelp wooden pedestal ")));
        Assert.AreEqual(1,images.Count(i=>i.name=="Monster wooden pedestal"));
        var pieces=images.Where(i=>i.name.StartsWith("Cabinet ")&&(i.name.Contains("shelf")||i.name.Contains("post")||i.name.Contains("cornice"))).ToArray();
        Assert.AreEqual(6,pieces.Length);foreach(var part in pieces){Assert.AreEqual(part.name.Contains("post")?Image.Type.Tiled:Image.Type.Sliced,part.type);Assert.AreNotEqual(Vector4.zero,part.sprite.border);Assert.IsFalse(part.raycastTarget);}
        ui.ShowTableSkins();yield return null;
        var buttons=ui.CanvasRoot.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("use-style-")).ToArray();Assert.AreEqual(3,buttons.Length);
        foreach(var button in buttons)
        {
            var rect=(RectTransform)button.transform;var card=(RectTransform)rect.parent;
            Assert.GreaterOrEqual(rect.anchoredPosition.x,12);Assert.LessOrEqual(rect.anchoredPosition.x+rect.rect.width,card.rect.width-12);
            Assert.LessOrEqual(-rect.anchoredPosition.y+rect.rect.height,card.rect.height-12,"Button and its press shadow need an inner bottom margin.");
        }
    }
}
