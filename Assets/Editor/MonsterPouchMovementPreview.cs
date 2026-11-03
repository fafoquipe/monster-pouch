using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEngine;

// Temporary runtime setups for visual QA only. No scene, deck or balance data is saved.
public static class MonsterPouchMovementPreview
{
    static LocalGameUI UI() => UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();

    public static void Deck()
    {
        var ui = UI(); ui.Match.Menu(); ui.OpenDecks();
        MonsterPouchPlayInspection.Capture("sept26-deck");
    }

    public static string Board()
    {
        var ui = UI(); var match = ui.Match;
        match.StartMatch("bugaloo", new[] { "flo", "tokoro", "blotan", "atori", "atong", "aky", "kayon" }, 2619);
        match.enabled = false;
        match.Player.AddCombatCoins(100);
        int placed = 0;
        for (int pass = 0; pass < 5; pass++)
        {
            for (int slot = 0; slot < match.Player.Offers.Count; slot++)
            {
                var offer = match.Player.Offers[slot];
                if (offer == null || match.Player.Owned.ContainsKey(offer.UnitId)) continue;
                var cell = match.Board.GetCell(placed % 3 * 2, 6 + placed / 3 * 2);
                if (match.BuyAndPlace(slot, offer.Token, cell)) placed++;
            }
            if (placed >= 7 || !match.Reroll(match.Player.OfferVersion)) break;
        }
        foreach (var actor in match.Actors.Values)
            actor.GetComponent<UnitPresentation>()?.Face(1, 0);
        ui.RenderPage();
        MonsterPouchPlayInspection.Capture("sept26-board");
        return "QA Whelps=" + placed;
    }

    public static void Walk()
    {
        var ui = UI();
        foreach (var actor in ui.Match.Actors.Values)
        {
            var view = actor.GetComponent<UnitPresentation>();
            view.Move(actor.transform.position, actor.transform.position + Vector3.right * .7f, 1.2f);
        }
        ui.StartCoroutine(CaptureWalk());
    }

    static IEnumerator CaptureWalk()
    {
        yield return new WaitForSeconds(.25f);
        MonsterPouchPlayInspection.Capture("sept26-walk-a");
        yield return new WaitForSeconds(.25f);
        MonsterPouchPlayInspection.Capture("sept26-walk-b");
    }

    public static string Cone()
    {
        var effects = UnityEngine.Object.FindFirstObjectByType<DocumentedBattleEffects>();
        var sprite = (Sprite)typeof(DocumentedBattleEffects).GetMethod("PixelSprite", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(effects, new object[] { "cone" });
        var target = RenderTexture.GetTemporary(200, 200, 0, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var copy = new Texture2D(200, 200, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(sprite.texture, target);
            RenderTexture.active = target;
            copy.ReadPixels(new Rect(0, 0, 200, 200), 0, 0); copy.Apply();
            string path = Path.GetFullPath("Logs/local-validation/screenshots/sept26-cone.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, copy.EncodeToPNG());
            return path;
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(copy); }
    }
}
