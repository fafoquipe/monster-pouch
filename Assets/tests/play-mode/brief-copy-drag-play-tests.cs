using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class BriefCopyDragPlayTests
{
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    readonly string[] keys={"monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps","monster-pouch.language"};
    string directory;string[] oldDirectories,saved;bool[] existed;bool spanish;float timeScale;int frameRate;
    LocalGameUI ui;InteractionCaptureGameView view;

    [UnitySetUp] public IEnumerator Setup()
    {
        directory=Path.Combine(Path.GetTempPath(),"MonsterPouchBriefCopyDragTests",Guid.NewGuid().ToString("N"));
        oldDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,Path.Combine(directory,"store-"+i));
        new LocalGuestProfileStore(Path.Combine(directory,"store-0")).OpenOrCreate(true);
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();spanish=GameText.Spanish;
        timeScale=Time.timeScale;frameRate=Application.targetFrameRate;Time.timeScale=1;
        view=new InteractionCaptureGameView();yield return view.Open();
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;ui.CloseModal();
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"atori"},741));ui.Match.Player.AddCombatCoins(100);
        Assert.IsTrue(ui.Match.Place("bugaloo",ui.Match.Board.GetCell(4,7)));ui.RenderPage();
        yield return Ready();
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        try
        {
            if(ui!=null){ui.CloseModal();ui.Match.Pause(false);ui.Match.Menu();}
            Time.timeScale=timeScale;Application.targetFrameRate=frameRate;
            for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,oldDirectories[i]);
            GameText.SetLanguage(spanish);
            for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
            PlayerPrefs.Save();
        }
        finally {view?.RestoreSelection();}
        yield return null;if(view!=null)yield return view.WaitForRestoration();
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchBriefCopyDragTests"))+Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(directory);Assert.IsTrue(target.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(target))Directory.Delete(target,true);
    }
    IEnumerator Ready()
    {
        float until=Time.realtimeSinceStartup+LoadingArtworkScreen.MinimumDisplaySeconds+LoadingArtworkScreen.FadeSeconds+3;
        while((!ui.BriefReady||ui.MapTransitionActive||LoadingArtworkScreen.IsBlocking)&&Time.realtimeSinceStartup<until)yield return null;
        Assert.IsTrue(ui.BriefReady);Assert.IsFalse(ui.MapTransitionActive);Assert.IsFalse(LoadingArtworkScreen.IsBlocking);
        yield return null;yield return null;Canvas.ForceUpdateCanvases();
    }
    RectTransform Named(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name==name);
    static Vector2 Center(RectTransform rect)=>RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
    Vector2 CellPoint(int x,int y)=>ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(x,y)));
    void Drag(int slot,Vector2 destination)
    {
        var card=Named("offer-"+slot);var gesture=card.GetComponent<BriefOfferGesture>();Assert.IsNotNull(gesture);
        Assert.AreEqual(ui.Match.Player.Offers[slot].UnitId,gesture.UnitId);
        int coins=ui.Match.Player.Coins;var offer=ui.Match.Player.Offers[slot];
        var pointer=new PointerEventData(EventSystem.current){pointerId=41,button=PointerEventData.InputButton.Left,position=Center(card)};
        gesture.OnPointerDown(pointer);pointer.position+=new Vector2(50,0);gesture.OnDrag(pointer);
        pointer.position=destination;gesture.OnDrag(pointer);
        Assert.AreEqual(coins,ui.Match.Player.Coins,"Hovering the copy does not buy it.");Assert.AreSame(offer,ui.Match.Player.Offers[slot]);
        gesture.OnPointerUp(pointer);
    }
    static string State(PouchState pouch)=>string.Join("|",pouch.Coins,pouch.OfferVersion,pouch.PoolCount,
        string.Join(";",pouch.Offers.Select(o=>o==null?"null":o.UnitId+":"+o.Token)),
        string.Join(";",pouch.Owned.Values.Select(u=>u.Definition.Id+":"+u.Copies+":"+u.Paid+":"+u.Location+":"+u.Deployment+":"+u.BriefSlot+":"+u.NextTrick+":"+string.Join(",",u.Tricks))));

    [UnityTest] public IEnumerator DraggingCopyOntoAnotherCharacterBuysItsSelectedUpgradeWithoutMovingEitherUnit()
    {
        Drag(0,CellPoint(1,7));yield return Ready();
        var own=ui.Match.Player.Owned["atori"];var actor=ui.Match.Actors[own];var ownCell=actor.CurrentCell;
        Assert.IsTrue(ui.Match.ChooseTrick("atori",2));yield return Ready();
        var occupant=ui.Match.Player.Monster;var otherActor=ui.Match.Actors[occupant];var otherCell=otherActor.CurrentCell;
        var otherTricks=occupant.Tricks.ToArray();int otherCopies=occupant.Copies,otherPaid=occupant.Paid;
        int balance=ui.Match.Player.Coins,price=ui.Match.Player.GetOfferCost(1),paid=own.Paid,actorCount=ui.Match.Actors.Count;
        var copy=ui.Match.Player.Offers[1];int rejected=0;ui.Match.ActionRejected+=_=>rejected++;
        Drag(1,CellPoint(otherCell.X,otherCell.Y));yield return Ready();
        Assert.AreEqual(0,rejected,"An occupied drop cell must not reject an already-owned copy.");
        Assert.AreEqual(2,own.Copies);CollectionAssert.AreEqual(new[]{false,false,true},own.Tricks);Assert.AreEqual(-1,own.NextTrick);
        Assert.AreEqual(balance-price,ui.Match.Player.Coins);Assert.AreEqual(paid+price,own.Paid);Assert.IsNull(ui.Match.Player.Offers[1]);
        Assert.AreSame(actor,ui.Match.Actors[own]);Assert.AreSame(ownCell,actor.CurrentCell);Assert.AreSame(actor,ownCell.OccupiedBy);Assert.AreEqual(ownCell.Coordinates,own.Deployment);
        Assert.AreSame(otherActor,ui.Match.Actors[occupant]);Assert.AreSame(otherCell,otherActor.CurrentCell);Assert.AreSame(otherActor,otherCell.OccupiedBy);
        CollectionAssert.AreEqual(otherTricks,occupant.Tricks);Assert.AreEqual(otherCopies,occupant.Copies);Assert.AreEqual(otherPaid,occupant.Paid);
        Assert.AreEqual(actorCount,ui.Match.Actors.Count);Assert.IsFalse(ui.IsPlacementExchanging);Assert.AreEqual("atori",ui.SelectedId);
        string after=State(ui.Match.Player);ui.DropOffer(1,copy.Token,CellPoint(otherCell.X,otherCell.Y));Assert.AreEqual(after,State(ui.Match.Player),"A stale drag cannot charge or consume a second time.");
    }

    [UnityTest] public IEnumerator NewUnitsStillNeedFreeCellsAndOutsideBriefAndBankDropsKeepTheirExistingRules()
    {
        var initial=ui.Match.Player.Offers[0];string before=State(ui.Match.Player);
        Drag(0,CellPoint(4,7));Assert.AreEqual(before,State(ui.Match.Player));Assert.IsFalse(ui.Match.Player.Owned.ContainsKey("atori"));
        yield return Ready();Drag(0,CellPoint(1,7));yield return Ready();
        var own=ui.Match.Player.Owned["atori"];var actor=ui.Match.Actors[own];var ownCell=actor.CurrentCell;
        Assert.IsTrue(ui.Match.ChooseTrick("atori",2));yield return Ready();before=State(ui.Match.Player);
        Drag(1,new Vector2(-100,-100));Assert.AreEqual(before,State(ui.Match.Player));yield return Ready();
        Drag(1,Center(Named("Brief drop")));Assert.AreEqual(before,State(ui.Match.Player));yield return Ready();
        int balance=ui.Match.Player.Coins,paid=own.Paid;
        var suppressed=ui.Match.Player.Offers[2];Assert.IsNotNull(suppressed);
        // A duplicate dropped into the bank reserves a separate base copy without buying a Trick.
        Drag(1,Center(Named("Bench drop")));yield return Ready();
        var reserve=ui.Match.Player.FindOwned("atori",true);
        Assert.IsNotNull(reserve);Assert.AreNotSame(own,reserve);
        Assert.AreEqual(balance,ui.Match.Player.Coins);Assert.AreEqual(1,own.Copies);Assert.IsFalse(own.Tricks.Any(active=>active));
        Assert.AreEqual(1,reserve.Copies);Assert.AreEqual(0,reserve.Paid);
        Assert.AreEqual(UnitLocation.Field,own.Location);Assert.AreSame(ownCell,actor.CurrentCell);Assert.AreSame(actor,ownCell.OccupiedBy);
        Assert.IsTrue(ui.Match.Player.Offers.All(o=>o==null||o.UnitId!="atori"));
        Assert.IsTrue(ui.Match.Store("atori",true));yield return Ready();
        Assert.AreEqual(balance+paid,ui.Match.Player.Coins,"Merging into the bank returns the field investment.");
        Assert.AreSame(reserve,ui.Match.Player.Owned["atori"]);Assert.AreEqual(UnitLocation.Bench,reserve.Location);
        Assert.IsFalse(ui.Match.Player.Owns(own));Assert.AreEqual(2,reserve.Copies);
        CollectionAssert.AreEqual(new[]{true,false,false},reserve.Tricks,"Bank fusion uses the next numerical Trick, not the selected offer Trick.");
        Assert.AreEqual(Mathf.Max(0,reserve.Definition.BaseCost)+Mathf.Max(0,reserve.Definition.Tricks[0].Cost),reserve.BankRedeployCost);
        Assert.IsFalse(ui.Match.Actors.ContainsKey(own));Assert.IsNull(ownCell.OccupiedBy);
        Assert.IsTrue(ui.Match.Player.Offers.All(o=>o==null||o.UnitId!="atori"));before=State(ui.Match.Player);
        ui.DropOffer(2,suppressed.Token,CellPoint(4,7));Assert.AreEqual(before,State(ui.Match.Player));
    }
}
