using System;
using MonsterPouch.Gameplay.Match;
using UnityEditor;

public static class MonsterPouchTsuPolish
{
    public static string UpdateAttackDescription()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Sal de Play Mode antes de actualizar la descripción.");
        var config=AssetDatabase.LoadAssetAtPath<MatchConfig>("Assets/resources/MonsterPouch/MatchConfig.asset");
        var tsu=config.Get("tsu");var source=DocumentedRoster.CreateTsu();
        tsu.BasicAttackName=source.BasicAttackName;tsu.BasicAttackDescription=source.BasicAttackDescription;
        EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);
        return "TSU: nombre y descripción del ataque actualizados. Estadísticas y mejoras conservadas.";
    }
}
