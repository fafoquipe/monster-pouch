using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Optional UI step inside Movement's existing NationNativePreview session.
// No scene lifecycle, roster activation, config assignment or persistent profile access.
public static class MonsterPouchNationUiAcceptance
{
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static bool running;
    static readonly Dictionary<string,string[]> Nations = new Dictionary<string,string[]> {
        {"geothar",new[]{"korran","golem-tectonico","la-minera","protectora-del-mausoleo"}},
        {"aireola",new[]{"alveyr","zephalontropo","alboguera","palista"}},
        {"aquarion",new[]{"drayven","titanica","jabalinero","maestro-de-las-mareas"}},
        {"naturalis",new[]{"cael","guarda-veterano","arboreo","antidota"}},
        {"volkanar",new[]{"serya","legionario","dagata","canonero-del-orvek"}}
    };
    [Serializable] public sealed class CharacterResult
    {
        public string id;
        public bool passed;
        public int renderedSelections, dynamicChecks;
        public List<string> failures = new List<string>();
        public List<string> captures = new List<string>();
    }
    [Serializable] public sealed class BatchResult
    {
        public string faction, startedUtc, finishedUtc, reportPath;
        public bool executedInUnity, uiPassed, completed;
        public int assertions;
        public string[] screenSizes = {"540x960","393x852"};
        public List<string> failures = new List<string>();
        public List<string> evidencePaths = new List<string>();
        public List<string> captures = new List<string>();
        public List<CharacterResult> characters = new List<CharacterResult>();
    }

    public static IEnumerator Run(LocalGameUI ui, string faction, string evidenceDirectory, Action<BatchResult> completed)
    {
        if(running)throw new InvalidOperationException("A nation UI acceptance step is already running.");
        if(!Nations.ContainsKey(faction??""))throw new ArgumentException("Unknown faction.",nameof(faction));
        RequireSession(ui);
        string folder=Path.GetFullPath(Path.Combine(evidenceDirectory,"ui-"+faction+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(folder);
        var result=new BatchResult { faction=faction,startedUtc=DateTime.UtcNow.ToString("o"),executedInUnity=true,
            reportPath=Path.Combine(folder,"ui-"+faction+".json") };
        result.evidencePaths.Add(result.reportPath);
        foreach(string id in Nations[faction])result.characters.Add(new CharacterResult{id=id});
        Write(result); // Remains false if Unity stops or the coroutine is interrupted.
        bool language=GameText.Spanish;
        var viewType=typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var view=EditorWindow.GetWindow(viewType);
        var index=viewType.GetProperty("selectedSizeIndex",Any);
        int previousSize=(int)index.GetValue(view);
        running=true;
        var steps=Steps(ui,result,folder);
        bool finished=false;
        try
        {
            while(true)
            {
                object current=null;bool next=false;
                try { next=steps.MoveNext();if(next)current=steps.Current; }
                catch(Exception error) { result.failures.Add(error.GetBaseException().ToString());break; }
                if(!next){finished=true;break;}
                yield return current;
            }
        }
        finally
        {
            try
            {
                (steps as IDisposable)?.Dispose();
                if(ui!=null)ui.CloseModal();
                SetLanguageMemory(language);
                index.SetValue(view,previousSize);view.Repaint();
            }
            catch(Exception error){result.failures.Add("Cleanup: "+error.GetBaseException());}
            running=false;
            result.completed=finished;
            foreach(var character in result.characters)
                character.passed=finished&&character.failures.Count==0&&character.renderedSelections==24&&character.captures.Count==(character==result.characters[0]?4:1)&&character.dynamicChecks>0;
            result.uiPassed=finished&&result.failures.Count==0&&result.characters.Count==4&&result.characters.All(c=>c.passed)&&result.captures.Count==7;
            result.finishedUtc=DateTime.UtcNow.ToString("o");
            Write(result);
            completed?.Invoke(result);
        }
    }

    static void RequireSession(LocalGameUI ui)
    {
        if(!Application.isPlaying||ui==null||ui.CanvasRoot==null||ui.Match==null)
            throw new InvalidOperationException("Run inside the ready MonsterPouchNationNativePreview scene.");
        // Reuse the session owner's identity/store validation, rather than inventing another session.
        var verified=(LocalGameUI)typeof(MonsterPouchNationNativePreview).GetMethod("RequireUI",Any).Invoke(null,null);
        if(verified!=ui||ui.Match.enabled||ui.MapTransitionActive||LoadingArtworkScreen.IsBlocking)
            throw new InvalidOperationException("Use the same isolated UI with Match.enabled=false and no active loading/map transition.");
    }
    static void SetLanguageMemory(bool spanish)
        =>typeof(GameText).GetProperty("Spanish",Any).GetSetMethod(true).Invoke(null,new object[]{spanish});
    static void Write(BatchResult result)=>File.WriteAllText(result.reportPath,JsonUtility.ToJson(result,true));
    static bool Check(BatchResult r,CharacterResult c,bool condition,string message)
    {
        r.assertions++;
        if(!condition){c.failures.Add(message);r.failures.Add(c.id+": "+message);}
        return condition;
    }
    static RectTransform Find(Transform parent,string name)=>parent.GetComponentsInChildren<RectTransform>().Single(t=>t.name==name);

    static IEnumerator Steps(LocalGameUI ui,BatchResult r,string folder)
    {
        var show=typeof(LocalGameUI).GetMethod("ShowAbilityCard",Any);
        var manifest=Resources.Load<TextAsset>("MonsterPouch/UI/AbilityArt/catalog");
        if(manifest==null)throw new InvalidOperationException("Missing real ability catalog.");
        var entries=JsonUtility.FromJson<AbilityUiManifest>(manifest.text).entries.ToDictionary(e=>e.key);
        var config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        string configBefore=JsonUtility.ToJson(config);
        foreach(string size in r.screenSizes)
        {
            ui.CloseModal();MonsterPouchNationNativePreview.Resize(size);
            var dimensions=size.Split('x');int width=int.Parse(dimensions[0]),height=int.Parse(dimensions[1]);
            float deadline=Time.realtimeSinceStartup+10;
            while((Screen.width!=width||Screen.height!=height)&&Time.realtimeSinceStartup<deadline)yield return null;
            if(Screen.width!=width||Screen.height!=height)throw new InvalidOperationException("GameView did not reach "+size);
            foreach(var c in r.characters)
            {
                RequireSession(ui);
                var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+c.id+"/Character");
                if(!Check(r,c,character!=null,"Character resource loads"))continue;
                string assetBefore=JsonUtility.ToJson(character);
                var definition=character.CreateRuntimeDefinition();var owned=new OwnedUnit(definition);
                if(!Check(r,c,definition.PassiveAbility!=null&&definition.BaseAbility!=null&&definition.Tricks?.Length==3&&definition.Tricks.All(a=>a!=null),"Six actual skill slots are present"))continue;
                var icon=CharacterUiArtwork.Icon(c.id);var portrait=CharacterUiArtwork.Portrait(c.id);var card=CharacterUiArtwork.Card(c.id);
                Check(r,c,icon!=null&&portrait!=null&&card!=null,"Icon/Card/Portrait load independently");
                Check(r,c,icon==character.Icon&&portrait==character.Portrait&&card==character.Card,"Character roles use their authored bindings");
                var basic=AbilityUiCatalog.Get(c.id+"/basic");
                Check(r,c,basic!=null&&basic!=icon&&basic!=portrait&&basic!=card,"Basic has its own artwork");
                var normal=LeaderBadgeArtwork.Get(c.id,false);var upgraded=LeaderBadgeArtwork.Get(c.id,true);
                Check(r,c,LeaderBadgeArtwork.FactionFor(c.id)==r.faction,"Leader faction mapping");
                Check(r,c,normal!=null&&upgraded!=null&&normal!=upgraded&&normal==UiSpriteReferences.Get("leader/"+r.faction+"/normal")&&upgraded==UiSpriteReferences.Get("leader/"+r.faction+"/upgraded"),"Both exact faction emblems load without fallback");
                foreach(bool es in new[]{true,false})
                {
                    SetLanguageMemory(es);
                    // The menu profile is already disposable under the NationNativePreview owner.
                    // Enemy inspection disables purchase paths; never click buy/equip or register a unit.
                    foreach(int selection in new[]{-2,-3,-1,0,1,2})
                    {
                        show.Invoke(ui,new object[]{owned,true,true,selection,null});
                        yield return new WaitForSecondsRealtime(.36f);
                        Canvas.ForceUpdateCanvases();yield return null;
                        var sheet=Find(ui.CanvasRoot,"Unit sheet");
                        var ability=selection==-3?definition.PassiveAbility:selection==-2?null:selection==-1?definition.BaseAbility:definition.Tricks[selection];
                        string key=ability==null?c.id+"/basic":ability.EffectId??ability.Id;
                        var expected=ability==null?basic:AbilityArtwork.Get(key);
                        var selected=Find(sheet,"Selected ability");
                        Check(r,c,expected!=null&&selected.GetComponentsInChildren<Image>().Any(i=>i.enabled&&i.sprite==expected),"Selected "+key+" real Sprite "+size+" "+es);
                        foreach(int slot in new[]{-2,-3,-1,0,1,2})
                        {
                            var a=slot==-2?null:slot==-3?definition.PassiveAbility:slot==-1?definition.BaseAbility:definition.Tricks[slot];
                            var s=a==null?basic:AbilityArtwork.Get(a.EffectId??a.Id);
                            var tile=Find(sheet,slot<0?"skill-slot-"+slot:"ability-"+slot);
                            Check(r,c,s!=null&&tile.GetComponentsInChildren<Image>().Any(i=>i.enabled&&i.sprite==s),"Visible tile "+slot+" uses own Sprite");
                        }
                        var description=Find(sheet,"Description viewport").GetComponentInChildren<Text>();
                        string expectedCopy=ability==null?LocalGameUI.BasicAttackDetailText(owned):GameText.AbilityDescription(ability,definition,es);
                        Check(r,c,!string.IsNullOrWhiteSpace(expectedCopy)&&description.text.StartsWith(expectedCopy,StringComparison.Ordinal)&&!description.text.Contains("{"),"Real rendered description "+key);
                        Check(r,c,description.color==Color.black&&description.cachedTextGenerator.vertexCount>4,"Native black glyphs render "+key);
                        if(ability!=null)
                        {
                            string title=GameText.AbilityName(ability,es);
                            Check(r,c,!string.IsNullOrWhiteSpace(title)&&selected.GetComponentsInChildren<Text>().Any(t=>t.text==title&&t.cachedTextGenerator.vertexCount>4),"Real localized title "+key);
                            ValidateDynamic(r,c,definition,ability,entries,es);
                        }
                        CheckFooter(r,c,ui,sheet);
                        c.renderedSelections++;
                        // One image per character, plus the first character across both
                        // languages/resolutions: seven captures per faction, 35 for all20.
                        if(selection==-1&&((size=="393x852"&&es)||c==r.characters[0]))
                        {
                            // A small role strip inside the existing figure stage; body animation QA
                            // remains entirely in Movement's presentation step. All widgets/assets are real.
                            var strip=AddRoleStrip(Find(sheet,"Figure stage"),c.id,icon,card,portrait,normal,upgraded,es);
                            var avatar=strip.GetComponentInChildren<PlayerAvatarGraphic>();
                            Canvas.ForceUpdateCanvases();yield return null;
                            Check(r,c,avatar.CharacterId==c.id&&icon!=null&&avatar.mainTexture==icon.texture,"Real profile widget uses the character Icon");
                            string capture=Path.Combine(folder,c.id+"-"+(es?"es":"en")+"-"+size+".png");
                            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(capture);
                            float until=Time.realtimeSinceStartup+10;
                            while((!File.Exists(capture)||new FileInfo(capture).Length<24)&&Time.realtimeSinceStartup<until)yield return null;
                            // Fresh run directory prevents an old file from passing this check.
                            if(Check(r,c,PngSize(capture,width,height),"Native screenshot written with current dimensions"))
                            {c.captures.Add(capture);r.captures.Add(capture);r.evidencePaths.Add(capture);}
                        }
                    }
                }
                Check(r,c,JsonUtility.ToJson(character)==assetBefore,"Serialized Character asset remains unchanged");
                Write(r);
            }
        }
        if(JsonUtility.ToJson(config)!=configBefore)r.failures.Add("Persistent MatchConfig changed during the UI step.");
    }

    static void ValidateDynamic(BatchResult r,CharacterResult c,UnitDefinition owner,TrickDefinition source,Dictionary<string,AbilityUiEntry> entries,bool es)
    {
        string key=source.EffectId??source.Id;
        if(!Check(r,c,entries.TryGetValue(key,out var entry),"Dynamic entry "+key))return;
        string template=es?entry.descriptionEs:entry.descriptionEn;
        if(!Check(r,c,!string.IsNullOrWhiteSpace(template),"ES/EN template "+key))return;
        // All mutation is on a detached serializable copy, never Character/MatchConfig/live actors.
        foreach(var p in AbilityBalance.Resolve(source))
        {
            bool percent=template.Contains("{"+p.Key+":percent}");
            if(!percent&&!template.Contains("{"+p.Key+"}"))continue;
            var copy=JsonUtility.FromJson<TrickDefinition>(JsonUtility.ToJson(source));
            float edited=p.Value<1?.417f:p.Value+11;
            copy.Description="STALE_NATIVE_UI_FIXTURE";
            copy.Parameters=new[]{new AbilityParameter{Key=p.Key,Value=edited}};
            string before=JsonUtility.ToJson(copy);
            float effective=AbilityBalance.Value(copy,p.Key,0);
            string expected=(effective*(percent?100:1)).ToString("0.##",CultureInfo.InvariantCulture);
            string text=GameText.AbilityDescription(copy,owner,es);
            Check(r,c,text.Contains(expected)&&!text.Contains("{")&&!text.Contains("STALE_NATIVE_UI_FIXTURE"),"Live effective value "+key+"/"+p.Key+" "+es);
            Check(r,c,JsonUtility.ToJson(copy)==before,"Formatting does not rewrite stored "+key+"/"+p.Key);
            c.dynamicChecks++;
        }
        if(template.Contains("{manaCost}"))
        {
            var detached=JsonUtility.FromJson<UnitDefinition>(JsonUtility.ToJson(owner));detached.EnergyMax=137;
            string text=GameText.AbilityDescription(source,detached,es);
            Check(r,c,text.Contains(es?"Maná: 137":"Mana: 137"),"Live owner mana "+key+" "+es);c.dynamicChecks++;
        }
    }
    static void CheckFooter(BatchResult r,CharacterResult c,LocalGameUI ui,RectTransform sheet)
    {
        var nav=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(t=>t.name=="Menu navigation");
        if(nav==null)return; // Combat inspection has no menu navigation.
        var corners=new Vector3[4];float top=float.NegativeInfinity;
        foreach(Transform child in nav){((RectTransform)child).GetWorldCorners(corners);top=Mathf.Max(top,corners.Max(v=>v.y));}
        foreach(string name in new[]{"ability-upgrade","Ability purchase status"})
        {Find(sheet,name).GetWorldCorners(corners);Check(r,c,corners.Min(v=>v.y)>top,name+" stays above navigation");}
    }
    static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {
        var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
    }
    static void Label(Transform parent,string text,float x,float y,float w,int size)
    {
        var label=Rect(parent,"QA label",x,y,w,26).gameObject.AddComponent<Text>();
        label.text=text;label.font=Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold")??Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.color=Color.white;label.fontSize=size;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
    }
    static void Picture(Transform parent,string name,Sprite sprite,float x,float y,float w,float h)
    {var image=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;}
    static RectTransform AddRoleStrip(Transform stage,string id,Sprite icon,Sprite card,Sprite portrait,Sprite normal,Sprite upgraded,bool es)
    {
        var strip=Rect(stage,"Native UI role samples",4,4,476,232);
        strip.gameObject.AddComponent<Image>().color=new Color(.04f,.16f,.25f,1);
        Label(strip,es?"PERFIL · CARTA · RETRATO · EMBLEMAS":"PROFILE · CARD · PORTRAIT · EMBLEMS",4,4,468,17);
        var avatar=Rect(strip,"Real profile",20,48,60,60).gameObject.AddComponent<PlayerAvatarGraphic>();avatar.Configure(id);
        Picture(strip,"Real card",card,104,42,108,145);Picture(strip,"Real portrait",portrait,224,42,108,145);
        Picture(strip,"Leader normal",normal,362,66,22,18);Picture(strip,"Leader upgraded",upgraded,412,66,22,18);
        Label(strip,es?"Normal / mejorado":"Normal / upgraded",338,100,130,13);
        Label(strip,id,4,199,468,15);return strip;
    }
    static bool PngSize(string path,int width,int height)
    {
        if(!File.Exists(path))return false;
        byte[] b=File.ReadAllBytes(path);if(b.Length<24||b[0]!=137||b[1]!=80||b[2]!=78||b[3]!=71)return false;
        int w=(b[16]<<24)|(b[17]<<16)|(b[18]<<8)|b[19],h=(b[20]<<24)|(b[21]<<16)|(b[22]<<8)|b[23];return w==width&&h==height;
    }
}
