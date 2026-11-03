using System;
using UnityEngine;
namespace MonsterPouch.Gameplay.Presentation
{
    public enum SceneryAnchor { Room, BoardTop, BoardLeft, BoardRight, BoardBottom }
    [Serializable] public sealed class SceneryElement
    {
        public string Name;
        public Sprite Sprite;
        public SceneryAnchor Anchor;
        [Tooltip("Offset in portrait design units; X right, Y down. Position is the foot of the object.")]
        public Vector2 Foot;
        [Min(1)] public float Width=100;
        [Range(0,.5f)] public float BottomPadding;
        public bool FlipX;
        [Range(-45,45)] public float Rotation;
        [Range(0,1)] public float Shadow=.18f;
        public int Order=5;
        [Range(0,1)] public float LightStrength;
        [Min(1)] public float LightRadius=260;
        public Color LightColor=new Color(1,.84f,.60f);
    }
    [CreateAssetMenu(menuName="Monster Pouch/Environment/Modular arena")]
    public sealed class ArenaSceneryDefinition:ScriptableObject
    {
        public Color Floor=new Color(.28f,.53f,.55f);
        public Color Wall=new Color(.55f,.54f,.66f);
        public Color WallDetail=new Color(.48f,.47f,.59f);
        public Color Trim=new Color(.72f,.65f,.47f);
        public Color Carpet=new Color(.38f,.13f,.18f);
        public bool StoneWall,Carpets;
        public bool Tabletop;
        [Range(0,1)] public float Saturation=.80f;
        [Tooltip("Optional repeatable texture; its tiles keep a fixed world size on every display.")]
        public Sprite FloorTexture;
        [Min(16)] public float FloorTileWidth=160;
        public float FloorStartsAboveBoard=20;
        [Tooltip("Ground projection away from the main light, X right and Y down.")]
        public Vector2 ShadowDirection=new Vector2(.65f,1f);
        [Range(0,.6f)] public float ShadowLength=.26f;
        public SceneryElement[] Elements=Array.Empty<SceneryElement>();
        public int Revision {get;private set;}
        void OnValidate(){FloorTileWidth=Mathf.Max(16,FloorTileWidth);Revision++;}
    }
}
