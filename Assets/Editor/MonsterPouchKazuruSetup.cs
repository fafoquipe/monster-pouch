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

public static class MonsterPouchKazuruSetup
{
    const string Art = "Assets/resources/MonsterPouch/Kazuru/";
    const string Folder = "Assets/resources/MonsterPouch/Characters/kazuru";
    const string Source = "ArtArchive/kazuru/";
    const int CellW = 320, CellH = 304;
    [Serializable] public class Manifest { public LobbyFrameClip[] clips; }
    static readonly string[] Poses = { "idle", "move", "attack", "guard", "defeat" };
    static readonly string[] Icons = { "power", "throw", "halo", "renewal" };

    [MenuItem("Monster Pouch/Characters/Install Kazuru")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Art); Directory.CreateDirectory(Folder);
        PrepareSpriteAtlas(); PrepareSmallAtlas("abilities-source.png", "kazuru-abilities.png", 64, 0); PrepareSmallAtlas("rings-source-v1.png", "kazuru-rings.png", 128);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Import(Art + "kazuru-combat.png", 5, 5, CellW, CellH, (r,c) => Poses[r]+"-"+c, new Vector2(.5f,8f/CellH));
        Import(Art + "kazuru-abilities.png", 2, 2, 64, 64, (r,c) => Icons[r*2+c], Vector2.one*.5f);
        Import(Art + "kazuru-rings.png", 2, 2, 128, 128, (r,c) => new[]{"ring","tilted","halo","ground"}[r*2+c], Vector2.one*.5f);
        var sprites = Sprites("kazuru-combat"); var icons = Sprites("kazuru-abilities");
        var definition = DocumentedRoster.CreateKazuru();
        foreach (var effect in new[] { definition.BaseAbility }.Concat(definition.Tricks))
        { effect.Parameters = AbilityBalance.Resolve(effect); effect.Description = AbilityBalance.Description(effect, definition); }
        var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Folder + "/Character.asset");
        if (character == null)
        {
            character = ScriptableObject.CreateInstance<CharacterDefinition>(); character.InitializeIdentity("kazuru");
            character.General = definition;
            character.Description = "Kazuru golpea con anillos orbitantes y fortalece el daño de los aliados con su Círculo Solar.";
            character.Attack = ScriptableObject.CreateInstance<CharacterAttack>(); character.Attack.Import(definition);
            AssetDatabase.CreateAsset(character.Attack, Folder + "/Attack.asset");
            character.Ability = CreateAbility(definition.BaseAbility, "Ability", icons[Icons[0]]);
            character.Upgrades = definition.Tricks.Select((t,i) => CreateAbility(t, "Upgrade"+(i+1), icons[Icons[i+1]])).ToArray();
            AssetDatabase.CreateAsset(character, Folder + "/Character.asset");
        }
        // Art reimports preserve edited balance, prices, and upgrade values.
        character.Ability.Icon = icons[Icons[0]]; EditorUtility.SetDirty(character.Ability);
        for (int i = 0; i < 3; i++) { character.Upgrades[i].Icon = icons[Icons[i+1]]; EditorUtility.SetDirty(character.Upgrades[i]); }
        var frontBounds = OpaqueBounds(AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"kazuru-combat.png"), new RectInt(4*CellW,4*CellH,CellW,CellH));
        var art = new UnitArt
        {
            Id = "kazuru", Portrait = sprites["idle-4"], WorldWidth = 1.15f, ReferencePixelWidth = frontBounds.width,
            DisplayScale = 1, TravelMotion = UnitTravelMotion.WholeBodyHop, AuthoredBodyMotion = true,
            SourceNotes = "Kazuru Monster, reference-based pixel art generated 2026-10-20. Five facings and poses; independently animated rings.",
            InGameSizePercent = character.Visuals?.InGameSizePercent ?? -1
        };
        int[] directions = { 0,1,2,3,4,3,2,1 };
        for (int d = 0; d < 8; d++)
        {
            int c = directions[d]; Sprite idle = sprites["idle-"+c], move = sprites["move-"+c], attack = sprites["attack-"+c], guard = sprites["guard-"+c];
            art.IdleDirections[d] = idle; art.AttackDirections[d] = attack;
            art.DirectionalAnimations[d] = new UnitDirectionalAnimation
            {
                Idle = new[]{idle,move,idle}, IdleFramesPerSecond = 2, Move = new[]{idle,move,idle,move},
                HopLeft = new[]{idle,move,idle}, HopRight = new[]{idle,move,idle},
                Attack = new[]{idle,attack,attack,idle}, Special = new[]{idle,guard,guard,idle},
                Death = new[]{idle,sprites["defeat-"+c]}, FlipX = d>4, AttackContactFrame = 1,
                AttackLungeDistance = .18f, AttackImpactSquash = .08f, SpecialLift = .04f, SpecialDuration = .65f,
                SpecialLungeDistance = .03f, SpecialPunchScale = .06f, SpecialReleasePoint = .46f
            };
        }
        character.Visuals = art; character.Portrait = character.Card = character.Icon = art.Portrait; EditorUtility.SetDirty(character);
        var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        if (!config.Characters.Any(c => c != null && c.Id == "kazuru")) config.Characters = config.Characters.Concat(new[]{character}).ToArray();
        config.Units = config.Units.Where(u => u.Id != "kazuru").Concat(new[]{character.CreateRuntimeDefinition()}).ToArray(); EditorUtility.SetDirty(config);
        var catalog = Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
        catalog.Units = catalog.Units.Where(a => a.Id != "kazuru").Concat(new[]{art}).ToArray(); EditorUtility.SetDirty(catalog);
        const string lobby = "Assets/resources/MonsterPouch/UI/LobbyFrames/clips.json";
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(lobby));
        float[] RectOf(Sprite sprite) => new[]{sprite.rect.x,sprite.rect.y,sprite.rect.width,sprite.rect.height};
        var front = sprites["idle-4"];
        var clip = new LobbyFrameClip
        {
            id = "kazuru", texture = "MonsterPouch/Kazuru/kazuru-combat", idleTexture = "MonsterPouch/Kazuru/kazuru-combat",
            idleRect = RectOf(front), idleBounds = new[]{(float)(frontBounds.x-4*CellW),(float)(frontBounds.y-4*CellH),(float)frontBounds.width,(float)frontBounds.height},
            idleAnchor = new[]{CellW*.5f,8f}, referenceHeight = frontBounds.height,
            frames = new[]{"idle-4","attack-4","guard-4","guard-4","idle-4"}.Select(name => new LobbyFramePose
                { rect = RectOf(sprites[name]), anchor = new[]{CellW*.5f,8f}, height = frontBounds.height, registrationScale = 1 }).ToArray()
        };
        MonsterPouchBriefPortraits.Fit(character);
        character.Card = character.Icon = character.Portrait; EditorUtility.SetDirty(character);
        manifest.clips = manifest.clips.Where(c => c.id != "kazuru").Concat(new[]{clip}).ToArray();
        File.WriteAllText(lobby,JsonUtility.ToJson(manifest,true));
        var projectileCatalog = Resources.Load<ProjectileArtCatalog>("MonsterPouch/ProjectileArt");
        var rings = Sprites("kazuru-rings");
        var projectile = new ProjectileArt {UnitId="kazuru",Flight=new[]{rings["tilted"]},DisplaySize=27,ReferencePixels=128,OrientToTarget=false};
        projectileCatalog.Projectiles=projectileCatalog.Projectiles.Where(p=>p.UnitId!="kazuru").Concat(new[]{projectile}).ToArray();
        EditorUtility.SetDirty(projectileCatalog);
        if(File.Exists(Source+"idle-source-v1.png")) ApplyIdle();
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); Validate();
    }

    [MenuItem("Monster Pouch/Characters/Install Kazuru idle")]
    public static void ApplyIdle()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        const string homePath=Art+"kazuru-idle-v1.png",pixelPath=Art+"kazuru-idle-pixel-v2.png";
        File.Copy(Source+"idle-source-v1.png",homePath,true);
        // The compact Brief pose keeps rings close so the face fills small square slots.
        string briefSource=Source+"brief-source-v2.png";
        var source=Read(File.Exists(briefSource)?briefSource:homePath);
        var bounds=OpaqueBounds(source,new RectInt(0,0,source.width,source.height));
        const int size=128;var pixels=new Color32[size*size];var data=source.GetPixels32();
        float scale=Mathf.Min(120f/bounds.width,120f/bounds.height);
        int width=Mathf.RoundToInt(bounds.width*scale),height=Mathf.RoundToInt(bounds.height*scale);
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int sx=bounds.x+Mathf.Min(bounds.width-1,Mathf.FloorToInt((x+.5f)/scale));
            int sy=bounds.y+Mathf.Min(bounds.height-1,Mathf.FloorToInt((y+.5f)/scale));
            var color=data[sy*source.width+sx];color.a=(byte)(color.a>=128?255:0);
            pixels[(4+y)*size+(size-width)/2+x]=color;
        }
        Save(pixelPath,size,size,pixels);UnityEngine.Object.DestroyImmediate(source);
        Sprite ImportIdle(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.isReadable=true;
            importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.spritePixelsPerUnit=80;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        var home=ImportIdle(homePath);var pixel=ImportIdle(pixelPath);
        LobbyFrameClip Clip(Sprite sprite,string path)
        {
            var texture=sprite.texture;var b=OpaqueBounds(texture,new RectInt(0,0,texture.width,texture.height));
            return new LobbyFrameClip {id="kazuru",idleTexture=path.Substring("Assets/resources/".Length).Replace(".png",""),
                idleRect=new[]{0f,0f,(float)texture.width,(float)texture.height},
                idleBounds=new[]{(float)b.x,(float)b.y,(float)b.width,(float)b.height},
                idleAnchor=new[]{b.x+b.width*.5f,(float)b.y},frames=Array.Empty<LobbyFramePose>()};
        }
        void StoreClip(string path,LobbyFrameClip clip)
        {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            manifest.clips=manifest.clips.Where(c=>c.id!="kazuru").Concat(new[]{clip}).ToArray();
            File.WriteAllText(path,JsonUtility.ToJson(manifest,true));AssetDatabase.ImportAsset(path);
        }
        StoreClip("Assets/resources/MonsterPouch/UI/LobbyFrames/clips.json",Clip(pixel,pixelPath));
        StoreClip("Assets/resources/MonsterPouch/UI/BattleHomeIdles/clips.json",Clip(home,homePath));
        var character=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Folder+"/Character.asset");
        character.Portrait=character.Card=character.Icon=pixel;EditorUtility.SetDirty(character);
        AssetDatabase.SaveAssets();
        foreach(string field in new[]{"clips","idles","homeIdles"})
            typeof(LobbyFrameGraphic).GetField(field,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)?.SetValue(null,null);
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
        if(right<left||top<bottom)throw new InvalidDataException("Empty Kazuru sprite cell "+rect);
        return new RectInt(left,bottom,right-left+1,top-bottom+1);
    }
    // Engine asset preparation: the authored originals remain intact in ArtArchive.
    // Pack the supplied drawings at native resolution. Detached sparkles belong
    // to the ability effects, not to the character silhouette or its size anchor.
    static void PrepareSpriteAtlas()
    {
        var source=Read(Source+"combat-source-v1.png"); var regions=new RectInt[25];
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
        if(regions.Any(r=>r.width>CellW-16||r.height>CellH-24))throw new InvalidDataException("Kazuru source pose exceeds its native-resolution cell.");
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
        Save(Art+"kazuru-combat.png",CellW*5,CellH*5,pixels); UnityEngine.Object.DestroyImmediate(source);
    }
    static void PrepareSmallAtlas(string input,string output,int size,int padding=4)
    {
        var source=Read(Source+input);var data=source.GetPixels32();int side=size*2;
        var pixels=new Color32[side*side];
        for(int r=0;r<2;r++)for(int c=0;c<2;c++)
        {
            var cell=new RectInt(c*source.width/2,r*source.height/2,source.width/2,source.height/2);
            var b=OpaqueBounds(source,cell);
            float scale=Mathf.Min((size-padding*2f)/b.width,(size-padding*2f)/b.height);
            int w=Mathf.RoundToInt(b.width*scale),h=Mathf.RoundToInt(b.height*scale);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int sx=b.x+Mathf.Min(b.width-1,Mathf.FloorToInt((x+.5f)/scale));
                int sy=b.y+Mathf.Min(b.height-1,Mathf.FloorToInt((y+.5f)/scale));
                var p=data[sy*source.width+sx];p.a=(byte)(p.a>=128?255:0);
                pixels[(r*size+(size-h)/2+y)*side+c*size+(size-w)/2+x]=p;
            }
        }
        Save(Art+output,side,side,pixels);UnityEngine.Object.DestroyImmediate(source);
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
        if(character==null||character.Validate()!="")throw new InvalidDataException(character?.Validate()??"Missing Kazuru");
        var runtime=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        try
        {
            if(runtime.Get("kazuru")==null||!runtime.Get("kazuru").IsMonster)throw new InvalidDataException("Kazuru unavailable in collection.");
            if(character.Visuals.DirectionalAnimations.Any(a=>a.Idle.Length==0||a.HopLeft.Length==0||a.HopRight.Length==0||a.Attack.Length==0||a.Special.Length==0||a.Death.Length==0))throw new InvalidDataException("Missing animation");
            foreach(var ability in new[]{character.Ability}.Concat(character.Upgrades))
                if(ability.Icon==null||ability.Icon.rect.width!=64||ability.Icon.rect.height!=64||AbilityArtwork.Get(ability.Definition.EffectId)==null)throw new InvalidDataException("Kazuru needs four square 64px icons.");
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"kazuru-combat.png");var data=tex.GetPixels32();
            if(data.Any(p=>p.a!=0&&p.a!=255))throw new InvalidDataException("Non-binary sprite alpha.");
            for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)
                if((x%CellW<2||x%CellW>=CellW-2||y%CellH<2||y%CellH>=CellH-2)&&data[y*tex.width+x].a!=0)throw new InvalidDataException("Sprite touches cell gutter.");
            Directory.CreateDirectory("Logs/local-validation/kazuru");
            File.WriteAllText("Logs/local-validation/kazuru/import.txt","Kazuru selectable Monster. 25 poses; 8 facings; idle, attack, cast and defeat; four 64px icons, four separate rings; clean cell gutters and binary alpha.\n"+
                string.Join("\n",new[]{character.Ability}.Concat(character.Upgrades).Select(a=>AbilityBalance.Description(a.Definition,character.CreateRuntimeDefinition()))));
        }
        finally{UnityEngine.Object.DestroyImmediate(runtime);}
    }
}
