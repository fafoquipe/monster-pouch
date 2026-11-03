using System.Collections.Generic;
using UnityEngine;
using System;
using MonsterPouch.Gameplay.Progression;
namespace MonsterPouch.Gameplay.Presentation
{
    [CreateAssetMenu(menuName="Monster Pouch/Environment style")]
    public sealed class EnvironmentStyle:ScriptableObject
    {
        [Tooltip("Existing network/save identity: classic, lounge or royal.")]
        public string Id,DisplayName;
        [Tooltip("Approved board progression role only; does not grant ownership or enable purchases.")]
        public TrophyBoardRole ProgressionRole;
        public TrophyBoardDefinition ToTrophyBoardDefinition()
        {
            if(string.IsNullOrWhiteSpace(Id)||Id!=Id.Trim())
                throw new InvalidOperationException("A stable environment ID is required.");
            if(ProgressionRole==TrophyBoardRole.Unspecified||!Enum.IsDefined(typeof(TrophyBoardRole),ProgressionRole))
                throw new InvalidOperationException("This environment has no approved progression role.");
            return new TrophyBoardDefinition {BoardId=Id,Role=ProgressionRole};
        }
        public Sprite LobbyBackground,MatchBackground,Preview;
        public ArenaSceneryDefinition Scenery;
        public Color Panel=new Color(.16f,.22f,.25f),Accent=new Color(.30f,.42f,.44f),Paper=new Color(.92f,.89f,.80f);
        public Color Border=new Color(.48f,.53f,.48f),Shine=new Color(.72f,.74f,.61f);
        public Color Button=new Color(.57f,.25f,.26f),PrimaryButton=new Color(.80f,.63f,.32f);
        public Color ButtonText=new Color(.96f,.91f,.78f),PrimaryButtonText=new Color(.18f,.16f,.14f);
        public Color FurnitureTint=new Color(.89f,.87f,.81f),FurnitureLining=new Color(.21f,.26f,.31f);
        public Color Ambient=new Color(.84f,.88f,.89f),Lamp=new Color(1,.88f,.69f);
        [Range(0,1)] public float WindowStrength=.12f,LampStrength=.12f;
        public Color LightAt(float x,float height)
        {
            float window=Mathf.Clamp01(height)*WindowStrength;
            float lamp=Mathf.Clamp01(Mathf.Abs(x))*LampStrength;
            return Color.Lerp(Color.Lerp(Ambient,Color.white,window),Lamp,lamp);
        }
        static readonly Dictionary<string,EnvironmentStyle> cached=new Dictionary<string,EnvironmentStyle>();
        public static EnvironmentStyle Find(string id)
        {
            if(!cached.TryGetValue(id,out var style))cached[id]=style=Resources.Load<EnvironmentStyle>("MonsterPouch/Styles/"+id);
            return style;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()=>cached.Clear();
    }
}
