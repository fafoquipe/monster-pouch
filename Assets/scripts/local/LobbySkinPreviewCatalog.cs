using UnityEngine;

namespace MonsterPouch.Local
{
    // Colors copied from the existing style/nation assets. No asset references: reading
    // card metadata must not load combat panoramas, miniature maps or lobby pedestals.
    internal static class LobbySkinPreviewCatalog
    {
        internal static string ResourcePath(string id) => "MonsterPouch/UI/LobbyPreviews/lobby-preview-" + id;
        internal static Color[] Palette(string id)
        {
            switch(id)
            {
                case "classic": return new[] { new Color(0.16f,0.22f,0.27f,1f), new Color(1f,1f,1f,0f), new Color(0.03f,0.05f,0.06f,0f), new Color(1f,1f,1f,1f) };
                case "lounge": return new[] { new Color(0.14f,0.24f,0.24f,1f), new Color(1f,1f,1f,0f), new Color(0.03f,0.05f,0.06f,0f), new Color(1f,1f,1f,1f) };
                case "royal": return new[] { new Color(0.27f,0.18f,0.17f,1f), new Color(1f,1f,1f,0f), new Color(0.03f,0.05f,0.06f,0f), new Color(1f,1f,1f,1f) };
                case "naturalis": return new[] { new Color(0.03f,0.15f,0.43f,1f), new Color(1f,1f,1f,0f), new Color(0.03f,0.05f,0.06f,0f), new Color(1f,1f,1f,1f) };
                case "aquarion": return new[] { new Color(0.03f,0.15f,0.43f,1f), new Color(1f,1f,1f,0f), new Color(0.03f,0.05f,0.06f,0f), new Color(1f,1f,1f,1f) };
                case "geothar": return new[] { new Color(0.03f,0.15f,0.43f,1f), new Color(1f,1f,1f,0f), new Color(0.03f,0.05f,0.06f,0f), new Color(1f,1f,1f,1f) };
                case "volkanar": return new[] { new Color(0.03f,0.15f,0.43f,1f), new Color(1f,1f,1f,0f), new Color(0.03f,0.05f,0.06f,0f), new Color(1f,1f,1f,1f) };
                case "casino-night": return new[] { new Color(0.098f,0.133f,0.22f,1f), new Color(1f,1f,1f,0f), new Color(0.03f,0.05f,0.06f,0f), new Color(1f,1f,1f,1f) };
                case "aireola": return new[] { new Color(0.03f,0.15f,0.43f,1f), new Color(1f,1f,1f,0f), new Color(0.03f,0.05f,0.06f,0f), new Color(1f,1f,1f,1f) };
                default: return Palette("naturalis");
            }
        }
    }
}
