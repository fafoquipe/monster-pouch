using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Status;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Renders the actual HUD components in an isolated scene. No balance or scene asset changes.
public static class MonsterPouchCombatHudReview
{
    public const string Folder = "docs/combat-hud-20261017";
    static Font font;
    static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform; LocalGameUI.SetRect(rt, new Rect(x, y, w, h)); return rt;
    }
    static void Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color)
    {
        var t = Rect(parent, text, x, y, w, h).gameObject.AddComponent<Text>(); t.font = font;
        t.text = text; t.fontSize = size; t.color = color; t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
    }
    static void Panel(Transform parent, float x, float y, float w, float h, Color color)
    { Rect(parent, "panel", x, y, w, h).gameObject.AddComponent<Image>().color = color; }

    [MenuItem("Monster Pouch/Review/Combat HUD")]
    public static void Capture()
    {
        Directory.CreateDirectory(Folder);
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Combat HUD review"); SceneManager.MoveGameObjectToScene(root, scene);
        var target = new RenderTexture(1200, 960, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(1200, 960, TextureFormat.RGBA32, false);
        var old = RenderTexture.active;
        try
        {
            var cameraGo = new GameObject("Review camera"); cameraGo.transform.SetParent(root.transform);
            var camera = cameraGo.AddComponent<Camera>(); camera.scene = scene; camera.orthographic = true; camera.orthographicSize = 480;
            camera.transform.position = new Vector3(600, 480, -100); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .067f, .115f); camera.targetTexture = target; camera.allowHDR = false;
            var canvasGo = new GameObject("Review canvas", typeof(RectTransform), typeof(Canvas)); canvasGo.transform.SetParent(root.transform);
            var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
            var rt = (RectTransform)canvas.transform; rt.sizeDelta = new Vector2(1200, 960); rt.position = new Vector3(600, 480, 0);
            Label(rt, "MONSTER POUCH  /  ESTADOS DE COMBATE", 36, 22, 1120, 40, 29, Color.white);
            Label(rt, "Espada roja: daño  ·  Espadas azules: velocidad  ·  Aro: duración restante", 36, 67, 1120, 28, 18, new Color(.62f, .76f, .9f));
            Sample(rt, "trimol", "TRIMOL · PASIVA PREPARADA", 36, 120, BoardSide.Blue, 3,
                new[] { new ActiveStatus("attack-up", -1), new ActiveStatus("haste", -1) }, new[] { new PassiveProgress("trimol-boulder", 3, 4) });
            Sample(rt, "blotan", "BLOTAN · INVISIBILIDAD", 430, 120, BoardSide.Blue, 3,
                new[] { new ActiveStatus("stealth", 4) }, Array.Empty<PassiveProgress>(), .45f);
            Sample(rt, "popow", "POPOW · ENERGÍA + PASIVA", 824, 120, BoardSide.Red, 0,
                new[] { new ActiveStatus("guard", -1) }, new[] { new PassiveProgress("popow-sweep", 1, 3) });

            Label(rt, "CICLO DE TRIMOL", 36, 395, 300, 30, 20, Color.white);
            string[] captions = { "Inicio", "1 golpe", "2 golpes", "Siguiente: roca", "Nuevo ciclo" };
            for (int i = 0; i < 5; i++)
            {
                var display = Rect(rt, "cycle", 66 + i * 224, 455, 52, 28); display.localScale = Vector3.one * 2;
                var statuses = display.gameObject.AddComponent<UnitStatusBadges>();
                statuses.Present(Array.Empty<ActiveStatus>(), new[] { new PassiveProgress("trimol-boulder", i == 4 ? 0 : i, 4) }, .2f);
                Label(rt, captions[i], 36 + i * 224, 483, 210, 25, 17, new Color(.77f, .83f, .93f));
            }
            Label(rt, "ICONOS DE ESTADO · misma familia visual para todo el plantel", 36, 546, 1120, 30, 20, Color.white);
            var ids = new[] { "attack-up", "haste", "stealth", "guard", "shield", "reflect", "stun", "electric-stun", "root", "slow", "burn", "anti-heal", "weakness", "sleep", "taunt", "invulnerable", "pull", "chocolate", "control-immunity" };
            var names = new[] { "Daño", "Velocidad", "Invisible", "Defensa", "Escudo", "Reflejo", "Aturdido", "Electrocutado", "Inmóvil", "Lentitud", "Quemadura", "Anticuración", "Vulnerable", "Sueño", "Provocación", "Invulnerable", "Arrastre", "Chocolate", "Inamovible" };
            for (int i = 0; i < ids.Length; i++)
            {
                float x = 36 + i % 6 * 191, y = 600 + i / 6 * 82;
                var circle = Rect(rt, ids[i], x, y, 32, 32).gameObject.AddComponent<CombatBadgeRing>(); circle.Set(CombatHudSymbols.Tint(ids[i]), .72f, false, .2f, ids[i] == "control-immunity", false, ids[i] == "invulnerable");
                var symbol = Rect(circle.transform, "symbol", 6, 6, 20, 20).gameObject.AddComponent<Image>(); symbol.sprite = CombatHudSymbols.Get(ids[i]);
                symbol.enabled = ids[i] != "invulnerable";
                Label(rt, names[i], x + 40, y + 1, 148, 30, 15, new Color(.84f, .89f, .96f));
            }
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1200, 960), 0, 0); texture.Apply();
            File.WriteAllBytes(Folder + "/combat-hud-preview.png", texture.EncodeToPNG());
            File.WriteAllText(Folder + "/capture.txt", "Rendered real UnitVitalsUI, UnitStatusBadges and CombatBadgeRing components in an isolated Unity preview scene.");
        }
        finally { RenderTexture.active = old; Object.DestroyImmediate(texture); Object.DestroyImmediate(target); Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
    }
    static void Sample(RectTransform parent, string id, string title, float x, float y, BoardSide side, int stars,
        ActiveStatus[] active, PassiveProgress[] progress, float remaining = 1)
    {
        Panel(parent, x, y, 340, 248, new Color(.075f, .11f, .18f));
        Label(parent, title, x + 16, y + 14, 324, 25, 16, new Color(.81f, .88f, .98f));
        var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>("Assets/resources/MonsterPouch/Characters/" + id + "/Character.asset");
        var definition = character.CreateRuntimeDefinition(); var owned = new OwnedUnit(definition);
        for (int i = 0; i < stars; i++) owned.Tricks[i] = true;
        var actorGo = new GameObject("review actor"); actorGo.transform.SetParent(parent); var actor = actorGo.AddComponent<WhelpUnit>();
        actor.Initialize(id, side, UnitStats.FromDefinition(definition, owned)); actor.ApplyDamage(Mathf.RoundToInt(actor.BaseStats.MaxHealth * .28f)); actor.AddEnergy(actor.BaseStats.EnergyMax * .55f);
        var art = Rect(parent, id, x + 99, y + 130, 142, 105).gameObject.AddComponent<Image>(); art.sprite = character.Visuals.Portrait; art.preserveAspect = true;
        if (id == "blotan") art.color = new Color(.55f, .85f, 1, .6f);
        var bar = Rect(parent, "health-" + id, x + 110, y + 87, 52, 28); bar.localScale = Vector3.one * 2.3f;
        bar.gameObject.AddComponent<UnitVitalsUI>().Refresh(actor, owned, 1, BoardSide.Blue);
        var statuses = bar.gameObject.AddComponent<UnitStatusBadges>(); statuses.Present(active, progress, .2f);
        if (remaining < 1) statuses.Present(active.Select(s => new ActiveStatus(s.Id, s.Remaining * remaining)).ToArray(), progress, .2f);
    }
}
