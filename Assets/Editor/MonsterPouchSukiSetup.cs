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

public static class MonsterPouchSukiSetup
{
    const string Art = "Assets/resources/MonsterPouch/Suki/";
    const string Folder = "Assets/resources/MonsterPouch/Characters/suki";
    const string Source = "ArtArchive/suki/";
    const int CellW = 96, CellH = 112;
    [Serializable] public class Manifest { public LobbyFrameClip[] clips; }
    static readonly string[] Poses = { "idle", "crouch", "left", "right", "punch", "windup", "release", "defeat" };
    static readonly string[] Icons = { "shield", "energy", "opening", "bonus" };

    [MenuItem("Monster Pouch/Characters/Install Suki")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Art); Directory.CreateDirectory(Folder);
        PrepareSpriteAtlas(); PrepareIcons(); PrepareFish();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Import(Art + "suki-combat.png", 5, 8, CellW, CellH, (r,c) => Poses[r]+"-"+c, new Vector2(.5f,8f/CellH));
        Import(Art + "suki-abilities.png", 2, 2, 64, 64, (r,c) => Icons[r*2+c], Vector2.one*.5f);
        Import(Art + "suki-fish.png", 1, 1, 40, 24, (r,c) => "fish", Vector2.one*.5f);
        var sprites = Sprites("suki-combat"); var icons = Sprites("suki-abilities");
        var definition = DocumentedRoster.CreateSuki();
        foreach (var effect in new[] { definition.BaseAbility }.Concat(definition.Tricks))
        { effect.Parameters = AbilityBalance.Resolve(effect); effect.Description = AbilityBalance.Description(effect, definition); }
        var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(Folder + "/Character.asset");
        if (character == null)
        {
            character = ScriptableObject.CreateInstance<CharacterDefinition>(); character.InitializeIdentity("suki");
            character.General = definition;
            character.Description = "Suki protege a su equipo con pescados que se convierten en escudos temporales de vida extra.";
            character.Attack = ScriptableObject.CreateInstance<CharacterAttack>(); character.Attack.Import(definition);
            AssetDatabase.CreateAsset(character.Attack, Folder + "/Attack.asset");
            character.Ability = CreateAbility(definition.BaseAbility, "Ability", icons[Icons[0]]);
            character.Upgrades = definition.Tricks.Select((t,i) => CreateAbility(t, "Upgrade"+(i+1), icons[Icons[i+1]])).ToArray();
            AssetDatabase.CreateAsset(character, Folder + "/Character.asset");
        }
        // Art reimports preserve edited balance, prices, and upgrade values.
        character.Ability.Icon = icons[Icons[0]]; EditorUtility.SetDirty(character.Ability);
        for (int i = 0; i < 3; i++) { character.Upgrades[i].Icon = icons[Icons[i+1]]; EditorUtility.SetDirty(character.Upgrades[i]); }
        var frontBounds = OpaqueBounds(AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"suki-combat.png"), new RectInt(4*CellW,7*CellH,CellW,CellH));
        var art = new UnitArt
        {
            Id = "suki", Portrait = sprites["idle-4"], WorldWidth = 1.23f, ReferencePixelWidth = frontBounds.width,
            DisplayScale = 1, TravelMotion = UnitTravelMotion.AlternatingHop, AuthoredBodyMotion = true,
            SourceNotes = "Suki revision 2: original character identity and user Pokrak animation reference. Five views, eight poses; registered feet, distinct hops, punch anticipation and overhand fish release.",
            InGameSizePercent = character.Visuals?.InGameSizePercent ?? -1
        };
        int[] directions = { 0,1,2,3,4,3,2,1 };
        for (int d = 0; d < 8; d++)
        {
            int c = directions[d]; Sprite idle = sprites["idle-"+c], crouch = sprites["crouch-"+c], left = sprites["left-"+c], right = sprites["right-"+c], punch = sprites["punch-"+c];
            art.IdleDirections[d] = idle; art.AttackDirections[d] = punch;
            art.DirectionalAnimations[d] = new UnitDirectionalAnimation
            {
                Idle = new[]{idle}, Move = new[]{crouch,left,idle,crouch,right,idle},
                HopLeft = new[]{crouch,left,idle}, HopRight = new[]{crouch,right,idle},
                Attack = new[]{idle,crouch,punch,punch,crouch,idle},
                Special = new[]{sprites["windup-"+c],sprites["release-"+c],sprites["release-"+c],crouch,idle},
                Death = new[]{idle,crouch,sprites["defeat-"+c],sprites["defeat-"+c]},
                FlipX = d>4, SpecialFlipX = d>4, AttackContactFrame = 2,
                AttackLungeDistance = .30f, AttackImpactSquash = .06f, SpecialLift = .06f, SpecialDuration = .65f,
                SpecialLungeDistance = .18f, SpecialPunchScale = .05f, SpecialReleasePoint = .46f
            };
        }
        character.Visuals = art; character.Portrait = character.Card = character.Icon = art.Portrait; EditorUtility.SetDirty(character);
        var config = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        if (!config.Characters.Any(c => c != null && c.Id == "suki")) config.Characters = config.Characters.Concat(new[]{character}).ToArray();
        config.Units = config.Units.Where(u => u.Id != "suki").Concat(new[]{character.CreateRuntimeDefinition()}).ToArray(); EditorUtility.SetDirty(config);
        var catalog = Resources.Load<UnitArtCatalog>("MonsterPouch/UnitArt");
        catalog.Units = catalog.Units.Where(a => a.Id != "suki").Concat(new[]{art}).ToArray(); EditorUtility.SetDirty(catalog);
        const string lobby = "Assets/resources/MonsterPouch/UI/LobbyFrames/clips.json";
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(lobby));
        float[] RectOf(Sprite sprite) => new[]{sprite.rect.x,sprite.rect.y,sprite.rect.width,sprite.rect.height};
        var front = sprites["idle-4"];
        var clip = new LobbyFrameClip
        {
            id = "suki", texture = "MonsterPouch/Suki/suki-combat", idleTexture = "MonsterPouch/Suki/suki-combat",
            idleRect = RectOf(front), idleBounds = new[]{(float)(frontBounds.x-4*CellW),(float)(frontBounds.y-7*CellH),(float)frontBounds.width,(float)frontBounds.height},
            idleAnchor = new[]{CellW*.5f,8f}, referenceHeight = frontBounds.height,
            frames = new[]{"idle-4","windup-4","release-4","release-4","idle-4"}.Select(name => new LobbyFramePose
                { rect = RectOf(sprites[name]), anchor = new[]{CellW*.5f,8f}, height = frontBounds.height, registrationScale = 1 }).ToArray()
        };
        MonsterPouchIdleArtwork.RestoreWhelpIdle(character,clip);
        manifest.clips = manifest.clips.Where(c => c.id != "suki").Concat(new[]{clip}).ToArray();
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
        if(right<left||top<bottom)throw new InvalidDataException("Empty Suki sprite cell "+rect);
        return new RectInt(left,bottom,right-left+1,top-bottom+1);
    }
    // Engine asset preparation: the authored originals remain intact in ArtArchive.
    // Sampling onto a fixed logical pixel grid keeps consistent size and opaque crisp edges.
    static void PrepareSpriteAtlas()
    {
        var source=Read(Source+"combat-source-v2.png"); var regions=new RectInt[40];
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
            if(members.Count<100)continue;
            components.Add(new RectInt(left,bottom,right-left+1,top-bottom+1));
            foreach(int index in members)keep[index]=true;
        }
        if(components.Count!=40)throw new InvalidDataException("Expected 40 separate authored poses, found "+components.Count);
        var ordered=components.OrderByDescending(b=>b.center.y).ToArray();
        for(int r=0;r<8;r++)
        {
            var row=ordered.Skip(r*5).Take(5).OrderBy(b=>b.x).ToArray();
            for(int c=0;c<5;c++)regions[r*5+c]=row[c];
        }
        // Register the planted feet, rather than recentering the silhouette whenever
        // a mitten extends. The punch then moves the hand without sliding the body.
        var anchors=regions.Select(b=>FootCenter(data,source.width,b)).ToArray();
        float reach=Enumerable.Range(0,40).Max(i=>Mathf.Max(anchors[i]-regions[i].xMin,regions[i].xMax-anchors[i]));
        float scale=Mathf.Min(40f/reach,94f/regions.Max(r=>r.height));
        var pixels=new Color32[CellW*5*CellH*8];
        for(int r=0;r<8;r++)for(int c=0;c<5;c++)
        {
            var bounds=regions[r*5+c]; int w=Mathf.Max(1,Mathf.RoundToInt(bounds.width*scale)),h=Mathf.Max(1,Mathf.RoundToInt(bounds.height*scale));
            int left=c*CellW+CellW/2-Mathf.RoundToInt((anchors[r*5+c]-bounds.xMin)*scale);
            int bottom=(7-r)*CellH+8+(r==2||r==3?3:0);
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int sample=(bounds.y+Mathf.Min(bounds.height-1,Mathf.FloorToInt((y+.5f)/scale)))*source.width+bounds.x+Mathf.Min(bounds.width-1,Mathf.FloorToInt((x+.5f)/scale));
                var p=data[sample];if(!keep[sample])continue;p.a=255;pixels[(bottom+y)*CellW*5+left+x]=p;
            }
        }
        Save(Art+"suki-combat.png",CellW*5,CellH*8,pixels); UnityEngine.Object.DestroyImmediate(source);
    }
    static float FootCenter(Color32[] pixels,int width,RectInt bounds)
    {
        int left=bounds.xMax,right=bounds.xMin;
        int band=Mathf.Max(3,Mathf.RoundToInt(bounds.height*.14f));
        for(int y=bounds.yMin;y<bounds.yMin+band;y++)for(int x=bounds.xMin;x<bounds.xMax;x++)
            if(pixels[y*width+x].a>=128){left=Mathf.Min(left,x);right=Mathf.Max(right,x+1);}
        return (left+right)*.5f;
    }
    static void PrepareIcons()
    {
        var source=Read(Source+"abilities-source.png"); var data=source.GetPixels32(); var pixels=new Color32[128*128];
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {
            int sx=Mathf.Min(source.width-1,Mathf.FloorToInt((x+.5f)*source.width/128f));
            int sy=Mathf.Min(source.height-1,Mathf.FloorToInt((y+.5f)*source.height/128f));
            var p=data[sy*source.width+sx];p.a=255;pixels[y*128+x]=p;
        }
        Save(Art+"suki-abilities.png",128,128,pixels); UnityEngine.Object.DestroyImmediate(source);
    }
    static void PrepareFish()
    {
        var source=Read(Source+"fish-source.png"); var b=OpaqueBounds(source,new RectInt(0,0,source.width,source.height));
        float scale=Mathf.Min(36f/b.width,20f/b.height);int w=Mathf.RoundToInt(b.width*scale),h=Mathf.RoundToInt(b.height*scale);
        var data=source.GetPixels32();var pixels=new Color32[40*24];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            var p=data[(b.y+Mathf.Min(b.height-1,Mathf.FloorToInt((y+.5f)/scale)))*source.width+b.x+Mathf.Min(b.width-1,Mathf.FloorToInt((x+.5f)/scale))];
            if(p.a<128)continue;p.a=255;pixels[(y+(24-h)/2)*40+x+(40-w)/2]=p;
        }
        Save(Art+"suki-fish.png",40,24,pixels);UnityEngine.Object.DestroyImmediate(source);
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
        importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.spritePixelsPerUnit=96;
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
        if(character==null||character.Validate()!="")throw new InvalidDataException(character?.Validate()??"Missing Suki");
        var runtime=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        try
        {
            if(runtime.Get("suki")==null||!runtime.TryResolveWhelpSelection(new[]{"suki"},out _,out _))throw new InvalidDataException("Suki unavailable in collection.");
            if(character.Visuals.DirectionalAnimations.Any(a=>a.Idle.Length==0||a.HopLeft.Length==0||a.HopRight.Length==0||a.Attack.Length==0||a.Special.Length==0||a.Death.Length==0))throw new InvalidDataException("Missing animation");
            foreach(var ability in new[]{character.Ability}.Concat(character.Upgrades))
                if(ability.Icon==null||ability.Icon.rect.width!=64||ability.Icon.rect.height!=64||AbilityArtwork.Get(ability.Definition.EffectId)==null)throw new InvalidDataException("Suki needs four square 64px icons.");
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"suki-combat.png");var data=tex.GetPixels32();
            if(data.Any(p=>p.a!=0&&p.a!=255))throw new InvalidDataException("Non-binary sprite alpha.");
            for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)
                if((x%CellW<2||x%CellW>=CellW-2||y%CellH<2||y%CellH>=CellH-2)&&data[y*tex.width+x].a!=0)throw new InvalidDataException("Sprite touches cell gutter.");
            Directory.CreateDirectory("Logs/local-validation/suki");
            File.WriteAllText("Logs/local-validation/suki/import.txt","Suki selectable. 40 poses; 8 facings; alternating hops; melee, cast and defeat; four square 64px icons; clean cell gutters and binary alpha.\n"+
                string.Join("\n",new[]{character.Ability}.Concat(character.Upgrades).Select(a=>AbilityBalance.Description(a.Definition,character.CreateRuntimeDefinition()))));
        }
        finally{UnityEngine.Object.DestroyImmediate(runtime);}
    }
}
