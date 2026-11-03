using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class BriefFeedbackPlayTests
{
    LocalGameUI ui;
    [UnitySetUp] public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        ui.Match.StartMatch("bugaloo",12);ui.RenderPage();
        yield return new WaitForSecondsRealtime(.4f);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.RenderPage();ui.Match.enabled=true;}yield return null;}

    [UnityTest] public IEnumerator RerollPressPopsAndSpinsOnceAcrossOfferRebuildWithoutDoubleSpending()
    {
        var hit=Find("Reroll");var button=hit.GetComponent<Button>();
        var pointer=new PointerEventData(EventSystem.current){position=Center(hit)};
        ExecuteEvents.Execute(hit.gameObject,pointer,ExecuteEvents.pointerDownHandler);
        Assert.Less(Find("Reroll feedback").localScale.x,1);
        ExecuteEvents.Execute(hit.gameObject,pointer,ExecuteEvents.pointerUpHandler);
        Assert.AreEqual(1,Find("Reroll feedback").localScale.x,.001f);
        int before=ui.Match.Player.Rerolls,version=ui.Match.Player.OfferVersion;
        button.onClick.Invoke();ui.RequestReroll(version);
        float previous=0,travel=0,maxScale=0;bool rebuilt=false;int iconId=Find("Reroll arrow").GetInstanceID();
        float until=Time.unscaledTime+.9f;
        while(Time.unscaledTime<until)
        {
            yield return null;
            var arrow=Find("Reroll arrow");float angle=arrow.localEulerAngles.z;
            travel+=Mathf.DeltaAngle(previous,angle);previous=angle;
            maxScale=Mathf.Max(maxScale,Find("Reroll feedback").localScale.x);
            rebuilt|=arrow.GetInstanceID()!=iconId;
            Assert.Less(Quaternion.Angle(Quaternion.identity,Find("Reroll count").rotation),.01f);
        }
        Assert.IsTrue(rebuilt,"The shop commit must rebuild its offers while preserving the spin.");
        Assert.That(travel,Is.EqualTo(-360).Within(2));Assert.Greater(maxScale,1.08f);
        Assert.AreEqual(1,Find("Reroll feedback").localScale.x,.001f);
        Assert.AreEqual(before-1,ui.Match.Player.Rerolls);Assert.IsTrue(ui.BriefReady);
        Assert.AreEqual(ui.Match.Player.Rerolls.ToString(),Find("Reroll count").GetComponent<Text>().text);
    }

    [UnityTest] public IEnumerator FieldSaleEmitsMoonkensFromBriefAndRefundsOnce()
    {yield return SellTroop(UnitLocation.Field);}
    [UnityTest] public IEnumerator BankSaleRemovesTroopWithoutRefundingOrAnimatingAnAlreadyReturnedInvestment()
    {yield return SellTroop(UnitLocation.Bench);}

    IEnumerator SellTroop(UnitLocation source)
    {
        var offer=ui.Match.Player.Offers.First(o=>o!=null);int slot=ui.Match.Player.Offers.ToList().IndexOf(offer);
        Vector2 destination=ui.GameCamera.WorldToScreenPoint(ui.Match.Mapper.GetWorldPosition(ui.Match.Board.GetCell(1,7)));
        ui.DropOffer(slot,offer.Token,destination);yield return null;
        var unit=ui.Match.Player.Owned[offer.UnitId];
        if(source==UnitLocation.Bench)
        {
            int balance=ui.Match.Player.Coins,investment=unit.Paid;
            ui.Drop(unit,Center(Find("Bench drop")));yield return null;
            Assert.AreEqual(balance+investment,ui.Match.Player.Coins);
            Assert.AreEqual(investment,unit.BankRedeployCost);
        }
        Assert.AreEqual(source,unit.Location);
        int before=ui.Match.Player.Coins,refund=unit.SaleRefund;
        var drop=Find("Brief drop");Vector2 salePoint=Center(drop);
        if(source==UnitLocation.Bench)
        {
            ui.ToggleBankContents();yield return null;
            var gesture=Find("bank-unit-"+unit.Definition.Id).GetComponent<UnitCardGesture>();
            var pointer=new PointerEventData(EventSystem.current){position=Center((RectTransform)gesture.transform)};
            gesture.OnPointerDown(pointer);pointer.position=salePoint;gesture.OnDrag(pointer);
            gesture.OnPointerUp(pointer);
        }
        else ui.Drop(unit,salePoint);
        Assert.IsFalse(ui.Match.Player.Owned.ContainsKey(offer.UnitId));
        Assert.AreEqual(before+refund,ui.Match.Player.Coins);
        var chips=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Where(r=>r.name=="Sold Moonken").ToArray();
        if(source==UnitLocation.Bench)
        {
            Assert.AreEqual(0,refund);Assert.IsEmpty(chips,"A sale for zero must not animate invented income.");
            ui.Drop(unit,salePoint);Assert.AreEqual(before,ui.Match.Player.Coins);
            yield return null;yield return null;
            Assert.IsNull(Optional("bank-unit-"+offer.UnitId));Assert.IsNull(Optional("Bank contents"));
            Assert.IsNull(Optional("Sold Moonken"));
            yield break;
        }
        Assert.GreaterOrEqual(chips.Length,3);
        Vector2 iconSize=Find("Moonkens icon").rect.size;
        foreach(var chip in chips)
        {
            Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(drop,Center(chip),null),"Sale must start inside the main Brief.");
            Assert.AreEqual(iconSize,chip.rect.size,"Flying Moonkens must match the HUD icon's full size.");
        }
        ui.Drop(unit,salePoint);Assert.AreEqual(before+refund,ui.Match.Player.Coins,"A stale release cannot sell twice.");
        yield return null;
        Assert.IsNull(Optional("brief-"+offer.UnitId));Assert.IsNull(Optional("Bank contents"));
        bool sawPop=false,sawPopBeforeLastArrival=false;
        float deadline=Time.unscaledTime+1.2f;
        Vector2 landing=Center(Find("Moonkens icon"));
        while(Time.unscaledTime<deadline)
        {
            var remaining=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Where(r=>r.name=="Sold Moonken").ToArray();
            foreach(var chip in remaining)Assert.AreEqual(iconSize,chip.rect.size,"Moonkens must not shrink in flight.");
            float scale=Find("Moonkens icon").localScale.x;
            sawPop|=scale>1.04f;
            sawPopBeforeLastArrival|=remaining.Length>0&&remaining.Length<chips.Length&&scale>1.01f;
            Assert.Less(Vector2.Distance(landing,Center(Find("Moonkens icon"))),.1f,"The pop must stay centered.");
            Assert.AreEqual(Vector3.one,Find("Battle resources").localScale,"The number must not move with the icon.");
            yield return null;
        }
        Assert.IsTrue(sawPop);Assert.IsTrue(sawPopBeforeLastArrival,"The icon must react before the last Moonkens arrive.");
        Assert.AreEqual(Vector3.one,Find("Moonkens icon").localScale);
        Assert.IsNull(Optional("Sold Moonken"));
    }

    [Test] public void CurrencyKeepsItsNameAcrossLanguagesAndLegacyDescriptions()
    {
        const string key="monster-pouch.language";
        bool oldLanguage=GameText.Spanish,hadPreference=PlayerPrefs.HasKey(key);
        string preference=PlayerPrefs.GetString(key);
        try
        {
            foreach(bool spanish in new[]{true,false})
            {
                GameText.SetLanguage(spanish);
                foreach(string alias in new[]{"Moonkens","Moonken","Moon-Ken","Moon Tokens","munken","munkens"})
                {
                    Assert.AreEqual("Moonkens",GameText.Resolve(alias));
                    var saved=new TrickDefinition{Id="custom-currency",Name=alias,Description="+2 "+alias};
                    Assert.AreEqual("Moonkens",GameText.AbilityName(saved));
                    Assert.AreEqual("+2 Moonkens",GameText.AbilityDescription(saved,null));
                }
                Assert.AreEqual(spanish?"No tienes suficientes Moonkens.":"Not enough Moonkens.",
                    GameText.Resolve("No tienes suficientes Moon Tokens."));
                Assert.AreEqual("MOONKENS",GameText.Resolve("MOON TOKENS"));
                var blotan=DocumentedRoster.CreateBlotan();
                Assert.That(GameText.AbilityDescription(blotan.BaseAbility,blotan),Does.Contain("Moonkens"));
                Assert.That(GameText.AbilityDescription(blotan.Tricks[2],blotan),Does.Contain("Moonkens"));
                Assert.AreEqual("Moonbeam",GameText.Resolve("Moonbeam"));
            }
        }
        finally
        {
            GameText.SetLanguage(oldLanguage);
            if(hadPreference)PlayerPrefs.SetString(key,preference);else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    [UnityTest] public IEnumerator MonsterCannotBeSoldAndBlockedBriefStoreDoesNotRemoveTroop()
    {
        var offer=ui.Match.Player.Offers.First(o=>o!=null);int slot=ui.Match.Player.Offers.ToList().IndexOf(offer);
        ui.DropOffer(slot,offer.Token,Center(Find("Bench drop")));yield return null;
        var unit=ui.Match.Player.Owned[offer.UnitId];int coins=ui.Match.Player.Coins;
        Assert.IsFalse(ui.Match.Store(offer.UnitId,false));Assert.AreEqual(UnitLocation.Bench,unit.Location);
        ui.Drop(ui.Match.Player.Monster,Center(Find("Brief drop")));
        Assert.AreEqual(coins,ui.Match.Player.Coins);Assert.IsNull(Optional("Sold Moonken"));
        Assert.IsTrue(ui.Match.Player.Owned.ContainsKey("bugaloo"));yield return null;
    }

    RectTransform Optional(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name==name);
    RectTransform Find(string name){var r=Optional(name);Assert.IsNotNull(r,name);return r;}
    static Vector2 Center(RectTransform rect)
    {Canvas.ForceUpdateCanvases();return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));}
}
