using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEngine;
using UnityEngine.UI;

public static class MonsterPouchClashPreview
{
    static LocalGameUI ui;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static string CaptureAll()
    {
        ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(ui==null||!Application.isPlaying)return "Enter Play Mode first.";
        ui.Match.enabled=false;ui.StartCoroutine(Record());return "Capturing lobby actions, collection, bars and compact upgrades.";
    }
    public static string CaptureCombat()
    {
        ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(ui==null||!Application.isPlaying)return "Enter Play Mode first.";
        ui.Match.enabled=false;ui.StartCoroutine(Combat());return "Capturing a real Stein super and a board hop.";
    }
    static IEnumerator Combat()
    {
        ui.CloseModal();ui.Match.StartMatch("anuik",new[]{"stein"},915);ui.Match.Player.AddCombatCoins(30);
        var offer=ui.Match.Player.Offers[0];ui.Match.BuyAndPlace(0,offer.Token,ui.Match.Board.GetCell(2,5));
        ui.Match.Ready();
        var stein=ui.Match.Actors.First(p=>p.Key.Definition.Id=="stein"&&p.Value.Side==BoardSide.Blue).Value;
        stein.AddEnergy(stein.BaseStats.EnergyMax);
        bool hop=false;int rays=0;
        for(int tick=0;tick<40&&ui.Match.Phase==MatchPhase.Combat;tick++)
        {
            ui.Match.Advance(.1f);
            rays=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Count(t=>t.name=="super-ray-stein");
            if(rays>0)
            {
                MonsterPouchPlayInspection.Capture("clash-stein-super");
                yield return new WaitForSecondsRealtime(.12f);break;
            }
            if(!hop&&ui.Match.Actors.Values.Any(a=>a.GetComponent<MonsterPouch.Gameplay.Presentation.UnitPresentation>().IsMoving))
            {
                yield return new WaitForSecondsRealtime(.12f);
                MonsterPouchPlayInspection.Capture("clash-board-hop");hop=true;
            }
            yield return new WaitForSecondsRealtime(.1f);
        }
        MonsterPouchLocalBridge.Reply("clash-combat-preview","Stein rays="+rays+"; board hop captured="+hop);
        ui.Match.Menu();ui.OpenHome();ui.Match.enabled=true;
    }
    static IEnumerator Shot(string name)
    {
        yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
        MonsterPouchPlayInspection.Capture("clash-"+name);yield return new WaitForSecondsRealtime(.15f);
    }
    static IEnumerator Record()
    {
        var report=new StringBuilder();
        ui.CloseModal();ui.Match.Menu();ui.OpenHome();yield return Shot("lobby");
        ui.OpenDecks();ui.OpenCharacterPicker(0);
        yield return Shot("static-collection");ui.CloseModal();
        foreach(var unit in ui.Match.Config.Units)
        {
            typeof(LocalGameUI).GetMethod("ShowCollectionInfo",Private).Invoke(ui,new object[]{unit});
            yield return null;
            var pose=ui.CanvasRoot.GetComponentsInChildren<LobbyPersonality>().Last();
            pose.PlayGesture();pose.Advance(1.5f);
            yield return Shot("gesture-"+unit.Id);
            foreach(var button in ui.CanvasRoot.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("ability-")))
            {
                button.onClick.Invoke();Canvas.ForceUpdateCanvases();
                foreach(var text in ui.CanvasRoot.GetComponentsInChildren<Text>())
                    if(text.gameObject.activeInHierarchy&&text.GetComponentInParent<ScrollRect>()==null&&
                        text.preferredHeight>text.rectTransform.rect.height+2&&!text.resizeTextForBestFit)
                        report.AppendLine(unit.Id+": "+text.text+" needs "+text.preferredHeight+" / "+text.rectTransform.rect.height);
                yield return null;
            }
            ui.CloseModal();
        }
        ui.Match.StartMatch("anuik",new[]{"stein"},915);ui.Match.Player.AddCombatCoins(30);
        yield return Shot("initial-default");
        var offer=ui.Match.Player.Offers[0];ui.Match.BuyAndPlace(0,offer.Token,ui.Match.Board.GetCell(2,8));
        ui.ShowInspection(ui.Match.Player.Owned["stein"],false);
        ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="ability-0").onClick.Invoke();
        yield return Shot("compact-stein");ui.CloseModal();ui.Match.Ready();
        foreach(var actor in ui.Match.Actors.Values)actor.AddEnergy(actor.BaseStats.EnergyMax*.5f);
        yield return Shot("vitals");
        ui.Match.Menu();ui.OpenHome();ui.Match.enabled=true;
        MonsterPouchLocalBridge.Reply("clash-preview",report.Length==0?"All previews captured; no clipped labels.":report.ToString());
    }
}
