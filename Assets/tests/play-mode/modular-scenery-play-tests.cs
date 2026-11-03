using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
public sealed class ModularSceneryPlayTests
{
    [UnityTest] public IEnumerator PiecesKeepTheirFeetAndSizeWhenTheViewportChanges()
    {
        var saved=CardTableTheme.Current;
        try
        {
            CardTableTheme.Select(TableStyle.CampoReal);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
            var ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
            var scenery=Object.FindFirstObjectByType<ModularArenaScenery>();Assert.IsTrue(scenery.IsVisible);
            var sprites=scenery.transform.Find("Modular arena scenery/Independent environment pieces").GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name!="Repeating floor texture").ToArray();
            Assert.AreEqual(scenery.Definition.Elements.Length,sprites.Length);
            var feet=sprites.Select(s=>s.bounds.min).ToArray();var sizes=sprites.Select(s=>s.bounds.size).ToArray();
            var relayout=typeof(LocalGameUI).GetMethod("ApplyViewportLayout",BindingFlags.NonPublic|BindingFlags.Instance);
            foreach(var safe in new[]{new Rect(0,0,Screen.width,Screen.height),new Rect(Screen.width*.2f,0,Screen.width*.6f,Screen.height)})
            {
                relayout.Invoke(ui,new object[]{safe});yield return null;
                for(int i=0;i<sprites.Length;i++)
                {Assert.AreEqual(feet[i],sprites[i].bounds.min);Assert.AreEqual(sizes[i],sprites[i].bounds.size);Assert.AreEqual(1,sprites[i].sharedMaterial.GetFloat("_PixelSize"));}
            }
            var arena=Object.FindFirstObjectByType<CardTableArena>();
            foreach(var sprite in sprites)
            {
                var topLeft=arena.PortraitPoint(new Vector3(sprite.bounds.min.x,sprite.bounds.max.y));
                var bottomRight=arena.PortraitPoint(new Vector3(sprite.bounds.max.x,sprite.bounds.min.y));
                Assert.IsFalse(Rect.MinMaxRect(topLeft.x,topLeft.y,bottomRight.x,bottomRight.y).Overlaps(arena.FramePortraitBounds),sprite.name+" overlaps the board frame.");
            }
        }
        finally {CardTableTheme.Select(saved);}
    }
    [UnityTest] public IEnumerator ParkUsesSeparateSpritesWithCorrectTexturesAndClearPlayableCells()
    {
        var saved=CardTableTheme.Current;
        try
        {
            CardTableTheme.Select(TableStyle.Default);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
            var ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
            var scenery=Object.FindFirstObjectByType<ModularArenaScenery>();
            var arena=Object.FindFirstObjectByType<CardTableArena>();
            var sprites=scenery.transform.Find("Modular arena scenery/Independent environment pieces").GetComponentsInChildren<SpriteRenderer>().Where(r=>r.name!="Repeating floor texture").ToArray();
            Assert.GreaterOrEqual(sprites.Select(r=>r.sprite.name).Distinct().Count(),26);
            Assert.Greater(sprites.Count(r=>r.sprite.name.EndsWith("-ball")),100);
            foreach(var sprite in sprites)
            {
                Assert.AreSame(sprite.sprite.texture,sprite.sharedMaterial.mainTexture,sprite.name+" sampled another atlas.");
                var a=arena.PortraitPoint(new Vector3(sprite.bounds.min.x,sprite.bounds.max.y));
                var b=arena.PortraitPoint(new Vector3(sprite.bounds.max.x,sprite.bounds.min.y));
                Assert.IsFalse(Rect.MinMaxRect(a.x,a.y,b.x,b.y).Overlaps(arena.BoardPortraitBounds),sprite.name+" entered playable cells.");
            }
            var relayout=typeof(LocalGameUI).GetMethod("ApplyViewportLayout",BindingFlags.NonPublic|BindingFlags.Instance);
            var before=sprites.Select(r=>r.bounds).ToArray();
            relayout.Invoke(ui,new object[]{new Rect(Screen.width*.1f,0,Screen.width*.8f,Screen.height)});yield return null;
            for(int i=0;i<sprites.Length;i++)Assert.AreEqual(before[i],sprites[i].bounds,sprites[i].name+" moved on resize.");
        }
        finally{CardTableTheme.Select(saved);}
    }
    [Test] public void MovingALampChangesNearbyLightAndDisablingItRestoresAmbient()
    {
        var go=new GameObject("scenery lamp fixture");
        try
        {
            var lamp=go.AddComponent<SceneryLamp>();lamp.Radius=3;lamp.Strength=.5f;lamp.Color=new Color(1,.8f,.4f);
            var point=new Vector3(1000,1000);go.transform.position=point;
            var ambient=new Color(.6f,.6f,.6f);var before=SceneryLamp.Illuminate(point,ambient);
            Assert.Greater(before.r,ambient.r);go.transform.position=point+Vector3.right*20;
            Assert.AreEqual(ambient,SceneryLamp.Illuminate(point,ambient));
            go.transform.position=point;go.SetActive(false);Assert.AreEqual(ambient,SceneryLamp.Illuminate(point,ambient));
        }
        finally {Object.DestroyImmediate(go);}
    }
}
