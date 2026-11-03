using System.Collections;
using System.Linq;
using System.Reflection;
using MonsterPouch.Gameplay.Progression;
using MonsterPouch.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
public sealed class RedesignIntegralPlayTests
{
    [UnityTest] public IEnumerator ProgressShowsCurrentNamesAndPreservesStoredUnlockIds()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        var ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        var progressField=typeof(LocalGameUI).GetField("progress",BindingFlags.NonPublic|BindingFlags.Instance);
        var configField=typeof(LocalGameUI).GetField("progressionConfig",BindingFlags.NonPublic|BindingFlags.Instance);
        var originalProgress=progressField.GetValue(ui);var originalConfig=configField.GetValue(ui);
        var config=ScriptableObject.CreateInstance<ProgressionConfig>();
        var progress=new PlayerProgress();
        progress.Unlocks.AddRange(new[]{"anuik","bugaloo","popow","qa-display-reward"});
        string saved=progress.Serialize();
        config.Rewards=new[]{new LevelReward{UnlockId="qa-display-reward",DisplayName="Premio QA"}};
        try
        {
            progressField.SetValue(ui,progress);configField.SetValue(ui,config);
            ui.ShowProgression();yield return null;
            string expected=string.Join(" · ",new[]{"anuik","bugaloo","popow"}
                .Select(id=>ui.Match.Config.Get(id).DisplayName).Concat(new[]{"Premio QA"}));
            var labels=ui.CanvasRoot.Find("Player progression").GetComponentsInChildren<Text>();
            Assert.IsTrue(labels.Any(label=>label.text==expected),"Progress must use current catalogue names and configured reward labels.");
            Assert.IsFalse(labels.Any(label=>label.text.Contains("anuik")||label.text.Contains("bugaloo")||label.text.Contains("qa-display-reward")));
            Assert.AreEqual(saved,progress.Serialize(),"Rendering names must not rewrite stored identities.");
        }
        finally
        {
            ui.CloseModal();progressField.SetValue(ui,originalProgress);configField.SetValue(ui,originalConfig);
            Object.Destroy(config);
        }
        yield return null;
    }
    [UnityTest] public IEnumerator SwipesRemainUsableAndDoNotCreateAFixedDuplicate()
    {
        yield return SceneManager.LoadSceneAsync("Assets/Scenes/main-scene.unity");yield return null;yield return null;
        var ui=Object.FindFirstObjectByType<LocalGameUI>();ui.Match.enabled=false;
        ui.OpenHome();yield return new WaitForSecondsRealtime(.4f);
        Assert.Greater(ui.GetComponentsInChildren<RetroButtonMotion>().Length,0);
        foreach(var face in ui.GetComponentsInChildren<RetroButtonFace>())
        {
            Assert.IsNotNull(face.GetComponent<CanvasRenderer>());var mesh=face.canvasRenderer.GetMesh();Assert.IsNotNull(mesh);
            Assert.Greater(mesh.vertexCount,0,"A button face must actually render");
        }
        foreach(var platform in ui.GetComponentsInChildren<LobbyPedestalGraphic>())Assert.IsNotNull(platform.GetComponent<CanvasRenderer>());
        for(int i=0;i<4;i++)
        {
            Vector2 drag=Vector2.right*(i%2==0?Screen.width*.8f:-Screen.width*.8f);
            Assert.IsTrue(ui.BeginMenuSwipe(drag));ui.EndMenuSwipe(drag);yield return new WaitForSecondsRealtime(.5f);
            var root=ui.CanvasRoot.parent.GetComponentsInChildren<Image>().Single(im=>im.name=="Full screen menu background");Assert.IsNull(root.sprite);
            Assert.IsTrue(ui.CanSwipeMenu);
            Assert.AreEqual(1,ui.CanvasRoot.GetComponentsInChildren<SquareSceneryView>().Length);
        }
        var button=ui.GetComponentsInChildren<RetroButtonMotion>().First();var rect=(RectTransform)button.transform;
        var position=rect.anchoredPosition;var size=rect.sizeDelta;
        button.OnSubmit(null);yield return null;
        Assert.AreEqual(position,rect.anchoredPosition);Assert.AreEqual(size,rect.sizeDelta);
        ui.ShowProgression();yield return null;
        Assert.IsTrue(ui.CanvasRoot.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("XP")));
    }
}
