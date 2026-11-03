using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;
public static class MonsterPouchLobbyThemeSetup
{
    public static string Apply()
    {
        foreach(string id in new[]{"classic","lounge","royal"})
        {
            var style=AssetDatabase.LoadAssetAtPath<EnvironmentStyle>("Assets/resources/MonsterPouch/Styles/"+id+".asset");
            Undo.RecordObject(style,"Restore lobby theme palette");
            style.Button=id=="classic"?new Color(.24f,.40f,.56f):id=="lounge"?new Color(.13f,.35f,.31f):new Color(.55f,.25f,.28f);
            style.PrimaryButton=id=="classic"?new Color(.85f,.69f,.38f):id=="lounge"?new Color(.78f,.65f,.39f):new Color(.84f,.64f,.32f);
            style.ButtonText=new Color(.96f,.92f,.82f);style.PrimaryButtonText=new Color(.18f,.16f,.14f);
            style.FurnitureTint=id=="classic"?new Color(.89f,.87f,.81f):id=="lounge"?new Color(.78f,.86f,.79f):new Color(.91f,.79f,.70f);
            style.FurnitureLining=id=="classic"?new Color(.19f,.25f,.30f):id=="lounge"?new Color(.10f,.22f,.20f):new Color(.30f,.13f,.17f);
            EditorUtility.SetDirty(style);
        }
        AssetDatabase.SaveAssets();return "Three independent lobby palettes: blue/play park, green/hotel, burgundy/royal; buttons and furniture follow lobby selection.";
    }
}
