using MonsterPouch.Gameplay.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // This presentation selection preserves the original preference key and values.
    // The Maps package owns TableStyle/FromMapId; the lobby never aliases Aireola to royal.
    public static class LobbyThemeSelection
    {
        public static string Id => LobbyThemeIdentity.Resolve(PlayerPrefs.GetString(LocalAccountPreferences.Key(CardTableTheme.LobbyPreferenceKey), "naturalis"));
        public static TableStyle BaseStyle(string id) => CardTableTheme.FromMapId(LobbyThemeIdentity.Resolve(id));
        public static string Name(string id) => id == "aireola" ? "AIREOLA" : id == "classic" ?
            (GameText.Spanish ? "PARQUE DE JUEGOS" : "PLAYGROUND") : CardTableTheme.Name(BaseStyle(id));
        public static void Select(string id)
        {
            if (!LobbyThemeIdentity.IsKnown(id)) throw new System.ArgumentException("Unknown lobby cosmetic", nameof(id));
            PlayerPrefs.SetString(LocalAccountPreferences.Key(CardTableTheme.LobbyPreferenceKey), id);
            PlayerPrefs.Save();
        }
    }

    public sealed partial class LocalGameUI
    {
        bool LobbyArtworkForMenuAvailable => LobbyArtwork.Find(LobbyThemeSelection.Id)?.Background != null;

        bool RenderPaintedLobby()
        {
            var artwork = LobbyArtwork.Find(LobbyThemeSelection.Id);
            if (artwork == null || artwork.Background == null) return false;

            screenBackdrop.sprite = null;
            screenBackdrop.color = Color.black;
            Vector2 lower = ToCanvas(Vector2.zero), upper = ToCanvas(new Vector2(Screen.width, Screen.height));
            Rect visible = Rect.MinMaxRect(lower.x, upper.y, upper.x, lower.y);
            var root = Panel(page, "Menu background", visible, Color.black, false);
            root.gameObject.AddComponent<RectMask2D>();

            // Include the entire physical display behind both the header and navigation.
            var frame = artwork.Frame(visible);
            frame.position -= visible.position;
            var plane = Panel(root, "Painted lobby artwork", frame, Color.clear, false);
            var image = plane.gameObject.AddComponent<Image>();
            image.sprite = artwork.Background;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            // The clean scene has no baked pedestal and no palette shader.
            return true;
        }

        bool HasLobbyCharacter(string id) => !string.IsNullOrEmpty(id) && art?.Get(id)?.Portrait != null;

        bool RenderLobbyPedestal(Vector2 feet)
        {
            var artwork = LobbyArtwork.Find(LobbyThemeSelection.Id);
            if (artwork == null || artwork.Pedestal == null) return false;
            var target = Panel(page, "Home character pedestal", artwork.PedestalFrame(feet), Color.clear, false);
            var image = target.gameObject.AddComponent<Image>();
            image.sprite = artwork.Pedestal;
            image.type = UnityEngine.UI.Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            return true;
        }
    }
}
