using System;
using System.Linq;
using MonsterPouch.Local;
using UnityEngine;
using UnityEngine.UI;

public static class MonsterPouchReworkPreview
{
    static LocalGameUI UI=>UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
    public static string Tap(string name)
    {
        var button=UI.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        button.onClick.Invoke();return "Pressed "+name;
    }
    public static string Collection(){UI.Match.Menu();UI.Match.enabled=false;UI.OpenCollection();return "Collection";}
    public static string Bank()
    {
        UI.CloseModal();UI.Match.enabled=false;UI.Match.StartMatch("bugaloo",new[]{"stein","atori","flo"},42);
        UI.Match.Player.AddCombatCoins(40);
        for(int i=0;i<3;i++)
        {
            int slot=Enumerable.Range(0,3).Where(s=>UI.Match.Player.Offers[s]!=null).DefaultIfEmpty(-1).First();
            if(slot<0){UI.Match.Reroll(UI.Match.Player.OfferVersion);slot=Enumerable.Range(0,3).First(s=>UI.Match.Player.Offers[s]!=null);}
            if(!UI.Match.BuyAndPlace(slot,UI.Match.Player.Offers[slot].Token,null,true))throw new InvalidOperationException(UI.Match.Notice);
        }
        UI.RenderPage();return "Three stored troops";
    }
}
