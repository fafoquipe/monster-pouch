using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class AbilitySummaryCardsPlayTests
{
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    readonly string[] keys={"monster-pouch.language",CardTableTheme.PreferenceKey,CardTableTheme.LobbyPreferenceKey,
        "monster-pouch.progress.v1","monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    string root;string[] oldDirectories,saved;bool[] existed;bool spanish;
    LocalGameUI ui;InteractionCaptureGameView view;

    [UnitySetUp] public IEnumerator Setup()
    {
        // Opening/resizing Game View yields before the scene load; retire stale readers first.
        foreach(var previous in Object.FindObjectsByType<LocalGameUI>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {previous.gameObject.SetActive(false);Object.Destroy(previous.gameObject);}
        ui=null;
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchAbilitySummaries",Guid.NewGuid().ToString("N"));
        oldDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,Path.Combine(root,"store-"+i));
        new LocalGuestProfileStore(Path.Combine(root,"store-0")).OpenOrCreate(true);
        saved=keys.Select(PlayerPrefs.GetString).ToArray();existed=keys.Select(PlayerPrefs.HasKey).ToArray();spanish=GameText.Spanish;
        GameText.SetLanguage(true);CardTableTheme.Select(TableStyle.Default);
        view=new InteractionCaptureGameView();yield return view.Open();
        var preview=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null);
        preview.GetMethod("Resize").Invoke(null,new object[]{"393x852"});
        for(int i=0;i<120&&(Screen.width!=393||Screen.height!=852);i++)yield return null;
        Assert.AreEqual(new Vector2Int(393,852),new Vector2Int(Screen.width,Screen.height));
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();Assert.IsNotNull(ui);ui.Match.enabled=false;
        float deadline=Time.realtimeSinceStartup+10;
        while(LoadingArtworkScreen.IsBlocking&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.IsFalse(LoadingArtworkScreen.IsBlocking);
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.gameObject.SetActive(false);Object.Destroy(ui.gameObject);}
        yield return null;
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,oldDirectories[i]);
        GameText.SetLanguage(spanish);
        for(int i=0;i<keys.Length;i++){if(existed[i])PlayerPrefs.SetString(keys[i],saved[i]);else PlayerPrefs.DeleteKey(keys[i]);}
        PlayerPrefs.Save();
        if(view!=null){view.RestoreSelection();yield return view.WaitForRestoration();}
        if(root!=null&&Directory.Exists(root))
        {
            string parent=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchAbilitySummaries"))+Path.DirectorySeparatorChar;
            string target=Path.GetFullPath(root);Assert.IsTrue(target.StartsWith(parent,StringComparison.OrdinalIgnoreCase));
            Directory.Delete(target,true);
        }
    }

    [UnityTest] public IEnumerator MobileCardsUseConciseLiveFlowerMarkAndSpeedSummaries()
    {
        ui.OpenCollection();
        var show=typeof(LocalGameUI).GetMethod("ShowAbilityCard",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(var selection in new[]{new {Id="flo",Index=2},new {Id="tauris",Index=0},new {Id="atori",Index=1},new {Id="aky",Index=0}})
        {
            var definition=ui.Match.Config.Get(selection.Id);Assert.IsNotNull(definition);
            var owned=new OwnedUnit(definition);
            show.Invoke(ui,new object[]{owned,false,true,selection.Index,null});
            yield return new WaitForSecondsRealtime(.35f);Canvas.ForceUpdateCanvases();yield return null;
            var sheet=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Unit sheet");
            var description=sheet.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Description viewport").GetComponentInChildren<Text>();
            Assert.AreEqual(GameText.AbilityDescription(definition.Tricks[selection.Index],definition),description.text);
            Assert.AreEqual(Color.black,description.color,"Player descriptions must be fully black and legible.");
            Assert.That(description.text,Does.Not.Contain("{").And.Not.Contain("velocidad de vuelo").And.Not.Contain("proyectil"));
            Assert.Greater(description.cachedTextGenerator.vertexCount,4,"Native summary text must render.");
            if(selection.Id=="flo")Assert.That(description.text,Does.Contain("INVULNERABILIDAD").And.Not.Contain("-49").And.Not.Contain("−49"));
            string directory=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Logs","local-validation","ability-summaries");
            Directory.CreateDirectory(directory);string path=Path.Combine(directory,selection.Id+"-"+(selection.Index+1)+"-393x852.png");
            ScreenCapture.CaptureScreenshot(path);
            float deadline=Time.realtimeSinceStartup+10;
            while((!File.Exists(path)||new FileInfo(path).Length<=24)&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return null;yield return null;
            Assert.IsTrue(File.Exists(path)&&new FileInfo(path).Length>24,"Native screenshot must be written.");
        }
    }
    [Test] public void RegisteredBasicPassiveActiveAndUpgradeArtLoadsAsSprites()
    {
        Assert.GreaterOrEqual(ui.Match.Config.Units.Length,24);
        foreach(var unit in ui.Match.Config.Units.Where(u=>u!=null))
        {
            var basic=AbilityUiCatalog.Get(unit.Id+"/basic");
            Assert.IsNotNull(basic,unit.Id+" basic Sprite import/binding");
            var character=Resources.Load<MonsterPouch.Gameplay.Characters.CharacterDefinition>("MonsterPouch/Characters/"+unit.Id+"/Character");
            Assert.IsNotNull(character,unit.Id);
            Assert.AreNotSame(character.Portrait,basic,unit.Id+" basic must not be a portrait");
            Assert.AreNotSame(character.Icon,basic,unit.Id+" basic must not be a character icon");
            foreach(var ability in new[]{unit.BaseAbility}.Concat(unit.Tricks).Where(a=>a!=null))
                Assert.IsNotNull(AbilityArtwork.Get(ability.EffectId??ability.Id),unit.Id+" / "+ability.Id);
        }
        var source=Resources.Load<TextAsset>("MonsterPouch/UI/AbilityArt/catalog");
        Assert.IsNotNull(source);
        foreach(var entry in JsonUtility.FromJson<AbilityUiManifest>(source.text).entries)
        {
            if(string.IsNullOrEmpty(entry.resource))continue;
            var sprite=string.IsNullOrEmpty(entry.spriteName)?Resources.Load<Sprite>(entry.resource):
                Resources.LoadAll<Sprite>(entry.resource).FirstOrDefault(s=>s.name==entry.spriteName);
            Assert.IsNotNull(sprite,entry.key+" resource must import as Sprite: "+entry.resource);
            Assert.AreSame(sprite,AbilityUiCatalog.Get(entry.key),entry.key+" catalog binding");
        }
        var badges=JsonUtility.FromJson<LeaderBadgeManifest>(Resources.Load<TextAsset>("MonsterPouch/UI/LeaderBadges/catalog").text);
        foreach(var badge in badges.badges)
        foreach(var resource in new[]{badge.normal,badge.upgraded})
            if(resource.StartsWith("MonsterPouch/"))Assert.IsNotNull(Resources.Load<Sprite>(resource),resource);
    }

    [Test] public void MissingAbilityArtworkUsesNeutralPlaceholder()
    {
        const string key="native-artwork-missing-fixture";
        var draw=typeof(LocalGameUI).GetMethod("AbilityIllustration",BindingFlags.Instance|BindingFlags.NonPublic,null,
            new[]{typeof(Transform),typeof(Sprite),typeof(string),typeof(string),typeof(Rect)},null);
        LogAssert.Expect(LogType.Warning,"Missing ability artwork: "+key+". Check its Sprite importer and catalog binding.");
        var tile=(RectTransform)draw.Invoke(ui,new object[]{ui.CanvasRoot,null,key,"martio",new Rect(0,0,56,56)});
        Assert.AreEqual("?",tile.GetComponentsInChildren<Text>().Single(t=>t.name=="Missing ability artwork").text);
        var character=Resources.Load<MonsterPouch.Gameplay.Characters.CharacterDefinition>("MonsterPouch/Characters/martio/Character");
        Assert.IsFalse(tile.GetComponentsInChildren<Image>().Any(i=>i.sprite!=null&&(i.sprite==character.Portrait||i.sprite==character.Icon)));
        Object.DestroyImmediate(tile.gameObject);
    }

    [UnityTest] public IEnumerator BasicCardActionAndStatusRemainAboveNavigation()
    {
        var resize=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("MonsterPouchLayoutPreview")).First(t=>t!=null).GetMethod("Resize");
        var show=typeof(LocalGameUI).GetMethod("ShowAbilityCard",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(var size in new[]{new Vector2Int(540,960),new Vector2Int(393,852)})
        foreach(bool es in new[]{true,false})
        {
            resize.Invoke(null,new object[]{size.x+"x"+size.y});
            for(int i=0;i<120&&(Screen.width!=size.x||Screen.height!=size.y);i++)yield return null;
            Assert.AreEqual(size,new Vector2Int(Screen.width,Screen.height));
            GameText.SetLanguage(es);ui.OpenHome();yield return null;yield return null;
            show.Invoke(ui,new object[]{new OwnedUnit(ui.Match.Config.Get("martio")),false,true,-2,null});
            yield return new WaitForSecondsRealtime(.35f);Canvas.ForceUpdateCanvases();yield return null;
            var sheet=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Unit sheet");
            var nav=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Menu navigation");
            var corners=new Vector3[4];
            float highestNav=float.NegativeInfinity;
            foreach(Transform child in nav)
            {
                ((RectTransform)child).GetWorldCorners(corners);
                highestNav=Mathf.Max(highestNav,corners.Max(c=>c.y));
            }
            foreach(string name in new[]{"ability-upgrade","Ability purchase status"})
            {
                var control=sheet.GetComponentsInChildren<RectTransform>().Single(t=>t.name==name);
                control.GetWorldCorners(corners);
                Assert.Greater(corners.Min(c=>c.y),highestNav,name+" must clear every navigation tab");
            }
            var status=sheet.GetComponentsInChildren<Text>().Single(t=>t.name=="Ability purchase status");
            Assert.Greater(status.cachedTextGenerator.vertexCount,4,"Status must render");
            var expected=AbilityUiCatalog.Get("martio/basic");
            Assert.IsNotNull(expected,"Martio basic Sprite must load before comparing card images.");
            var basicTile=sheet.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="skill-slot--2");
            var selected=sheet.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="Selected ability");
            Assert.IsTrue(basicTile.GetComponentsInChildren<Image>().Any(i=>i.sprite==expected));
            Assert.IsTrue(selected.GetComponentsInChildren<Image>().Any(i=>i.sprite==expected));
            string directory=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Logs","local-validation","ui-native-fixes");
            Directory.CreateDirectory(directory);
            string screenshot=Path.Combine(directory,"martio-basic-"+(es?"es":"en")+"-"+size.x+"x"+size.y+".png");
            ScreenCapture.CaptureScreenshot(screenshot);yield return null;yield return null;
        }
    }

}
