using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class CharacterPreviewPlayTests
{
    LocalGameUI ui;
    LobbyAnimationDirector director;
    EronyaMapTestAccount account;
    string[] saved;
    bool[] existed;
    readonly string[] keys={"monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    DeckLibrary Decks => (DeckLibrary)typeof(LocalGameUI).GetField("decks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);

    [UnitySetUp] public IEnumerator LoadRealMenu()
    {
        RoadmapTestProgress.Step("Character preview: loading scene");
        // Reuse the isolated legacy account used by menu tests. A fresh human
        // account correctly shows onboarding instead of a populated Home/Brief.
        account=new EronyaMapTestAccount(0);
        // Exercise the current static presentation; browsing must not revive old gestures.
        LobbyAnimationDirector.PlaybackEnabled=false;
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();
        var loading=SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        RoadmapTestProgress.Step("Character preview: scene load requested");
        yield return loading;
        RoadmapTestProgress.Step("Character preview: scene loaded");
        yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);
        ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+15;
        while((LoadingArtworkScreen.IsBlocking||ui.MapTransitionActive)&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking,"Finish loading before testing menu interaction.");
        Assert.IsFalse(ui.MapTransitionActive,"Finish the actual menu transition before testing portraits.");
        Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);
        ui.OpenDecks();yield return new WaitForSecondsRealtime(.3f);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Starter selection"),
            "The isolated legacy account must reach the real Brief, not onboarding.");
        Assert.AreEqual("decks",ui.MenuPage);
        RoadmapTestProgress.Step("Character preview: Brief opened");
        director=ui.LobbyDirector;director.enabled=false;
        yield return null;
    }

    [UnityTearDown] public IEnumerator RestoreSavedChoices()
    {
        LobbyAnimationDirector.PlaybackEnabled=false;
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;}
        if(director!=null)director.enabled=true;
        for(int i=0;i<keys.Length;i++)
        {
            if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);
        }
        PlayerPrefs.Save();
        account?.Dispose();account=null;yield return null;
    }

    [UnityTest] public IEnumerator WhelpIsOnlyAssignedAfterUseAndDetailsPreserveTheOriginalSlot()
    {
        int slot=2;string id=UnusedWhelp();string before=Decks.Serialize();
        string[] original=(string[])Decks.Current.Whelps.Clone();
        ui.OpenCharacterPicker(slot);yield return null;
        AssertStaticGrid(false);Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("info-")));
        yield return Tap("loadout-"+id);
        AssertUnchanged(before);AssertPreview(id);AssertMobileTargets(PanelNamed("Character actions"));
        yield return Tap("character-preview-details");
        Assert.IsNotNull(PanelNamed("Character details"));AssertUnchanged(before);
        AssertMobileTargets(PanelNamed("Character details"));
        yield return Tap("character-details-back");
        AssertPreview(id);AssertUnchanged(before);
        yield return Tap("character-preview-use");
        Assert.AreEqual(id,Decks.Current.Whelps[slot]);
        for(int i=0;i<original.Length;i++)if(i!=slot)Assert.AreEqual(original[i],Decks.Current.Whelps[i],"Another Brief slot was silently changed.");
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Character actions"));
        string directory=(string)typeof(LocalGameUI).GetProperty("DeckDirectoryOverride",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        Assert.AreEqual(id,new LocalDeckStore(directory).LoadOrImport("",ui.Match.Config,Decks.Current.Monster,original).Current.Whelps[slot]);
    }

    [UnityTest] public IEnumerator SelectingAnotherCardMovesOnlyTheInlineActionsAndKeepsTheGrid()
    {
        var heroes=ui.Match.Config.Units.Where(d=>d.IsMonster).ToArray();
        string before=Decks.Serialize();ui.OpenCharacterPicker(-1);yield return null;
        yield return Tap("loadout-"+heroes[0].Id);AssertPreview(heroes[0].Id);
        yield return Tap("loadout-"+heroes[1].Id);AssertPreview(heroes[1].Id);
        Assert.AreEqual(1,ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Count(r=>r.name=="Character actions"));
        AssertStaticGrid(true);AssertUnchanged(before);
    }

    [UnityTest] public IEnumerator DisabledPresentationKeepsLobbyBriefAndEveryPreviewIdleAfterTimeAndTaps()
    {
        Assert.IsFalse(LobbyAnimationDirector.PlaybackEnabled);
        string before=Decks.Serialize();
        RoadmapTestProgress.Step("Static portraits: opening Home");
        ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
        RoadmapTestProgress.Step("Static portraits: checking Home");
        yield return AssertStaticPortraits(ui.CanvasRoot,false,expectedId:Decks.Current.Monster);
        RoadmapTestProgress.Step("Static portraits: opening Brief");
        ui.OpenDecks();yield return new WaitForSecondsRealtime(.3f);
        yield return AssertStaticPortraits(ui.CanvasRoot,false,true,Decks.Current.Monster+","+string.Join(",",Decks.Current.Whelps));
        foreach(var definition in ui.Match.Config.Units)
        {
            RoadmapTestProgress.Step("Static portraits: "+definition.Id);
            ui.OpenCharacterPicker(definition.IsMonster?-1:0);yield return null;
            yield return Tap("loadout-"+definition.Id);AssertPreview(definition.Id);
            AssertStaticGrid(definition.IsMonster);
            yield return Tap("character-preview-details");
            Assert.IsFalse(PanelNamed("Character details").GetComponentsInChildren<RectTransform>().Any(r=>r.name=="art-"),definition.Id+": empty serialized abilities must not request artwork.");
            yield return AssertStaticPortraits(PanelNamed("Character details"),true,expectedId:definition.Id);
            yield return Tap("character-details-back");
            AssertStaticGrid(definition.IsMonster);AssertUnchanged(before);
        }
        Assert.AreEqual(0,director.ActiveCount);
    }

    [UnityTest] public IEnumerator PublicDetailsStayIdleEvenWhenLobbyGesturesAreEnabled()
    {
        LobbyAnimationDirector.PlaybackEnabled=true;
        string id=UnusedWhelp();string before=Decks.Serialize();
        ui.OpenCharacterPicker(0);yield return null;
        yield return Tap("loadout-"+id);yield return Tap("character-preview-details");
        var stage=PanelNamed("Figure stage");
        Assert.IsEmpty(stage.GetComponentsInChildren<LobbyPersonality>());
        Assert.IsNull(stage.GetComponent<Button>(),"The idle cannot be tapped to trigger a gesture.");
        var idle=stage.GetComponentInChildren<LobbyFrameGraphic>();Assert.IsNotNull(idle);
        var sprite=idle.CurrentSprite;
        director.Advance(120);idle.SampleFrame(.5f);yield return null;
        Assert.IsFalse(idle.HasAuthoredFrames);Assert.IsTrue(idle.IsAtRest);Assert.AreSame(sprite,idle.CurrentSprite);
        AssertUnchanged(before);
        LobbyAnimationDirector.PlaybackEnabled=false;
    }

    [UnityTest] public IEnumerator LibraryWhelpRequiresAnExplicitDestinationAndReturnsToItsLibraryTab()
    {
        string id=UnusedWhelp();string before=Decks.Serialize();
        string[] original=(string[])Decks.Current.Whelps.Clone();
        ui.OpenCollection();yield return null;yield return Tap("WHELPS");AssertStaticGrid(false);
        yield return Tap("loadout-"+id);AssertUnchanged(before);
        yield return Tap("character-preview-use");
        Assert.IsNotNull(PanelNamed("Character destination"));AssertUnchanged(before);
        AssertMobileTargets(PanelNamed("Character destination"));
        Assert.IsEmpty(PanelNamed("Character destination").GetComponentsInChildren<LobbyPersonality>());
        yield return Tap("character-slot-4");AssertPreview(id);AssertUnchanged(before);
        yield return Tap("character-preview-details");
        yield return Tap("character-details-back");AssertPreview(id);AssertUnchanged(before);
        yield return Tap("character-preview-use");
        Assert.AreEqual(id,Decks.Current.Whelps[4]);
        for(int i=0;i<original.Length;i++)if(i!=4)Assert.AreEqual(original[i],Decks.Current.Whelps[i]);
        AssertStaticGrid(false);
    }

    [UnityTest] public IEnumerator LibraryHeroUsesTheHeroPositionAndPreservesTheMonsterTab()
    {
        string id=ui.Match.Config.Units.First(d=>d.IsMonster&&d.Id!=Decks.Current.Monster).Id;
        string before=Decks.Serialize();string[] original=(string[])Decks.Current.Whelps.Clone();
        ui.OpenCollection();yield return null;yield return Tap("MONSTERS");AssertStaticGrid(true);
        yield return Tap("loadout-"+id);AssertUnchanged(before);
        yield return Tap("MONSTERS");AssertStaticGrid(true);AssertUnchanged(before);
        yield return Tap("loadout-"+id);yield return Tap("character-preview-use");
        Assert.AreEqual(id,Decks.Current.Monster);CollectionAssert.AreEqual(original,Decks.Current.Whelps);
        AssertStaticGrid(true);
    }

    IEnumerator AssertStaticPortraits(Transform container,bool tapModel,bool brief=false,string expectedId=null)
    {
        RoadmapTestProgress.Step("Static portraits: canvas rebuild "+container.name);
        Canvas.ForceUpdateCanvases();
        var personalities=container.GetComponentsInChildren<LobbyPersonality>();
        if(tapModel||brief)Assert.IsEmpty(personalities,"Detail and Brief must never register lobby gestures.");
        else Assert.IsNotEmpty(personalities,"Exercise the real displayed Home figure.");
        var drawings=container.GetComponentsInChildren<LobbyFrameGraphic>();
        Assert.IsNotEmpty(drawings,"Exercise the real displayed idle artwork: container="+container.name+", page="+ui.MenuPage+", expected="+expectedId);
        var originals=drawings.Select(d=>d.CurrentSprite).ToArray();
        var feet=drawings.Select(d=>d.FootAnchor).ToArray();
        var meshes=drawings.Select(d=>d.canvasRenderer.GetMesh().vertices).ToArray();
        RoadmapTestProgress.Step("Static portraits: meshes captured");
        for(int i=0;i<drawings.Length;i++)
        {
            string id=drawings[i].UnitId;
            Assert.IsNotNull(drawings[i].transform.parent.GetComponentInChildren<LobbyContactShadow>(),id);
            if(drawings[i].UsesCharacterIdle)
                Assert.AreSame(CharacterUiArtwork.GetCharacter(id).Visuals.Idle(MonsterPouch.Gameplay.Presentation.UnitFacing.South),originals[i],id+": preserve the existing full-body idle fallback.");
            else
            {
                Assert.AreEqual("gogos-minis-idle-"+id,originals[i].name,id);
                // Preserve the approved idle for this surface, including Home.
                string idlePath=drawings[i].SourceClip.idleTexture;
                Assert.IsNotEmpty(idlePath,id);
                Assert.AreSame(Resources.Load<Texture2D>(idlePath),originals[i].texture,id+": preserve the configured idle artwork.");
            }
            var personality=personalities.FirstOrDefault(p=>p.UnitId==id);
            if(personality!=null)
            {
                Assert.IsNotNull(personality.GetComponentInChildren<LobbyContactShadow>(),id);
                Assert.IsFalse(personality.PlayGesture(),id+": a manual call bypassed the pause.");
                personality.Advance(120);
            }
            Assert.IsFalse(drawings[i].HasAuthoredFrames,id+": static presentation must not load a gesture atlas.");
            drawings[i].SampleFrame(.5f);
        }
        if(tapModel)
        {
            var stage=container.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Figure stage");
            var screen=RectTransformUtility.WorldToScreenPoint(null,stage.TransformPoint(stage.rect.center));
            var pointer=new PointerEventData(EventSystem.current){position=screen,button=PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.IsNotEmpty(hits,"The visible portrait must be tested through the real UI raycast.");
            var handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.IsNotNull(handler);
            Assert.IsTrue(handler.transform.IsChildOf(container),"A foreground panel covers this portrait.");
            ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler);
        }
        director.Advance(120);yield return null;Canvas.ForceUpdateCanvases();
        Assert.AreEqual(0,director.ActiveCount,"Elapsed time or taps restarted a paused character.");
        for(int i=0;i<drawings.Length;i++)
        {
            string id=drawings[i].UnitId;
            Assert.IsFalse(personalities.Any(p=>p.UnitId==id&&p.IsGesturing),id);
            Assert.AreEqual(-1,drawings[i].FrameIndex,id);Assert.IsTrue(drawings[i].IsAtRest,id);
            Assert.AreSame(originals[i],drawings[i].CurrentSprite,id);
            Assert.AreEqual(feet[i],drawings[i].FootAnchor,id);
            CollectionAssert.AreEqual(meshes[i],drawings[i].canvasRenderer.GetMesh().vertices,id+": static geometry moved.");
        }
    }

    [UnityTest] public IEnumerator MenuContextSizeDoublesRealBodyAndShadowWithoutMovingPedestalFeet()
    {
        string heroId=Decks.Current.Monster;
        int pieceSlot=System.Array.FindIndex(Decks.Current.Whelps,id=>!string.IsNullOrEmpty(id));
        Assert.GreaterOrEqual(pieceSlot,0);
        string pieceId=Decks.Current.Whelps[pieceSlot];
        var hero=CharacterUiArtwork.GetCharacter(heroId).Visuals;
        var piece=CharacterUiArtwork.GetCharacter(pieceId).Visuals;
        float heroLobby=hero.LobbySizePercent,heroBrief=hero.BriefSizePercent,pieceBrief=piece.BriefSizePercent;
        float heroGame=hero.InGameSizePercent,pieceGame=piece.InGameSizePercent;
        try
        {
            for(int scenario=0;scenario<3;scenario++)
            {
                bool home=scenario==0;var art=scenario==2?piece:hero;
                hero.LobbySizePercent=hero.BriefSizePercent=piece.BriefSizePercent=100;
                string panel=home?"Choose featured Monster":scenario==1?"deck-monster":"deck-slot-"+pieceSlot;
                if(home)ui.OpenDecks();else ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
                if(home)ui.OpenHome();else ui.OpenDecks();yield return new WaitForSecondsRealtime(.3f);
                var original=CaptureMenuGeometry(PanelNamed(panel));
                if(home)art.LobbySizePercent=200;else art.BriefSizePercent=200;
                if(home)ui.OpenDecks();else ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
                if(home)ui.OpenHome();else ui.OpenDecks();yield return new WaitForSecondsRealtime(.3f);
                var doubled=CaptureMenuGeometry(PanelNamed(panel));
                Assert.That(doubled.body.x,Is.EqualTo(original.body.x*2).Within(.02f),panel+": actual rendered width");
                Assert.That(doubled.body.y,Is.EqualTo(original.body.y*2).Within(.02f),panel+": actual rendered height");
                Assert.That(doubled.shadow.x,Is.EqualTo(original.shadow.x*2).Within(.02f),panel+": actual shadow width");
                Assert.That(doubled.shadow.y,Is.EqualTo(original.shadow.y*2).Within(.02f),panel+": actual shadow height");
                Assert.That(Vector3.Distance(doubled.feet,original.feet),Is.LessThan(.02f),panel+": pedestal feet moved");
                Assert.AreSame(original.sprite,doubled.sprite,panel+": sizing changed idle artwork");
                Assert.AreEqual(heroGame,hero.InGameSizePercent);Assert.AreEqual(pieceGame,piece.InGameSizePercent);
                if(!home)Assert.IsEmpty(PanelNamed(panel).GetComponentsInChildren<LobbyPersonality>());
            }
        }
        finally
        {
            hero.LobbySizePercent=heroLobby;hero.BriefSizePercent=heroBrief;piece.BriefSizePercent=pieceBrief;
        }
    }
    (Vector2 body,Vector2 shadow,Vector3 feet,Sprite sprite) CaptureMenuGeometry(Transform panel)
    {
        Canvas.ForceUpdateCanvases();
        var body=panel.GetComponentsInChildren<LobbyFrameGraphic>().Single();
        var shadow=panel.GetComponentsInChildren<LobbyContactShadow>().Single();
        var bodyMesh=body.canvasRenderer.GetMesh();var shadowMesh=shadow.canvasRenderer.GetMesh();
        Assert.AreEqual(4,bodyMesh.vertexCount,"Measure the actual complete idle quad.");
        Assert.Greater(shadowMesh.vertexCount,0);
        Assert.IsTrue(body.IsAtRest);Assert.AreEqual(-1,body.FrameIndex);
        Vector3 feet=body.transform.TransformPoint(body.FootAnchor);
        Assert.That(Vector3.Distance(feet,shadow.transform.TransformPoint(shadow.FootAnchor)),Is.LessThan(.02f),"Body and shadow must share their world-space support.");
        return ((Vector2)bodyMesh.bounds.size,(Vector2)shadowMesh.bounds.size,feet,body.CurrentSprite);
    }

    [UnityTest] public IEnumerator FactionsIntersectBothCategoriesWithoutChangingDeckOrSelection()
    {
        ui.OpenCollection();yield return null;string before=Decks.Serialize(),selected=ui.SelectedId;
        foreach(string faction in new[]{""}.Concat(BriefCollectionFilter.FactionIds))
        {
            yield return Tap("Collection faction filter");
            AssertMobileTargets(PanelNamed("Faction choices"));
            yield return Tap("Collection faction "+(faction==""?"all":faction));
            foreach(bool monsters in new[]{true,false})
            {
                yield return Tap(monsters?"MONSTERS":"WHELPS");AssertStaticGrid(monsters);AssertUnchanged(before);
                Assert.AreEqual(selected,ui.SelectedId);AssertMobileTargets(PanelNamed("Collection filters"));
                var availability=(System.Collections.Generic.IEnumerable<string>)typeof(LocalGameUI)
                    .GetProperty(monsters?"AvailableMonsterIdsForDeck":"AvailableWhelpIdsForDeck",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
                foreach(var card in PanelNamed("Collection grid").GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("loadout-")))
                {
                    bool locked=availability!=null&&!availability.Contains(card.name.Substring(8));
                    Assert.AreEqual(locked,card.transform.Find("Locked character shade")!=null,card.name);
                }
            }
        }
    }
    [UnityTest] public IEnumerator FactionAndCategorySurviveDetailsAndReopening()
    {
        ui.OpenCollection();yield return null;string before=Decks.Serialize();
        yield return Tap("WHELPS");yield return Tap("Collection faction filter");yield return Tap("Collection faction naturalis");
        yield return Tap("loadout-rosie");yield return Tap("character-preview-details");yield return Tap("character-details-back");
        AssertPreview("rosie");AssertStaticGrid(false);AssertUnchanged(before);
        ui.CloseModal();ui.OpenCollection();yield return null;
        Assert.AreEqual("Naturalis",ButtonNamed("Collection faction filter").GetComponentInChildren<Text>().text);
        AssertStaticGrid(false);AssertUnchanged(before);
    }
    [UnityTest] public IEnumerator EmptyFactionKeepsTheChosenFilterAndDeck()
    {
        ui.OpenCollection();yield return null;yield return Tap("WHELPS");
        string before=Decks.Serialize(),selected=ui.SelectedId;var original=ui.Match.Config.Units;
        try
        {
            // Limit the fixture catalogue only after opening. Filter callbacks must not
            // normalize/save the deck, reload account data or choose another character.
            ui.Match.Config.Units=original.Where(d=>d.Id=="rosie").ToArray();
            yield return Tap("Collection faction filter");yield return Tap("Collection faction aquarion");
            Assert.IsNotNull(PanelNamed("Collection empty state"));
            Assert.AreEqual("Aquarion",ButtonNamed("Collection faction filter").GetComponentInChildren<Text>().text);
            Assert.AreEqual(0,PanelNamed("Collection viewport").GetComponent<CollectionGridWindow>().ItemCount);
            Assert.AreEqual(selected,ui.SelectedId);AssertUnchanged(before);
        }
        finally {ui.Match.Config.Units=original;}
    }

    string UnusedWhelp()=>ui.Match.Config.Units.First(d=>!d.IsMonster&&!Decks.Current.Whelps.Contains(d.Id)).Id;
    RectTransform PanelNamed(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name==name);
    Button ButtonNamed(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name==name);
    void AssertUnchanged(string before)
    {
        Assert.AreEqual(before,Decks.Serialize(),"Browsing or previewing must not assign a character.");
        for(int i=0;i<keys.Length;i++)
        {
            Assert.AreEqual(existed[i],PlayerPrefs.HasKey(keys[i]),keys[i]);
            Assert.AreEqual(saved[i],PlayerPrefs.GetString(keys[i]),keys[i]);
        }
    }
    void AssertPreview(string id)
    {
        var actions=PanelNamed("Character actions");
        var card=(RectTransform)ButtonNamed("loadout-"+id).transform;
        Assert.AreSame(card.parent,actions.parent,"Actions must stay inside the character grid.");
        Assert.AreEqual(card.anchoredPosition.x,actions.anchoredPosition.x);
        Assert.Less(actions.anchoredPosition.y,card.anchoredPosition.y-110);
        var details=(RectTransform)ButtonNamed("character-preview-details").transform;
        var use=(RectTransform)ButtonNamed("character-preview-use").transform;
        Assert.Greater(details.anchoredPosition.y,use.anchoredPosition.y,"Detalle goes above Usar.");
        Assert.IsEmpty(actions.GetComponentsInChildren<LobbyPersonality>());
    }

    void AssertStaticGrid(bool monsters)
    {
        var grid=PanelNamed("Collection grid");Assert.IsEmpty(grid.GetComponentsInChildren<LobbyPersonality>());
        Assert.IsTrue(grid.GetComponentsInChildren<LobbyFrameGraphic>().All(d=>d.IsAtRest&&d.FrameIndex==-1),
            "Collection thumbnails must remain in their original idle.");
        var ids=grid.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("loadout-")).Select(b=>b.name.Substring(8)).ToArray();
        var window=grid.parent.GetComponent<CollectionGridWindow>();Assert.IsNotNull(window);
        string faction=(string)typeof(LocalGameUI).GetField("collectionFaction",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
        var all=BriefCollectionFilter.Select(ui.Match.Config.Units,monsters,faction).Select(d=>d.Id).ToArray();
        CollectionAssert.AreEqual(all,window.ItemIds,"The filtered model includes every matching character, even offscreen.");
        var scroll=grid.parent.GetComponent<ScrollRect>();
        int columns=Mathf.Max(1,Mathf.FloorToInt((scroll.viewport.rect.width+12)/124));
        var visible=BriefCollectionFilter.VisibleIndices(all.Length,columns,scroll.content.anchoredPosition.y,scroll.viewport.rect.height).Select(i=>all[i]);
        CollectionAssert.AreEquivalent(visible,ids,"Only viewport rows may instantiate full-body cards.");
        Assert.LessOrEqual(window.PooledCellCount,Mathf.Min(all.Length,columns*(Mathf.CeilToInt(scroll.viewport.rect.height/136)+1)));
        var bodies=grid.GetComponentsInChildren<LobbyFrameGraphic>();
        CollectionAssert.AreEquivalent(ids,bodies.Select(d=>d.UnitId),"Every selection card must contain its full body.");
        foreach(var body in bodies) {
            Assert.IsNotNull(body.CurrentSprite,body.UnitId);
            if(body.UsesCharacterIdle)Assert.AreSame(CharacterUiArtwork.GetCharacter(body.UnitId).Visuals.Idle(MonsterPouch.Gameplay.Presentation.UnitFacing.South),body.CurrentSprite);
            else Assert.AreEqual("gogos-minis-idle-"+body.UnitId,body.CurrentSprite.name);
            Assert.AreNotSame(CharacterUiArtwork.Icon(body.UnitId),body.CurrentSprite,"Profile icon leaked into selection.");
            Assert.IsFalse(body.HasAuthoredFrames,"Selection must never load gesture/combat frames.");
        }
        Assert.IsEmpty(grid.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("info-")));
    }
    void AssertMobileTargets(RectTransform panel)
    {
        Canvas.ForceUpdateCanvases();var corners=new Vector3[4];var safe=ui.CanvasRoot.rect;
        foreach(var button in panel.GetComponentsInChildren<Button>().Where(b=>b.interactable))
        {
            var rect=(RectTransform)button.transform;
            // The logical portrait is 540 wide; 68-unit controls remain at least
            // 44 pixels when the safe portrait is displayed on a 360-wide phone.
            Assert.GreaterOrEqual(rect.rect.width*(360f/540f),44,button.name+" has a narrow mobile target.");
            Assert.GreaterOrEqual(rect.rect.height*(360f/540f),44,button.name+" has a short mobile target.");
            rect.GetWorldCorners(corners);
            foreach(var corner in corners)
            {
                Vector3 local=ui.CanvasRoot.InverseTransformPoint(corner);
                Assert.That(local.x,Is.InRange(safe.xMin-.1f,safe.xMax+.1f),button.name);
                Assert.That(local.y,Is.InRange(safe.yMin-.1f,safe.yMax+.1f),button.name);
            }
        }
    }
    IEnumerator ScrollButtonIntoView(Button button)
    {
        var scroll=button.GetComponentInParent<ScrollRect>();
        if(scroll==null)yield break;
        var rect=(RectTransform)button.transform;
        // Drive the shipped ScrollRect through its real EventSystem handler.
        // Do not teleport the content or click a card behind the viewport mask.
        for(int attempt=0;attempt<32;attempt++)
        {
            Canvas.ForceUpdateCanvases();
            Bounds bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,rect);
            Rect visible=scroll.viewport.rect;
            // Viewport edges are visible. Requiring extra padding at the top
            // asks an already visible first-row card to scroll beyond its limit.
            const float epsilon=.01f;
            if(bounds.min.y>=visible.yMin-epsilon&&bounds.max.y<=visible.yMax+epsilon)yield break;
            float direction=bounds.min.y<visible.yMin?-1:1;
            Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(visible.center));
            var pointer=new PointerEventData(EventSystem.current){position=screen,scrollDelta=new Vector2(0,direction*6)};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.IsNotEmpty(hits,button.name+": the real scroll viewport is not reachable.");
            var handler=ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject);
            Assert.AreSame(scroll.gameObject,handler,button.name+": another control blocks scrolling.");
            Vector2 before=scroll.content.anchoredPosition;
            ExecuteEvents.Execute(handler,pointer,ExecuteEvents.scrollHandler);
            yield return null;Canvas.ForceUpdateCanvases();
            Assert.AreNotEqual(before,scroll.content.anchoredPosition,button.name+": wheel input did not reveal the clipped card.");
        }
        Assert.Fail(button.name+": failed to bring the complete touch target inside its viewport after scrolling.");
    }

    IEnumerator RevealVirtualCard(string id)
    {
        var viewport=PanelNamed("Collection viewport");var scroll=viewport.GetComponent<ScrollRect>();
        var window=viewport.GetComponent<CollectionGridWindow>();
        int index=System.Array.IndexOf(window.ItemIds.ToArray(),id);Assert.GreaterOrEqual(index,0,id+": absent from the selected filters.");
        int columns=Mathf.Max(1,Mathf.FloorToInt((viewport.rect.width+12)/124));
        for(int attempt=0;attempt<64;attempt++)
        {
            if(ui.CanvasRoot.GetComponentsInChildren<Button>().Any(b=>b.name=="loadout-"+id))yield break;
            float direction=index/columns*136<scroll.content.anchoredPosition.y?1:-1;
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,viewport.TransformPoint(viewport.rect.center)),scrollDelta=new Vector2(0,direction*6)};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Assert.IsNotEmpty(hits);var handler=ExecuteEvents.GetEventHandler<IScrollHandler>(hits[0].gameObject);
            Assert.AreSame(scroll.gameObject,handler,"Use the actual scroll surface to materialize distant cards.");
            ExecuteEvents.Execute(handler,pointer,ExecuteEvents.scrollHandler);yield return null;Canvas.ForceUpdateCanvases();
        }
        Assert.Fail("Virtualized card never became visible: "+id);
    }

    IEnumerator Tap(string name)
    {
        yield return new WaitForSecondsRealtime(.28f);Canvas.ForceUpdateCanvases();
        if(name.StartsWith("loadout-"))yield return RevealVirtualCard(name.Substring(8));
        var button=ButtonNamed(name);
        Assert.IsTrue(button.interactable,name);
        yield return ScrollButtonIntoView(button);
        var rect=(RectTransform)button.transform;
        Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var pointer=new PointerEventData(EventSystem.current){position=screen,button=PointerEventData.InputButton.Left};
        var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsNotEmpty(hits,name+" is not reachable through the real EventSystem.");
        GameObject handler=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.AreSame(button.gameObject,handler,name+" is covered by another control.");
        ExecuteEvents.Execute(handler,pointer,ExecuteEvents.pointerClickHandler);
        yield return new WaitForSecondsRealtime(.3f);
    }
}
