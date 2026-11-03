using System.Collections;
using System.Collections.Generic;
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

public sealed class MobileMenuBankPlayTests
{
    LocalGameUI ui;
    int sleep;
    [UnitySetUp] public IEnumerator Load()
    {
        sleep=Screen.sleepTimeout;
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.RenderPage();ui.Match.enabled=true;}
        Screen.sleepTimeout=sleep;yield return null;
    }
    [UnityTest] public IEnumerator ScreenStaysAwakeFromPreparationThroughCombatAndRestoresInLobby()
    {
        int original=Screen.sleepTimeout;
        ui.Match.StartMatch("bugaloo",22);ui.RenderPage();yield return null;
        Assert.AreEqual(SleepTimeout.NeverSleep,Screen.sleepTimeout);
        ui.Match.Ready();ui.RenderPage();yield return null;
        Assert.AreEqual(MatchPhase.Combat,ui.Match.Phase);Assert.AreEqual(SleepTimeout.NeverSleep,Screen.sleepTimeout);
        ui.Match.Menu();ui.RenderPage();yield return null;
        Assert.AreEqual(original,Screen.sleepTimeout);
    }
    [UnityTest] public IEnumerator HorizontalGestureNavigatesAndVerticalGestureDoesNotChangeTabs()
    {
        ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
        var gesture=ui.CanvasRoot.GetComponent<MenuSwipeGesture>();
        var e=SwipePointer(-.65f);
        gesture.OnBeginDrag(e);gesture.OnDrag(e);gesture.OnEndDrag(e);
        Assert.AreEqual(3,ui.MenuTab);yield return new WaitForSecondsRealtime(.45f);
        Assert.IsNotNull(Find("Help"));Assert.AreEqual(1,Find("Page").GetComponent<CanvasGroup>().alpha);
        ui.SwipeMenu(new Vector2(10,300));Assert.AreEqual(3,ui.MenuTab);
        ui.SwipeMenu(new Vector2(Screen.width*.65f,0));yield return new WaitForSecondsRealtime(.45f);
        Assert.AreEqual(2,ui.MenuTab);Assert.IsNull(Optional("Help"));
        ui.NavigateMenu(0);yield return new WaitForSecondsRealtime(.3f);
        ui.SwipeMenu(new Vector2(Screen.width*.65f,0));Assert.AreEqual(0,ui.MenuTab,"First tab is a boundary.");
        yield return new WaitForSecondsRealtime(.45f);
        ui.OpenCollection();yield return null;
        var scroll=Find("Collection viewport").GetComponent<MenuCollectionScroll>();
        scroll.OnBeginDrag(e);scroll.OnDrag(e);scroll.OnEndDrag(e);
        yield return new WaitForSecondsRealtime(.45f);Assert.AreEqual(1,ui.MenuTab);
    }
    [UnityTest] public IEnumerator DragFollowsFingerBeforeReleaseAndShortSwipeSettlesBackWithoutRebuilding()
    {
        ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
        var original=Find("Choose featured Monster");
        var page=Find("Page");var gesture=ui.CanvasRoot.GetComponent<MenuSwipeGesture>();
        float scale=ui.CanvasRoot.GetComponentInParent<Canvas>().scaleFactor;
        var e=SwipePointer(-.25f);gesture.OnBeginDrag(e);gesture.OnDrag(e);
        Assert.AreEqual(-Screen.width*.25f,page.anchoredPosition.x*scale,.1f,"Content must move before finger release.");
        Assert.AreEqual(2,ui.MenuTab);Assert.IsFalse(e.eligibleForClick);
        Assert.IsNotNull(Optional("Menu swipe preview"));
        e.position=e.pressPosition+new Vector2(-Screen.width*.48f,0);gesture.OnDrag(e);
        Assert.AreEqual(-Screen.width*.48f,page.anchoredPosition.x*scale,.1f);
        gesture.OnEndDrag(e);Assert.AreEqual(2,ui.MenuTab);
        yield return new WaitForSecondsRealtime(.1f);
        Assert.Less(page.anchoredPosition.x,-1,"The return should remain visibly animated.");
        yield return new WaitForSecondsRealtime(.35f);
        Assert.AreEqual(Vector2.zero,page.anchoredPosition);
        Assert.AreSame(original,Find("Choose featured Monster"));Assert.IsNull(Optional("Menu swipe preview"));
        Assert.IsTrue(page.GetComponent<CanvasGroup>().blocksRaycasts);
    }
    [UnityTest] public IEnumerator DragCanReverseDirectionAndOnlyCrossingHalfCommits()
    {
        ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
        var gesture=ui.CanvasRoot.GetComponent<MenuSwipeGesture>();var e=SwipePointer(-.65f);
        gesture.OnBeginDrag(e);gesture.OnDrag(e);Assert.AreEqual(2,ui.MenuTab);
        e.position=e.pressPosition+new Vector2(Screen.width*.65f,0);gesture.OnDrag(e);
        Assert.AreEqual(2,ui.MenuTab,"Crossing the threshold must not commit while the finger is down.");
        gesture.OnEndDrag(e);Assert.AreEqual(1,ui.MenuTab);
        yield return new WaitForSecondsRealtime(.45f);
        Assert.IsNotNull(Find("deck-monster"));Assert.IsNull(Optional("Help"));Assert.IsNull(Optional("Menu swipe preview"));
        e=SwipePointer(-.5f);gesture.OnBeginDrag(e);gesture.OnDrag(e);gesture.OnEndDrag(e);
        yield return new WaitForSecondsRealtime(.45f);Assert.AreEqual(1,ui.MenuTab,"An exact tie stays on the original page.");
        e=SwipePointer(-.51f);gesture.OnBeginDrag(e);gesture.OnDrag(e);gesture.OnEndDrag(e);
        yield return new WaitForSecondsRealtime(.45f);Assert.AreEqual(2,ui.MenuTab);
        Assert.AreEqual("home",ui.MenuPage);Assert.AreEqual(Vector2.zero,Find("Page").anchoredPosition);
    }
    [UnityTest] public IEnumerator CollectionPreservesItsScrollWhenSwipeReturnsAndVerticalDragDoesNotPage()
    {
        ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
        ui.OpenCollection();yield return null;Canvas.ForceUpdateCanvases();
        var collection=Find("Character selection");var scroll=Find("Collection viewport").GetComponent<MenuCollectionScroll>();
        var position=scroll.content.anchoredPosition;var e=SwipePointer(.3f);
        scroll.OnBeginDrag(e);scroll.OnDrag(e);
        Assert.Greater(collection.anchoredPosition.x,0,"Collection must follow the same live drag.");
        scroll.OnEndDrag(e);yield return new WaitForSecondsRealtime(.45f);
        Assert.AreSame(collection,Find("Character selection"));Assert.AreEqual(position,scroll.content.anchoredPosition);
        Assert.AreEqual(2,ui.MenuTab);Assert.AreEqual(Vector2.zero,collection.anchoredPosition);
        e=SwipePointer(0);e.position=e.pressPosition+new Vector2(4,Screen.height*.25f);
        scroll.OnBeginDrag(e);scroll.OnDrag(e);scroll.OnEndDrag(e);
        Assert.AreEqual(Vector2.zero,Find("Page").anchoredPosition);Assert.AreEqual(2,ui.MenuTab);
        Assert.IsNull(Optional("Menu swipe preview"));
    }
    [UnityTest] public IEnumerator EndTabsResistDraggingAndInterruptedDragRestoresControls()
    {
        foreach(int tab in new[]{0,4})
        {
            ui.NavigateMenu(tab);yield return new WaitForSecondsRealtime(.3f);
            var delta=new Vector2(Screen.width*(tab==0?.75f:-.75f),0);
            Assert.IsTrue(ui.BeginMenuSwipe(delta));Assert.IsNull(Optional("Menu swipe preview"));
            ui.EndMenuSwipe(delta);yield return new WaitForSecondsRealtime(.45f);
            Assert.AreEqual(tab,ui.MenuTab);Assert.AreEqual(Vector2.zero,Find("Page").anchoredPosition);
        }
        ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
        var swipe=new Vector2(-Screen.width*.65f,0);Assert.IsTrue(ui.BeginMenuSwipe(swipe));
        ui.CancelMenuSwipe();ui.EndMenuSwipe(swipe);yield return null;
        Assert.AreEqual(2,ui.MenuTab);Assert.IsNull(Optional("Menu swipe preview"));
        Assert.AreEqual(Vector2.zero,Find("Page").anchoredPosition);
        Assert.IsTrue(Find("Page").GetComponent<CanvasGroup>().blocksRaycasts);
        Assert.IsTrue(Find("Menu navigation").GetComponent<CanvasGroup>().blocksRaycasts);
    }
    [UnityTest] public IEnumerator EmptyMenuSpaceKeepsReceivingSwipesDuringTransitions()
    {
        ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
        // A blank patch of the lobby must route through the same handler as its buttons.
        var start=RectTransformUtility.WorldToScreenPoint(null,Find("Page").TransformPoint(new Vector2(30,-300)));
        Assert.AreEqual(ui.CanvasRoot.gameObject,DragTarget(start));
        var gesture=ui.CanvasRoot.GetComponent<MenuSwipeGesture>();
        var e=SwipePointer(-.65f);gesture.OnBeginDrag(e);gesture.OnDrag(e);gesture.OnEndDrag(e);
        Assert.AreEqual(ui.CanvasRoot.gameObject,DragTarget(start),"A second press must remain reachable while the page settles.");
        yield return new WaitForSecondsRealtime(.45f);
        Assert.AreEqual(ui.CanvasRoot.gameObject,DragTarget(start));
        ui.Match.StartMatch("bugaloo",22);ui.RenderPage();yield return null;
        Assert.IsNull(Optional("Menu swipe surface"),"The lobby surface must not cover combat controls.");
        Assert.IsNotNull(Find("Board input"));
    }
    [UnityTest] public IEnumerator RapidSwipesCanRepeatAcrossEveryTabWithoutWaitingForAnimation()
    {
        ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
        var gesture=ui.CanvasRoot.GetComponent<MenuSwipeGesture>();
        int[] route={3,4,3,2,1,0,1,2,3,4,3,2};
        for(int cycle=0;cycle<2;cycle++)
        {
            foreach(int target in route)
            {
                var e=SwipePointer(target>ui.MenuTab?-.65f:.65f);
                gesture.OnBeginDrag(e);gesture.OnDrag(e);gesture.OnEndDrag(e);
                Assert.AreEqual(target,ui.MenuTab,"Every new swipe must be accepted, including consecutive reversals.");
            }
            yield return new WaitForSecondsRealtime(.45f);
            Assert.AreEqual(Vector2.zero,Find("Page").anchoredPosition);
            Assert.IsNull(Optional("Menu swipe preview"));Assert.IsNull(Optional("Outgoing menu"));
            Assert.IsTrue(Find("Page").GetComponent<CanvasGroup>().blocksRaycasts);
            Assert.IsTrue(Find("Menu navigation").GetComponent<CanvasGroup>().interactable);
        }
    }
    [UnityTest] public IEnumerator NewSwipeCanInterruptTabAnimationAndShortSwipeReturningHome()
    {
        ui.OpenHome();yield return new WaitForSecondsRealtime(.3f);
        ui.OpenDecks();
        var gesture=ui.CanvasRoot.GetComponent<MenuSwipeGesture>();
        var e=SwipePointer(-.65f);gesture.OnBeginDrag(e);gesture.OnDrag(e);gesture.OnEndDrag(e);
        Assert.AreEqual(2,ui.MenuTab,"A button transition must not consume the next swipe.");
        e=SwipePointer(.25f);gesture.OnBeginDrag(e);gesture.OnDrag(e);gesture.OnEndDrag(e);
        Assert.AreEqual(2,ui.MenuTab,"A short swipe keeps the current page.");
        e=SwipePointer(.65f);gesture.OnBeginDrag(e);gesture.OnDrag(e);gesture.OnEndDrag(e);
        Assert.AreEqual(1,ui.MenuTab,"Returning from a short swipe must not block the next gesture.");
        yield return new WaitForSecondsRealtime(.45f);
        Assert.AreEqual(Vector2.zero,Find("Page").anchoredPosition);Assert.IsNull(Optional("Menu swipe preview"));
    }
    GameObject DragTarget(Vector2 screen)
    {
        Canvas.ForceUpdateCanvases();
        var results=new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},results);
        Assert.IsNotEmpty(results,"The menu must have a touch surface.");
        return ExecuteEvents.GetEventHandler<IDragHandler>(results[0].gameObject);
    }
    static PointerEventData SwipePointer(float fraction)
    {
        var start=new Vector2(Screen.width*.5f,Screen.height*.5f);
        return new PointerEventData(EventSystem.current){pressPosition=start,position=start+new Vector2(Screen.width*fraction,0),eligibleForClick=true};
    }
    [UnityTest] public IEnumerator StylesAreInCollectionAndLogoIsAbsentFromLobby()
    {
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Image>().Any(i=>i.sprite!=null&&i.sprite.name=="Monster Pouch supplied logo"));
        ui.OpenCollection();yield return new WaitForSecondsRealtime(.3f);
        Find("collection-styles-tab").GetComponent<Button>().onClick.Invoke();yield return null;
        Assert.IsNotNull(Find("Scenario skins"));
        Find("collection-deck-tab").GetComponent<Button>().onClick.Invoke();yield return null;
        Assert.IsNotNull(Find("Collection grid"));
        typeof(LocalGameUI).GetMethod("ShowSettings",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);yield return null;
        Assert.IsFalse(Find("Settings").GetComponentsInChildren<Text>().Any(t=>t.text.Contains("ESCENARIO")||t.text.Contains("ESTILOS")||t.text.Contains("SCENERY")));
    }
    [UnityTest] public IEnumerator ClosedBankOpensOnDragShowsThreeSlotsAndWithdrawsSameUnit()
    {
        ui.Match.StartMatch("bugaloo",12);ui.RenderPage();yield return new WaitForSecondsRealtime(.4f);
        var offer=ui.Match.Player.Offers.First(o=>o!=null);int slot=ui.Match.Player.Offers.ToList().IndexOf(offer);
        Vector2 target=Center(Find("Bench drop"));
        ui.PreviewOffer(slot,offer.Token,target);yield return new WaitForSecondsRealtime(.21f);
        Assert.Greater(ui.BankOpenAmount,.99f);
        ui.DropOffer(slot,offer.Token,target);yield return null;yield return new WaitForSecondsRealtime(.21f);
        var unit=ui.Match.Player.Owned[offer.UnitId];Assert.AreEqual(UnitLocation.Bench,unit.Location);
        Assert.Less(ui.BankOpenAmount,.01f);Assert.IsNull(Optional("bank-unit-"+offer.UnitId));
        Find("Bench drop").GetComponent<Button>().onClick.Invoke();yield return null;
        Assert.IsNotNull(Find("bank-unit-"+offer.UnitId));Assert.IsNotNull(Find("bank-empty-1"));Assert.IsNotNull(Find("bank-empty-2"));
        int copies=unit.Copies,paid=unit.Paid,reserved=unit.BankReservedCost,balance=ui.Match.Player.Coins,cost=unit.BankRedeployCost;
        Assert.AreEqual(0,paid);Assert.AreEqual(Mathf.Max(0,unit.Definition.BaseCost),reserved);
        var cell=ui.Match.Board.GetCell(1,7);var dest=ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(cell));
        var card=Find("bank-unit-"+offer.UnitId).GetComponent<UnitCardGesture>();
        var pointer=new PointerEventData(EventSystem.current){position=Center((RectTransform)card.transform)};
        card.OnPointerDown(pointer);pointer.position=dest;card.OnDrag(pointer);yield return null;
        Assert.AreEqual(0,Find("Bank contents").GetComponent<CanvasGroup>().alpha);
        card.OnPointerUp(pointer);yield return null;
        Assert.AreSame(unit,ui.Match.Player.Owned[offer.UnitId]);Assert.AreEqual(UnitLocation.Field,unit.Location);
        Assert.AreEqual(copies,unit.Copies);Assert.AreEqual(paid+reserved,unit.Paid);Assert.IsTrue(ui.Match.Actors.ContainsKey(unit));
        Assert.AreEqual(balance-cost,ui.Match.Player.Coins);Assert.AreEqual(0,unit.BankRedeployCost);
    }
    [UnityTest] public IEnumerator CompactReadyShowsGreenCheckCanBeCancelledAndCountersStayTogether()
    {
        ui.Match.StartMatch("bugaloo",12);ui.RenderPage();yield return null;
        var coins=Find("Battle resources");var reroll=Find("Reroll");var bank=Find("Bench drop");var ready=Find("LISTO");
        Assert.AreEqual(coins.anchoredPosition.y,reroll.anchoredPosition.y,.1f);
        Assert.Less(coins.rect.width,90);Assert.Less(reroll.rect.width,90);
        Assert.Less(ready.rect.width,120);Assert.Less(ready.rect.height,50);
        Assert.Greater(ready.anchoredPosition.y,coins.anchoredPosition.y);
        Assert.Less(coins.anchoredPosition.y,0);
        var bankArt=Find("Bank closed").GetComponent<Image>();
        float fittedHeight=Mathf.Min(bankArt.rectTransform.rect.height,bankArt.rectTransform.rect.width*bankArt.sprite.rect.height/bankArt.sprite.rect.width);
        Assert.AreEqual(202,-bank.anchoredPosition.y+fittedHeight,.01f,"The visible bank and main brief must share a baseline.");
        Find("Bench drop").GetComponent<Button>().onClick.Invoke();yield return null;
        Assert.GreaterOrEqual(-Find("Bank contents").anchoredPosition.y,CardTableArena.BriefTop);
        Find("bank-close").GetComponent<Button>().onClick.Invoke();yield return null;
        var button=ready.GetComponent<Button>();button.onClick.Invoke();yield return new WaitForSecondsRealtime(.13f);
        Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);
        // Read the rebuilt button. Ready is now cancellable during the existing countdown.
        ready=Find("LISTO");button=ready.GetComponent<Button>();
        Assert.IsTrue(ui.Match.OnlineReady);
        Assert.IsTrue(ready.GetComponentsInChildren<Text>().Any(t=>t.text=="✓"&&t.color.a>0));
        Assert.Greater(Find("Ready green fill").rect.width,0);Assert.IsTrue(button.interactable);
        button.onClick.Invoke();yield return null;Assert.IsFalse(ui.Match.OnlineReady);
        ui.Match.Advance(1.3f);Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);
        Find("LISTO").GetComponent<Button>().onClick.Invoke();yield return null;
        ui.Match.Advance(1.1f);Assert.AreEqual(MatchPhase.Preparation,ui.Match.Phase);
        ui.Match.Advance(.2f);yield return null;Assert.AreEqual(MatchPhase.Combat,ui.Match.Phase);
    }
    [UnityTest] public IEnumerator ReservedBankTwinKeepsItsOwnGestureIdentityAndMergesIntoTheFieldUnit()
    {yield return ExerciseBankTwinFusion(false);}
    [UnityTest] public IEnumerator FieldTwinMergesIntoItsReservedBankCopyAndRedeploysWithInheritedTricks()
    {yield return ExerciseBankTwinFusion(true);}
    IEnumerator ExerciseBankTwinFusion(bool intoBank)
    {
        Assert.IsTrue(ui.Match.StartMatch("bugaloo",new[]{"tsu"},12));
        ui.Match.Player.AddCombatCoins(50);ui.RenderPage();
        float deadline=Time.unscaledTime+5;
        while((!ui.BriefReady||LoadingArtworkScreen.IsBlocking)&&Time.unscaledTime<deadline)yield return null;
        Assert.IsTrue(ui.BriefReady);Assert.IsFalse(LoadingArtworkScreen.IsBlocking);
        var cell=ui.Match.Board.GetCell(1,7);
        Vector2 fieldPoint=ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(cell));
        var offer=ui.Match.Player.Offers[0];ui.DropOffer(0,offer.Token,fieldPoint);yield return null;yield return null;
        var field=ui.Match.Player.Owned["tsu"];
        Assert.IsTrue(ui.Match.BuyTrick("tsu",2,1,ui.Match.Player.Offers[1].Token));
        yield return null;yield return null;
        int paid=field.Paid,balance=ui.Match.Player.Coins;
        var source=Find("offer-2").GetComponent<BriefOfferGesture>();
        var pointer=new PointerEventData(EventSystem.current){position=Center((RectTransform)source.transform)};
        source.OnPointerDown(pointer);pointer.position=Center(Find("Bench drop"));source.OnDrag(pointer);source.OnPointerUp(pointer);
        yield return null;yield return null;
        var reserve=ui.Match.Player.FindOwned("tsu",true);
        Assert.IsNotNull(reserve);Assert.AreNotSame(field,reserve);
        Assert.AreEqual(balance,ui.Match.Player.Coins);Assert.AreEqual(2,field.Copies);Assert.AreEqual(1,reserve.Copies);
        Assert.IsTrue(ui.Match.Player.Offers.All(o=>o==null));Assert.IsTrue(ui.Match.Actors.ContainsKey(field));
        Assert.IsFalse(ui.Match.Actors.ContainsKey(reserve));
        Find("Bench drop").GetComponent<Button>().onClick.Invoke();yield return null;yield return null;
        var card=Find("bank-unit-tsu").GetComponent<UnitCardGesture>();
        pointer=new PointerEventData(EventSystem.current){position=Center((RectTransform)card.transform)};
        card.OnPointerDown(pointer);card.OnPointerUp(pointer);yield return null;yield return null;
        Assert.AreSame(reserve,ui.SelectedOwned,"Selecting the bank card must not select its field twin by ID.");
        if(intoBank)
        {
            ui.Drop(field,Center(Find("Bench drop")));yield return null;yield return null;
            Assert.AreSame(reserve,ui.Match.Player.Owned["tsu"]);Assert.IsFalse(ui.Match.Player.Owns(field));
            Assert.AreEqual(balance+paid,ui.Match.Player.Coins);Assert.AreEqual(0,reserve.Paid);
            Assert.AreEqual(UnitLocation.Bench,reserve.Location);Assert.AreEqual(3,reserve.Copies);
            Assert.IsTrue(reserve.Tricks[0]);Assert.IsTrue(reserve.Tricks[2]);Assert.IsFalse(reserve.Tricks[1]);
            int cost=Mathf.Max(0,field.Definition.BaseCost)+Mathf.Max(0,field.Definition.Tricks[0].Cost)+Mathf.Max(0,field.Definition.Tricks[2].Cost);
            Assert.AreEqual(cost,reserve.BankRedeployCost);Assert.IsFalse(ui.Match.Actors.ContainsKey(field));
            Assert.IsFalse(ui.Match.Actors.ContainsKey(reserve));
            Find("Bench drop").GetComponent<Button>().onClick.Invoke();yield return null;yield return null;
            card=Find("bank-unit-tsu").GetComponent<UnitCardGesture>();
            pointer=new PointerEventData(EventSystem.current){position=Center((RectTransform)card.transform)};
            card.OnPointerDown(pointer);pointer.position=fieldPoint;card.OnDrag(pointer);card.OnPointerUp(pointer);
            yield return null;yield return null;
            Assert.AreEqual(UnitLocation.Field,reserve.Location);Assert.AreEqual(cost,reserve.Paid);
            Assert.AreEqual(balance+paid-cost,ui.Match.Player.Coins);Assert.IsTrue(ui.Match.Actors.ContainsKey(reserve));
            Assert.AreEqual(0,reserve.BankRedeployCost);
        }
        else
        {
            int cost=Mathf.Max(0,field.Definition.Tricks[0].Cost);
            ui.Match.Player.AddCombatCoins(-ui.Match.Player.Coins);
            ui.Drop(reserve,fieldPoint);yield return null;yield return null;
            Assert.AreSame(reserve,ui.Match.Player.FindOwned("tsu",true));Assert.AreEqual(0,ui.Match.Player.Coins);
            Assert.AreEqual(2,field.Copies);Assert.AreEqual(paid,field.Paid);
            ui.Match.Player.AddCombatCoins(balance);
            Find("Bench drop").GetComponent<Button>().onClick.Invoke();yield return null;yield return null;
            card=Find("bank-unit-tsu").GetComponent<UnitCardGesture>();
            pointer=new PointerEventData(EventSystem.current){position=Center((RectTransform)card.transform)};
            card.OnPointerDown(pointer);
            pointer.position=ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(ui.Match.Actors[ui.Match.Player.Monster].CurrentCell));
            card.OnDrag(pointer);card.OnPointerUp(pointer);yield return null;yield return null;
            Assert.AreSame(field,ui.Match.Player.Owned["tsu"]);Assert.IsFalse(ui.Match.Player.Owns(reserve));
            Assert.AreEqual(cell.Coordinates,field.Deployment);Assert.AreEqual(3,field.Copies);
            Assert.IsTrue(field.Tricks[0]);Assert.IsTrue(field.Tricks[2]);Assert.IsFalse(field.Tricks[1]);
            Assert.AreEqual(balance-cost,ui.Match.Player.Coins);Assert.AreEqual(paid+cost,field.Paid);
            Assert.AreSame(ui.Match.Actors[field],cell.OccupiedBy);
            ui.Drop(reserve,fieldPoint);Assert.AreEqual(balance-cost,ui.Match.Player.Coins,"A stale gesture cannot merge twice.");
        }
        Assert.IsEmpty(ui.Match.Player.BankReserves);
        Assert.AreEqual(1,ui.Match.Player.AllOwned.Count(u=>u.Definition.Id=="tsu"));
        Assert.AreEqual(1,ui.Match.Actors.Keys.Count(u=>u.Definition.Id=="tsu"));
        Assert.IsNull(Optional("Bank contents"));
    }
    [Test] public void AbilitySheetShowsTheConfiguredDamageInsteadOfAnObsoleteMultiplier()
    {
        var definition=DocumentedRoster.CreateTokoro();definition.Damage=3;
        definition.BaseAbility.Parameters=new[]{new AbilityParameter{Key="damageOverride",Value=27}};
        string text=LocalGameUI.AbilityDetailText(new OwnedUnit(definition),definition.BaseAbility);
        Assert.That(text,Does.Contain("27"));Assert.That(text,Does.Not.Contain("×2"));
    }
    RectTransform Optional(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name==name);
    RectTransform Find(string name){var found=Optional(name);Assert.IsNotNull(found,name);return found;}
    static Vector2 Center(RectTransform rect){Canvas.ForceUpdateCanvases();return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));}
}
