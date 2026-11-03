using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class MonsterPouchSukiReview
{
    const string Folder = "Logs/local-validation/suki/";
    static TestRunnerApi runner;
    public static void RunAnimationTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || MonsterPouchLunarPreview.TestState().Contains("testsRunning=True"))
            throw new System.InvalidOperationException("Wait for the current test run.");
        runner=ScriptableObject.CreateInstance<TestRunnerApi>();
        TestRunnerApi.RegisterTestCallback(new Results("animation-v2"));
        runner.Execute(new ExecutionSettings(new Filter {testMode=TestMode.EditMode,testNames=new[]{
            "MonsterPouch.Gameplay.Tests.EditMode.SukiAnimationTests",
            "MonsterPouch.Gameplay.Tests.EditMode.CharacterCombatMotionTests",
            "MonsterPouch.Gameplay.Tests.EditMode.ReferenceHopTests"
        }}) {runSynchronously=true});
    }
    public static void RunEditTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || MonsterPouchLunarPreview.TestState().Contains("testsRunning=True"))
            throw new System.InvalidOperationException("Wait for the current test run.");
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        TestRunnerApi.RegisterTestCallback(new Results("edit"));
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = new[] {
            "MonsterPouch.Gameplay.Tests.EditMode.SukiTests", "MonsterPouch.Gameplay.Tests.EditMode.RosieTests",
            "MonsterPouch.Gameplay.Tests.EditMode.AbilityOwnershipTests", "MonsterPouch.Gameplay.Tests.DocumentedRosterTests",
            "MonsterPouch.Gameplay.Tests.EditMode.AbilityDescriptionSyncTests", "MonsterPouch.Gameplay.Tests.EditMode.EnergySuperActionTests"
        } }) { runSynchronously = true });
    }
    public static void RunPlayTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || MonsterPouchLunarPreview.TestState().Contains("testsRunning=True"))
            throw new System.InvalidOperationException("Wait for the current test run.");
        SessionState.SetString("MonsterPouch.Validation.PendingRun", "suki/play");
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { "SukiPlayTests", "CombatHudPlayTests" } }));
    }
    sealed class Results : ICallbacks
    {
        readonly string kind;
        public Results(string kind) { this.kind = kind; }
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            Directory.CreateDirectory(Folder);
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(Folder + kind + ".xml"));
            File.WriteAllText(Folder + kind + "-status.txt", result.ResultState + "; passed=" + result.PassCount + "; failed=" + result.FailCount);
            TestRunnerApi.UnregisterTestCallback(this);
        }
    }
}
