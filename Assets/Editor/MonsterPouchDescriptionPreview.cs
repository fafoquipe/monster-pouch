using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;

public static class MonsterPouchDescriptionPreview
{
    public static string Character(string selection)
    {
        var parts=selection.Split(':');
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        ui.Match.Menu();ui.Match.enabled=false;ui.OpenCollection();
        var unit=ui.Match.Config.Get(parts[0]);
        typeof(LocalGameUI).GetMethod("ShowAbilityCard",BindingFlags.Instance|BindingFlags.NonPublic)
            .Invoke(ui,new object[]{new OwnedUnit(unit),false,true,parts.Length>1?int.Parse(parts[1]):-1,null});
        return GameText.AbilityDescription(parts.Length>1&&int.Parse(parts[1])>=0?unit.Tricks[int.Parse(parts[1])]:unit.BaseAbility,unit);
    }
    public static string ExportDescriptions()
    {
        var config=AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/resources/MonsterPouch/MatchConfig.asset");
        // Resolve Character assets through the normal detached runtime catalogue.
        var runtime=config.CreateRuntimeConfig();
        try
        {
            runtime.hideFlags=HideFlags.HideAndDontSave;
            bool spanish=BalanceText.Spanish;
            var text=new StringBuilder(spanish
                ? "# Descripciones de personajes\n\nExportadas del balance guardado en el idioma seleccionado.\n\n"
                : "# Character descriptions\n\nExported from saved balance in the selected language.\n\n");
            foreach(var unit in runtime.Units)
            {
                text.AppendLine("## "+unit.DisplayName+"\n");
                foreach(var ability in new[]{unit.BaseAbility}.Concat(unit.Tricks).Where(a=>a!=null))
                    text.AppendLine("- **"+GameText.AbilityName(ability,spanish)+":** "+GameText.AbilityDescription(ability,unit,spanish));
                text.AppendLine();
            }
            string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../docs/DESCRIPCIONES-PERSONAJES-20261008.md"));
            File.WriteAllText(path,text.ToString());return path;
        }
        finally { Object.DestroyImmediate(runtime); }
    }
}
