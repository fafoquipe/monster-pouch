using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
public sealed class October8MenuPlayTests
{
    LocalGameUI ui;
    [UnitySetUp] public IEnumerator Setup(){yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;}
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if(ui!=null){ui.CloseModal();ui.Match.Menu();ui.Match.enabled=true;}
        yield return null;
    }
    [UnityTest] public IEnumerator ModesAndRoomCodeInputWorkWithUppercaseAndClipboard()
    {
        ui.ShowBattleModes();yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text=="BOT"));
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text==GameText.Resolve("SALA")));
        ui.ShowJoinRoom();yield return null;
        var input=ui.CanvasRoot.GetComponentInChildren<InputField>();input.text="ab12cd";
        Assert.AreEqual("AB12CD",input.text);
        string prior=GUIUtility.systemCopyBuffer;
        try
        {
            GUIUtility.systemCopyBuffer=" z9-xy01 ";Click("PEGAR");Assert.AreEqual("Z9XY01",input.text);
            Click("COPIAR");Assert.AreEqual("Z9XY01",GUIUtility.systemCopyBuffer);
        }
        finally{GUIUtility.systemCopyBuffer=prior;}
    }
    void Click(string label)=>ui.CanvasRoot.GetComponentsInChildren<Button>().First(b=>b.GetComponentsInChildren<Text>().Any(t=>t.text==GameText.Resolve(label))).onClick.Invoke();
    [UnityTest] public IEnumerator ShopChallengesAndHeroCollectionReplaceOldTabs()
    {
        ui.NavigateMenu(0);yield return new WaitForSecondsRealtime(.3f);
        Assert.IsNotNull(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="Shop page"));
        ui.NavigateMenu(4);yield return new WaitForSecondsRealtime(.3f);
        Assert.IsNotNull(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="Challenges page"));
        ui.OpenHome();ui.OpenCollection();yield return null;
        Assert.IsNotNull(ui.CanvasRoot.GetComponentsInChildren<RectTransform>().FirstOrDefault(r=>r.name=="Character selection"));
    }
    [UnityTest] public IEnumerator SafeAreaBackdropIsOneContinuousImage()
    {
        typeof(LocalGameUI).GetMethod("ApplyViewportLayout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{new Rect(0,54,Screen.width,Screen.height-100)});
        ui.OpenDecks();yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();
        var background=ui.CanvasRoot.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Menu background");
        var corners=new Vector3[4];background.GetWorldCorners(corners);
        Assert.Less(Vector2.Distance(corners[0],Vector2.zero),1);Assert.Less(Vector2.Distance(corners[2],new Vector2(Screen.width,Screen.height)),1);
    }
}
