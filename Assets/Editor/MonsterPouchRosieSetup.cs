using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class MonsterPouchRosieSetup
{
    const string Folder = "Assets/resources/MonsterPouch/Characters/rosie";
    const string Combat = "Assets/resources/MonsterPouch/Rosie/rosie-combat.png";
    const string Icons = "Assets/resources/MonsterPouch/Rosie/rosie-atlas.png";
    const string Defeat = "Assets/resources/MonsterPouch/Rosie/rosie-defeat.png";
    const string Abilities = "Assets/resources/MonsterPouch/Rosie/rosie-abilities.png";
    const string Lobby = "Assets/resources/MonsterPouch/UI/LobbyFrames/clips.json";
    [Serializable] public class Manifest { public LobbyFrameClip[] clips; }

    [MenuItem("Monster Pouch/Characters/Install Rosie")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Import(Combat, true); Import(Icons, false); Import(Defeat, true, true); Import(Abilities, false, false, true);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(Combat).OfType<Sprite>().ToDictionary(s => s.name);
        var projectilesSprites = AssetDatabase.LoadAllAssetsAtPath(Icons).OfType<Sprite>().ToDictionary(s => s.name);
        var icons = AssetDatabase.LoadAllAssetsAtPath(Abilities).OfType<Sprite>().ToDictionary(s => s.name);
        var defeat = AssetDatabase.LoadAllAssetsAtPath(Defeat).OfType<Sprite>().ToDictionary(s => s.name);
        var definition = DocumentedRoster.CreateRosie();
        foreach (var effect in new[] { definition.BaseAbility }.Concat(definition.Tricks))
        { effect.Parameters = AbilityBalance.Resolve(effect); effect.Description = AbilityBalance.Description(effect, definition); }
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Folder + "/Character.asset");
        if (character == null)
        {
            character = ScriptableObject.CreateInstance<CharacterDefinition>(); character.InitializeIdentity("rosie");
            character.General = definition;
            character.Description = "Pequeña y frágil, Rosie ataca con rositas y cuida a su equipo con una gran rosa curativa.";
            character.Attack = AssetDatabase.LoadAssetAtPath<CharacterAttack>(Folder + "/Attack.asset");
            if(character.Attack == null)
            {
                character.Attack = ScriptableObject.CreateInstance<CharacterAttack>(); character.Attack.Import(definition);
                AssetDatabase.CreateAsset(character.Attack, Folder + "/Attack.asset");
            }
            character.Ability = CreateAbility(definition.BaseAbility, "Ability", icons["rose"]);
            character.Upgrades = definition.Tricks.Select((t, i) => CreateAbility(t, "Upgrade" + (i + 1), icons[new[] { "haste", "shield", "immunity" }[i]])).ToArray();
            AssetDatabase.CreateAsset(character, Folder + "/Character.asset");
        }
        // Re-running the art import preserves the user's edited balance and prices.
        // Replace only the withdrawn third upgrade from Rosie's initial installation.
        if(character.Upgrades[2].Definition.EffectId == "rosie-full-bloom")
        {
            int cost = character.Upgrades[2].Definition.Cost;
            character.Upgrades[2].Definition = definition.Tricks[2];
            character.Upgrades[2].Definition.Cost = cost;
            character.General.Tricks[2] = character.Upgrades[2].Copy();
        }
        character.Ability.Icon = icons["rose"]; EditorUtility.SetDirty(character.Ability);
        for(int i=0;i<3;i++)
        {
            character.Upgrades[i].Icon = icons[new[]{"haste","shield","immunity"}[i]];
            EditorUtility.SetDirty(character.Upgrades[i]);
        }
        var art = new UnitArt
        {
            Id = "rosie", Portrait = sprites["idle-4"], WorldWidth = 1.18f, ReferencePixelWidth = sprites["idle-4"].rect.width,
            TravelMotion = UnitTravelMotion.AlternatingHop, AuthoredBodyMotion = true, DisplayScale = 1f,
            SourceNotes = "User design 2026-10-17; imagegen transparent pixel artwork; five authored views mirrored for eight directions."
        };
        int[] columns = { 0, 1, 2, 3, 4, 3, 2, 1 };
        for (int d = 0; d < 8; d++)
        {
            int c = columns[d]; Sprite idle = sprites["idle-" + c], move = sprites["move-" + c], attack = sprites["attack-" + c], special = sprites["special-" + c];
            art.IdleDirections[d] = idle; art.AttackDirections[d] = attack;
            art.DirectionalAnimations[d] = new UnitDirectionalAnimation
            {
                Idle = new[] { idle }, Move = new[] { idle, move, idle, move },
                HopLeft = new[] { idle, move, idle }, HopRight = new[] { idle, move, idle },
                Attack = new[] { idle, attack, attack, idle }, Special = new[] { idle, special, special, idle },
                Death = new[] { defeat["fall-" + c], defeat["dead-" + c] },
                AttackContactFrame = 1, FlipX = d > 4, SpecialDuration = .5f,
                AttackLungeDistance = .06f, SpecialLungeDistance = .03f, SpecialLift = .025f
            };
        }
        character.Visuals = art; character.Portrait = character.Card = character.Icon = art.Portrait;
        EditorUtility.SetDirty(character);
        var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        if (!config.Characters.Any(c => c != null && c.Id == "rosie")) config.Characters = config.Characters.Concat(new[] { character }).ToArray();
        config.Units = config.Units.Where(u => u.Id != "rosie").Concat(new[] { character.CreateRuntimeDefinition() }).ToArray();
        EditorUtility.SetDirty(config);
        var catalog = Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
        catalog.Units = catalog.Units.Where(a => a.Id != "rosie").Concat(new[] { art }).ToArray(); EditorUtility.SetDirty(catalog);
        var projectiles = Resources.Load<ProjectileArtCatalog>("MonsterPouch/ProjectileArt");
        var rose = AssetDatabase.LoadAllAssetsAtPath(MonsterPouchFloralArt.RosePath).OfType<Sprite>().FirstOrDefault() ?? projectilesSprites["rose"];
        projectiles.Projectiles = projectiles.Projectiles.Where(a => a.UnitId != "rosie").Concat(new[] { new ProjectileArt
        { UnitId = "rosie", Flight = new[] { rose }, DisplaySize = 25, ReferencePixels = Mathf.Max(rose.rect.width, rose.rect.height), OrientToTarget = true } }).ToArray();
        EditorUtility.SetDirty(projectiles);
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Lobby));
        Sprite front = sprites["idle-4"];
        float[] RectOf(Sprite sprite) => new[] { sprite.rect.x, sprite.rect.y, sprite.rect.width, sprite.rect.height };
        var clip = new LobbyFrameClip
        {
            id = "rosie", texture = "MonsterPouch/Rosie/rosie-combat", idleTexture = "MonsterPouch/Rosie/rosie-combat",
            idleRect = RectOf(front), idleBounds = new[] { 0f, 0f, front.rect.width, front.rect.height },
            idleAnchor = new[] { front.rect.width * .5f, 0f }, referenceHeight = front.rect.height,
            frames = new[] { "idle-4", "attack-4", "special-4", "special-4", "idle-4" }.Select(name =>
            { var s = sprites[name]; return new LobbyFramePose { rect = RectOf(s), anchor = new[] { s.rect.width * .5f, 0f }, height = s.rect.height, registrationScale = 1 }; }).ToArray()
        };
        MonsterPouchIdleArtwork.RestoreWhelpIdle(character,clip);
        manifest.clips = manifest.clips.Where(c => c.id != "rosie").Concat(new[] { clip }).ToArray();
        File.WriteAllText(Lobby, JsonUtility.ToJson(manifest, true));
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Validate();
    }

    static CharacterAbility CreateAbility(TrickDefinition definition, string name, Sprite icon)
    {
        var asset = ScriptableObject.CreateInstance<CharacterAbility>(); asset.Definition = definition; asset.Icon = icon;
        AssetDatabase.CreateAsset(asset, Folder + "/" + name + ".asset"); return asset;
    }
    static void Import(string path, bool combat, bool death = false, bool abilities = false)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.isReadable = true;
        importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 2048;
        importer.spritePixelsPerUnit = 256; importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(death)importer.spritePixelsPerUnit = 256f * texture.width / AssetDatabase.LoadAssetAtPath<Texture2D>(Combat).width;
        var data = texture.GetPixels32(); var slices = new List<SpriteRect>();
        int columns = combat ? 5 : 4, rows = combat ? (death ? 2 : 4) : 1;
        for (int row = 0; row < rows; row++) for (int col = 0; col < columns; col++)
        {
            int x0 = Mathf.RoundToInt(texture.width * col / (float)columns), x1 = Mathf.RoundToInt(texture.width * (col + 1) / (float)columns);
            int y0 = combat ? Mathf.RoundToInt(texture.height * (rows - row - 1) / (float)rows) : 0;
            int y1 = combat ? Mathf.RoundToInt(texture.height * (rows - row) / (float)rows) : Mathf.RoundToInt(texture.height * (abilities ? 1 : .28f));
            // Sprite importer rectangles are in ORIGINAL source pixels, even when the final atlas is downscaled.
            if(abilities)
            {
                float side=texture.width/4f;
                float cardHeight=texture.height*64f/Mathf.RoundToInt(texture.height*256f/texture.width);
                slices.Add(new SpriteRect { name=new[]{"rose","haste","shield","immunity"}[col],
                    rect=new Rect(side*col,(texture.height-cardHeight)*.5f,side,cardHeight),
                    alignment=SpriteAlignment.Center,pivot=Vector2.one*.5f });
                continue;
            }
            if(death){y0=row==0?Mathf.RoundToInt(texture.height*.36f):0;y1=row==0?texture.height:Mathf.RoundToInt(texture.height*.36f);}
            int left = x1, right = x0, bottom = y1, top = y0;
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                if (data[y * texture.width + x].a > 32) { left = Math.Min(left, x); right = Math.Max(right, x); bottom = Math.Min(bottom, y); top = Math.Max(top, y); }
            if (right <= left || top <= bottom) throw new InvalidDataException("Empty Rosie sprite.");
            slices.Add(new SpriteRect { name = combat ? (death ? new[]{"fall","dead"}[row] : new[] { "idle", "move", "attack", "special" }[row]) + "-" + col : new[] { "rose", "haste", "shield", abilities ? "immunity" : "bloom" }[col],
                rect = new Rect(left, bottom, right - left + 1, top - bottom + 1), alignment = SpriteAlignment.Custom, pivot = new Vector2(.5f, combat ? .01f : .5f) });
        }
        var factories = new SpriteDataProviderFactories(); factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer); provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects().ToDictionary(r => r.name);
        foreach(var slice in slices) slice.spriteID = existing.TryGetValue(slice.name, out var previous) ? previous.spriteID : GUID.Generate();
        slices.AddRange(existing.Values.Where(s=>s.name.EndsWith("-brief-portrait",StringComparison.Ordinal)));
        provider.SetSpriteRects(slices.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(slices.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        if(abilities)importer.maxTextureSize = 256;
        importer.SaveAndReimport();
    }

    public static void Validate()
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Folder + "/Character.asset");
        if (character == null || character.Validate() != "") throw new InvalidDataException(character?.Validate() ?? "Missing Rosie");
        var runtime = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        try
        {
            if (runtime.Get("rosie") == null || !runtime.TryResolveWhelpSelection(new[] { "rosie" }, out _, out _)) throw new InvalidDataException("Rosie is not selectable.");
            if (character.Visuals.DirectionalAnimations.Any(a => a == null || a.Idle.Length == 0 || a.Move.Length == 0 || a.Attack.Length == 0 || a.Special.Length == 0 || a.Death.Length == 0)) throw new InvalidDataException("Missing Rosie animation.");
            foreach (var effect in new[] { character.Ability }.Concat(character.Upgrades))
                if (effect.Icon == null || AbilityArtwork.Get(effect.Definition.EffectId) == null) throw new InvalidDataException("Missing Rosie icon.");
            foreach (var effect in new[] { character.Ability }.Concat(character.Upgrades))
                if(Mathf.Abs(effect.Icon.rect.width-64)>.1f || Mathf.Abs(effect.Icon.rect.height-64)>.1f || effect.Icon.texture.filterMode != FilterMode.Point)
                    throw new InvalidDataException("Rosie ability icons must match the roster's 64px pixel art: "+effect.Icon.name+" "+effect.Icon.rect+" "+effect.Icon.texture.width+"px "+effect.Icon.texture.filterMode);
            string report = "Rosie registered in collection, runtime roster, balance editor, art and projectile catalogs. Eight directions, idle/move/attack/super clips, lobby gesture and four ability icons validated.\n" +
                string.Join("\n", new[] { character.Ability }.Concat(character.Upgrades).Select(a => a.Definition.Name + ": " + AbilityBalance.Description(a.Definition, character.CreateRuntimeDefinition())));
            Directory.CreateDirectory("Logs/local-validation/rosie"); File.WriteAllText("Logs/local-validation/rosie/import.txt", report); Debug.Log(report);
        }
        finally { UnityEngine.Object.DestroyImmediate(runtime); }
    }
}
