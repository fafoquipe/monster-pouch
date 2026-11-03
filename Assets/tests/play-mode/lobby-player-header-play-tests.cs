using System;
using System.Linq;
using System.Reflection;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Native UI/text generation only. No scene, LocalGameUI.Awake, account or economic transaction is started.
public sealed class LobbyPlayerHeaderPlayTests
{
    static readonly MethodInfo Draw=typeof(LocalGameUI).GetMethod("DrawLobbyPlayerHeader",BindingFlags.Static|BindingFlags.NonPublic);
    static readonly MethodInfo IconPath=typeof(LocalGameUI).GetMethod("LobbyTrophyIconResource",BindingFlags.Static|BindingFlags.NonPublic);

    [TestCase(320,568,true)] [TestCase(320,568,false)]
    [TestCase(1280,720,true)] [TestCase(1280,720,false)]
    public void ExistingPlateFitsTheRealNameAndLargeTrophyAmountsAtMobileAndPcScales(int width,int height,bool spanish)
    {
        bool oldLanguage=GameText.Spanish;
        bool hadLanguage=PlayerPrefs.HasKey("monster-pouch.language");string savedLanguage=PlayerPrefs.GetString("monster-pouch.language");
        var font=Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold");Assert.IsNotNull(font);Assert.IsNotNull(Draw);
        var canvasObject=new GameObject("Header layout fixture",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        PortraitViewport.Fit(new Vector2(width,height),new Rect(0,0,width,height),out float scale);
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasObject.GetComponent<CanvasScaler>().scaleFactor=scale;canvas.scaleFactor=scale;
        // A temporary QA sprite exercises the visible icon layout; it is not a production fallback.
        var qaIcon=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f));
        try
        {
            GameText.SetLanguage(spanish);
            foreach(string player in new[]{"Álex V.","Alexandra Montenegro","WWWWWWWWWWWWWWWWWWWW"})
                foreach(long trophies in new long[]{0,1000,999999})
                {
                    var plate=new GameObject("Lobby trophies",typeof(RectTransform),typeof(CanvasRenderer),typeof(LobbyControlFace),typeof(Button));
                    plate.transform.SetParent(canvasObject.transform,false);var rect=(RectTransform)plate.transform;
                    LocalGameUI.SetRect(rect,new Rect(100,94,210,88));
                    int clicks=0;plate.GetComponent<Button>().onClick.AddListener(()=>clicks++);
                    Draw.Invoke(null,new object[]{rect,font,player,trophies,qaIcon});Canvas.ForceUpdateCanvases();
                    var name=plate.GetComponentsInChildren<Text>().Single(t=>t.name=="Lobby player name");
                    var amount=plate.GetComponentsInChildren<Text>().Single(t=>t.name=="Lobby trophy amount");
                    var icon=plate.GetComponentsInChildren<Image>().Single(i=>i.name=="Lobby trophy icon");
                    Assert.AreEqual(new Vector2(210,88),rect.rect.size);Assert.AreEqual(new Vector2(100,-94),rect.anchoredPosition);
                    Assert.AreEqual(player,name.text,"Names must remain literal and must not be translated.");
                    Assert.AreEqual(TextAnchor.MiddleCenter,name.alignment);Assert.IsFalse(name.supportRichText);
                    Assert.AreEqual(LobbyAccountSummary.Number(trophies),amount.text);
                    Assert.IsFalse(plate.GetComponentsInChildren<Text>().Any(t=>t.text=="COPAS"||t.text=="TROPHIES"));
                    Assert.That(name.cachedTextGenerator.lineCount,Is.InRange(1,2));
                    Assert.GreaterOrEqual(name.cachedTextGenerator.characterCountVisible,name.text.Length,player);
                    // TextGenerator reports the rendered pixel size. The dynamic font
                    // follows the canvas scale, while Text.minSize is in canvas units.
                    Assert.AreEqual(16,name.resizeTextMinSize);
                    Assert.GreaterOrEqual(name.cachedTextGenerator.fontSizeUsedForBestFit,
                        Mathf.FloorToInt(16*name.pixelsPerUnit),player);
                    Assert.LessOrEqual(name.cachedTextGenerator.fontSizeUsedForBestFit,
                        Mathf.CeilToInt(22*name.pixelsPerUnit),player);
                    Assert.AreEqual(1,amount.cachedTextGenerator.lineCount,amount.text);
                    Assert.GreaterOrEqual(amount.cachedTextGenerator.characterCountVisible,amount.text.Length,amount.text);
                    Assert.AreSame(qaIcon,icon.sprite);Assert.AreEqual(new Vector2(32,32),icon.rectTransform.rect.size);
                    Assert.IsTrue(icon.preserveAspect);Assert.IsFalse(icon.raycastTarget);Assert.IsFalse(amount.raycastTarget);Assert.IsFalse(name.raycastTarget);
                    var nameBounds=Bounds(name.rectTransform);var amountBounds=Bounds(amount.rectTransform);var iconBounds=Bounds(icon.rectTransform);var plateBounds=Bounds(rect);
                    AssertInside(plateBounds,nameBounds);AssertInside(plateBounds,amountBounds);AssertInside(plateBounds,iconBounds);
                    Assert.GreaterOrEqual(nameBounds.yMin,amountBounds.yMax-.1f);
                    Assert.That(iconBounds.xMin-amountBounds.xMax,Is.EqualTo(8*scale).Within(.1f));
                    Assert.That((amountBounds.xMin+iconBounds.xMax)*.5f,Is.EqualTo(plateBounds.center.x).Within(.1f));
                    Assert.That(nameBounds.center.x,Is.EqualTo(plateBounds.center.x).Within(.1f));
                    plate.GetComponent<Button>().onClick.Invoke();Assert.AreEqual(1,clicks);
                    Object.DestroyImmediate(plate);
                }
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);Object.DestroyImmediate(qaIcon);
            GameText.SetLanguage(oldLanguage);if(hadLanguage)PlayerPrefs.SetString("monster-pouch.language",savedLanguage);else PlayerPrefs.DeleteKey("monster-pouch.language");PlayerPrefs.Save();
        }
    }

    [Test] public void MissingApprovedIconKeepsItsSpaceWithoutDisplayingAnInventedFallback()
    {
        var root=new GameObject("Missing icon fixture",typeof(RectTransform));var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(210,88);
        try
        {
            Draw.Invoke(null,new object[]{rect,Resources.Load<Font>("MonsterPouch/UI/Fonts/Lato-Bold"),"Player One",0L,null});
            var icon=root.GetComponentsInChildren<Image>().Single();Assert.IsNull(icon.sprite);Assert.AreEqual(0,icon.color.a);
            Assert.AreEqual(new Vector2(32,32),icon.rectTransform.rect.size);
        }
        finally{Object.DestroyImmediate(root);}
    }

    [TestCase(0,"naturalis")] [TestCase(149,"naturalis")] [TestCase(150,"aquarion")]
    [TestCase(299,"aquarion")] [TestCase(300,"geothar")] [TestCase(449,"geothar")]
    [TestCase(450,"volkanar")] [TestCase(599,"volkanar")] [TestCase(600,"royal")] [TestCase(999999,"royal")]
    public void TrophyArtworkUsesTheCurrentScoreStage(long trophies,string nation)
    {
        Assert.IsNotNull(IconPath);
        Assert.AreEqual("MonsterPouch/UI/Progression/Trophies/"+nation,(string)IconPath.Invoke(null,new object[]{trophies}));
    }
    static Rect Bounds(RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
    static void AssertInside(Rect outer,Rect inner)
    {
        Assert.GreaterOrEqual(inner.xMin,outer.xMin-.1f);Assert.LessOrEqual(inner.xMax,outer.xMax+.1f);
        Assert.GreaterOrEqual(inner.yMin,outer.yMin-.1f);Assert.LessOrEqual(inner.yMax,outer.yMax+.1f);
    }
}
