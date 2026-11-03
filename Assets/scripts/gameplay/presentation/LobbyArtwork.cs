using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    // Cosmetic identity; it need not register a combat map or progression stage.
    [CreateAssetMenu(menuName = "Monster Pouch/Lobby artwork")]
    public sealed class LobbyArtwork : ScriptableObject
    {
        public string Id;
        public Sprite Background;
        public Sprite Pedestal;
        [Tooltip("Surface center where the character's feet rest; normalized from the top left of the pedestal sprite.")]
        public Vector2 PedestalFootAnchor = new Vector2(.5f, .23f);
        [Tooltip("Pedestal width in the existing 540 x 960 UI. Character scale is independent.")]
        [Min(1)] public float DisplayPedestalWidth = 256;

        public static LobbyArtwork Find(string mapId) =>
            Resources.Load<LobbyArtwork>("MonsterPouch/Styles/Lobbies/" + mapId);

        // Fill the physical viewport uniformly, cropping at its edges. Only the excess
        // width beyond 21:9 may form side margins; characters never affect framing.
        public const float MaximumCoverAspect = 21f / 9f;
        public Rect Frame(Rect viewport)
        {
            return Background == null ? default : Frame(viewport, Background.rect.size);
        }

        // Shared by the startup background; keep one coverage policy for both screens.
        public static Rect Frame(Rect viewport, Vector2 sourceSize)
        {
            if (viewport.width <= 0 || viewport.height <= 0 || sourceSize.x <= 0 || sourceSize.y <= 0) return default;
            float aspect = sourceSize.x / sourceSize.y;
            float coveredWidth = Mathf.Min(viewport.width, viewport.height * MaximumCoverAspect);
            float height = Mathf.Max(viewport.height, coveredWidth / aspect);
            float width = height * aspect;
            return new Rect(viewport.center.x - width * .5f,
                viewport.center.y - height * .5f, width, height);
        }

        public Rect PedestalFrame(Vector2 feet, float width = 0)
        {
            if (Pedestal == null) return default;
            if (width <= 0) width = DisplayPedestalWidth;
            float height = width * Pedestal.rect.height / Pedestal.rect.width;
            return new Rect(feet.x - width * PedestalFootAnchor.x,
                feet.y - height * PedestalFootAnchor.y, width, height);
        }
    }
}
