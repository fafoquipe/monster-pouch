using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class NewWhelpPreparationPlayTests
{
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    LocalGameUI ui;
    readonly Dictionary<string,string> prefs=new Dictionary<string,string>();
    readonly HashSet<string> existed=new HashSet<string>();
    static readonly string[] Keys={"monster-pouch.decks.v1","monster-pouch.monster","monster-pouch.whelps"};
    [UnitySetUp] public IEnumerator Setup()
    {
        foreach(string key in Keys){if(PlayerPrefs.HasKey(key))existed.Add(key);prefs[key]=PlayerPrefs.GetString(key);}
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.Match.Pause(false);ui.Match.Menu();ui.Match.enabled=true;}
        foreach(string key in Keys){if(existed.Contains(key))PlayerPrefs.SetString(key,prefs[key]);else PlayerPrefs.DeleteKey(key);}
        yield return null;
    }
    void Place(Dictionary<string,OwnedUnit> units,string id,int x,int y)
    {
        var owned=new OwnedUnit(ui.Match.Config.Get(id));
        typeof(OwnedUnit).GetProperty("Location").SetValue(owned,UnitLocation.Field);
        typeof(OwnedUnit).GetProperty("Deployment").SetValue(owned,new Vector2Int(x,y));units.Add(id,owned);
    }
    static void Sample(UnitPresentation view,float time)
    {
        typeof(UnitPresentation).GetField("idleElapsed",Private).SetValue(view,time);
        typeof(UnitPresentation).GetMethod("AdvancePresentation",Private).Invoke(view,new object[]{0f});
    }
    void Capture(string name)
    {
        string folder=Path.GetFullPath("Logs/local-validation/preparation-idles-20261017");Directory.CreateDirectory(folder);
        ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
    }
    [UnityTest] public IEnumerator ThreeNewWhelpsBreatheDuringRealPreparationAndKeepTheirBoardPositions()
    {
        var match=ui.Match;Assert.IsTrue(match.StartMatch("anuik",new[]{"suki","rosie","pokrak"},317));
        // Keep all three visible in the real Brief for the visual review.
        var offers=(PouchOffer[])typeof(PouchState).GetField("offers",Private).GetValue(match.Player);
        var offerConstructor=typeof(PouchOffer).GetConstructor(Private,null,new[]{typeof(string),typeof(long)},null);
        for(int i=0;i<3;i++)offers[i]=(PouchOffer)offerConstructor.Invoke(new object[]{new[]{"suki","rosie","pokrak"}[i],offers[i].Token});
        typeof(MatchController).GetMethod("ClearActors",Private).Invoke(match,null);
        var owned=(Dictionary<string,OwnedUnit>)typeof(PouchState).GetField("owned",Private).GetValue(match.Player);
        owned.Clear();Place(owned,"anuik",1,8);Place(owned,"suki",2,7);Place(owned,"rosie",3,8);Place(owned,"pokrak",4,7);
        typeof(MatchController).GetMethod("SyncActors",Private).Invoke(match,null);ui.RenderPage();
        yield return new WaitForSecondsRealtime(.4f);
        Assert.AreEqual(MatchPhase.Preparation,match.Phase);
        var actors=match.Actors.Values.Where(a=>new[]{"suki","rosie","pokrak"}.Contains(a.BaseStats.DefinitionId)).ToArray();
        Assert.AreEqual(3,actors.Length);
        var views=actors.Select(a=>a.GetComponent<UnitPresentation>()).ToArray();
        var positions=actors.Select(a=>a.transform.position).ToArray();var cells=actors.Select(a=>a.CurrentCell).ToArray();
        foreach(var view in views){view.enabled=false;view.Face(0,1);Sample(view,.65f);}
        var inhale=views.Select(v=>v.VisualRoot.localScale).ToArray();
        Capture("preparation-inhale");yield return null;yield return null;
        foreach(var view in views)Sample(view,1.95f);
        for(int i=0;i<views.Length;i++)
        {
            Assert.Greater(inhale[i].y,views[i].VisualRoot.localScale.y);
            Assert.AreEqual(positions[i],actors[i].transform.position);Assert.AreSame(cells[i],actors[i].CurrentCell);
            Assert.IsFalse(views[i].IsAttacking);Assert.IsFalse(views[i].IsMoving);Assert.IsFalse(views[i].IsUsingAbility);
        }
        Capture("preparation-exhale");yield return null;yield return null;
        // A player's normally deployed figures face north; verify the rear view too.
        foreach(var view in views){view.Face(0,-1);Sample(view,.65f);view.enabled=true;}
        Capture("preparation-deployed");yield return null;yield return null;
        match.Pause(true);yield return null;
        var paused=views.Select(v=>v.VisualRoot.localScale).ToArray();
        yield return new WaitForSecondsRealtime(.15f);
        CollectionAssert.AreEqual(paused,views.Select(v=>v.VisualRoot.localScale));
        match.Pause(false);yield return new WaitForSecondsRealtime(.2f);
        Assert.IsTrue(views.Where((v,i)=>v.VisualRoot.localScale!=paused[i]).Any());
    }
}

