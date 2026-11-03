using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyPedestalGraphic : MaskableGraphic
    {
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Box(r.x + 8, r.y - 5, r.width - 16, r.height, new Color(.10f, .12f, .14f, .40f));
            Box(r.x + 4, r.y, r.width - 8, r.height * .65f, new Color(.22f, .23f, .23f));
            Box(r.x, r.y + r.height * .35f, r.width, r.height * .5f, new Color(.38f, .36f, .31f));
            Box(r.x + 7, r.y + r.height * .72f, r.width - 14, r.height * .28f, new Color(.56f, .54f, .46f));
            void Box(float x, float y, float w, float h, Color c)
            {
                int n = vh.currentVertCount;
                vh.AddVert(new Vector2(x, y), c, Vector2.zero);
                vh.AddVert(new Vector2(x, y + h), c, Vector2.zero);
                vh.AddVert(new Vector2(x + w, y + h), c, Vector2.zero);
                vh.AddVert(new Vector2(x + w, y), c, Vector2.zero);
                vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
            }
        }
    }

    public sealed partial class LocalGameUI
    {
        LobbyFurnitureDefinition furniture, cabinetFurniture;
        string cabinetStyle;
        LobbyFurnitureDefinition Furniture => furniture != null ? furniture :
            furniture = Resources.Load<LobbyFurnitureDefinition>("MonsterPouch/Styles/LobbyFurniture");

        LobbyFurnitureDefinition CabinetFurniture
        {
            get
            {
                string style = CardTableTheme.LobbyDefinition != null ? CardTableTheme.LobbyDefinition.Id : "classic";
                if (cabinetFurniture == null || cabinetStyle != style)
                {
                    cabinetStyle = style;
                    cabinetFurniture = Resources.Load<LobbyFurnitureDefinition>("MonsterPouch/Styles/Furniture/" + style);
                }
                return cabinetFurniture != null ? cabinetFurniture : Furniture;
            }
        }

        Material CabinetMaterial(LobbyFurnitureDefinition skin,bool backing=false)
        {
            var palette = page.GetComponentInChildren<LobbyCabinetPalette>();
            if (palette == null)
            {
                // Page children move together when a swipe preview becomes the live
                // page. Keep the material owner with that content, not on the
                // temporary page root that is destroyed at the end of the swipe.
                var owner = Panel(page, "Cabinet palette", new Rect(), Color.clear, false);
                palette = owner.gameObject.AddComponent<LobbyCabinetPalette>();
            }
            return backing?palette.GetBacking(skin):palette.Get(skin);
        }

        Image FurnitureImage(string name, Sprite sprite, Rect rect, bool tiled = false, LobbyFurnitureDefinition skin = null)
        {
            if (sprite == null) return null;
            var target = Panel(page, name, rect, Color.clear, false);
            var image = target.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.raycastTarget = false;
            image.type = tiled ? UnityEngine.UI.Image.Type.Tiled : UnityEngine.UI.Image.Type.Simple;
            if (skin != null)
            {
                image.color = Color.white;
                image.material = CabinetMaterial(skin);
                if (tiled)
                {
                    // Preserve the source depth of a shelf and width of a post.
                    // Only the long axis repeats; caps and ornaments keep their proportions.
                    bool post = sprite == skin.Post;
                    // Horizontal wood grain is continuous; only the repeating post ornaments tile.
                    image.type=post?UnityEngine.UI.Image.Type.Tiled:UnityEngine.UI.Image.Type.Sliced;
                    float source = post ? sprite.rect.width : sprite.rect.height;
                    float targetSize = post ? rect.width : rect.height;
                    float reference = image.canvas != null ? image.canvas.referencePixelsPerUnit : 100;
                    image.pixelsPerUnitMultiplier = source / Mathf.Max(1, targetSize) * reference / sprite.pixelsPerUnit;
                }
            }
            else
                image.color = CardTableTheme.LobbyDefinition != null ? CardTableTheme.LobbyDefinition.FurnitureTint : new Color(.89f, .87f, .81f, 1);
            if(!tiled) {
                var shadow = target.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(.04f, .03f, .025f, .32f);
                shadow.effectDistance = new Vector2(2, -4); shadow.useGraphicAlpha = true;
            }
            return image;
        }

        void Podium(string name, float center, float foot, float width, LobbyFurnitureDefinition skin = null)
        {
            var definition = skin != null ? skin : Furniture;
            if (definition?.Pedestal == null) return;
            var sprite = definition.Pedestal;
            float height = width * sprite.rect.height / sprite.rect.width;
            FurnitureImage(name, sprite, new Rect(center - width * .5f, foot - height * definition.PedestalFootHeight, width, height), false, skin);
        }

        void Pedestal(Rect rect)
        {
            // Reuse the active environment's existing pedestal and palette.
            var skin = CabinetFurniture;
            if (skin?.Pedestal != null) { Podium("Home wooden pedestal", rect.center.x, rect.y + 7, rect.width * .85f, skin); return; }
            var p = Panel(page, "Figure platform", rect, Color.clear, false);
            p.gameObject.AddComponent<LobbyPedestalGraphic>();
        }

        void CabinetBacking(string name, Sprite sprite, Rect rect, LobbyFurnitureDefinition skin)
        {
            if (sprite == null) return;
            var target = Panel(page, name, rect, Color.clear, false);
            var graphic = target.gameObject.AddComponent<LobbyCabinetBacking>();
            graphic.Configure(sprite, CabinetMaterial(skin,true));
        }

        void DisplayCabinet()
        {
            var skin = CabinetFurniture;
            if (skin == null || skin.Pedestal == null || skin.Shelf == null) return;
            // The original character foot coordinates stay unchanged on every screen.
            float aspect = skin.Pedestal.rect.height / skin.Pedestal.rect.width;
            float ShelfY(float foot, float width) =>
                foot + width * aspect * (1 - skin.PedestalFootHeight) - skin.ShelfHeight * skin.ShelfSurfaceHeight;
            float upper = ShelfY(291, skin.FeaturedPedestalWidth);
            float middle = ShelfY(514, 110);
            float lower = ShelfY(710, 130);
            float depth = skin.ShelfHeight * skin.ShelfSurfaceHeight;

            // The backing, supports, shelves, crest and podiums are separate layers.
            // Extra screen area shows the existing room; the cabinet remains centered.
            CabinetBacking("Cabinet upper lining", skin.UpperBacking, new Rect(34, 147, 472, upper + depth - 147), skin);
            var lining=CardTableTheme.IsLounge?skin.UpperBacking??skin.Backing:skin.Backing;
            CabinetBacking("Cabinet middle lining", lining,
                new Rect(34, upper + skin.ShelfHeight - 5, 472, middle + depth - upper - skin.ShelfHeight + 5), skin);
            CabinetBacking("Cabinet lower lining", lining,
                new Rect(34, middle + skin.ShelfHeight - 5, 472, lower + depth - middle - skin.ShelfHeight + 5), skin);

            float bottom = lower + skin.ShelfHeight;
            FurnitureImage("Cabinet left post", skin.Post, new Rect(16, 141, skin.PostWidth, bottom - 141), true, skin);
            FurnitureImage("Cabinet right post", skin.Post, new Rect(524 - skin.PostWidth, 141, skin.PostWidth, bottom - 141), true, skin);
            FurnitureImage("Cabinet cornice", skin.Cornice, new Rect(16, 128, 508, skin.CorniceHeight), true, skin);
            if(CardTableTheme.Lobby==TableStyle.GrandCasaHotel) {
                var crest=Panel(page,"Cabinet crest",new Rect(68,12,404,128),Color.clear,false);
                crest.gameObject.AddComponent<LobbyCabinetCrest>().AddLettering();
            } else {
                var crest=FurnitureImage("Cabinet crest",skin.Crest,new Rect(70,12,400,126),false,skin);
                if(crest!=null) {
                    crest.preserveAspect=true;crest.rectTransform.pivot=Vector2.one*.5f;
                    crest.rectTransform.anchoredPosition=new Vector2(270,-75);
                }
            }
            FurnitureImage("Cabinet upper shelf", skin.Shelf, new Rect(16, upper, 508, skin.ShelfHeight), true, skin);
            FurnitureImage("Cabinet middle shelf", skin.Shelf, new Rect(16, middle, 508, skin.ShelfHeight), true, skin);
            FurnitureImage("Cabinet lower shelf", skin.Shelf, new Rect(16, lower, 508, skin.ShelfHeight), true, skin);

            if (HasLobbyCharacter(chosen)) Podium("Monster wooden pedestal", 270, 291, skin.FeaturedPedestalWidth, skin);
            for (int i = 0; i < 4; i++)
                if (HasLobbyCharacter(decks.Current.Whelps[i])) Podium("Whelp wooden pedestal " + i, 84 + i * 124, 514, 110, skin);
            for (int i = 0; i < 3; i++)
                if (HasLobbyCharacter(decks.Current.Whelps[i + 4])) Podium("Whelp wooden pedestal " + (i + 4), new[] { 100f, 270, 440 }[i], 710, 130, skin);
        }
    }
}
