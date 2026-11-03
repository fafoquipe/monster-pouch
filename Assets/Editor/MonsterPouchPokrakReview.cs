using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class MonsterPouchPokrakReview
{
    const string Folder = "Logs/local-validation/pokrak/";
    static TestRunnerApi runner;
    public static void RunEditTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || MonsterPouchLunarPreview.TestState().Contains("testsRunning=True"))
            throw new System.InvalidOperationException("Wait for current test run.");
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        TestRunnerApi.RegisterTestCallback(new Results());
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] {
            "MonsterPouch.Gameplay.Tests.EditMode.PokrakTests", "MonsterPouch.Gameplay.Tests.EditMode.SukiTests",
            "MonsterPouch.Gameplay.Tests.EditMode.RosieTests", "MonsterPouch.Gameplay.Tests.EditMode.AbilityOwnershipTests",
            "MonsterPouch.Gameplay.Tests.DocumentedRosterTests", "MonsterPouch.Gameplay.Tests.EditMode.AbilityDescriptionSyncTests",
            "MonsterPouch.Gameplay.Tests.EditMode.EnergySuperActionTests", "MonsterPouch.Gameplay.Tests.EditMode.BalanceInspectorLayoutTests",
            "MonsterPouch.Gameplay.Tests.EditMode.BugalooSuperTests", "MonsterPouch.Gameplay.Tests.EditMode.CombatHudSnapshotTests"
        } }) { runSynchronously = true });
    }
    public static void RunPlayTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || MonsterPouchLunarPreview.TestState().Contains("testsRunning=True"))
            throw new System.InvalidOperationException("Wait for current test run.");
        Directory.CreateDirectory(Folder);
        SessionState.SetString("MonsterPouch.Validation.PendingRun", "pokrak/play");
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { "PokrakPlayTests" } }));
    }
    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            Directory.CreateDirectory(Folder);
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(Folder + "edit.xml"));
            File.WriteAllText(Folder + "edit-status.txt", result.ResultState + "; passed=" + result.PassCount + "; failed=" + result.FailCount);
            TestRunnerApi.UnregisterTestCallback(this);
        }
    }
}
