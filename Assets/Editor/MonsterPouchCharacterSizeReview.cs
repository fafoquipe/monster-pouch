using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class MonsterPouchCharacterSizeReview
{
    const string Output = "Logs/local-validation/character-size-20261017/";
    static TestRunnerApi runner;

    public static void RunValidation()
    {
        Directory.CreateDirectory(Output);
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        TestRunnerApi.RegisterTestCallback(new Results());
        runner.Execute(new ExecutionSettings(new Filter {
            testMode = TestMode.EditMode,
            testNames = new[] { "CharacterSizeTests", "UnitWalkShadowTests", "CharacterCombatMotionTests" }
                .Select(name => "MonsterPouch.Gameplay.Tests.EditMode." + name).ToArray()
        }) { runSynchronously = true });
    }

    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(Output + "editmode.xml"));
            File.WriteAllText(Output + "test-status.txt", result.ResultState + "; passed=" + result.PassCount + "; failed=" + result.FailCount);
            TestRunnerApi.UnregisterTestCallback(this);
            if (!Application.isBatchMode) EditorApplication.delayCall += MonsterPouchBalanceEditor.Open;
        }
    }
}
