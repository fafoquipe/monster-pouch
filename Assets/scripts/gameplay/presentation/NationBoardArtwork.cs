using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    // Art review data only. This type does not register a selectable map or a progression reward.
    [CreateAssetMenu(menuName = "Monster Pouch/Art review/Nation board artwork")]
    public sealed class NationBoardArtwork : ScriptableObject
    {
        public string NationId, DisplayName;
        public Sprite Miniature, LobbyBackground;
        // One continuous painted environment. Coordinates are normalized from the TOP LEFT.
        // The source's quiet courtyard is mapped to the existing logical board, never vice versa.
        public Sprite CombatPanorama;
        public Rect PlayableArea = new Rect(.42105263f, .30851064f, .15071770f, .37340426f);
        // Phones have a separately composed environment around the same logical courtyard.
        public Sprite CombatPortrait;
        public Rect PortraitPlayableArea = new Rect(.19286224f, .280444f, .6142755f, .394726f);
        // Optional painted grid intersections, row-major, normalized from the top left.
        // Register original artwork to the existing cells; never resize the logical board.
        public Vector2[] PaintedGrid = new Vector2[0];
        public Color GridLine = new Color(.035f, .075f, .075f, .48f);
        [Range(.2f, 2)] public float GridLineWidth = 1.35f;
        public Sprite FloorTexture, TileTexture, HorizontalFrame, VerticalFrame, Backdrop, FlowerCluster;
        public Color Light = new Color(.72f, .82f, .62f, 1);
        public Color Dark = new Color(.40f, .61f, .43f, 1);
        public Color FrameTint = Color.white;
        public Color FloorTint = Color.white;
        [Min(16)] public float FloorTileWidth = 160;
        [Min(16)] public float BackdropWidth = 320;
        [Min(8)] public float FlowerWidth = 36;
    }
}
