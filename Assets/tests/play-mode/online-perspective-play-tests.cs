using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class OnlinePerspectivePlayTests
{
    LocalGameUI ui;
    [UnitySetUp] public IEnumerator Setup()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();}yield return null;
    }
    void Start(BoardSide side)
    {
        ui.CloseModal();ui.Match.Menu();
        Assert.IsTrue(ui.Match.StartOnlineMatch(side,"popow",new[]{"tsu"},"bugaloo",new[]{"aky"},921,
            CardTableTheme.MapId(CardTableTheme.Current),CardTableTheme.MapId(CardTableTheme.Current)));
    }
    void Combat()
    {
        ui.Match.SetReady(true);ui.Match.SetOpponentReady(true);
        Assert.IsTrue(ui.Match.RevealOpponent(new PreparationCommand[0],ui.Match.Round));
        Assert.IsTrue(ui.Match.StartOnlineCombat(ui.Match.Round));
    }
    Button Button(string name)=>ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name==name);
    void Touch(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)button.transform;
        var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
        Assert.That(pointer.position.x,Is.InRange(0,Screen.width));Assert.That(pointer.position.y,Is.InRange(0,Screen.height));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Assert.IsNotEmpty(hits,"The button must be reachable by a real screen touch.");
        Assert.AreSame(button,hits[0].gameObject.GetComponentInParent<Button>(),"Another layer covers "+button.name);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    void AssertVitals()
    {
        Canvas.ForceUpdateCanvases();
        foreach(var actor in ui.Match.Actors.Values)
        {
            var bar=ui.CanvasRoot.GetComponentsInChildren<UnitVitalsUI>().Single(b=>b.name=="health-"+actor.UnitId);
            var mesh=bar.canvasRenderer.GetMesh();
            try
            {
                var colors=mesh.colors;
                if(actor.Side==ui.Match.LocalSide)
                    Assert.IsTrue(colors.Any(c=>c.g>.6f&&c.r<.4f&&c.b>.8f),"Own unit must have a cyan bar: "+actor.UnitId);
                else
                    Assert.IsTrue(colors.Any(c=>c.r>.8f&&c.g<.4f&&c.b<.4f),"Enemy must have a distinguishable muted red bar: "+actor.UnitId);
            }
            finally{Object.Destroy(mesh);}
        }
    }
    [UnityTest] public IEnumerator BothPerspectivesShowOwnDamageAndCyanAllies()
    {
        foreach(var side in new[]{BoardSide.Blue,BoardSide.Red})
        {
            Start(side);yield return null;yield return null;AssertVitals();
            Combat();yield return null;yield return null;AssertVitals();
            var expected=new List<string>();int incoming=0;
            System.Action<BattleUnit,BattleUnit,int> record=(source,target,amount)=>
            {
                if(amount<=0)return;
                if(source.Side==side&&target.Side!=side)expected.Add(amount.ToString());
                else if(source.Side!=side&&target.Side==side)incoming++;
            };
            ui.Match.Impacted+=record;
            try
            {
                // Both attacks resolve on the actual simulation; no mocked hit values.
                for(int tick=0;tick<600&&(expected.Count==0||incoming==0);tick++)ui.Match.Advance(.05f);
                Assert.Greater(expected.Count,0);Assert.Greater(incoming,0);
                // Match is disabled in this fixture; let only the presentation buffer reveal its totals.
                yield return new WaitForSeconds(.2f);
                var labels=ui.CanvasRoot.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("damage-number-")).Select(t=>t.text).ToArray();
                Assert.AreEqual(expected.Sum(x=>long.Parse(x)),labels.Sum(x=>long.Parse(x)),"Only damage dealt by the local player's team is displayed: "+side);
                Assert.That(labels.Length,Is.InRange(1,expected.Count),"Multiple impacts may share one target's label.");
                Assert.AreEqual(labels.Length,ui.GetComponent<CombatDamageNumbers>().ActiveNumberCount);
                var sizing=ui.GetComponent<CombatDamageNumbers>().Settings;
                foreach(var label in ui.CanvasRoot.GetComponentsInChildren<Text>().Where(t=>t.name.StartsWith("damage-number-")))
                    Assert.That(label.fontSize*label.rectTransform.localScale.x,Is.InRange(sizing.MinimumSize-.001f,sizing.MaximumSize+.001f));
            }
            finally{ui.Match.Impacted-=record;}
        }
    }
    [UnityTest] public IEnumerator OnlineOptionsStayReachableThroughCombatResultsAndExit()
    {
        foreach(var side in new[]{BoardSide.Blue,BoardSide.Red})
        {
            Start(side);yield return new WaitForSecondsRealtime(.3f);
            Touch(Button("Battle pause"));yield return null;
            Assert.IsFalse(ui.Match.Paused);Assert.AreEqual(1,Time.timeScale);
            Touch(Button("CONTINUAR"));Combat();yield return new WaitForSecondsRealtime(.3f);
            Touch(Button("Battle pause"));yield return null;
            Touch(Button("How to play"));yield return null;Touch(Button("GOT IT"));yield return null;
            Touch(Button("Sound"));yield return null;Touch(Button(GameText.Back));yield return null;
            Assert.IsTrue(Button("VOLVER AL MENÚ").isActiveAndEnabled);
            float remaining=ui.Match.Remaining;ui.Match.Advance(.2f);
            Assert.Less(ui.Match.Remaining,remaining,"Opening options must not freeze one online peer.");
            foreach(var actor in ui.Match.Actors.Values.Where(a=>a.Side==ui.Match.RivalSide))actor.ApplyDamage(actor.CurrentHealth);
            ui.Match.Advance(.2f);yield return null;
            Assert.AreEqual(MatchPhase.RoundResult,ui.Match.Phase);
            Assert.IsTrue(Button("VOLVER AL MENÚ").isActiveAndEnabled,"The round change must not dismiss online options.");
            Touch(Button("CONTINUAR"));yield return null;
            Touch(Button("Battle pause"));yield return null;
            bool exited=false;System.Action onExit=()=>exited=true;ui.Match.OnlineExited+=onExit;
            try{Touch(Button("VOLVER AL MENÚ"));Assert.IsTrue(exited);Assert.IsFalse(ui.Match.IsOnline);Assert.AreEqual(MatchPhase.Menu,ui.Match.Phase);}
            finally{ui.Match.OnlineExited-=onExit;}
            yield return null;
        }
    }
    [UnityTest] public IEnumerator BotMatchStillPausesAndResumes()
    {
        Assert.IsTrue(ui.Match.StartMatch("popow",new[]{"tsu"},922));yield return new WaitForSecondsRealtime(.3f);
        Touch(Button("Battle pause"));Assert.IsTrue(ui.Match.Paused);Assert.AreEqual(0,Time.timeScale);
        float remaining=ui.Match.Remaining;ui.Match.Advance(2);Assert.AreEqual(remaining,ui.Match.Remaining);
        yield return null;Touch(Button("CONTINUAR"));Assert.IsFalse(ui.Match.Paused);Assert.AreEqual(1,Time.timeScale);
    }
}
