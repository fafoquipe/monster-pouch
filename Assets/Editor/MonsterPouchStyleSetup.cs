using System.IO;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Progression;
using UnityEditor;
using UnityEngine;
public static class MonsterPouchStyleSetup
{
    public static string Apply()
    {
        const string folder="Assets/resources/MonsterPouch/Styles";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        string[] ids={"classic","lounge","royal"},names={"Parque de Bolas","Grand Casa Hotel","Campo Real"};
        for(int i=0;i<ids.Length;i++)
        {
            string path=folder+"/"+ids[i]+".asset";if(AssetDatabase.LoadAssetAtPath<EnvironmentStyle>(path)!=null)continue;
            var style=ScriptableObject.CreateInstance<EnvironmentStyle>();style.Id=ids[i];style.DisplayName=names[i];
            style.ProgressionRole=i==0?TrophyBoardRole.InitialProgression:i==1?TrophyBoardRole.PaidCosmetic:TrophyBoardRole.Progression;
            style.LobbyBackground=style.MatchBackground=style.Preview=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/resources/MonsterPouch/UI/Scenery/"+ids[i]+"-square.png");
            style.Panel=i==2?new Color(.27f,.18f,.17f):i==1?new Color(.14f,.24f,.24f):new Color(.16f,.22f,.27f);
            style.Accent=i==2?new Color(.47f,.28f,.26f):i==1?new Color(.28f,.40f,.38f):new Color(.30f,.39f,.47f);
            style.Ambient=i==2?new Color(.87f,.85f,.81f):i==1?new Color(.80f,.87f,.85f):new Color(.84f,.88f,.89f);
            AssetDatabase.CreateAsset(style,path);
        }
        AssetDatabase.SaveAssets();return "Three editable environment styles created; lobby preference remains local, match map keeps network IDs.";
    }
}
