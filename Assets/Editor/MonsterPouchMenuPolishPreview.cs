using System.Reflection;
using MonsterPouch.Local;
using UnityEngine;

public static class MonsterPouchMenuPolishPreview
{
    public static string Page(string name)
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(ui==null||!Application.isPlaying)return "Enter Play Mode first.";
        ui.Match.Menu();ui.Match.enabled=false;ui.CloseModal();
        switch(name)
        {
            case "home":ui.OpenHome();break;
            case "brief":ui.OpenDecks();break;
            case "collection":ui.OpenCollection();break;
            case "styles":ui.OpenCollection();ui.ShowTableSkins();break;
            case "online":ui.OpenOnline();break;
            case "join":ui.ShowJoinRoom();break;
            case "modes":ui.ShowBattleModes();break;
            case "shop":ui.NavigateMenu(0);break;
            case "challenges":ui.NavigateMenu(4);break;
            default:
                string method=name=="settings"?"ShowSettings":name=="audio"?"ShowAudio":"ShowHelp";
                typeof(LocalGameUI).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ui,null);break;
        }
        return name;
    }
}
