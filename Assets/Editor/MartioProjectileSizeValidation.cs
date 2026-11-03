using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class MartioProjectileSizeValidation
{
    const string Running = "MonsterPouch.MartioProjectileSize.Validation";
    static TestRunnerApi runner;
    static MartioProjectileSizeValidation() { TestRunnerApi.RegisterTestCallback(new Results()); }
    public static void Run()
    {
        Directory.CreateDirectory("Logs/local-validation/martio-projectile-size");
        SessionState.SetBool(Running, true);
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.PlayMode,
            testNames = new[] { "MartioProjectileSizePlayTests", "MartioPlayTests" } }));
    }
    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(Running, false)) return;
            string folder = Path.GetFullPath("Logs/local-validation/martio-projectile-size");
            Directory.CreateDirectory(folder);
            TestRunnerApi.SaveResultToFile(result, Path.Combine(folder, "playmode.xml"));
            File.WriteAllText(Path.Combine(folder, "playmode-result.txt"), result.ResultState + "; passed=" + result.PassCount + "; failed=" + result.FailCount + "; skipped=" + result.SkipCount);
            SessionState.EraseBool(Running);
        }
    }
}
