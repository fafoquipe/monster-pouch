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

public static class MonsterPouchPokrakSetup
{
    const string Art = "Assets/resources/MonsterPouch/Pokrak/";
    const string Folder = "Assets/resources/MonsterPouch/Characters/pokrak";
    const string Source = "ArtArchive/pokrak/";
    const int CellW = 288, CellH = 272;
    [Serializable] public class Manifest { public LobbyFrameClip[] clips; }
    static readonly string[] Poses = { "idle", "move", "attack", "guard", "defeat" };
    static readonly string[] Icons = { "bubble", "duration", "area", "reduction" };

    [MenuItem("Monster Pouch/Characters/Install Pokrak")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Art); Directory.CreateDirectory(Folder);
        PrepareSpriteAtlas(); PrepareIcons();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Import(Art + "pokrak-combat.png", 5, 5, CellW, CellH, (r,c) => Poses[r]+"-"+c, new Vector2(.5f,8f/CellH));
        Import(Art + "pokrak-abilities.png", 2, 2, 64, 64, (r,c) => Icons[r*2+c], Vector2.one*.5f);
        var sprites = Sprites("pokrak-combat"); var icons = Sprites("pokrak-abilities");
        var definition = DocumentedRoster.CreatePokrak();
        foreach (var effect in new[] { definition.BaseAbility }.Concat(definition.Tricks))
        { effect.Parameters = AbilityBalance.Resolve(effect); effect.Description = AbilityBalance.Description(effect, definition); }
        var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Folder + "/Character.asset");
        if (character == null)
        {
            character = ScriptableObject.CreateInstance<CharacterDefinition>(); character.InitializeIdentity("pokrak");
            character.General = definition;
            character.Description = "Pokrak ataca con sus pinzas y protege a sus aliados con burbujas que reducen el daño recibido.";
            character.Attack = ScriptableObject.CreateInstance<CharacterAttack>(); character.Attack.Import(definition);
            AssetDatabase.CreateAsset(character.Attack, Folder + "/Attack.asset");
            character.Ability = CreateAbility(definition.BaseAbility, "Ability", icons[Icons[0]]);
            character.Upgrades = definition.Tricks.Select((t,i) => CreateAbility(t, "Upgrade"+(i+1), icons[Icons[i+1]])).ToArray();
            AssetDatabase.CreateAsset(character, Folder + "/Character.asset");
        }
        // Art reimports preserve edited balance, prices, and upgrade values.
        character.Ability.Icon = icons[Icons[0]]; EditorUtility.SetDirty(character.Ability);
        for (int i = 0; i < 3; i++) { character.Upgrades[i].Icon = icons[Icons[i+1]]; EditorUtility.SetDirty(character.Upgrades[i]); }
        var frontBounds = OpaqueBounds(AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"pokrak-combat.png"), new RectInt(4*CellW,4*CellH,CellW,CellH));
        var art = new UnitArt
        {
            Id = "pokrak", Portrait = sprites["idle-4"], WorldWidth = 1.2f, ReferencePixelWidth = frontBounds.width,
            DisplayScale = 1, TravelMotion = UnitTravelMotion.AlternatingHop, AuthoredBodyMotion = true,
            SourceNotes = "User-supplied Pokrak atlas 2026-10-17. Five views and five poses packed at original pixel resolution; separate UI idle retained.",
            InGameSizePercent = character.Visuals?.InGameSizePercent ?? -1
        };
        int[] directions = { 0,1,2,3,4,3,2,1 };
        for (int d = 0; d < 8; d++)
        {
            int c = directions[d]; Sprite idle = sprites["idle-"+c], move = sprites["move-"+c], attack = sprites["attack-"+c], guard = sprites["guard-"+c];
            art.IdleDirections[d] = idle; art.AttackDirections[d] = attack;
            art.DirectionalAnimations[d] = new UnitDirectionalAnimation
            {
                Idle = new[]{idle}, Move = new[]{idle,move,idle,move},
                HopLeft = new[]{idle,move,idle}, HopRight = new[]{idle,move,idle},
                Attack = new[]{idle,attack,attack,idle}, Special = new[]{idle,guard,guard,idle},
                Death = new[]{idle,sprites["defeat-"+c]}, FlipX = d>4, AttackContactFrame = 1,
                AttackLungeDistance = .18f, AttackImpactSquash = .08f, SpecialLift = .04f, SpecialDuration = .65f,
                SpecialLungeDistance = .03f, SpecialPunchScale = .06f, SpecialReleasePoint = .46f
            };
        }
        character.Visuals = art; character.Portrait = character.Card = character.Icon = art.Portrait; EditorUtility.SetDirty(character);
        var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        if (!config.Characters.Any(c => c != null && c.Id == "pokrak")) config.Characters = config.Characters.Concat(new[]{character}).ToArray();
        config.Units = config.Units.Where(u => u.Id != "pokrak").Concat(new[]{character.CreateRuntimeDefinition()}).ToArray(); EditorUtility.SetDirty(config);
        var catalog = Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
        catalog.Units = catalog.Units.Where(a => a.Id != "pokrak").Concat(new[]{art}).ToArray(); EditorUtility.SetDirty(catalog);
        const string lobby = "Assets/resources/MonsterPouch/UI/LobbyFrames/clips.json";
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(lobby));
        float[] RectOf(Sprite sprite) => new[]{sprite.rect.x,sprite.rect.y,sprite.rect.width,sprite.rect.height};
        var front = sprites["idle-4"];
        var clip = new LobbyFrameClip
        {
            id = "pokrak", texture = "MonsterPouch/Pokrak/pokrak-combat", idleTexture = "MonsterPouch/Pokrak/pokrak-combat",
            idleRect = RectOf(front), idleBounds = new[]{(float)(frontBounds.x-4*CellW),(float)(frontBounds.y-4*CellH),(float)frontBounds.width,(float)frontBounds.height},
            idleAnchor = new[]{CellW*.5f,8f}, referenceHeight = frontBounds.height,
            frames = new[]{"idle-4","attack-4","guard-4","guard-4","idle-4"}.Select(name => new LobbyFramePose
                { rect = RectOf(sprites[name]), anchor = new[]{CellW*.5f,8f}, height = frontBounds.height, registrationScale = 1 }).ToArray()
        };
        MonsterPouchIdleArtwork.RestoreWhelpIdle(character,clip);
        manifest.clips = manifest.clips.Where(c => c.id != "pokrak").Concat(new[]{clip}).ToArray();
        File.WriteAllText(lobby,JsonUtility.ToJson(manifest,true));
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Validate();
    }

    static Dictionary<string,Sprite> Sprites(string name) => AssetDatabase.LoadAllAssetsAtPath(Art+name+".png").OfType<Sprite>().ToDictionary(s=>s.name);
    static CharacterAbility CreateAbility(TrickDefinition definition, string name, Sprite icon)
    {
        var asset = ScriptableObject.CreateInstance<CharacterAbility>(); asset.Definition = definition; asset.Icon = icon;
        AssetDatabase.CreateAsset(asset,Folder+"/"+name+".asset"); return asset;
    }
    static Texture2D Read(string path)
    {
        var tex = new Texture2D(2,2,TextureFormat.RGBA32,false);
        if (!tex.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException(path);
        return tex;
    }
    static RectInt OpaqueBounds(Texture2D tex, RectInt rect)
    {
        var data=tex.GetPixels32(); int left=rect.xMax,right=rect.xMin,bottom=rect.yMax,top=rect.yMin;
        for(int y=rect.yMin;y<rect.yMax;y++)for(int x=rect.xMin;x<rect.xMax;x++)
            if(data[y*tex.width+x].a>=128){left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);}
        if(right<left||top<bottom)throw new InvalidDataException("Empty Pokrak sprite cell "+rect);
        return new RectInt(left,bottom,right-left+1,top-bottom+1);
    }
    // Engine asset preparation: the authored originals remain intact in ArtArchive.
    // Pack the supplied drawings at native resolution. Detached sparkles belong
    // to the ability effects, not to the character silhouette or its size anchor.
    static void PrepareSpriteAtlas()
    {
        var source=Read(Source+"combat-reference-20261017.png"); var regions=new RectInt[25];
        var data=source.GetPixels32();var visited=new bool[data.Length];var keep=new bool[data.Length];
        var components=new List<RectInt>();var queue=new Queue<int>();var members=new List<int>();
        for(int start=0;start<data.Length;start++)
        {
            if(visited[start]||data[start].a<128)continue;
            visited[start]=true;queue.Enqueue(start);members.Clear();
            int left=source.width,right=0,bottom=source.height,top=0;
            while(queue.Count>0)
            {
                int index=queue.Dequeue(),x=index%source.width,y=index/source.width;members.Add(index);
                left=Math.Min(left,x);right=Math.Max(right,x);bottom=Math.Min(bottom,y);top=Math.Max(top,y);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;if(nx<0||ny<0||nx>=source.width||ny>=source.height)continue;
                    int next=ny*source.width+nx;if(visited[next]||data[next].a<128)continue;
                    visited[next]=true;queue.Enqueue(next);
                }
            }
            if(members.Count<1000)continue;
            components.Add(new RectInt(left,bottom,right-left+1,top-bottom+1));
            foreach(int index in members)keep[index]=true;
        }
        if(components.Count!=25)throw new InvalidDataException("Expected 25 separate authored poses, found "+components.Count);
        var ordered=components.OrderByDescending(b=>b.center.y).ToArray();
        for(int r=0;r<5;r++)
        {
            var row=ordered.Skip(r*5).Take(5).OrderBy(b=>b.x).ToArray();
            for(int c=0;c<5;c++)regions[r*5+c]=row[c];
        }
        const float scale=1f;
        if(regions.Any(r=>r.width>CellW-16||r.height>CellH-24))throw new InvalidDataException("Pokrak source pose exceeds its native-resolution cell.");
        var pixels=new Color32[CellW*5*CellH*5];
        for(int r=0;r<5;r++)for(int c=0;c<5;c++)
        {
            var bounds=regions[r*5+c]; int w=Mathf.Max(1,Mathf.RoundToInt(bounds.width*scale)),h=Mathf.Max(1,Mathf.RoundToInt(bounds.height*scale));
            int left=c*CellW+(CellW-w)/2, bottom=(4-r)*CellH+8+(r==1?3:0);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int sample=(bounds.y+Mathf.Min(bounds.height-1,Mathf.FloorToInt((y+.5f)/scale)))*source.width+bounds.x+Mathf.Min(bounds.width-1,Mathf.FloorToInt((x+.5f)/scale));
                var p=data[sample];if(!keep[sample])continue;p.a=255;pixels[(bottom+y)*CellW*5+left+x]=p;
            }
        }
        Save(Art+"pokrak-combat.png",CellW*5,CellH*5,pixels); UnityEngine.Object.DestroyImmediate(source);
    }
    static void PrepareIcons()
    {
        var source=Read(Source+"abilities-source.png"); var data=source.GetPixels32(); var pixels=new Color32[128*128];
        // The authored card seam is slightly above the image midpoint. Slice each
        // actual row before sizing it to 64px so adjacent backgrounds cannot leak.
        int seam=Mathf.RoundToInt(source.height*.52f);
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {
            int sx=Mathf.Min(source.width-1,Mathf.FloorToInt((x+.5f)*source.width/128f));
            int sy=y<64?Mathf.FloorToInt((y+.5f)*seam/64f):seam+Mathf.FloorToInt((y-64+.5f)*(source.height-seam)/64f);
            var p=data[sy*source.width+sx];p.a=255;pixels[y*128+x]=p;
        }
        Save(Art+"pokrak-abilities.png",128,128,pixels); UnityEngine.Object.DestroyImmediate(source);
    }
    static void Save(string path,int width,int height,Color32[] pixels)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);texture.SetPixels32(pixels);texture.Apply();
        File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
    }
    static void Import(string path,int cols,int rows,int width,int height,Func<int,int,string> name,Vector2 pivot)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;
        importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.isReadable=true;
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.spritePixelsPerUnit=80;
        foreach(string platform in new[]{"Android","Standalone"})
        {
            var settings=importer.GetPlatformTextureSettings(platform);settings.overridden=true;settings.format=TextureImporterFormat.RGBA32;
            settings.maxTextureSize=2048;importer.SetPlatformTextureSettings(settings);
        }
        importer.SaveAndReimport();
        var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);provider.InitSpriteEditorDataProvider();
        var old=provider.GetSpriteRects().ToDictionary(r=>r.name);var slices=new List<SpriteRect>();
        for(int r=0;r<rows;r++)for(int c=0;c<cols;c++)
        {
            string id=name(r,c);slices.Add(new SpriteRect{name=id,rect=new Rect(c*width,(rows-r-1)*height,width,height),alignment=SpriteAlignment.Custom,pivot=pivot,
                spriteID=old.TryGetValue(id,out var previous)?previous.spriteID:GUID.Generate()});
        }
        slices.AddRange(old.Values.Where(s=>s.name.EndsWith("-brief-portrait",StringComparison.Ordinal)));
        provider.SetSpriteRects(slices.ToArray());provider.GetDataProvider<ISpriteNameFileIdDataProvider>()?.SetNameFileIdPairs(slices.Select(s=>new SpriteNameFileIdPair(s.name,s.spriteID)));
        provider.Apply();importer.SaveAndReimport();
    }
    public static void Validate()
    {
        var character=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Folder+"/Character.asset");
        if(character==null||character.Validate()!="")throw new InvalidDataException(character?.Validate()??"Missing Pokrak");
        var runtime=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        try
        {
            if(runtime.Get("pokrak")==null||!runtime.TryResolveWhelpSelection(new[]{"pokrak"},out _,out _))throw new InvalidDataException("Pokrak unavailable in collection.");
            if(character.Visuals.DirectionalAnimations.Any(a=>a.Idle.Length==0||a.HopLeft.Length==0||a.HopRight.Length==0||a.Attack.Length==0||a.Special.Length==0||a.Death.Length==0))throw new InvalidDataException("Missing animation");
            foreach(var ability in new[]{character.Ability}.Concat(character.Upgrades))
                if(ability.Icon==null||ability.Icon.rect.width!=64||ability.Icon.rect.height!=64||AbilityArtwork.Get(ability.Definition.EffectId)==null)throw new InvalidDataException("Pokrak needs four square 64px icons.");
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"pokrak-combat.png");var data=tex.GetPixels32();
            if(data.Any(p=>p.a!=0&&p.a!=255))throw new InvalidDataException("Non-binary sprite alpha.");
            for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)
                if((x%CellW<2||x%CellW>=CellW-2||y%CellH<2||y%CellH>=CellH-2)&&data[y*tex.width+x].a!=0)throw new InvalidDataException("Sprite touches cell gutter.");
            Directory.CreateDirectory("Logs/local-validation/pokrak");
            File.WriteAllText("Logs/local-validation/pokrak/import.txt","Pokrak selectable. 25 poses; 8 facings; melee claws, guarding cast and defeat; four square 64px icons; clean cell gutters and binary alpha.\n"+
                string.Join("\n",new[]{character.Ability}.Concat(character.Upgrades).Select(a=>AbilityBalance.Description(a.Definition,character.CreateRuntimeDefinition()))));
        }
        finally{UnityEngine.Object.DestroyImmediate(runtime);}
    }
}
