using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class MonsterPouchMartioValidation
{
    const string Mode = "MonsterPouch.Martio.ValidationMode";
    static TestRunnerApi runner;
    static MonsterPouchMartioValidation() { TestRunnerApi.RegisterTestCallback(new Results()); }
    public static void Run()
    {
        Directory.CreateDirectory("Logs/local-validation/martio");
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        SessionState.SetString(Mode, "editmode");
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
            testNames = new[] { "MartioCombatIntegrationTests", "MartioBoardOccupancyTests", "MartioHeadingRangeTests",
                "MartioProjectileTests", "BraidedProjectileGeometryTests", "DocumentedCombatTests", "KazuruTests", "AbilityOwnershipTests" }
                .Select(name => "MonsterPouch.Gameplay.Tests.EditMode." + name)
                .Concat(new[] { "MonsterPouch.Gameplay.Tests.DocumentedRosterTests" }).ToArray()
        }) { runSynchronously = true });
    }
    public static void RunPlay()
    {
        Directory.CreateDirectory("Logs/local-validation/martio");
        SessionState.SetString(Mode, "playmode");
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode, testNames = new[] { "MartioPlayTests" } }));
    }
    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string mode = SessionState.GetString(Mode, ""); if (mode.Length == 0) return;
            TestRunnerApi.SaveResultToFile(result, Path.GetFullPath("Logs/local-validation/martio/" + mode + ".xml"));
            File.WriteAllText("Logs/local-validation/martio/" + mode + "-result.txt", result.ResultState + "; passed=" + result.PassCount + "; failed=" + result.FailCount + "; skipped=" + result.SkipCount);
            SessionState.EraseString(Mode);
        }
    }
}
