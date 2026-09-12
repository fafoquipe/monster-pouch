using System;
using UnityEditor;

public static class MonsterPouchSerializationRepair
{
    public static void RenameDataScripts()
    {
        Move("Assets/scripts/gameplay/match/match-config.cs","Assets/scripts/gameplay/match/MatchConfig.cs");
        Move("Assets/scripts/gameplay/presentation/unit-art-catalog.cs","Assets/scripts/gameplay/presentation/UnitArtCatalog.cs");
        AssetDatabase.Refresh();
    }
    static void Move(string from,string to)
    {
        if(AssetDatabase.LoadMainAssetAtPath(from)==null)return;
        string error=AssetDatabase.MoveAsset(from,to);
        if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
    }
}
