using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;

// Explicit importer: never runs on reload and never changes gameplay or external effects.
public static class MonsterPouchReferenceRedesign
{
    const string Folder = "Assets/resources/MonsterPouch/ReferenceRedesign/";
    const string CatalogPath = "Assets/resources/MonsterPouch/UnitArt.asset";
    const string LobbyPath = "Assets/resources/MonsterPouch/UI/LobbyFrames/clips.json";
    static readonly int[] Rows = {4,3,2,1,0,7,6,5};
    static readonly Dictionary<Texture2D, Color32[]> SourcePixels = new Dictionary<Texture2D, Color32[]>();
    [Serializable] public sealed class Manifest { public Entry[] characters; }
    [Serializable] public sealed class Entry
    {
        public string id;
        public bool flying;
        public int[] rows;
        public int[] mirror;
        public int[] rowEdgesTop;
        public int[] special;
        public int[] death;
        public int columns = 12;
        public int rowCount = 8;
        public string notes;
        public SourceFrame[] sourceFrames;
        public float visibleWorldWidth;
    }
    [Serializable] public sealed class LobbyManifest { public LobbyFrameClip[] clips; }
    [Serializable] public sealed class Audit
    {
        public string id, source;
        public int directions, frames;
        public float worldWidth, referencePixels;
        public string[] warnings;
    }
    [Serializable] public sealed class Report { public string importedUtc; public Audit[] characters; }
    sealed class Prepared
    {
        public Entry entry;
        public CharacterDefinition character;
        public UnitArt art;
        public LobbyFrameClip lobby;
        public Audit audit;
    }

    const string PendingPath = "Library/ReferenceRedesign.pending";
    const string StatusPath = "Logs/local-validation/reference-redesign-status.txt";
    static double idleSince;
    static UnityEditor.TestTools.TestRunner.Api.TestRunnerApi validationRunner;
    static bool validationActive;

    [InitializeOnLoadMethod]
    static void ResumeRequestedImport()
    {
        if (File.Exists(PendingPath)) EditorApplication.update += ApplyWhenIdle;
    }

    static void ApplyWhenIdle()
    {
        if (!File.Exists(PendingPath)) { EditorApplication.update -= ApplyWhenIdle; return; }
        if (BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || File.Exists("Library/MonsterPouchCommand.json") ||
            !string.IsNullOrEmpty(SessionState.GetString("MonsterPouch.Validation.PendingRun", "")))
        { idleSince = 0; return; }
        if (idleSince == 0) { idleSince = EditorApplication.timeSinceStartup; return; }
        if (EditorApplication.timeSinceStartup - idleSince < 10) return;
        EditorApplication.update -= ApplyWhenIdle;
        Directory.CreateDirectory("Logs/local-validation");
        // Consume a user-requested one-shot marker, not a recurring import on every reload.
        File.Delete(PendingPath);
        try
        {
            File.WriteAllText(StatusPath, "Importing reference artwork...");
            string result = Apply();
            File.WriteAllText(StatusPath, result + "\nArtwork applied. Running focused edit-mode checks.");
            RunValidation();
        }
        catch (Exception exception)
        {
            File.WriteAllText(StatusPath, "FAILED: " + exception);
            Debug.LogException(exception);
        }
    }

    public static void RunValidation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Wait for the active Play Mode run to finish before reference Edit Mode tests.");
        if (validationActive) throw new InvalidOperationException("Reference validation already running.");
        validationRunner = ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
        UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.RegisterTestCallback(new ValidationResults());
        validationActive = true;
        validationRunner.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(
            new UnityEditor.TestTools.TestRunner.Api.Filter
            {
                testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
                testNames = new[] {
                    "MonsterPouch.Gameplay.Tests.EditMode.ReferenceHopTests",
                    "MonsterPouch.Gameplay.Tests.EditMode.ReferenceRosterQualityTests",
                    "MonsterPouch.Gameplay.Tests.EditMode.UnitAuthoredAnimationTests",
                    "MonsterPouch.Gameplay.Tests.EditMode.UnitPresentationTimingTests",
                    "MonsterPouch.Gameplay.Tests.EditMode.ControlMotionPresentationTests"
                }
            }));
    }

    sealed class ValidationResults : UnityEditor.TestTools.TestRunner.Api.ICallbacks
    {
        public void RunStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor tests) { }
        public void TestStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor test) { }
        public void TestFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result) { }
        public void RunFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result)
        {
            if (!validationActive || !ContainsReferenceResults(result)) return;
            validationActive = false;
            UnityEditor.TestTools.TestRunner.Api.TestRunnerApi.SaveResultToFile(result,
                Path.GetFullPath("Logs/local-validation/reference-redesign-tests.xml"));
            File.AppendAllText(StatusPath, "\nTests: " + result.ResultState + "; passed=" + result.PassCount +
                "; failed=" + result.FailCount + "; skipped=" + result.SkipCount);
        }
        static bool ContainsReferenceResults(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor result)
        {
            if (result.FullName.StartsWith("MonsterPouch.Gameplay.Tests.EditMode.ReferenceRosterQualityTests")) return true;
            return result.HasChildren && result.Children.Any(ContainsReferenceResults);
        }
    }

    [MenuItem("Monster Pouch/Art/Apply supplied reference redesign")]
    public static void ApplyMenu() => Debug.Log(Apply());

    public static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Exit Play Mode before importing character artwork.");
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "manifest.json"));
        if (manifest?.characters == null || manifest.characters.Length == 0)
            throw new InvalidDataException("No reference characters to import.");
        if (manifest.characters.Select(x => x.id).Distinct().Count() != manifest.characters.Length)
            throw new InvalidDataException("Duplicate character IDs.");
        var catalog = AssetDatabase.LoadAssetAtPath<UnitArtCatalog>(CatalogPath);
        if (catalog == null) throw new InvalidDataException("Missing art catalog.");
        var lobby = JsonUtility.FromJson<LobbyManifest>(File.ReadAllText(LobbyPath));
        var prepared = new List<Prepared>();
        SourcePixels.Clear();
        // Prepare and validate every sprite before assigning any character.
        foreach (var entry in manifest.characters) prepared.Add(Prepare(entry, catalog));

        string backup = Path.Combine("Backups", "reference-redesign-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
        Backup(CatalogPath, backup);
        Backup(LobbyPath, backup);
        foreach (var item in prepared) Backup(AssetDatabase.GetAssetPath(item.character), backup);
        foreach (var item in prepared)
        {
            var character = item.character;
            character.Visuals = Clone(item.art);
            character.Portrait = item.art.Portrait;
            character.Card = item.art.Portrait;
            character.Icon = item.art.Portrait;
            int index = Array.FindIndex(catalog.Units, a => a.Id == item.entry.id);
            if (index < 0) throw new InvalidDataException("Unknown catalog ID: " + item.entry.id);
            catalog.Units[index] = Clone(item.art);
            EditorUtility.SetDirty(character);
            var clips = new List<LobbyFrameClip>(lobby.clips);
            int lobbyIndex = clips.FindIndex(c => c.id == item.entry.id);
            if (lobbyIndex < 0) clips.Add(item.lobby); else clips[lobbyIndex] = item.lobby;
            lobby.clips = clips.ToArray();
        }
        EditorUtility.SetDirty(catalog);
        File.WriteAllText(LobbyPath, JsonUtility.ToJson(lobby, true));
        AssetDatabase.ImportAsset(LobbyPath);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Logs/local-validation");
        File.WriteAllText("Logs/local-validation/reference-redesign.json",
            JsonUtility.ToJson(new Report { importedUtc = DateTime.UtcNow.ToString("o"),
                characters = prepared.Select(p => p.audit).ToArray() }, true));
        return "Imported " + prepared.Count + " reference designs; combat directions, portraits and lobby updated. Backup: " + backup;
    }

    static Prepared Prepare(Entry entry, UnitArtCatalog catalog)
    {
        var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
            "Assets/resources/MonsterPouch/Characters/" + entry.id + "/Character.asset");
        if (character == null || character.Id != entry.id) throw new InvalidDataException("Unknown character: " + entry.id);
        var previous = character.Visuals ?? catalog.Get(entry.id);
        if (previous == null) throw new InvalidDataException("Missing visual settings: " + entry.id);
        string path = "ArtArchive/LegacyCleanup-20261102/Assets/art/ReferenceRedesign/Sources/" + entry.id + "-combat.png";
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        string packedPath = PackSource(path, entry);
        Sprite[,] frames = MonsterPouchSpriteAnimationImporter.ImportGrid(packedPath, 100, entry.columns, entry.rowCount,
            entry.id + "-reference-v2", adaptive: false,
            rowEdgesTop: Enumerable.Range(0,entry.rowCount+1).Select(r=>r*192).ToArray());
        int[] rows = entry.rows != null && entry.rows.Length == 8 ? entry.rows : Rows;
        var warnings = new List<string>();
        // Keep the existing world width and scale; use the new south pose as the size reference.
        var art = Clone(previous);
        art.Id = entry.id;
        art.TravelMotion = entry.flying ? UnitTravelMotion.Flying : UnitTravelMotion.AlternatingHop;
        art.AuthoredBodyMotion = true;
        if (entry.visibleWorldWidth > 0)
            art.WorldWidth = entry.visibleWorldWidth / (1.2f * art.DisplayScale);
        art.Portrait = frames[rows[4],0];
        art.ReferencePixelWidth = VisibleWidth(art.Portrait);
        art.WalkRigs = new UnitSpriteRig[8];
        art.DirectionalAnimations = new UnitDirectionalAnimation[8];
        art.IdleDirections = new Sprite[8];
        art.AttackDirections = new Sprite[8];
        for (int facing=0; facing<8; facing++)
        {
            int row = rows[facing];
            if (row < 0 || row >= entry.rowCount) throw new InvalidDataException("Invalid direction row.");
            var old = previous.Animation((UnitFacing)facing);
            var clip = old == null ? new UnitDirectionalAnimation() :
                JsonUtility.FromJson<UnitDirectionalAnimation>(JsonUtility.ToJson(old));
            clip.Idle = Seq(frames,row,0,1,0,1);
            clip.Move = Seq(frames,row,2,3,4,5);
            clip.HopLeft = Seq(frames,row,2,3,4);
            clip.HopRight = Seq(frames,row,4,5,2);
            clip.Attack = Seq(frames,row,6,7,0);
            clip.Special = Seq(frames,row,entry.special ?? new[]{8,9,9,0});
            // The generated side release turned its back. Keep the visible three-quarter roar.
            if (entry.id == "tokoro" && (facing == 2 || facing == 6))
                clip.Special = new[]{frames[row,8],frames[1,9],frames[1,9],frames[row,0]};
            clip.Death = Seq(frames,row,entry.death ?? new[]{10,11});
            // Reviving characters keep their runtime revive behavior, with the new artwork throughout.
            clip.Revive = old?.Revive != null && old.Revive.Length > 0
                ? clip.Death.Reverse().Concat(clip.Special).ToArray() : Array.Empty<Sprite>();
            clip.AttackContactFrame = 1;
            clip.IdleFramesPerSecond = 2.5f;
            clip.MoveFramesPerSecond = 8;
            if (entry.id == "stein")
            {
                clip.MoveFramesPerSecond = 10;
                clip.AttackLungeDistance = .24f;
                clip.AttackImpactSquash = .13f;
                clip.SpecialLungeDistance = .18f;
                clip.SpecialLift = .14f;
                clip.SpecialPunchScale = .16f;
            }
            clip.FlipX = entry.mirror != null && entry.mirror.Contains(facing);
            clip.SpecialFlipX = clip.FlipX;
            clip.MoveFlipX = false;
            art.DirectionalAnimations[facing] = clip;
            art.IdleDirections[facing] = clip.Idle[0];
            art.AttackDirections[facing] = clip.Attack[1];
            foreach (var sprite in clip.Idle.Concat(clip.Move).Concat(clip.Attack).Concat(clip.Special).Concat(clip.Death).Concat(clip.Revive))
            {
                if (sprite == null || sprite.rect.width < 8 || sprite.rect.height < 8)
                    throw new InvalidDataException("Empty or invalid sprite in " + entry.id);
            }
        }
        art.ProvisionalDirectionalProjection = false;
        art.SourceNotes = "User reference redesign 2026-10-15. Built-in image generation; immutable RGBA source. " + entry.notes;
        var rest = frames[rows[4],0];
        var visible = VisibleBounds(rest);
        int[] specials = entry.special ?? new[]{8,9,9,0};
        var gesture = Seq(frames,rows[4],0,1,specials[0],specials[1],specials[1],specials[0],1,0);
        string resource = "MonsterPouch/ReferenceRedesign/" + entry.id + "-clean-v3";
        var lobby = new LobbyFrameClip
        {
            id = entry.id, texture = resource, idleTexture = resource,
            idleRect = RectValues(rest.rect),
            idleBounds = new[]{(float)visible.x,(float)visible.y,(float)visible.width,(float)visible.height},
            idleAnchor = new[]{rest.pivot.x,rest.pivot.y},
            referenceHeight = visible.height,
            frames = gesture.Select(s => new LobbyFramePose
            {
                rect = RectValues(s.rect), anchor = new[]{s.pivot.x,s.pivot.y},
                height = VisibleBounds(s).height, registrationScale = 1
            }).ToArray()
        };
        return new Prepared { entry=entry, character=character, art=art, lobby=lobby,
            audit=new Audit { id=entry.id, source=path,directions=8,frames=entry.columns*entry.rowCount,
                worldWidth=art.WorldWidth,referencePixels=art.ReferencePixelWidth,warnings=warnings.ToArray() } };
    }
    [Serializable] public sealed class SourceFrame
    {
        public int[] rect;
        public int area;
        public float[] center;
    }

    // Technical sprite extraction only: source RGBA files remain immutable.
    // Connected silhouettes may have overlapping bounding rectangles (wide wings).
    // Copy their original pixels into padded cells without scaling or recoloring.
    static string PackSource(string sourcePath, Entry entry)
    {
        if (entry.sourceFrames == null || entry.sourceFrames.Length != entry.columns * entry.rowCount)
            throw new InvalidDataException("Missing measured silhouettes: " + entry.id);
        const int cellWidth = 256, cellHeight = 192;
        var source = new Texture2D(2,2,TextureFormat.RGBA32,false);
        var packed = new Texture2D(cellWidth*entry.columns,cellHeight*entry.rowCount,TextureFormat.RGBA32,false);
        try
        {
            if (!ImageConversion.LoadImage(source,File.ReadAllBytes(sourcePath)))
                throw new InvalidDataException(sourcePath);
            var input=source.GetPixels32();
            var output=new Color32[packed.width*packed.height];
            for(int index=0; index<entry.sourceFrames.Length; index++)
            {
                var frame=entry.sourceFrames[index];
                int sx=frame.rect[0],top=frame.rect[1],w=frame.rect[2],h=frame.rect[3];
                if(w>cellWidth-8||h>cellHeight-8)throw new InvalidDataException("Oversized silhouette: "+entry.id);
                var mask=new bool[w*h];
                int seed=-1;float closest=float.MaxValue;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    if(input[(source.height-1-top-y)*source.width+sx+x].a<=128)continue;
                    float dx=sx+x-frame.center[0],dy=top+y-frame.center[1],distance=dx*dx+dy*dy;
                    if(distance<closest){closest=distance;seed=y*w+x;}
                }
                if(seed<0)throw new InvalidDataException("Empty silhouette");
                var queue=new Queue<int>();queue.Enqueue(seed);mask[seed]=true;int count=0;
                while(queue.Count>0)
                {
                    int p=queue.Dequeue(),px=p%w,py=p/w;count++;
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int x=px+dx,y=py+dy;
                        if(x<0||x>=w||y<0||y>=h||mask[y*w+x])continue;
                        if(input[(source.height-1-top-y)*source.width+sx+x].a<=128)continue;
                        mask[y*w+x]=true;queue.Enqueue(y*w+x);
                    }
                }
                if(count!=frame.area)throw new InvalidDataException("Component changed: "+entry.id+" frame "+index);
                int destX=(index%entry.columns)*cellWidth+(cellWidth-w)/2;
                int destY=packed.height-(index/entry.columns+1)*cellHeight+8;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    Color32 pixel=input[(source.height-1-top-y)*source.width+sx+x];
                    bool keep=mask[y*w+x];
                    // Only the measured connected silhouette belongs to this sprite.
                    // Transparent gutters must not contain low-alpha colored speckles from generation.
                    if(keep)output[(destY+h-1-y)*packed.width+destX+x]=pixel;
                }
            }
            packed.SetPixels32(output);packed.Apply(false,false);
            string result=Folder+entry.id+"-clean-v3.png";
            byte[] bytes=packed.EncodeToPNG();
            if(!File.Exists(result)||!File.ReadAllBytes(result).SequenceEqual(bytes))File.WriteAllBytes(result,bytes);
            return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(source);UnityEngine.Object.DestroyImmediate(packed); }
    }
    static UnitArt Clone(UnitArt a) => JsonUtility.FromJson<UnitArt>(JsonUtility.ToJson(a));
    static Sprite[] Seq(Sprite[,] f,int row,params int[] columns) => columns.Select(c => f[row,c]).ToArray();
    static float[] RectValues(Rect r) => new[]{r.x,r.y,r.width,r.height};
    static float VisibleWidth(Sprite s) => VisibleBounds(s).width;
    static RectInt VisibleBounds(Sprite s)
    {
        var r=s.rect;
        if (!SourcePixels.TryGetValue(s.texture, out var pixels))
        {
            var source = new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if (!ImageConversion.LoadImage(source,File.ReadAllBytes(AssetDatabase.GetAssetPath(s.texture))))
                    throw new InvalidDataException("Cannot read source pixels.");
                pixels=source.GetPixels32(); SourcePixels[s.texture]=pixels;
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
        int x0=(int)r.xMax,y0=(int)r.yMax,x1=(int)r.x,y1=(int)r.y;
        for(int y=(int)r.y;y<(int)r.yMax;y++)
        for(int x=(int)r.x;x<(int)r.xMax;x++)
        {
            if(pixels[y*s.texture.width+x].a<=128)continue;
            x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x+1);y1=Math.Max(y1,y+1);
        }
        if(x1<=x0||y1<=y0)throw new InvalidDataException("Invisible sprite: "+s.name);
        return new RectInt(x0-(int)r.x,y0-(int)r.y,x1-x0,y1-y0);
    }
    static void Backup(string path,string folder)
    {
        string output=Path.Combine(folder,path);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.Copy(path,output,false);
    }
}

