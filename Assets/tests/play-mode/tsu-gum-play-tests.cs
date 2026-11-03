using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class TsuGumPlayTests
{
    LocalGameUI ui;
    [UnityTearDown] public IEnumerator Cleanup(){if(ui!=null){ui.Match.Pause(false);ui.Match.Menu();}yield return null;}
    [UnityTest] public IEnumerator TsuLaunchesRoundGumWithBasicAttackAndFlightPauses()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        Assert.IsTrue(ui.Match.StartMatch("anuik",new[]{"tsu"},736));ui.Match.Player.AddCombatCoins(30);
        int slot=System.Array.FindIndex(ui.Match.Player.Offers.ToArray(),o=>o!=null&&o.UnitId=="tsu");
        Assert.GreaterOrEqual(slot,0);Assert.IsTrue(ui.Match.BuyAndPlace(slot,ui.Match.Player.Offers[slot].Token,ui.Match.Board.GetCell(2,6)));
        var actor=ui.Match.Actors[ui.Match.Player.Owned["tsu"]];ui.Match.Ready();
        var target=ui.Match.Actors[ui.Match.Bot.Monster];
        var view=actor.GetComponent<UnitPresentation>();
        float delay=CombatSimulation.GetImpactDelay(actor,target);
        view.Attack(target.transform.position,true,delay);view.UseAbility("tsu-root");Assert.IsFalse(view.IsUsingAbility);
        var effects=ui.GetComponent<DocumentedBattleEffects>();
        typeof(DocumentedBattleEffects).GetMethod("Attacked",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effects,new object[]{actor,target,true});
        Image gum=null;float timeout=Time.realtimeSinceStartup+5;
        while(gum==null&&Time.realtimeSinceStartup<timeout)
        {yield return null;gum=ui.CanvasRoot.GetComponentsInChildren<Image>().FirstOrDefault(i=>i.name=="documented-projectile-tsu"&&i.sprite!=null);}
        Assert.IsNotNull(gum);ui.Match.Pause(true);
        int width=gum.sprite.texture.width;
        var buffer=RenderTexture.GetTemporary(width,width,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;var copy=new Texture2D(width,width,TextureFormat.RGBA32,false);
        Color32[] pixels;
        try
        {
            Graphics.Blit(gum.sprite.texture,buffer);RenderTexture.active=buffer;
            copy.ReadPixels(new Rect(0,0,width,width),0,0);copy.Apply();pixels=copy.GetPixels32();
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(buffer);Object.Destroy(copy);}
        var opaque=Enumerable.Range(0,pixels.Length).Where(i=>pixels[i].a>0).ToArray();
        int minX=opaque.Min(i=>i%width),maxX=opaque.Max(i=>i%width),minY=opaque.Min(i=>i/width),maxY=opaque.Max(i=>i/width);
        Assert.AreEqual(maxX-minX,maxY-minY,"Gum should be round, without a stick beneath it.");
        Assert.Greater(opaque.Count(i=>i/width==minY),2,"The bottom edge should be part of the ball, not a narrow candy stick.");
        Assert.IsFalse(gum.raycastTarget);var position=gum.rectTransform.anchoredPosition;
        yield return new WaitForSecondsRealtime(.15f);Assert.AreEqual(position,gum.rectTransform.anchoredPosition);
        var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/local-validation/screenshots"));Directory.CreateDirectory(folder);
        yield return Application.isBatchMode ? null : new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,"tsu-bola-chicle.png"));yield return null;
        ui.Match.Pause(false);yield return new WaitForSeconds(delay+.2f);
        Assert.IsTrue(gum==null,"The projectile should disappear after reaching its target.");
    }
}
