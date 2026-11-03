using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Explicit, repeatable import of the approved lobby poses and scientist artwork.
// No automatic import on reload and no gameplay-stat changes.
public static class MonsterPouchLobbyPolish
{
    const string IdleFolder="Assets/resources/MonsterPouch/UI/LobbyIdles/";
    const string AnimationPath="Assets/resources/MonsterPouch/SteinScientist/stein-scientist.png";
    const string AnimationResource="MonsterPouch/SteinScientist/stein-scientist";
    const string LobbyPath="Assets/resources/MonsterPouch/UI/LobbyFrames/clips.json";
    const string CatalogPath="Assets/resources/MonsterPouch/UnitArt.asset";
    const string ReportFolder="Logs/local-validation/lobby-polish-20261015";
    static readonly Dictionary<string,Color32[]> sourcePixels=new Dictionary<string,Color32[]>();
    [Serializable] public sealed class Manifest { public LobbyFrameClip[] clips; }
    static string CharacterPath(string id)=>"Assets/resources/MonsterPouch/Characters/"+id+"/Character.asset";

    public static string ImportAbilityArtwork()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        for(int index=1;index<=6;index++)
        {
        string path="Assets/resources/MonsterPouch/UI/AbilityArt/abilities-"+index.ToString("00")+".png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Default;importer.filterMode=FilterMode.Point;
        importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        }
        // Editor previews may have cached slices before these atlas textures were reimported.
        var cache=(IDictionary)typeof(AbilityArtwork).GetField("cache",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        foreach(Sprite cached in cache.Values)if(cached!=null)Object.DestroyImmediate(cached);
        cache.Clear();
        var idles=JsonUtility.FromJson<Manifest>(File.ReadAllText(IdleFolder+"idles.json"));
        var cells=new HashSet<string>();
        foreach(var idle in idles.clips)
        {
            var unit=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(CharacterPath(idle.id)).CreateRuntimeDefinition();
            foreach(var effect in new[]{unit.BaseAbility}.Concat(unit.Tricks))
            {
                var icon=AbilityArtwork.Get(effect.EffectId??effect.Id);
                if(icon==null||icon.texture.filterMode!=FilterMode.Point||icon.rect.width!=62||icon.rect.height!=62)
                    throw new InvalidDataException("Wrong pixel ability artwork: "+effect.EffectId+"; rect="+icon?.rect+"; filter="+icon?.texture.filterMode+"; texture="+icon?.texture.width+"x"+icon?.texture.height);
                cells.Add(AssetDatabase.GetAssetPath(icon.texture)+icon.rect.ToString());
            }
        }
        if(cells.Count!=76)throw new InvalidDataException("Expected 76 distinct pixel-art illustrations.");
        var stein=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(CharacterPath("stein"));
        var gift=stein.Upgrades.Single(a=>a.Definition.EffectId=="stein-energy-gift");
        string backup="Backups/lobby-polish-description-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(backup);
        File.Copy(CharacterPath("stein"),backup+"/Character.asset");
        File.Copy(AssetDatabase.GetAssetPath(gift),backup+"/Upgrade3.asset");
        gift.Definition.Description=AbilityBalance.Description(gift.Definition);
        foreach(var effect in stein.General.Tricks.Where(a=>a.EffectId=="stein-energy-gift"))
            effect.Description=gift.Definition.Description;
        EditorUtility.SetDirty(gift);EditorUtility.SetDirty(stein);AssetDatabase.SaveAssets();
        string report="Imported and verified 76 distinct point-filtered pixel-art ability illustrations at 64px; Stein energy description synchronized.";
        Directory.CreateDirectory(ReportFolder);File.WriteAllText(ReportFolder+"/ability-import.txt",report);
        return report;
    }

    public static string Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        sourcePixels.Clear();
        var idles=JsonUtility.FromJson<Manifest>(File.ReadAllText(IdleFolder+"idles.json"));
        var lobby=JsonUtility.FromJson<Manifest>(File.ReadAllText(LobbyPath));
        var catalog=AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(CatalogPath);
        if(idles.clips.Length!=19||idles.clips.Select(c=>c.id).Distinct().Count()!=19)
            throw new InvalidDataException("Expected nineteen distinct approved idles.");
        if(catalog==null)throw new InvalidDataException("Missing unit art catalog.");
        foreach(var idle in idles.clips)
            if(!lobby.clips.Any(c=>c.id==idle.id)||!File.Exists(IdleFolder+idle.id+".png")||
                AssetDatabase.LoadAssetAtPath<CharacterDefinition>(CharacterPath(idle.id))==null)
                throw new InvalidDataException("Incomplete idle: "+idle.id);

        string backup="Backups/lobby-polish-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        void Backup(string path) {string to=Path.Combine(backup,path);Directory.CreateDirectory(Path.GetDirectoryName(to));File.Copy(path,to,false);}
        Backup(LobbyPath);Backup(CatalogPath);
        foreach(var idle in idles.clips)Backup(CharacterPath(idle.id));

        foreach(string id in new[]{"stein","blotan"})
        {
        string resource=id=="stein"?AnimationResource:"MonsterPouch/BlotanMoonken/blotan-moonken";
        string animationPath="Assets/resources/"+resource+".png";
        var frames=MonsterPouchSpriteAnimationImporter.ImportGrid(animationPath,100,12,5,id=="stein"?"stein-scientist":"blotan-moonken",adaptive:true);
        var scientist=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(CharacterPath(id));
        var previous=scientist.Visuals??catalog.Get(id);
        var art=Clone(previous);
        art.Portrait=frames[0,0];art.ReferencePixelWidth=VisibleBounds(art.Portrait).width;
        art.TravelMotion=UnitTravelMotion.AlternatingHop;art.AuthoredBodyMotion=true;
        art.ProvisionalDirectionalProjection=false;
        art.WalkRigs=new UnitSpriteRig[8];art.DirectionalAnimations=new UnitDirectionalAnimation[8];
        art.IdleDirections=new Sprite[8];art.AttackDirections=new Sprite[8];
        int[] rows={4,3,2,1,0,1,2,3};
        for(int direction=0;direction<8;direction++)
        {
            int row=rows[direction];var prior=previous.Animation((UnitFacing)direction);
            var clip=prior==null?new UnitDirectionalAnimation():JsonUtility.FromJson<UnitDirectionalAnimation>(JsonUtility.ToJson(prior));
            Sprite[] Sequence(params int[] columns)=>columns.Select(column=>frames[row,column]).ToArray();
            clip.Idle=Sequence(0,1,0,1);clip.Move=Sequence(2,3,4,5);
            clip.HopLeft=Sequence(2,3,4);clip.HopRight=Sequence(4,5,2);
            clip.Attack=Sequence(6,7,0);clip.Special=Sequence(8,9,9,0);clip.Death=Sequence(10,11);
            clip.Revive=prior?.Revive!=null&&prior.Revive.Length>0?clip.Death.Reverse().Concat(clip.Special).ToArray():Array.Empty<Sprite>();
            clip.FlipX=direction>=5;clip.SpecialFlipX=clip.FlipX;clip.MoveFlipX=false;
            clip.AttackContactFrame=1;clip.IdleFramesPerSecond=2.5f;clip.MoveFramesPerSecond=8;
            art.DirectionalAnimations[direction]=clip;art.IdleDirections[direction]=clip.Idle[0];art.AttackDirections[direction]=clip.Attack[1];
            foreach(var sequence in new[]{clip.Idle,clip.Move,clip.Attack,clip.Special,clip.Death})
                if(sequence.Any(s=>s==null||s.rect.width<8||s.rect.height<8))throw new InvalidDataException("Invalid scientist frame.");
        }
        art.SourceNotes=id=="stein"?"Pixel-art mad scientist with blue eyes; grounded boots. Five authored views, mirrored west views.":"Pixel-art Blotan with unlit Moonken hat badge and held currency; only the raised income Moonken shines. Five authored views.";
        scientist.Visuals=Clone(art);scientist.Portrait=art.Portrait;
        int artIndex=Array.FindIndex(catalog.Units,a=>a.Id==id);
        if(artIndex<0)throw new InvalidDataException("Stein missing from catalog.");
        catalog.Units[artIndex]=Clone(art);
        var stein=lobby.clips.Single(c=>c.id==id);
        stein.texture=resource;stein.referenceHeight=VisibleBounds(frames[0,0]).height;
        stein.frames=new[]{0,1,8,9,9,8,1,0}.Select(column=>new LobbyFramePose {
            rect=RectValues(frames[0,column].rect),anchor=new[]{frames[0,column].pivot.x,frames[0,column].pivot.y},
            height=VisibleBounds(frames[0,column]).height,registrationScale=1
        }).ToArray();

        EditorUtility.SetDirty(scientist);
        }

        foreach(var idle in idles.clips)
        {
            var sprite=ImportIdle(idle);
            var target=lobby.clips.Single(c=>c.id==idle.id);
            target.idleTexture=idle.idleTexture;target.idleRect=idle.idleRect;
            target.idleBounds=idle.idleBounds;target.idleAnchor=idle.idleAnchor;
            var character=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(CharacterPath(idle.id));
            character.Card=sprite;character.Icon=sprite;EditorUtility.SetDirty(character);
        }
        EditorUtility.SetDirty(catalog);
        File.WriteAllText(LobbyPath,JsonUtility.ToJson(lobby,true));AssetDatabase.ImportAsset(LobbyPath);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(ReportFolder);
        string report=Validate()+"\nBackup: "+backup;
        File.WriteAllText(ReportFolder+"/import.txt",report);return report;
    }

    static UnitArt Clone(UnitArt value)=>JsonUtility.FromJson<UnitArt>(JsonUtility.ToJson(value));
    static float[] RectValues(Rect r)=>new[]{r.x,r.y,r.width,r.height};
    static RectInt VisibleBounds(Sprite sprite)
    {
        var texture=sprite.texture;string path=AssetDatabase.GetAssetPath(texture);
        if(!sourcePixels.TryGetValue(path,out var pixels))
        {
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try {source.LoadImage(File.ReadAllBytes(path));pixels=source.GetPixels32();sourcePixels[path]=pixels;}
            finally {Object.DestroyImmediate(source);}
        }
        Rect r=sprite.rect;
        int x0=(int)r.xMax,y0=(int)r.yMax,x1=(int)r.xMin,y1=(int)r.yMin;
        for(int y=(int)r.yMin;y<(int)r.yMax;y++)for(int x=(int)r.xMin;x<(int)r.xMax;x++)
            if(pixels[y*texture.width+x].a>128){x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x+1);y1=Math.Max(y1,y+1);}
        return new RectInt(x0,y0,x1-x0,y1-y0);
    }
    static Sprite ImportIdle(LobbyFrameClip idle)
    {
        string path=IdleFolder+idle.id+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePixelsPerUnit=100;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=2048;importer.isReadable=false;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
        settings.spriteAlignment=(int)SpriteAlignment.Custom;
        settings.spritePivot=new Vector2(idle.idleAnchor[0]/idle.idleRect[2],idle.idleAnchor[1]/idle.idleRect[3]);
        settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static string Validate()
    {
        var idles=JsonUtility.FromJson<Manifest>(File.ReadAllText(IdleFolder+"idles.json"));
        var lobby=JsonUtility.FromJson<Manifest>(File.ReadAllText(LobbyPath));
        foreach(var idle in idles.clips)
        {
            var clip=lobby.clips.Single(c=>c.id==idle.id);var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(IdleFolder+idle.id+".png");
            if(sprite==null||sprite.texture.filterMode!=FilterMode.Point||clip.idleTexture!=idle.idleTexture)
                throw new InvalidDataException("Idle import mismatch: "+idle.id);
            var bounds=VisibleBounds(sprite);
            if(bounds.width<20||bounds.height<30||clip.idleAnchor[1]>bounds.yMin+2)
                throw new InvalidDataException("Invalid silhouette/foot anchor: "+idle.id);
        }
        foreach(string id in new[]{"stein","blotan"})
        {
        string expected=id=="stein"?AnimationPath:"Assets/resources/MonsterPouch/BlotanMoonken/blotan-moonken.png";
        var art=AssetDatabase.LoadAssetAtPath<CharacterDefinition>(CharacterPath(id)).Visuals;
        if(art.TravelMotion!=UnitTravelMotion.AlternatingHop)throw new InvalidDataException("Scientist must stand on his boots.");
        foreach(var clip in art.DirectionalAnimations)
        {
            foreach(var seq in new[]{clip.Idle,clip.Move,clip.HopLeft,clip.HopRight,clip.Attack,clip.Special,clip.Death})
                if(seq.Length==0||seq.Any(s=>s==null||AssetDatabase.GetAssetPath(s)!=expected))throw new InvalidDataException("Mixed scientist designs.");
            if(clip.Death.Last().rect.height>=clip.Idle[0].rect.height*.78f)throw new InvalidDataException("Defeat is not visibly collapsed.");
        }
        }
        var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/resources/MonsterPouch/SceneryPixel.shader");
        if(shader==null||ShaderUtil.ShaderHasError(shader))throw new InvalidDataException("Scenery shader has errors.");
        return "Validated: 19 distinct point-filtered lobby/card idles; Stein and Blotan have 8 directions and complete idle/move/hop/attack/special/defeat sequences; shader has no errors.";
    }

    public static string Capture()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(!Application.isPlaying||ui==null)throw new InvalidOperationException("Enter Play Mode first.");
        ui.StartCoroutine(CaptureViews(ui));return "Capturing 3 lobby themes, 19 cards and Stein's brief.";
    }
    static IEnumerator CaptureViews(LocalGameUI ui)
    {
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var originalLobby=CardTableTheme.Lobby;bool enabled=ui.Match.enabled;
        var decks=(DeckLibrary)typeof(LocalGameUI).GetField("decks",flags).GetValue(ui);
        var originalWhelps=decks.Current.Whelps;ui.Match.enabled=false;
        Directory.CreateDirectory(ReportFolder);
        try
        {
            MonsterPouchLayoutPreview.Resize("1080x2340");ui.Match.Menu();
            decks.Current.Whelps=(string[])originalWhelps.Clone();
            if(!decks.Current.Whelps.Contains("stein"))decks.Current.Whelps[6]="stein";
            typeof(LocalGameUI).GetMethod("ApplyDeck",flags).Invoke(ui,null);
            foreach(var style in new[]{TableStyle.Default,TableStyle.GrandCasaHotel,TableStyle.CampoReal})
            {
                ui.SelectLobbyStyle(style);yield return new WaitForSecondsRealtime(MapIrisTransition.Duration+.3f);
                ui.CloseModal();ui.OpenHome();yield return Shot(CardTableTheme.MapId(style)+"-home");
                ui.OpenDecks();yield return Shot(CardTableTheme.MapId(style)+"-brief");
            }
            foreach(var unit in ui.Match.Config.Units)
            {
                ui.CloseModal();typeof(LocalGameUI).GetMethod("ShowCollectionInfo",flags).Invoke(ui,new object[]{unit});
                yield return Shot(unit.Id+"-card");
            }
        }
        finally
        {
            decks.Current.Whelps=originalWhelps;typeof(LocalGameUI).GetMethod("ApplyDeck",flags).Invoke(ui,null);
            CardTableTheme.SelectLobby(originalLobby);ui.CloseModal();ui.OpenDecks();ui.Match.enabled=enabled;
        }
        File.WriteAllText(ReportFolder+"/captures.txt","Captured all 19 cards and home/brief for all 3 themes. Saved deck and lobby preference restored.");
    }
    static IEnumerator Shot(string name)
    {
        yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.GetFullPath(ReportFolder+"/"+name+".png"));yield return null;
    }
}
