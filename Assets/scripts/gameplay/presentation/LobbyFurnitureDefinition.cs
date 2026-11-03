using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    [CreateAssetMenu(menuName = "Monster Pouch/Environment/Lobby furniture")]
    public sealed class LobbyFurnitureDefinition : ScriptableObject
    {
        public Sprite Shelf, Pedestal, Cornice, Post;
        public Sprite UpperBacking, Backing, Crest;

        [Tooltip("Fraction of the pedestal image height where the feet touch its upper surface.")]
        [Range(0, 1)] public float PedestalFootHeight = .24f;
        public float PostWidth = 24, ShelfHeight = 34, CorniceHeight = 30;

        [Header("Brief cabinet palette")]
        [Range(0, 1)] public float Saturation = .68f;
        public Color Tint = new Color(.96f, .96f, .94f, 1);

        [Header("Brief cabinet support")]
        [Min(1)] public float FeaturedPedestalWidth = 286;
        [Tooltip("Shelf depth at which the front of a pedestal rests, measured from the sprite top.")]
        [Range(0, 1)] public float ShelfSurfaceHeight = .55f;
    }
}
