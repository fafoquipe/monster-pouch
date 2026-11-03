using MonsterPouch.Gameplay.Presentation;
using UnityEngine;

namespace MonsterPouch.Local
{
    // The furniture and lining share two materials owned by a child of the page.
    // The owner moves with the cabinet and releases them only when that content is removed.
    public sealed class LobbyCabinetPalette : MonoBehaviour
    {
        Material owned, backing;
        public Material Get(LobbyFurnitureDefinition skin)
        {
            if (owned == null)
            {
                owned = PixelScenery.CreateMaterial();
                if (owned == null) return null;
                owned.name = "Brief furniture accents";
                owned.SetVector("_CropRect", new Vector4(0, 0, 1, 1));
                owned.SetFloat("_PixelSize", 1);
            }
            owned.SetFloat("_Saturation", skin.Saturation);
            owned.SetColor("_Color", skin.Tint);
            owned.SetFloat("_Brightness",1.06f);
            owned.SetFloat("_Contrast",1f);
            owned.SetFloat("_WarmAccentBoost",.27f);
            owned.SetFloat("_FabricRed",CardTableTheme.Lobby==TableStyle.GrandCasaHotel?1f:0f);
            return owned;
        }
        public Material GetBacking(LobbyFurnitureDefinition skin)
        {
            var furniture=Get(skin);
            if(furniture==null)return null;
            if(backing==null)backing=new Material(furniture){name="Brief velvet lining"};
            backing.SetFloat("_VelvetBacking",CardTableTheme.Lobby==TableStyle.GrandCasaHotel?1f:0f);
            return backing;
        }
        void OnDestroy()
        {
            if (owned != null) Destroy(owned);
            if (backing != null) Destroy(backing);
        }
    }
}
