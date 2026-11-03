using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Progression;
using MonsterPouch.Gameplay.Units;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class RoadmapResultPlayTests
{
    EronyaMapTestAccount mapAccount;
    LocalGameUI ui;
    MatchController match;
    string previousProgress;
    bool hadProgress;
    const string ProgressKey="monster-pouch.progress.v1";

    [UnitySetUp] public IEnumerator Setup()
    {
        mapAccount=new EronyaMapTestAccount();
        hadProgress=PlayerPrefs.HasKey(ProgressKey);previousProgress=PlayerPrefs.GetString(ProgressKey);
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");
        yield return null;
        ui=Object.FindFirstObjectByType<LocalGameUI>();match=ui.Match;match.enabled=false;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(hadProgress)PlayerPrefs.SetString(ProgressKey,previousProgress);else PlayerPrefs.DeleteKey(ProgressKey);
        PlayerPrefs.Save();
        if(match!=null)match.Menu();
        yield return null;
        mapAccount?.Dispose();
    }
    [UnityTest] public IEnumerator VictoryOnlyAppearsAtEndOfBO5AndUsesNonBlockingConfetti()
    {
        match.StartMatch("popow",20261020);yield return null;
        for(int win=1;win<=3;win++)
        {
            match.Ready();yield return EndRound(BoardSide.Red);
            Assert.AreEqual(win,match.PlayerWins);
            if(win<3)
            {
                Assert.IsFalse(TextVisible("VICTORYYY"));
                Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="Victory confetti"));
                match.Advance(match.RoundTransitionDuration+.1f);yield return null;
            }
        }
        Assert.AreEqual(MatchPhase.MatchResult,match.Phase);Assert.IsTrue(TextVisible("VICTORYYY"));
        Assert.IsFalse(TextVisible("YOU LOSE"));
        var progressField=typeof(LocalGameUI).GetField("progress",BindingFlags.Instance|BindingFlags.NonPublic);
        var progress=(PlayerProgress)progressField.GetValue(ui);
        Assert.Contains(match.SessionId,progress.CompletedMatches,"Finishing the BO5 must commit its progress receipt.");
        string awarded=progress.Serialize();
        match.Advance(match.RoundStampStartTime+match.Config.RoundUI.StampDuration+.3f);
        yield return null;yield return null;
        var confetti=ui.CanvasRoot.GetComponentsInChildren<CanvasGroup>().Single(g=>g.name=="Victory confetti");
        Assert.Greater(confetti.alpha,0);Assert.IsFalse(confetti.blocksRaycasts);
        Assert.AreEqual(awarded,((PlayerProgress)progressField.GetValue(ui)).Serialize(),"Presentation must not award XP again.");
        yield return new WaitForSecondsRealtime(.3f);
        Capture("victory");yield return null;yield return null;
    }
    [UnityTest] public IEnumerator DefeatUsesApprovedTextRedTintAndNoVictoryConfetti()
    {
        match.StartMatch("popow",20261021);yield return null;
        for(int loss=1;loss<=3;loss++)
        {
            match.Ready();yield return EndRound(BoardSide.Blue);
            if(loss<3){match.Advance(match.RoundTransitionDuration+.1f);yield return null;}
        }
        Assert.AreEqual(MatchPhase.MatchResult,match.Phase);
        Assert.IsTrue(TextVisible("YOU LOSE"));Assert.IsFalse(TextVisible("VICTORYYY"));
        match.Advance(match.RoundTransitionDuration);yield return null;yield return null;
        var backdrop=ui.CanvasRoot.GetComponentsInChildren<Image>().Single(i=>i.name=="Round transition");
        Assert.Greater(backdrop.color.r,backdrop.color.g);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="Victory confetti"));
        yield return new WaitForSecondsRealtime(.3f);
        Capture("defeat");yield return null;yield return null;
    }
    [UnityTest] public IEnumerator EliminationHasNeitherTimeoutBannerNorHealthBars()
    {
        match.StartMatch("popow",20261020); match.Ready();
        yield return EndRound(BoardSide.Red);
        Assert.IsFalse(match.UsesHealthTiebreaker);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Timeout banner"));
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Round health comparison"));
        match.Advance(match.RoundStampStartTime); yield return null; yield return null;
        Assert.AreEqual(1,ui.RoundTransitionAlpha,.0001f);
    }

    [UnityTest] public IEnumerator TimeoutBannerIsLocalizedAndPrecedesEveryHealthBar()
    {
        bool oldLanguage=GameText.Spanish, hadLanguage=PlayerPrefs.HasKey("monster-pouch.language");
        string savedLanguage=PlayerPrefs.GetString("monster-pouch.language");
        try
        {
            foreach(bool spanish in new[]{true,false})
            {
                GameText.SetLanguage(spanish);
                match.StartMatch("popow",20261022); match.Ready();
                foreach(var actor in match.Actors.Values)
                    actor.Initialize(actor.UnitId,actor.Side,new UnitStats(101,0,99,100,100));
                match.Simulation.Begin(match.Actors.Values.ToArray());
                match.Advance(40); yield return null; yield return null;
                Assert.AreEqual(0,match.Remaining);
                Assert.IsTrue(match.UsesHealthTiebreaker);
                var banner=ui.CanvasRoot.GetComponentsInChildren<CanvasGroup>().Single(g=>g.name=="Timeout banner");
                var health=ui.CanvasRoot.GetComponentsInChildren<CanvasGroup>().Single(g=>g.name=="Round health comparison");
                Assert.AreEqual(1,banner.alpha);
                Assert.IsTrue(TextVisible(spanish?"¡TIEMPOOO!":"TIMEEE!!"));
                Assert.IsTrue(TextVisible(spanish?"DESEMPATE":"TIEBREAKER"));
                Assert.AreEqual(0,health.alpha); Assert.AreEqual(0,ui.RoundTransitionAlpha);
                match.Advance(.4f); yield return null; yield return null;
                Capture(spanish?"timeout-banner-es":"timeout-banner-en");
                match.Advance(match.RoundHealthStartTime-match.RoundResultElapsed-.01f); yield return null; yield return null;
                Assert.Greater(banner.alpha,0); Assert.AreEqual(0,health.alpha);
                match.Advance(.02f); yield return null; yield return null;
                Assert.IsFalse(banner.gameObject.activeSelf);
                Assert.AreEqual(1,health.alpha); Assert.AreEqual(0,ui.RoundTransitionAlpha);
                match.Menu(); yield return null;
            }
        }
        finally
        {
            GameText.SetLanguage(oldLanguage);
            if(hadLanguage)PlayerPrefs.SetString("monster-pouch.language",savedLanguage);else PlayerPrefs.DeleteKey("monster-pouch.language");
            PlayerPrefs.Save();
        }
    }

    [UnityTest] public IEnumerator HealthSequenceUsesLivingUnitsAndCompletesBeforeFlags()
    {
        Assert.IsTrue(match.StartMatch("popow",new[]{"atori"},20261022));
        var offer=match.Player.Offers[0];
        Assert.IsTrue(match.BuyAndPlace(0,offer.Token,match.Board.GetCell(1,8)));
        match.Ready();yield return null;
        // Isolated passive actors make the real scene reach timeout without variable attacks.
        // This edits instance stats only; no character definition or catalogue is changed.
        foreach(var actor in match.Actors.Values)
            actor.Initialize(actor.UnitId,actor.Side,new UnitStats(101,0,99,100,100));
        match.Simulation.Begin(match.Actors.Values.ToArray());
        var monster=match.Actors[match.Player.Monster];
        var whelp=match.Actors.Values.Single(a=>a.Side==BoardSide.Blue&&a.Category==UnitCategory.Whelp);
        monster.ApplyDamage(monster.CurrentHealth);whelp.ApplyDamage(88);
        match.Advance(40);yield return null;yield return null;
        Assert.AreEqual(MatchPhase.RoundResult,match.Phase);
        var summary=match.Simulation.HealthSummary;
        Assert.AreEqual(7,summary.BlueTotal);
        Assert.IsFalse(summary.Contributions.Any(c=>c.Unit==monster));
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="Health contribution "+monster.UnitId));
        float duration=match.Config.RoundUI.HealthDuration;
        // Red Monster first, while Blue has no living Monster to contribute.
        match.Advance(match.RoundHealthStartTime+duration*.85f/summary.SlotCount*.8f);
        yield return null;yield return null;
        Assert.AreEqual(0,ui.PresentedBlueHealth);
        Assert.Greater(ui.PresentedRedHealth,0);
        Assert.AreEqual(0,ui.RoundTransitionAlpha);
        Assert.AreEqual(0,ui.RoundStampProgress);
        // Enter the Whelp division, before its half-health begins flying to the bar.
        float divideTime=match.RoundHealthStartTime+duration*.85f/summary.SlotCount*1.2f;
        match.Advance(divideTime-match.RoundResultElapsed);yield return null;yield return null;
        var upper=HealthText("Health upper half ",whelp.UnitId);
        var lower=HealthText("Health lower half ",whelp.UnitId);
        var contribution=HealthText("Health contribution ",whelp.UnitId);
        Assert.AreEqual("13",upper.text);Assert.AreEqual("13",lower.text);
        Assert.IsTrue(upper.GetComponent<DiagonalHealthHalf>().UpperHalf);
        Assert.IsFalse(lower.GetComponent<DiagonalHealthHalf>().UpperHalf);
        Assert.Greater(upper.rectTransform.anchoredPosition.y,lower.rectTransform.anchoredPosition.y);
        Assert.Less(upper.rectTransform.anchoredPosition.x,lower.rectTransform.anchoredPosition.x);
        Assert.Greater(upper.color.a,.9f);Assert.Greater(lower.color.a,.9f);
        Assert.Greater(upper.color.g,upper.color.r);Assert.Greater(upper.color.g,upper.color.b);
        Assert.AreEqual(Color.black,upper.GetComponent<Outline>().effectColor);
        Assert.GreaterOrEqual(upper.GetComponent<Outline>().effectDistance.x,2.5f);
        AssertAboveCharacter(lower,whelp);
        foreach(var enemy in summary.Contributions.Where(c=>c.Side==match.RivalSide))
        {
            var enemyLabel=HealthText("Health contribution ",enemy.UnitId);
            Assert.Greater(enemyLabel.color.r,enemyLabel.color.g);
        }
        Assert.AreEqual(0,contribution.color.a,"Original text is replaced by two clipped pieces during the cut.");
        Assert.IsFalse(TextVisible("13 ÷ 2"));
        yield return new WaitForSecondsRealtime(.3f);
        Capture("health-division");yield return null;yield return null;
        match.Pause(true);match.Advance(5);yield return null;
        Assert.AreEqual(divideTime,match.RoundResultElapsed,.0001f);
        match.Pause(false);
        AdvanceToWhelpSlot(.32f,summary.SlotCount,duration);yield return null;yield return null;
        // Pause rebuilds the page; inspect its current label rather than a retired component.
        contribution=HealthText("Health contribution ",whelp.UnitId);
        Assert.AreEqual("7",contribution.text);
        Assert.Greater(contribution.color.a,.7f);
        Assert.AreEqual(0,ui.PresentedBlueHealth,"Half-health emerges before transferring.");
        upper=HealthText("Health upper half ",whelp.UnitId);
        lower=HealthText("Health lower half ",whelp.UnitId);
        Assert.That(upper.color.a,Is.InRange(.5f,.8f),"Cut pieces must fade progressively as the half-value emerges.");
        Assert.AreEqual(upper.color.a,lower.color.a,.0001f);
        AssertAboveCharacter(lower,whelp);
        yield return new WaitForSecondsRealtime(.3f);
        Capture("health-half-emerges");
        AdvanceToWhelpSlot(.6f,summary.SlotCount,duration);yield return null;yield return null;
        var orb=HealthOrb(whelp.UnitId);
        Assert.AreEqual(0,contribution.color.a,"The travelling value must have become an orb.");
        Assert.Greater(orb.color.a,.9f);Assert.Greater(orb.color.g,orb.color.r);
        Assert.IsFalse(orb.raycastTarget);
        Assert.That(upper.color.a,Is.GreaterThan(0).And.LessThan(.1f),"The pieces should still have a faint fading tail.");
        AssertHealthLabelsAreIntegers();
        foreach(var enemy in summary.Contributions.Where(c=>c.Side==match.RivalSide))
            Assert.Greater(HealthOrb(enemy.UnitId).color.r,HealthOrb(enemy.UnitId).color.g);
        Capture("health-orbs-flight");
        AdvanceToWhelpSlot(.86f,summary.SlotCount,duration);yield return null;yield return null;
        Assert.Greater(orb.rectTransform.localScale.x,1.5f,"Orb enlarges near the bar.");
        Assert.AreEqual(0,upper.color.a);Assert.AreEqual(0,lower.color.a);
        AssertHealthLabelsAreIntegers();
        Assert.Greater(ui.PresentedBlueHealth,5);
        Capture("health-arrival-pop");
        AdvanceToWhelpSlot(.97f,summary.SlotCount,duration);yield return null;yield return null;
        Assert.Less(orb.rectTransform.localScale.x,.3f,"Orb quickly shrinks into the bar.");
        Capture("health-arrival-shrink");
        float completed=match.RoundHealthStartTime+duration*.93f;
        match.Advance(completed-match.RoundResultElapsed);yield return null;yield return null;
        Assert.AreEqual(summary.BlueTotal,ui.PresentedBlueHealth,.0001f);
        Assert.AreEqual(summary.RedTotal,ui.PresentedRedHealth,.0001f);
        AssertHealthLabelsAreIntegers();
        Assert.AreEqual("7",ui.CanvasRoot.GetComponentsInChildren<Text>().Single(t=>t.name=="Blue health total").text);
        Assert.AreEqual(0,ui.RoundStampProgress);
        var bars=ui.CanvasRoot.GetComponentsInChildren<RoundHealthBarArtwork>();
        Assert.AreEqual(1,bars.Single(b=>b.name=="Red health track").Fill.fillAmount,.001f);
        Assert.AreEqual((float)summary.BlueTotal/summary.Maximum,
            bars.Single(b=>b.name=="Blue health track").Fill.fillAmount,.001f);
        Capture("health-complete");yield return null;yield return null;
        match.Advance(match.RoundTransitionDuration-match.RoundResultElapsed+.1f);
        yield return null;yield return null;
        Assert.AreEqual(MatchPhase.Preparation,match.Phase);
        Assert.IsFalse(ui.CanvasRoot.GetComponentsInChildren<Transform>().Any(t=>t.name=="Round health comparison"));
    }
    [UnityTest] public IEnumerator HealthColorsFollowLocalPlayerOnRedSide()
    {
        Assert.IsTrue(match.StartOnlineMatch(BoardSide.Red,"popow",new[]{"atori"},"bugaloo",new[]{"flo"},921,
            CardTableTheme.MapId(CardTableTheme.Current),CardTableTheme.MapId(CardTableTheme.Current)));
        match.SetReady(true);match.SetOpponentReady(true);
        Assert.IsTrue(match.RevealOpponent(new PreparationCommand[0],match.Round));
        Assert.IsTrue(match.StartOnlineCombat(match.Round));yield return null;
        foreach(var actor in match.Actors.Values)
            actor.Initialize(actor.UnitId,actor.Side,new UnitStats(101,0,99,100,100));
        match.Simulation.Begin(match.Actors.Values.ToArray());
        match.Advance(40);yield return null;yield return null;
        var summary=match.Simulation.HealthSummary;
        Assert.AreEqual(2,summary.Contributions.Count);
        match.Advance(match.RoundHealthStartTime);yield return null;yield return null;
        foreach(var item in summary.Contributions)
        {
            var label=HealthText("Health contribution ",item.UnitId);
            AssertAboveCharacter(label,item.Unit);
            Assert.AreEqual(item.Side==match.LocalSide,label.color.g>label.color.r);
            var bar=ui.CanvasRoot.GetComponentsInChildren<RoundHealthBarArtwork>().Single(i=>i.name==item.Side+" health track");
            Assert.AreEqual(item.Side==match.LocalSide,bar.IsLocal);
            Assert.AreEqual(match.MapForSide(item.Side),bar.MapId);
        }
        match.Advance(match.Config.RoundUI.HealthDuration*.85f*.6f);yield return null;yield return null;
        foreach(var item in summary.Contributions)
        {
            var orb=HealthOrb(item.UnitId);
            Assert.Greater(orb.color.a,.9f);
            Assert.AreEqual(item.Side==match.LocalSide,orb.color.g>orb.color.r);
        }
        yield return new WaitForSecondsRealtime(.3f);
        Capture("health-orbs-red-perspective");
    }
    [UnityTest] public IEnumerator HealthBarArtworkBelongsToEachPlayerAcrossMapChangesAndBothPerspectives()
    {
        bool hadPreference=PlayerPrefs.HasKey(CardTableTheme.PreferenceKey);
        string previousMap=PlayerPrefs.GetString(CardTableTheme.PreferenceKey);
        try
        {
            foreach(var localSide in new[]{BoardSide.Blue,BoardSide.Red})
            {
                string localMap=localSide==BoardSide.Blue?"royal":"classic";
                Assert.IsTrue(match.StartOnlineMatch(localSide,"popow",new[]{"atori"},"bugaloo",new[]{"flo"},921,localMap,"lounge"));
                // Neither the current preference nor the alternating round arena owns these bars.
                CardTableTheme.Select(TableStyle.CampoReal);
                for(int round=1;round<=2;round++)
                {
                    match.SetReady(true);match.SetOpponentReady(true);
                    Assert.IsTrue(match.RevealOpponent(new PreparationCommand[0],match.Round));
                    Assert.IsTrue(match.StartOnlineCombat(match.Round));yield return null;
                    foreach(var actor in match.Actors.Values)
                        actor.Initialize(actor.UnitId,actor.Side,new UnitStats(101,0,99,100,100));
                    match.Simulation.Begin(match.Actors.Values.ToArray());
                    match.Advance(40);yield return null;yield return null;
                    var bars=ui.CanvasRoot.GetComponentsInChildren<RoundHealthBarArtwork>();
                    Assert.AreEqual(2,bars.Length);
                    var own=bars.Single(b=>b.IsLocal);var rival=bars.Single(b=>!b.IsLocal);
                    Assert.AreEqual(localMap,own.MapId);Assert.AreEqual("lounge",rival.MapId);
                    Assert.AreEqual("classic",own.ArtworkId);Assert.AreEqual("classic-v1",own.Fill.sprite.texture.name);
                    Assert.AreEqual("classic",rival.ArtworkId);Assert.AreEqual("classic-v1",rival.Fill.sprite.texture.name);
                    Assert.AreEqual(FilterMode.Point,own.Fill.sprite.texture.filterMode);
                    Assert.Greater(own.Fill.sprite.texture.width,2048,"Import must preserve the measured source coordinates.");
                    Assert.AreEqual(0,own.Fill.fillAmount);
                    own.SetValue(25,100);Assert.AreEqual(.25f,own.Fill.fillAmount);
                    own.SetValue(0,100);Assert.AreEqual(0,own.Fill.fillAmount);
                    match.Advance(match.RoundHealthStartTime+match.Config.RoundUI.HealthDuration*.93f);
                    yield return null;yield return null;
                    Assert.AreEqual(1,own.Fill.fillAmount,.001f);Assert.AreEqual(1,rival.Fill.fillAmount,.001f);
                    yield return new WaitForSecondsRealtime(.8f);
                    Capture("health-player-maps-"+localSide+"-round-"+round);
                    if(round==1)Assert.IsTrue(match.NextOnlineRound(round));
                }
                match.Menu();yield return null;
            }
        }
        finally
        {
            if(hadPreference)PlayerPrefs.SetString(CardTableTheme.PreferenceKey,previousMap);
            else PlayerPrefs.DeleteKey(CardTableTheme.PreferenceKey);
            PlayerPrefs.Save();
        }
    }

    void AssertAboveCharacter(Text label,BattleUnit unit)
    {
        Canvas.ForceUpdateCanvases();
        var renderer=unit.GetComponent<UnitPresentation>().Renderer;
        var bounds=renderer.bounds;
        float characterTop=ui.GameCamera.WorldToScreenPoint(new Vector3(bounds.center.x,bounds.max.y,bounds.center.z)).y;
        var corners=new Vector3[4];label.rectTransform.GetWorldCorners(corners);
        float labelBottom=corners.Min(p=>RectTransformUtility.WorldToScreenPoint(null,p).y);
        Assert.Greater(labelBottom,characterTop,"Health must appear above, never on, the character.");
    }
    RoundHealthOrb HealthOrb(string id)=>ui.CanvasRoot.GetComponentsInChildren<RoundHealthOrb>().Single(o=>o.name=="Health orb "+id);
    void AssertHealthLabelsAreIntegers()
    {
        foreach(var label in ui.CanvasRoot.GetComponentsInChildren<Text>()
            .Where(t=>t.name.StartsWith("Health ")||t.name.EndsWith(" health total")))
            Assert.IsTrue(int.TryParse(label.text,out _),label.name+" must never show decimals: "+label.text);
    }
    Text HealthText(string prefix,string id)=>ui.CanvasRoot.GetComponentsInChildren<Text>().Single(t=>t.name==prefix+id);
    void AdvanceToWhelpSlot(float slot,int count,float duration)
    {
        float target=match.RoundHealthStartTime+duration*.85f/count*(1+slot);
        match.Advance(target-match.RoundResultElapsed);
    }
    bool TextVisible(string text)=>ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text==text);
    IEnumerator EndRound(BoardSide losingSide)
    {
        for(int i=0;i<200&&match.Phase==MatchPhase.Combat;i++)
        {
            foreach(var actor in match.Actors.Values.Concat(match.Simulation.CombatOnlyUnits)
                .Where(a=>a!=null&&a.IsAlive&&a.Side==losingSide).Distinct().ToArray())
                actor.ApplyDamage(actor.CurrentHealth);
            match.Advance(.1f);
            yield return null;
        }
        yield return null;yield return null;
        Assert.That(match.Phase,Is.EqualTo(MatchPhase.RoundResult).Or.EqualTo(MatchPhase.MatchResult));
    }
    void Capture(string name)
    {
        var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/roadmap-v1.2/"+name+".png"));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        // Batchmode does not render the Game view or service ScreenCapture requests.
        // Render the actual scene and its UI into a portrait target, then restore it.
        var camera=ui.GameCamera;
        var canvas=ui.CanvasRoot.GetComponentInParent<Canvas>().rootCanvas;
        var scaler=canvas.GetComponent<CanvasScaler>();
        var oldTarget=camera.targetTexture;var oldRect=camera.rect;
        float oldAspect=camera.aspect,oldScale=canvas.scaleFactor,oldDistance=canvas.planeDistance;
        var oldMode=canvas.renderMode;var oldCamera=canvas.worldCamera;
        int oldOrder=canvas.sortingOrder;bool oldOverride=canvas.overrideSorting;
        bool scalerEnabled=scaler!=null&&scaler.enabled;
        var oldPosition=ui.CanvasRoot.anchoredPosition;
        var previous=RenderTexture.active;
        var target=new RenderTexture(540,960,24,RenderTextureFormat.ARGB32);
        var pixels=new Texture2D(540,960,TextureFormat.RGBA32,false);
        try
        {
            target.Create();camera.targetTexture=target;camera.rect=new Rect(0,0,1,1);camera.aspect=540f/960;
            if(scaler!=null)scaler.enabled=false;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;
            // Match Overlay ordering: world sprites must remain behind every UI element.
            canvas.overrideSorting=true;canvas.sortingOrder=32760;
            canvas.planeDistance=1;canvas.scaleFactor=1;
            ui.CanvasRoot.anchoredPosition=Vector2.zero;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,540,960),0,0);pixels.Apply();
            File.WriteAllBytes(path,pixels.EncodeToPNG());
            Assert.Greater(new FileInfo(path).Length,1000,"A real scene capture must be produced.");
        }
        finally
        {
            RenderTexture.active=previous;
            camera.targetTexture=oldTarget;camera.rect=oldRect;camera.aspect=oldAspect;
            canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;canvas.planeDistance=oldDistance;canvas.scaleFactor=oldScale;
            canvas.overrideSorting=oldOverride;canvas.sortingOrder=oldOrder;
            if(scaler!=null)scaler.enabled=scalerEnabled;
            ui.CanvasRoot.anchoredPosition=oldPosition;
            target.Release();Object.Destroy(target);Object.Destroy(pixels);
            Canvas.ForceUpdateCanvases();
        }
    }
}
