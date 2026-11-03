using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEngine;
using UnityEngine.UI;

public static class MonsterPouchRedesignPreview
{
    static LocalGameUI ui;
    public static string CaptureAll()
    {
        ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(ui==null||!Application.isPlaying)return "Enter Play Mode first.";
        ui.Match.enabled=false;
        ui.StartCoroutine(Record());
        return "Capturing redesign screenshots.";
    }
    static IEnumerator Shot(string name)
    {
        yield return new WaitForSecondsRealtime(.32f);
        Canvas.ForceUpdateCanvases();
        MonsterPouchPlayInspection.Capture("redesign-"+name);
        yield return new WaitForSecondsRealtime(.15f);
    }
    static IEnumerator Record()
    {
        ui.CloseModal();ui.Match.Menu();ui.OpenHome();
        yield return Shot("lobby");
        typeof(LocalGameUI).GetMethod("ShowCollectionInfo",BindingFlags.NonPublic|BindingFlags.Instance)
            .Invoke(ui,new object[]{ui.Match.Config.Get("jazar")});
        yield return Shot("collection-jazzar");
        ui.CloseModal();
        ui.Match.StartMatch("anuik",new[]{"jazar"},813);ui.Match.Player.AddCombatCoins(30);
        var offers=ui.Match.Player.Offers;
        ui.Match.BuyAndPlace(0,offers[0].Token,ui.Match.Board.GetCell(0,9));
        ui.ShowInspection(ui.Match.Player.Owned["jazar"],false);
        ui.CanvasRoot.GetComponentsInChildren<Button>().First(b=>b.name=="ability-2").onClick.Invoke();
        yield return Shot("compact-upgrades");
        ui.CloseModal();ui.Match.BuyTrick("jazar",2,1,offers[1].Token);ui.Match.Ready();
        yield return Shot("toxic-emerge");
        yield return new WaitForSecondsRealtime(.45f);
        yield return Shot("toxic-bubbles");
        for(int i=0;i<160&&ui.Match.Phase==MatchPhase.Combat;i++)
        {
            foreach(var actor in ui.Match.Actors.Values.Where(u=>u.Side==BoardSide.Red&&u.IsAlive).ToArray())actor.ApplyDamage(actor.CurrentHealth);
            ui.Match.Advance(.1f);yield return null;
        }
        ui.Match.NextRound();ui.RenderPage();
        yield return Shot("previous-round-holograms");
        ui.Match.Menu();ui.OpenHome();ui.Match.enabled=true;
        MonsterPouchLocalBridge.Reply("redesign-preview","All six screenshots captured; returned to lobby.");
    }
    public static string AuditText()
    {
        var current=Object.FindFirstObjectByType<LocalGameUI>();
        if(current==null)return "No game UI.";
        Canvas.ForceUpdateCanvases();
        var report=new StringBuilder();
        foreach(var t in current.CanvasRoot.GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy))
            if(t.preferredHeight>t.rectTransform.rect.height+2&&!t.resizeTextForBestFit)
                report.AppendLine(t.text+" | needs "+t.preferredHeight+" / "+t.rectTransform.rect.height);
        return report.Length==0?"No clipped text.":report.ToString();
    }
}

