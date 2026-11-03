using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ViewportLayoutPlayTests
{
    LocalGameUI ui;
    [UnitySetUp] public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTest] public IEnumerator PortraitCompositionAndPixelSamplingStayRegisteredAfterResizing()
    {
        var safeAreas=new[]{
            new Rect(0,0,Screen.width,Screen.height),
            new Rect(Screen.width*.2f,Screen.height*.03f,Screen.width*.6f,Screen.height*.94f),
            new Rect(Screen.width*.05f,Screen.height*.2f,Screen.width*.9f,Screen.height*.6f)};
        Vector2? homeFoot=null,deckFoot=null;
        foreach(var safe in safeAreas)
        {
            Apply(safe);ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
            var hero=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Choose featured Monster");
            if(homeFoot.HasValue)Assert.AreEqual(homeFoot.Value,hero.anchoredPosition);
            homeFoot=hero.anchoredPosition;Assert.AreEqual(270,hero.anchoredPosition.x+hero.rect.width*.5f,.01f);
            var backdrop=ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Square scenery");
            Assert.AreEqual("MonsterPouch/PortraitPixelScenery",backdrop.material.shader.name);
            Assert.IsTrue(backdrop.material.shader.isSupported);Assert.AreEqual(1,backdrop.material.GetFloat("_PixelSize"),"Preserve native pixel art; no additional block filter.");
            var crop=backdrop.material.GetVector("_CropRect");
            var source=backdrop.sprite.rect.size;
            Assert.AreEqual(crop.z*source.x/1440,crop.w*source.y/1440,.00001f,"Artwork must keep its native proportions while cropping.");
            Assert.AreEqual(.5f,crop.x+crop.z*.5f,.00001f);
            Assert.AreEqual(.5f,crop.y+crop.w*.5f,.00001f);
            var p=ui.GameCamera.WorldToScreenPoint(Vector3.zero);
            float dx=(ui.GameCamera.WorldToScreenPoint(Vector3.right)-p).magnitude;
            float dy=(ui.GameCamera.WorldToScreenPoint(Vector3.up)-p).magnitude;
            Assert.AreEqual(dx,dy,.1f);
            ui.OpenDecks();yield return new WaitForSecondsRealtime(.3f);
            var shelf=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="deck-monster");
            if(deckFoot.HasValue)Assert.AreEqual(deckFoot.Value,shelf.anchoredPosition);
            deckFoot=shelf.anchoredPosition;
        }
    }
    [UnityTest] public IEnumerator InsetSafeAreaKeepsControlsSafeAndArtworkInsidePortrait()
    {
        // Asymmetric notch, tall/narrow and wide/short safe bounds exercise both
        // fit axes while keeping header and navigation attached to the applied area.
        var safeAreas=new[]{
            new Rect(Screen.width*.13f,Screen.height*.11f,Screen.width*.72f,Screen.height*.81f),
            new Rect(Screen.width*.24f,Screen.height*.02f,Screen.width*.52f,Screen.height*.96f),
            new Rect(Screen.width*.04f,Screen.height*.21f,Screen.width*.92f,Screen.height*.62f)};
        foreach(var safe in safeAreas)
        {
            Apply(safe);ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
            CheckEdges(safe);
            var resource=ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="Lobby resource Earthkens");
            float scale=ScreenRect(ui.CanvasRoot).height/PortraitViewport.Height;
            Assert.AreEqual(10*scale,safe.yMax-ScreenRect((RectTransform)resource.transform).yMax,1,
                "The resource touch area follows the applied top inset, not a separate Screen.safeArea snapshot.");
            ui.OpenDecks();yield return new WaitForSecondsRealtime(.3f);CheckEdges(safe);
            var names=ui.Match.Config.Units.Select(u=>u.DisplayName.ToUpperInvariant()).ToArray();
            Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>names.Contains(t.text)),"Shelf names must not cover the figures.");
        }
    }
    [UnityTest] public IEnumerator RelayoutKeepsRevealedHeroOnItsCellAndLongPressStillWorks()
    {
        ui.Match.StartMatch("bugaloo",new[]{"stein"},1905);yield return null;
        var view=Object.FindFirstObjectByType<OpponentEchoView>();
        var size=view.Renderer.bounds.size;
        var snapshot=ui.Match.InitialOpponent;
        Vector3 foot=ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(snapshot.Position.x,snapshot.Position.y));
        var safeAreas=new[]{
            new Rect(Screen.width*.04f,Screen.height*.2f,Screen.width*.91f,Screen.height*.64f),
            new Rect(Screen.width*.23f,Screen.height*.02f,Screen.width*.52f,Screen.height*.96f),
            new Rect(0,0,Screen.width,Screen.height)};
        Rect rect=default;
        foreach(var safe in safeAreas)
        {
            Apply(safe);yield return null;
            var scenery=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Single(c=>c.name=="Full screen scenery");
            Assert.IsTrue(scenery.enabled);Assert.AreEqual(new Rect(0,0,1,1),scenery.rect);
            Assert.AreEqual(ui.GameCamera.cullingMask,scenery.cullingMask,"Margins reveal the registered world scene.");
            Assert.Less(Vector2.Distance(scenery.WorldToScreenPoint(foot),ui.GameCamera.WorldToScreenPoint(foot)),1.1f,"Scenery and board must meet without a projection seam.");
            Assert.AreEqual(foot,view.FootPosition,"Resizing cannot move a revealed unit to another board point.");
            Assert.AreEqual(size,view.Renderer.bounds.size,"Hologram geometry must not shrink with the layout.");
            var card=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="echo-"+snapshot.UnitId);
            rect=ScreenRect(card);
            Vector2 min=ui.GameCamera.WorldToScreenPoint(view.Renderer.bounds.min);
            Vector2 max=ui.GameCamera.WorldToScreenPoint(view.Renderer.bounds.max);
            Assert.Less(Vector2.Distance(rect.min,min),1.1f);
            Assert.Less(Vector2.Distance(rect.max,max),1.1f);
        }
        Assert.IsTrue(ui.BeginBoardPointer(rect.center));yield return new WaitForSecondsRealtime(PointerGesture.HoldDuration+.15f);
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text==GameText.Resolve("Habilidades del rival")));
        ui.EndBoardPointer(rect.center);ui.CloseModal();
    }
    void Apply(Rect safe)=>typeof(LocalGameUI).GetMethod("ApplyViewportLayout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{safe});
    static Rect ScreenRect(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
    void CheckEdges(Rect safe)
    {
        var root=ScreenRect(ui.CanvasRoot);Assert.Less(Vector2.Distance(root.center,safe.center),1);
        var background=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Menu background");
        Assert.LessOrEqual(ScreenRect(background).yMin,root.yMin+1);
        Assert.GreaterOrEqual(ScreenRect(background).yMax,root.yMax-1);
        Assert.Less(Vector2.Distance(ui.GameCamera.pixelRect.center,root.center),1);
        var backdrop=ui.CanvasRoot.parent.GetComponentsInChildren<Image>().Single(i=>i.name=="Full screen menu background");
        Assert.IsNull(backdrop.sprite);Assert.AreEqual(PixelScenery.MarginColor,backdrop.color);
        Assert.Less(Vector2.Distance(ScreenRect(backdrop.rectTransform).min,Vector2.zero),1);
        Assert.Less(Vector2.Distance(ScreenRect(backdrop.rectTransform).max,new Vector2(Screen.width,Screen.height)),1);
        Assert.Less(Vector2.Distance(Vector2.zero,ScreenRect(background).min),1);
        Assert.Less(Vector2.Distance(new Vector2(Screen.width,Screen.height),ScreenRect(background).max),1,"Scenery must cover the physical display.");
        foreach(var button in ui.CanvasRoot.GetComponentsInChildren<Button>())
        {
            var bounds=ScreenRect((RectTransform)button.transform);
            Assert.GreaterOrEqual(bounds.xMin,safe.xMin-1,button.name);Assert.LessOrEqual(bounds.xMax,safe.xMax+1,button.name);
            Assert.GreaterOrEqual(bounds.yMin,safe.yMin-1,button.name);Assert.LessOrEqual(bounds.yMax,safe.yMax+1,button.name);
        }
    }
}

