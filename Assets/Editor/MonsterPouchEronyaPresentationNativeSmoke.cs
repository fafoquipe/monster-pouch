using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Explicit calls only. Uses the existing isolated Nation session; never starts one,
// advances combat, edits MatchConfig, activates a character or saves an asset/profile.
public static class MonsterPouchEronyaPresentationNativeSmoke
{
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly MethodInfo Advance = typeof(UnitPresentation).GetMethod("AdvancePresentation", Any);
    static readonly string[] Directions = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };
    static readonly Vector2Int[] Offsets = { new Vector2Int(0,-1), new Vector2Int(1,-1), new Vector2Int(1,0),
        new Vector2Int(1,1), new Vector2Int(0,1), new Vector2Int(-1,1), new Vector2Int(-1,0), new Vector2Int(-1,-1) };
    static readonly Dictionary<string,string[]> Batches = new Dictionary<string,string[]>(StringComparer.Ordinal) {
        { "geothar", new[] { "korran", "golem-tectonico", "la-minera", "protectora-del-mausoleo" } },
        { "aireola", new[] { "alveyr", "zephalontropo", "alboguera", "palista" } },
        { "aquarion", new[] { "drayven", "titanica", "jabalinero", "maestro-de-las-mareas" } },
        { "naturalis", new[] { "cael", "guarda-veterano", "arboreo", "antidota" } },
        { "volkanar", new[] { "serya", "legionario", "dagata", "canonero-del-orvek" } }
    };
    static LocalGameUI ui;
    static GameObject root;
    static BattleUnit actor;
    static UnitPresentation view;
    static UnitArt art;
    static UnitDefinition definition;
    static BoardCell origin;
    static Coroutine running;
    static string directory, boardBefore, configBefore;
    static float simulationBefore, timeScaleBefore;
    static bool pausedBefore, pauseOwned;
    static int actorsBefore, roundBefore;
    static Report report;
    static CharacterReport current;
    static Texture2D sheet;
    const int TileSize = 208, SheetColumns = 4, SheetRows = 4;
    static readonly Dictionary<string,string> SourceHashes = new Dictionary<string,string>();

    [Serializable] public sealed class Sample
    {
        public string name, facing, sprite, png, fullFramePng, sha256;
        public Rect screenBounds, cropBounds;
        public Vector3 ground, foot, head;
        public Vector2 bodyReference;
        public int width, height, resetVersion, sheetColumn, sheetRow;
    }
    [Serializable] public sealed class CharacterReport
    {
        public string id, travelMotion, ability, error, reviewNote;
        public bool nativeChecksPassed, visualReviewed, pauseChecked, pausePassed, resetPassed, deathPassed;
        public float sizePercent, attackContactDelay, superDuration;
        public int directionsChecked, uniqueBodySprites;
        public List<Sample> samples = new List<Sample>();
    }
    [Serializable] public sealed class Report
    {
        public string faction, status = "running", error, startedUtc, finishedUtc, session, currentStep, directory;
        public string scope = "Real imported body sprites through UnitPresentation: eight facings, cardinal/diagonal travel, basic and Super poses, real pause, death/reset and anchors. Combat scheduling, projectile/VFX, status and UI acceptance have separate owners.";
        public bool nativeChecksPassed, presentationPassed, cleanupPassed, sessionPreserved, pausePassed;
        public bool productionAssetsWritten = false, combatAdvanced = false, matchConfigChanged = false;
        public List<CharacterReport> characters = new List<CharacterReport>();
        public List<string> evidencePaths = new List<string>();
    }

    public static string RunBatch(string faction)
    {
        if (!Batches.ContainsKey(faction ?? "")) throw new ArgumentException("Use geothar, aireola, aquarion, naturalis or volkanar.");
        if (running != null || root != null || report?.status == "running") throw new InvalidOperationException("A batch is active. Use Status or Cancel.");
        ui = Object.FindFirstObjectByType<LocalGameUI>();
        RequireSession();
        Check(!ui.Match.Paused && Mathf.Approximately(Time.timeScale, 1), "Resume the isolated QA battle before RunBatch.");
        Check(Advance != null, "UnitPresentation.AdvancePresentation is missing.");
        Check(!Object.FindObjectsByType<UnitPresentation>(FindObjectsSortMode.None).Any(v =>
            v.gameObject.name.StartsWith("QA-Martio-Art-Smoke-", StringComparison.Ordinal) ||
            v.gameObject.name.StartsWith("QA-Eronya-Presentation-", StringComparison.Ordinal)), "Another presentation helper owns a QA actor.");
        origin = FindEmptyRoute();
        directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/local-validation/eronya-presentation/",
            faction + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0,6)));
        Directory.CreateDirectory(directory);
        report = new Report { faction = faction, startedUtc = DateTime.UtcNow.ToString("o"), directory = directory,
            session = MonsterPouchNationNativePreview.Status() };
        SourceHashes.Clear();
        boardBefore = BoardIdentity(); actorsBefore = ui.Match.Actors.Count; roundBefore = ui.Match.Round;
        simulationBefore = ui.Match.Simulation.Elapsed; configBefore = JsonUtility.ToJson(ui.Match.Config);
        pausedBefore = ui.Match.Paused; timeScaleBefore = Time.timeScale;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += Watchdog;
        try
        {
            foreach (string id in Batches[faction]) ValidateCharacter(Load(id));
            SaveReport();
            running = ui.StartCoroutine(Drive());
            if (report.status != "running") running = null;
            return "Explicit native batch started: " + faction + ". Keep Game view rendering. " + directory;
        }
        catch (Exception error) { report.error = error.ToString(); Finish(); throw; }
    }

    public static string Status() => report == null ? "Not run." : JsonUtility.ToJson(report, true);
    public static string Cancel()
    {
        if (report?.status != "running") return Status();
        report.error = "Explicitly cancelled; presentationPassed remains false.";
        if (running != null && ui != null) ui.StopCoroutine(running);
        Finish(); return Status();
    }
    // A separate explicit call after looking at this character's native captures.
    // It cannot bless a failed/incomplete batch or survive missing/tampered evidence.
    public static string RecordVisualReview(string characterId, bool passed, string note)
    {
        Check(report != null && report.status != "running" && report.nativeChecksPassed && report.cleanupPassed && report.sessionPreserved,
            "Complete a successful native batch first. No approval is possible before Unity execution.");
        Check(!string.IsNullOrWhiteSpace(note), "Record what was inspected or the visible failure.");
        var entry = report.characters.SingleOrDefault(c => c.id == characterId);
        Check(entry != null && entry.nativeChecksPassed, "Character is not in the completed batch.");
        foreach (var c in report.characters) foreach (var s in c.samples.GroupBy(s => s.png).Select(g => g.First()))
            Check(File.Exists(s.png) && Hash(s.png) == s.sha256, "Capture is missing or changed: " + s.png);
        foreach (var p in SourceHashes) Check(Hash(p.Key) == p.Value, "Imported source changed since capture: " + p.Key);
        entry.visualReviewed = passed; entry.reviewNote = note;
        report.presentationPassed = report.characters.Count == 4 && report.characters.All(c => c.nativeChecksPassed && c.visualReviewed);
        report.status = report.presentationPassed ? "presentation-passed" : passed ? "awaiting-character-visual-reviews" : "visual-review-failed";
        SaveReport(); return Status();
    }

    static void BeforeReload() => Cancel();
    static void OnPlayMode(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode) Cancel(); }
    static double lastProgress;
    static void Watchdog()
    {
        if (report?.status != "running") return;
        if (!Application.isPlaying || ui == null || ui.Match == null || ui.Match.enabled ||
            (lastProgress > 0 && EditorApplication.timeSinceStartup - lastProgress > 90))
        { report.error = "QA session changed or Game view did not render for 90 seconds."; if (running != null && ui != null) ui.StopCoroutine(running); Finish(); }
    }

    static IEnumerator Drive()
    {
        var steps = Samples(); lastProgress = EditorApplication.timeSinceStartup;
        try
        {
            while (true)
            {
                object wait = null; bool next;
                try { next = steps.MoveNext(); if (next) wait = steps.Current; }
                catch (Exception error) { report.error = error.ToString(); if (current != null) current.error = error.Message; Debug.LogException(error); break; }
                if (!next) { report.nativeChecksPassed = true; break; }
                lastProgress = EditorApplication.timeSinceStartup; yield return wait;
            }
        }
        finally { (steps as IDisposable)?.Dispose(); Finish(); }
    }

    static IEnumerator Samples()
    {
        foreach (string id in Batches[report.faction])
        {
            RequireSession(); var character = Load(id); definition = character.CreateRuntimeDefinition();
            art = character.Visuals;
            current = new CharacterReport { id = id, travelMotion = art.TravelMotion.ToString(),
                sizePercent = art.InGameSizePercent, ability = definition.BaseAbility.EffectId };
            report.characters.Add(current);
            sheet = new Texture2D(TileSize * SheetColumns,TileSize * SheetRows,TextureFormat.RGBA32,false);
            sheet.SetPixels(Enumerable.Repeat(new Color(.09f,.12f,.16f,1),sheet.width*sheet.height).ToArray());
            root = new GameObject("QA-Eronya-Presentation-" + id) { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(ui.Match.transform, false);
            actor = definition.IsMonster ? (BattleUnit)root.AddComponent<MonsterUnit>() : root.AddComponent<WhelpUnit>();
            actor.Initialize("qa-presentation-" + id, BoardSide.Blue, UnitStats.FromDefinition(definition, null, false));
            actor.SetCurrentCell(origin); // Reference only; never enters Board occupancy or Match.Actors.
            view = root.AddComponent<UnitPresentation>(); view.enabled = false;
            current.uniqueBodySprites = BodySprites(art).Distinct().Count();
            Directory.CreateDirectory(Path.Combine(directory,id));
            for (int d = 0; d < 8; d++)
            {
                Reset(d); var clip = art.Animation((UnitFacing)d);
                Label("idle-" + Directions[d]); yield return new WaitForEndOfFrame();
                Capture(report.currentStep, clip.Idle, d == 4); current.directionsChecked++;

                Reset(d); view.Attack(Target(d), definition.AttackKind == AttackKind.Ranged);
                float delay = view.AttackContactDelay; current.attackContactDelay = delay;
                Check(delay > 0, "Basic attack contact delay is missing: " + id);
                Step(delay * .25f); CheckPose("basic-anticipation-" + Directions[d],clip.Attack);
                Step(delay * .75f);
                Check(view.IsAttacking && view.AttackContactReached, "Native basic pose did not reach its real contact clock.");
                var contact = new[] { clip.Attack[Mathf.Clamp(clip.AttackContactFrame,0,clip.Attack.Length-1)] };
                CheckPose("basic-contact-" + Directions[d],contact);
                if (d == 2) { Label("basic-contact-e"); yield return new WaitForEndOfFrame(); Capture(report.currentStep,contact); }
                Step(delay + 5); Check(!view.IsAttacking, "Basic attack did not finish.");

                Reset(d); view.UseAbility(current.ability);
                float duration = view.GetAbilityDuration(current.ability); current.superDuration = duration;
                Check(duration > 0 && view.IsUsingAbility, "Real Super has no native body presentation: " + id);
                float cast = AbilityTiming.Cast(actor.BaseStats,current.ability);
                float release = AbilityTiming.HasPhases(actor.BaseStats,current.ability) ? cast : duration * Mathf.Clamp(clip.SpecialReleasePoint,.15f,.85f);
                float sampleTime = Mathf.Min(duration * .95f, release + .00001f);
                Step(release * .25f); CheckPose("super-anticipation-" + Directions[d],clip.Special);
                Step(Mathf.Max(0,sampleTime - release * .25f));
                Check(view.IsUsingAbility, "Super finished before its release sample.");
                CheckPose("super-release-" + Directions[d],clip.Special);
                if (d == 2) { Label("super-release-e"); yield return new WaitForEndOfFrame(); Capture(report.currentStep,clip.Special); }
                Step(duration + .01f); Check(!view.IsUsingAbility, "Super pose did not return.");
            }

            foreach (int d in new[] { 2, 1 })
            {
                Reset(d); float duration = actor.BaseStats.MoveInterval, previous = 0;
                Check(duration > 0, "Missing real MoveInterval.");
                view.Move(root.transform.position, Target(d), duration);
                for (int phase = 0; phase < 3; phase++)
                {
                    float fraction = new[] { .215f, .61f, .895f }[phase]; Step(duration * (fraction - previous)); previous = fraction;
                    var clip = art.Animation(view.Facing);
                    Check(!actor.IsFlying, "Cosmetic flight must not enable gameplay flight/immunity.");
                    if (art.TravelMotion == UnitTravelMotion.Flying)
                    {
                        Check(view.TryGetPresentationAnchors(out var anchor) && anchor.FootPivotWorldPosition.y > anchor.GroundWorldPosition.y,
                            "Alveyr hover has no visual lift above grounded anchor.");
                    }
                    else if (phase == 1) Check(root.transform.position.y > Vector3.Lerp(ui.Match.Mapper.GetWorldPosition(origin),Target(d),.5f).y,
                        "Ground unit has no travel arc.");
                    Label((id == "alveyr" ? "float-" : "hop-") + (d == 2 ? "cardinal-" : "diagonal-") + phase);
                    var poses = id == "alveyr" ? clip.Move.Concat(clip.Idle).ToArray() : new[] { clip.HopLeft[phase] };
                    CheckPose(report.currentStep,poses);
                    if (phase == 1) { yield return new WaitForEndOfFrame(); Capture(report.currentStep,poses); }
                }
                Step(duration * (1.01f - previous)); Check(!view.IsMoving, "Native travel did not complete.");
            }

            if (report.characters.Count == 1)
            {
            Reset(2); view.Move(root.transform.position,Target(2),actor.BaseStats.MoveInterval); Step(actor.BaseStats.MoveInterval * .61f);
            var heldPosition = root.transform.position; var heldVisual = view.VisualRoot.localPosition; var heldSprite = view.Renderer.sprite;
            pauseOwned = true; ui.Match.Pause(true); view.enabled = true;
            yield return null; yield return null;
            Check(root.transform.position == heldPosition && view.VisualRoot.localPosition == heldVisual && view.Renderer.sprite == heldSprite,
                "Real paused Update advanced the presentation.");
            view.enabled = false; RestorePause(); current.pauseChecked = current.pausePassed = report.pausePassed = true;
            Label("pause-held-e"); yield return new WaitForEndOfFrame(); Capture(report.currentStep, new[] { heldSprite });
            Step(actor.BaseStats.MoveInterval * .1f); Check(root.transform.position != heldPosition, "Travel failed to resume after pause.");
            view.SetFrozen(true); var frozen = root.transform.position; Step(1);
            Check(!view.IsMoving && root.transform.position == frozen, "Round freeze did not settle movement.");
            }

            Reset(2); view.Die(); Step(.18f); Check(view.IsDying, "Death presentation did not begin.");
            Label("death-e"); yield return new WaitForEndOfFrame(); Capture(report.currentStep, art.Animation(UnitFacing.East).Death);
            Step(2); Check(view.DeathFinished, "Death did not finish."); current.deathPassed = true;
            int generation = actor.CombatResetVersion;
            actor.Initialize("qa-presentation-" + id, BoardSide.Blue, UnitStats.FromDefinition(definition, null, false));
            actor.SetCurrentCell(origin); Reset(4);
            Check(actor.CombatResetVersion != generation && !view.IsDying && !view.IsMoving && !view.IsUsingAbility && !view.IsFrozen,
                "Reset left residual presentation state.");
            current.resetPassed = true; Label("reset-s"); yield return new WaitForEndOfFrame(); Capture(report.currentStep, art.Animation(UnitFacing.South).Idle);
            current.nativeChecksPassed = true; SaveSheet(); RemoveActor(); SaveReport();
        }
    }

    static CharacterDefinition Load(string id)
    {
        var asset = Resources.Load<CharacterDefinition>("MonsterPouch/Characters/" + id + "/Character");
        Check(asset != null && asset.Id == id && asset.Visuals != null, "Imported Character/Visuals missing: " + id);
        return asset;
    }
    static void ValidateCharacter(CharacterDefinition asset)
    {
        var a = asset.Visuals;
        Check(a.Id == asset.Id && a.DirectionalAnimations?.Length == 8, "Eight real authored directions required: " + asset.Id);
        Check(asset.Id == "alveyr" ? a.TravelMotion == UnitTravelMotion.Flying :
            a.TravelMotion == UnitTravelMotion.WholeBodyHop && a.WholeBodyHopUsesPoses, "Wrong travel profile: " + asset.Id);
        RememberAsset(asset);
        for (int d = 0; d < 8; d++)
        {
            var c = a.Animation((UnitFacing)d);
            Check(c != null && !c.FlipX && !c.MoveFlipX && !c.SpecialFlipX, "Missing/mirrored authored facing: " + asset.Id);
            Check(c.Idle?.Length > 0 && c.Attack?.Length >= 2 && c.Special?.Length >= 2 && c.Death?.Length > 0,
                "Incomplete real clips: " + asset.Id + "/" + Directions[d]);
            Check(asset.Id == "alveyr" ? c.Move?.Length > 0 : c.HopLeft?.Length == 3, "Missing travel poses: " + asset.Id);
        }
        foreach (var sprite in BodySprites(a))
        {
            Check(sprite != null && sprite.texture != null && sprite.rect.width > 0 && sprite.pixelsPerUnit > 0, "Imported Sprite failed: " + asset.Id);
            string path = AssetDatabase.GetAssetPath(sprite);
            Check(!string.IsNullOrEmpty(path) && path.EndsWith(".png",StringComparison.OrdinalIgnoreCase) &&
                path.IndexOf(asset.Id,StringComparison.OrdinalIgnoreCase) >= 0, "Body sprite belongs to a different character: " + path);
            RememberAsset(sprite);
        }
    }
    static IEnumerable<Sprite> BodySprites(UnitArt a) => a.DirectionalAnimations.SelectMany(c =>
        c.Idle.Concat(c.Attack).Concat(c.Special).Concat(c.Death).Concat(a.TravelMotion == UnitTravelMotion.Flying ? c.Move : c.HopLeft));
    static void RememberAsset(Object asset)
    {
        string path = Path.GetFullPath(AssetDatabase.GetAssetPath(asset));
        Check(File.Exists(path), "Imported asset source is missing: " + path);
        SourceHashes[path] = Hash(path);
        if (File.Exists(path + ".meta")) SourceHashes[path + ".meta"] = Hash(path + ".meta");
    }
    static string Hash(string path)
    {
        if (!File.Exists(path)) return null;
        using (var sha = SHA256.Create()) using (var file = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
    }
    static BoardCell FindEmptyRoute()
    {
        for (int y = 5; y < 9; y++) for (int x = 1; x < 5; x++)
        {
            var cells = new[] { ui.Match.Board.GetCell(x,y), FacingCell(x,y,2), FacingCell(x,y,1) };
            if (cells.All(c => c != null && !c.IsBlocked && c.OccupiedBy == null && c.ReservedBy == null)) return cells[0];
        }
        throw new InvalidOperationException("No empty visible route. Prepare a frozen QA battle explicitly.");
    }
    static void Reset(int direction)
    {
        RequireSession(); root.transform.position = ui.Match.Mapper.GetWorldPosition(origin);
        view.Configure(actor,ui.Match.Mapper,art); view.enabled = false; view.Face(Offsets[direction]); Step(0);
        Check(view.Facing == (UnitFacing)direction, "Wrong board-to-facing mapping.");
    }
    static BoardCell FacingCell(int x,int y,int d)
    {
        var mapper=ui.Match.Mapper;int flip=mapper.Flipped?-1:1;
        int sx=(mapper.CellSize.x>=0?1:-1)*flip,sy=(mapper.CellSize.y>=0?-1:1)*flip;
        return ui.Match.Board.GetCell(x+Offsets[d].x*sx,y+Offsets[d].y*sy);
    }
    static Vector3 Target(int d) => ui.Match.Mapper.GetWorldPosition(FacingCell(origin.X,origin.Y,d));
    static void Step(float seconds) => Advance.Invoke(view,new object[] { seconds });
    static void Label(string step) { report.currentStep = step; }
    static void CheckPose(string name, Sprite[] expected)
    {
        Check(view.Renderer.enabled && view.Renderer.sprite != null && expected != null && expected.Contains(view.Renderer.sprite) &&
            !view.Renderer.flipX && view.Renderer.color.a > .1f, "Wrong/hidden native pose: " + current.id + "/" + name);
    }
    static void Capture(string name, Sprite[] expected, bool fullFrame = false)
    {
        RequireSession();
        CheckPose(name,expected);
        Check(view.TryGetPresentationAnchors(out var anchors) && anchors.Unit == actor && anchors.UnitResetVersion == actor.CombatResetVersion &&
            anchors.HasCalibratedBodyReference && anchors.ReferenceBodyWorldSize.x > 0 && anchors.ReferenceBodyWorldSize.y > 0 &&
            anchors.HeadWorldPosition.y > anchors.FootPivotWorldPosition.y, "Invalid native body anchors.");
        var camera = ui.GameCamera; Check(camera != null, "No native battle camera.");
        Bounds b = view.Renderer.bounds; Vector3 lo = camera.WorldToScreenPoint(b.min), hi = camera.WorldToScreenPoint(b.max);
        var rect = Rect.MinMaxRect(Mathf.Min(lo.x,hi.x),Mathf.Min(lo.y,hi.y),Mathf.Max(lo.x,hi.x),Mathf.Max(lo.y,hi.y));
        Check(lo.z > 0 && hi.z > 0 && rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= Screen.width && rect.yMax <= Screen.height &&
            rect.width >= 16 && rect.height >= 20, "Native body is clipped or too small to review. Increase Game view resolution; do not change art scale.");
        int x = Mathf.Max(0,Mathf.FloorToInt(rect.xMin)-16), y = Mathf.Max(0,Mathf.FloorToInt(rect.yMin)-16);
        int w = Mathf.Min(Screen.width-x,Mathf.CeilToInt(rect.xMax)-x+16), h = Mathf.Min(Screen.height-y,Mathf.CeilToInt(rect.yMax)-y+16);
        Texture2D frame = null;
        try
        {
            frame = ScreenCapture.CaptureScreenshotAsTexture();
            Check(frame != null && frame.width == Screen.width && frame.height == Screen.height, "Native framebuffer missing.");
            Check(current.samples.Count < SheetColumns * SheetRows,"Native contact sheet is full.");
            int col=current.samples.Count%SheetColumns, row=current.samples.Count/SheetColumns;
            float scale=Mathf.Min((TileSize-8f)/w,(TileSize-8f)/h);
            int dw=Mathf.Max(1,Mathf.RoundToInt(w*scale)),dh=Mathf.Max(1,Mathf.RoundToInt(h*scale));
            Color[] pixels=frame.GetPixels(x,y,w,h),draw=new Color[dw*dh];
            for(int py=0;py<dh;py++)for(int px=0;px<dw;px++)
                draw[py*dw+px]=pixels[Mathf.Min(h-1,Mathf.FloorToInt(py/scale))*w+Mathf.Min(w-1,Mathf.FloorToInt(px/scale))];
            sheet.SetPixels(col*TileSize+(TileSize-dw)/2,(SheetRows-1-row)*TileSize+(TileSize-dh)/2,dw,dh,draw);
            string path = Path.Combine(directory,current.id,"native-sheet.png"), full = "";
            if (fullFrame) { full = Path.Combine(directory,current.id,name + "-full.png"); File.WriteAllBytes(full,frame.EncodeToPNG()); }
            current.samples.Add(new Sample { name = name, facing = view.Facing.ToString(), sprite = AssetDatabase.GetAssetPath(view.Renderer.sprite),
                png = path, fullFramePng = full, screenBounds = rect, cropBounds = new Rect(x,y,w,h), sheetColumn=col, sheetRow=row,
                ground = anchors.GroundWorldPosition, foot = anchors.FootPivotWorldPosition, head = anchors.HeadWorldPosition,
                bodyReference = anchors.ReferenceBodyWorldSize, width = w, height = h, resetVersion = actor.CombatResetVersion });
        }
        finally { if (frame != null) Object.DestroyImmediate(frame); }
    }

    static void RequireSession()
    {
        if (!Application.isPlaying || EditorApplication.isCompiling || ui == null || ui.Match == null || ui.Match.enabled ||
            ui.Match.IsOnline || ui.Match.IsTraining || ui.Match.Phase != MatchPhase.Combat || ui.Match.Simulation == null ||
            ui.MapTransitionActive || LoadingArtworkScreen.IsBlocking ||
            !MonsterPouchNationNativePreview.Status().StartsWith("Ready; QA only;",StringComparison.Ordinal))
            throw new InvalidOperationException("Requires the existing isolated, frozen, visible QA combat. This helper will not start/change sessions.");
    }
    static string BoardIdentity()
    {
        var s = new StringBuilder();
        for (int y=0;y<10;y++) for (int x=0;x<6;x++)
        { var c=ui.Match.Board.GetCell(x,y); s.Append(x).Append(',').Append(y).Append(':').Append((c?.OccupiedBy as Object)?.GetInstanceID()??0)
            .Append('/').Append((c?.ReservedBy as Object)?.GetInstanceID()??0).Append(';'); }
        return s.ToString();
    }
    static void RestorePause()
    {
        if (!pauseOwned) return;
        if (ui != null && ui.Match != null) ui.Match.Pause(pausedBefore);
        Time.timeScale = timeScaleBefore; pauseOwned = false;
    }
    static void RemoveActor()
    {
        if (root != null) Object.DestroyImmediate(root);
        root = null; actor = null; view = null;
    }
    static void SaveSheet()
    {
        if(sheet==null)return;
        if(current!=null&&current.samples.Count>0)
        {
            sheet.Apply();string path=Path.Combine(directory,current.id,"native-sheet.png");
            File.WriteAllBytes(path,sheet.EncodeToPNG());string hash=Hash(path);
            foreach(var s in current.samples)s.sha256=hash;
        }
        Object.DestroyImmediate(sheet);sheet=null;
    }
    static void Finish()
    {
        AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload; EditorApplication.playModeStateChanged -= OnPlayMode;
        EditorApplication.update -= Watchdog;
        RestorePause(); SaveSheet(); RemoveActor(); running = null;
        if (report == null || report.status != "running") return;
        report.cleanupPassed = root == null;
        report.sessionPreserved = ui != null && ui.Match != null && !ui.Match.enabled && ui.Match.Actors.Count == actorsBefore &&
            ui.Match.Round == roundBefore && ui.Match.Simulation != null && ui.Match.Simulation.Elapsed == simulationBefore &&
            ui.Match.Paused == pausedBefore && Mathf.Approximately(Time.timeScale,timeScaleBefore) &&
            JsonUtility.ToJson(ui.Match.Config) == configBefore && BoardIdentity() == boardBefore;
        report.nativeChecksPassed &= report.characters.Count == 4 && report.pausePassed && report.characters.All(c => c.nativeChecksPassed) &&
            report.cleanupPassed && report.sessionPreserved && string.IsNullOrEmpty(report.error);
        report.presentationPassed = false;
        report.finishedUtc = DateTime.UtcNow.ToString("o");
        report.status = report.nativeChecksPassed ? "native-checks-passed-awaiting-visual-review" : "failed-or-cancelled";
        SaveReport();
    }
    static void SaveReport()
    {
        string index = Path.Combine(directory,"index.html");
        report.evidencePaths = new List<string> { Path.Combine(directory,"report.json"), index };
        var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Native Eronya presentation</title><style>body{background:#17202b;color:#eef;font:16px sans-serif;margin:24px}section{display:grid;grid-template-columns:repeat(4,228px);gap:14px}figure{margin:0;padding:10px;background:#273446}.shot{width:208px;height:208px}small{display:block;overflow-wrap:anywhere}a{color:#9df}</style>");
        html.Append("<h1>").Append(report.faction).Append(" — native framebuffer captures</h1><p>").Append(report.status)
            .Append(". Native framebuffer crops are uniformly enlarged with nearest-neighbor sampling; art assets/scales are unchanged. Body presentation only; review VFX/combat/UI separately.</p>");
        foreach (var c in report.characters)
        {
            html.Append("<h2>").Append(c.id).Append("</h2><section>");
            foreach (var s in c.samples)
            {
                string relative = c.id + "/" + s.name;
                html.Append("<figure><div class='shot' style=\"background-image:url('").Append(c.id).Append("/native-sheet.png');background-position:-")
                    .Append(s.sheetColumn*TileSize).Append("px -").Append(s.sheetRow*TileSize).Append("px\"></div><figcaption>").Append(s.name)
                    .Append("<small>").Append(s.facing).Append(" · ").Append(s.width).Append('×').Append(s.height).Append(" native crop</small>");
                if (!string.IsNullOrEmpty(s.fullFramePng)) html.Append("<a href='").Append(relative).Append("-full.png'>Full Game view</a>");
                html.Append("</figcaption></figure>");
            }
            html.Append("</section>");
        }
        File.WriteAllText(index,html.ToString());
        File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(report,true));
    }
    static void Check(bool condition,string message) { if (!condition) throw new InvalidOperationException(message); }
}
