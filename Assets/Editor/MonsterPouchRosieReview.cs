using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class MonsterPouchRosieReview
{
    static TestRunnerApi runner;
    public static void ApplyAndValidate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || MonsterPouchLunarPreview.TestState().Contains("testsRunning=True"))
            throw new System.InvalidOperationException("Wait for the existing test run to finish.");
        MonsterPouchRosieSetup.Apply();
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        TestRunnerApi.RegisterTestCallback(new Results());
        var names = new[] { "RosieTests", "AbilityOwnershipTests", "DocumentedRosterTests", "DocumentedCombatTests",
            "AbilityDescriptionSyncTests", "EnergySuperActionTests", "RangeAndSuperCadenceTests",
            "CombatHudSnapshotTests", "BalanceInspectorLayoutTests" };
        runner.Execute(new ExecutionSettings(new Filter { testMode=TestMode.EditMode,
            testNames=names.Select(n=>(n=="DocumentedRosterTests"?"MonsterPouch.Gameplay.Tests.":"MonsterPouch.Gameplay.Tests.EditMode.")+n).ToArray() }));
    }
    public static string PlayChecks()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || MonsterPouchLunarPreview.TestState().Contains("testsRunning=True"))
            throw new System.InvalidOperationException("Wait for the existing test run to finish.");
        SessionState.SetString("MonsterPouch.Validation.PendingRun","rosie/play-final");
        MonsterPouchLayoutPreview.Resize("1080x1920");
        runner=ScriptableObject.CreateInstance<TestRunnerApi>();
        return runner.Execute(new ExecutionSettings(new Filter { testMode=TestMode.PlayMode, testNames=new[]{"RosiePlayTests"} }));
    }
    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) {}
        public void TestStarted(ITestAdaptor test) {}
        public void TestFinished(ITestResultAdaptor result) {}
        public void RunFinished(ITestResultAdaptor result)
        {
            const string folder="Logs/local-validation/rosie/";
            Directory.CreateDirectory(folder);
            TestRunnerApi.SaveResultToFile(result,Path.GetFullPath(folder+"edit-final.xml"));
            File.WriteAllText(folder+"edit-status.txt",result.ResultState+"; passed="+result.PassCount+"; failed="+result.FailCount+"; skipped="+result.SkipCount);
            TestRunnerApi.UnregisterTestCallback(this);
            if(Application.isBatchMode)EditorApplication.Exit(result.FailCount==0&&result.PassCount>0?0:2);
        }
    }
}
