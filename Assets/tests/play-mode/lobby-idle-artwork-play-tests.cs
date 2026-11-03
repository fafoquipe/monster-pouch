using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Characters;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class LobbyIdleArtworkPlayTests
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    LocalGameUI ui;bool playback,spanish;string root,identity;string[] oldDirectories;
    static readonly PropertyInfo[] Overrides=new[]{"GuestProfileDirectoryOverride","ProgressionDirectoryOverride","DeckDirectoryOverride"}
        .Select(n=>typeof(LocalGameUI).GetProperty(n,BindingFlags.Static|BindingFlags.NonPublic)).ToArray();
    readonly Dictionary<string,string> prefs=new Dictionary<string,string>();
    readonly HashSet<string> existed=new HashSet<string>();
    static readonly string[] Keys={"monster-pouch.language","monster-pouch.decks.v1","monster-pouch.progress.v1","monster-pouch.monster","monster-pouch.whelps"};
    [UnitySetUp] public IEnumerator Setup()
    {
        playback=LobbyAnimationDirector.PlaybackEnabled;LobbyAnimationDirector.PlaybackEnabled=false;
        prefs.Clear();existed.Clear();
        foreach(string key in Keys){if(PlayerPrefs.HasKey(key))existed.Add(key);prefs[key]=PlayerPrefs.GetString(key);}
        root=Path.Combine(Path.GetTempPath(),"MonsterPouchIdleArtworkTests",Guid.NewGuid().ToString("N"));
        oldDirectories=Overrides.Select(p=>(string)p.GetValue(null)).ToArray();
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,Path.Combine(root,"store-"+i));
        identity=new LocalGuestProfileStore(Path.Combine(root,"store-0")).OpenOrCreate(true).ProfileId;
        spanish=GameText.Spanish;GameText.SetLanguage(true);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        LobbyAnimationDirector.PlaybackEnabled=playback;
        var previousUI=ui;ui=null;
        if(previousUI!=null){previousUI.CloseModal();previousUI.gameObject.SetActive(false);Object.Destroy(previousUI.gameObject);}
        yield return null;
        Assert.IsTrue(previousUI==null);
        for(int i=0;i<Overrides.Length;i++)Overrides[i].SetValue(null,oldDirectories[i]);
        GameText.SetLanguage(spanish);
        foreach(string key in Keys){if(existed.Contains(key))PlayerPrefs.SetString(key,prefs[key]);else PlayerPrefs.DeleteKey(key);}
        if(identity!=null){PlayerPrefs.DeleteKey(LobbyAvatarSelection.PreferenceKey(identity));PlayerPrefs.DeleteKey("monster-pouch.lobby.mode."+identity);}
        PlayerPrefs.Save();
        string prefix=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"MonsterPouchIdleArtworkTests"))+Path.DirectorySeparatorChar;
        string directory=Path.GetFullPath(root);Assert.IsTrue(directory.StartsWith(prefix,StringComparison.OrdinalIgnoreCase));
        if(Directory.Exists(directory))Directory.Delete(directory,true);
    }
    void Capture(string name)
    {
        string folder=Path.GetFullPath("Logs/local-validation/idle-artwork-20261017");Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
    }
    LobbyFrameGraphic Featured(string name)=>ui.CanvasRoot.GetComponentsInChildren<RectTransform>()
        .Single(r=>r.name==name).GetComponentInChildren<LobbyFrameGraphic>();
    [UnityTest] public IEnumerator DetailedMonstersStayExclusiveToBattleHomeAcrossNavigationAndReconfiguration()
    {
        foreach(string id in new[]{"anuik","bugaloo","popow","sepora","tauris"})
        {
            ui.CloseModal();ui.OpenHome();yield return new WaitForSecondsRealtime(.4f);
            typeof(LocalGameUI).GetField("chosen",Private).SetValue(ui,id);ui.RenderPage();yield return null;yield return null;
            Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Button>().Any(b=>b.name=="BATALLA"));
            var home=Featured("Choose featured Monster");
            Assert.AreEqual("MonsterPouch/UI/BattleHomeIdles/"+id,home.SourceClip.idleTexture,id);
            Assert.Greater(home.CurrentSprite.texture.width,500,id);
            Assert.IsFalse(home.HasAuthoredFrames,"Home idle must never switch to a coarse combat frame.");
            var homeTexture=home.CurrentSprite.texture;
            var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+id+"/Character");
            Assert.AreNotSame(homeTexture,character.Visuals.Portrait.texture,"Combat art remains separate.");
            Capture("home-"+id);yield return null;yield return null;
            ui.OpenDecks();yield return new WaitForSecondsRealtime(.4f);
            var brief=Featured("deck-monster");
            Assert.AreEqual("MonsterPouch/UI/LobbyIdles/"+id,brief.SourceClip.idleTexture,id);
            Assert.AreNotSame(homeTexture,brief.CurrentSprite.texture);
            Assert.Less(brief.CurrentSprite.texture.height,100);
            Capture("brief-"+id);yield return null;yield return null;
            // Reusing a graphic must select the current idle for each surface,
            // without loading or replaying the retired gesture atlases.
            Assert.IsTrue(brief.Configure(id,false,LobbyArtworkContext.BattleHome));
            Assert.AreSame(homeTexture,brief.CurrentSprite.texture);Assert.IsFalse(brief.HasAuthoredFrames);
            Assert.IsTrue(brief.Configure(id));Assert.IsFalse(brief.HasAuthoredFrames);
            Assert.AreEqual("MonsterPouch/UI/LobbyIdles/"+id,brief.SourceClip.idleTexture);
            var idle=brief.CurrentSprite;
            brief.SampleFrame(.5f);
            Assert.AreSame(idle,brief.CurrentSprite);
            Assert.AreEqual(-1,brief.FrameIndex);
        }
    }
    [UnityTest] public IEnumerator NewWhelpsHaveDedicatedMatchingPixelIdlesInBriefAndDetails()
    {
        ui.OpenDecks();yield return new WaitForSecondsRealtime(.4f);
        var decks=(DeckLibrary)typeof(LocalGameUI).GetField("decks",Private).GetValue(ui);
        string[] ids={"suki","rosie","pokrak"};
        for(int i=0;i<ids.Length;i++)decks.Current.Whelps[i]=ids[i];
        ui.RenderPage();yield return null;yield return null;
        for(int i=0;i<ids.Length;i++)
        {
            string id=ids[i];var graphic=Featured("deck-slot-"+i);
            Assert.AreEqual("MonsterPouch/UI/NewWhelpIdles/"+id,graphic.SourceClip.idleTexture);
            var character=Resources.Load<CharacterDefinition>("MonsterPouch/Characters/"+id+"/Character");
            Assert.AreSame(character.Card.texture,graphic.CurrentSprite.texture);
            Assert.AreSame(character.Card,character.Icon);
            Assert.AreEqual(68,graphic.CurrentSprite.texture.height);
            Assert.AreEqual(FilterMode.Point,graphic.CurrentSprite.texture.filterMode);
            Assert.AreNotSame(character.Visuals.Portrait.texture,graphic.CurrentSprite.texture);
        }
        Capture("new-whelps-brief");yield return null;yield return null;
        foreach(string id in ids)
        {
            typeof(LocalGameUI).GetMethod("ShowCollectionInfo",Private).Invoke(ui,new object[]{ui.Match.Config.Get(id)});yield return null;
            var graphic=ui.CanvasRoot.GetComponentsInChildren<LobbyFrameGraphic>().Last(f=>f.UnitId==id);
            Assert.AreEqual("MonsterPouch/UI/NewWhelpIdles/"+id,graphic.SourceClip.idleTexture);
            Capture("detail-"+id);yield return null;yield return null;ui.CloseModal();yield return null;
        }
        Assert.IsFalse(LobbyAnimationDirector.PlaybackEnabled,"Importing new idles does not resume paused animation.");
    }
}

