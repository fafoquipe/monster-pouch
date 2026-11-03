using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public sealed class CasinoHudPlayTests
{
    [UnityTearDown] public IEnumerator CleanupScene()
    {
        var ui=Object.FindFirstObjectByType<LocalGameUI>();
        if(ui!=null)
        {
            ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;
        }
        // Allow deferred UI destruction to finish before the next fixture reloads
        // the scene. The scene tests also run between synchronous NUnit cases.
        yield return null;
    }
    [Test] public void DamageSizesIncreaseAndStayBoundedIncludingCriticalAndPop()
    {
        var d=ScriptableObject.CreateInstance<DamageNumberSettings>();
        try
        {
            float previous=0;
            for(int damage=1;damage<=120;damage++)
            {
                float size=d.SizeFor(damage);Assert.GreaterOrEqual(size,previous);previous=size;
                foreach(bool critical in new[]{false,true})
                foreach(float pop in new[]{.65f,1,1.16f,1.30f})
                    Assert.That(d.ClampAnimatedSize(d.SizeFor(damage,critical)*pop),Is.InRange(d.MinimumSize,d.MaximumSize));
            }
            Assert.Greater(d.SizeFor(50),d.SizeFor(5));Assert.AreEqual(d.MinimumSize,d.SizeFor(0));Assert.AreEqual(d.MaximumSize,d.SizeFor(int.MaxValue));
        }
        finally{Object.DestroyImmediate(d);}
    }
    [TestCase(MatchPhase.Preparation,10.01f,0)]
    [TestCase(MatchPhase.Preparation,10f,10)]
    [TestCase(MatchPhase.Preparation,9.01f,10)]
    [TestCase(MatchPhase.Preparation,9f,9)]
    [TestCase(MatchPhase.Preparation,.01f,1)]
    [TestCase(MatchPhase.Combat,5.01f,0)]
    [TestCase(MatchPhase.Combat,5f,5)]
    [TestCase(MatchPhase.Combat,.01f,1)]
    [TestCase(MatchPhase.Combat,0,0)]
    [TestCase(MatchPhase.RoundResult,4,0)]
    [TestCase(MatchPhase.Menu,4,0)]
    public void CountdownUsesCorrectClockThreshold(MatchPhase phase,float seconds,int expected)
        =>Assert.AreEqual(expected,PhaseCountdownHud.NumberFor(phase,seconds));
    [Test] public void OnlineNamesPreserveActualPlayerNamesAndHandleEmptyValues()
    {
        Assert.AreEqual("Fafo",OnlineRoom.DisplayName(" Fafo "));
        Assert.AreEqual("RIVAL",OnlineRoom.DisplayName("   ","RIVAL"));
        Assert.AreEqual("Érika",OnlineRoom.DisplayName("Érika"));
        Assert.LessOrEqual(OnlineRoom.DisplayName(new string('x',40)).Length,20);
        Assert.AreEqual("Fafo",OnlineRoom.DisplayName("Fa\nfo"));
    }
    [UnityTest] public IEnumerator OpponentNameReadyAnimationAndCountdownAreNonBlocking()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;
        var ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        try
        {
            var room=ui.GetComponent<OnlineRoom>();
            var packetType=typeof(OnlineRoom).GetNestedType("Packet",BindingFlags.NonPublic);
            var peer=Activator.CreateInstance(packetType,true);
            packetType.GetField("playerName").SetValue(peer,"Rival Érika");
            typeof(OnlineRoom).GetField("opponent",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(room,peer);
            Assert.IsTrue(ui.Match.StartOnlineMatch(BoardSide.Red,"bugaloo",new[]{"stein"},"popow",new[]{"tsu"},932));
            yield return null;yield return null;
            Assert.AreEqual("Rival Érika",ui.CanvasRoot.GetComponentsInChildren<Text>().Single(t=>t.name=="Rival name").text);
            ui.Match.SetOpponentReady(true);yield return new WaitForSecondsRealtime(.42f);
            var check=ui.CanvasRoot.GetComponentsInChildren<OpponentReadyCheck>().Single();
            Canvas.ForceUpdateCanvases();var mesh=check.canvasRenderer.GetMesh();
            try{Assert.Greater(mesh.vertexCount,0);}finally{Object.Destroy(mesh);}
            Assert.IsFalse(check.raycastTarget);
            ui.Match.Advance(30.1f);yield return null;yield return null;
            var countdown=ui.GetComponent<PhaseCountdownHud>();Assert.AreEqual(10,countdown.VisibleNumber);
            var group=ui.CanvasRoot.GetComponentsInChildren<CanvasGroup>().Single(g=>g.name=="Central phase countdown");
            Assert.IsFalse(group.blocksRaycasts);Assert.IsFalse(group.interactable);
            Assert.IsFalse(group.GetComponentInChildren<Text>().raycastTarget);
            Assert.IsFalse(ui.Match.OnlineReady,"Countdown must not trigger readiness or consume preparation.");
            float time=ui.Match.Remaining;yield return new WaitForSecondsRealtime(.1f);Assert.AreEqual(time,ui.Match.Remaining);
            ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b=>b.name=="Battle pause").onClick.Invoke();Assert.AreEqual(0,group.alpha,"Opening a modal must suppress the countdown immediately.");yield return null;Assert.AreEqual(0,group.alpha);ui.CloseModal();
            ui.Match.SetOpponentReady(false);yield return new WaitForSecondsRealtime(.42f);
            check=ui.CanvasRoot.GetComponentsInChildren<OpponentReadyCheck>().Single();Canvas.ForceUpdateCanvases();mesh=check.canvasRenderer.GetMesh();
            try{Assert.AreEqual(0,mesh.vertexCount);}finally{Object.Destroy(mesh);}
        }
        finally{ui.Match.Menu();}
    }
    [UnityTest] public IEnumerator HotelHasOneContinuousFeltAndPreservesClockAndCartOutsideTheBoard()
    {
        var saved=CardTableTheme.Current;
        try
        {
            CardTableTheme.Select(TableStyle.GrandCasaHotel);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
            var ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
            var arena=Object.FindFirstObjectByType<CardTableArena>();var scenery=arena.GetComponent<ModularArenaScenery>();
            Assert.IsTrue(scenery.Definition.Tabletop);
            var sprites=scenery.transform.Find("Modular arena scenery/Independent environment pieces").GetComponentsInChildren<SpriteRenderer>();
            Assert.AreEqual(6,sprites.Length,"Original scene chips own the portrait; six existing accessories furnish the desktop margins.");
            Assert.IsTrue(scenery.Definition.Elements.All(e=>e.Rotation==0),"Preserve the original orientation of each accessory.");
            var decoration=scenery.transform.Find("Modular arena scenery/Floor, wall, trim and contact shadows").GetComponent<MeshFilter>();
            Assert.AreEqual(0,decoration.sharedMesh.vertexCount,"The felt has no betting outlines or decorative rails.");
            var shadows=scenery.transform.Find("Modular arena scenery").GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Projected prop shadows")).ToArray();
            var accessoryTextures=sprites.Select(s=>s.sprite.texture).Distinct().ToArray();
            Assert.AreEqual(accessoryTextures.Length,shadows.Length,"Only peripheral accessories use generated shadows.");
            CollectionAssert.AreEquivalent(accessoryTextures,shadows.Select(s=>s.sharedMaterial.mainTexture).ToArray());
            foreach(var shadow in shadows)
            {
                Assert.AreEqual("MonsterPouch/ScenerySilhouetteShadow",shadow.sharedMaterial.shader.name);
                Assert.IsNotNull(shadow.sharedMaterial.mainTexture);
                var clip=shadow.sharedMaterial.GetVector("_BoardExclusion");
                Assert.Less(clip.x,clip.z);Assert.Less(clip.y,clip.w);
                var mesh=shadow.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);Assert.Greater(mesh.colors.Max(c=>c.a),.25f);
            }
            foreach(var sprite in sprites)
            {
                var a=arena.PortraitPoint(new Vector3(sprite.bounds.min.x,sprite.bounds.max.y));
                var b=arena.PortraitPoint(new Vector3(sprite.bounds.max.x,sprite.bounds.min.y));
                Assert.IsFalse(Rect.MinMaxRect(a.x,a.y,b.x,b.y).Overlaps(arena.FramePortraitBounds),sprite.name);
                Assert.AreSame(sprite.sprite.texture,sprite.sharedMaterial.mainTexture);
            }
            var all=Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            foreach(string name in new[]{"clown-tower","shuffler-cart","table-frame","table-surface"})Assert.IsTrue(all.Single(r=>r.name==name).enabled,name);
            Assert.IsFalse(all.Single(r=>r.name=="casino-table-path").enabled);
            Assert.IsFalse(all.Single(r=>r.name=="Square environment").enabled);
            foreach(string name in new[]{"clown-tower","shuffler-cart"})
            {
                Assert.IsTrue(all.Single(r=>r.name==name+"-shadow").enabled,name+" lost its authored shadow.");
                Assert.IsFalse(shadows.Any(s=>s.sharedMaterial.mainTexture==all.Single(r=>r.name==name).sprite.texture),
                    name+" has both an authored and a generated shadow.");
            }
            var floor=all.Single(r=>r.name=="Repeating floor texture"&&r.gameObject.activeInHierarchy);
            Assert.AreEqual(SpriteDrawMode.Tiled,floor.drawMode);Assert.AreEqual("casino-table-path",floor.sprite.name);
            Assert.AreEqual(new Color(.90f,.90f,.90f,1),floor.color);
            var relayout=typeof(LocalGameUI).GetMethod("ApplyViewportLayout",BindingFlags.Instance|BindingFlags.NonPublic);
            var before=sprites.Select(s=>s.bounds).ToArray();
            relayout.Invoke(ui,new object[]{new Rect(0,0,Screen.width*.70f,Screen.height)});yield return null;
            for(int i=0;i<sprites.Length;i++)Assert.AreEqual(before[i],sprites[i].bounds);
        }
        finally{CardTableTheme.Select(saved);}
    }
}


