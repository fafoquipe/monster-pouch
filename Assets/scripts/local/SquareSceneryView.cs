using UnityEngine;
using UnityEngine.UI;
namespace MonsterPouch.Local
{
    public sealed class SquareSceneryView : MonoBehaviour
    {
        Material ownedMaterial;
        public void Configure(Image image, Sprite sprite)
        {
            ownedMaterial=PixelScenery.CreateMaterial(); image.material=ownedMaterial;
            image.sprite=sprite;image.color=Color.white;image.raycastTarget=false;
            PixelScenery.Configure(ownedMaterial,sprite);
            if(ownedMaterial!=null)
            {
                ownedMaterial.SetVector("_CropRect",new Vector4(0,0,1,1));
                bool park=CardTableTheme.Lobby==TableStyle.Default;
                bool painted=CardTableTheme.NationArtwork(CardTableTheme.Lobby)?.CombatPanorama!=null;
                bool nation=CardTableTheme.IsNation(CardTableTheme.Lobby)||painted;
                ownedMaterial.SetFloat("_Saturation",park||nation?1f:.58f);
                ownedMaterial.SetFloat("_Brightness",nation?1f:park?1.08f:.77f);
                ownedMaterial.SetFloat("_Contrast",painted?1f:park?1.06f:1f);
                ownedMaterial.SetFloat("_WarmAccentBoost",nation?0f:park?.38f:.22f);
                ownedMaterial.SetFloat("_RoyalBannerPalette",CardTableTheme.IsRoyal&&!painted?1f:0f);
                ownedMaterial.SetFloat("_HotelCarpetRed",CardTableTheme.IsLounge&&!painted?.75f:0f);
                ownedMaterial.SetFloat("_WarmTint",CardTableTheme.Lobby==TableStyle.GrandCasaHotel&&!painted?.32f:0f);
            }
        }
        void OnDestroy(){if(ownedMaterial!=null)Destroy(ownedMaterial);}
    }
    // A single square art plane; the camera reveals its sides without deforming it.
    public sealed class SquareArenaRoom : MonoBehaviour
    {
        SpriteRenderer drawing; Material material; Camera view;
        public void Configure(Camera camera, TableStyle style)
        {
            view=camera;
            if(drawing==null)
            {
                var go=new GameObject("Square environment");go.transform.SetParent(transform,false);
                drawing=go.AddComponent<SpriteRenderer>();drawing.sortingOrder=1;
                material=PixelScenery.CreateMaterial();drawing.sharedMaterial=material;
            }
            // Park and royal scenery own their original portrait composition and floor extensions.
            // A second square illustration behind the board changes the authored perspective.
            drawing.enabled=style==TableStyle.GrandCasaHotel&&MonsterPouch.Gameplay.Presentation.EnvironmentStyle.Find("lounge")?.Scenery==null;
            if(!drawing.enabled)return;
            var definition=MonsterPouch.Gameplay.Presentation.EnvironmentStyle.Find(CardTableTheme.MapId(style));
            var sprite=definition!=null?definition.MatchBackground:Resources.Load<Sprite>("MonsterPouch/UI/Scenery/"+CardTableTheme.MapId(style)+"-square");
            drawing.sprite=sprite;drawing.enabled=sprite!=null;
            if(style==TableStyle.GrandCasaHotel&&sprite!=null)
            {
                // Repeat the original felt floor at native scale; preserve the authored props/frame.
                drawing.drawMode=SpriteDrawMode.Tiled;drawing.size=Vector2.one*120;
                drawing.transform.localScale=Vector3.one;drawing.transform.position=Vector3.zero;
                drawing.sharedMaterial=Resources.Load<Material>("MonsterPouch/UnitSpriteMaterial");return;
            }
            drawing.drawMode=SpriteDrawMode.Simple;drawing.sharedMaterial=material;
            PixelScenery.Configure(material,sprite);
            if(material!=null)material.SetFloat("_Saturation",.76f);
            if(material!=null)material.SetVector("_CropRect",new Vector4(0,0,1,1));
            if(sprite==null)return;
            float size=1440f*(camera.orthographicSize*2/960f);
            drawing.transform.position=new Vector3(camera.transform.position.x,camera.transform.position.y,0);
            drawing.transform.localScale=new Vector3(size/sprite.bounds.size.x,size/sprite.bounds.size.y,1);
        }
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
}
