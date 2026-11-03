using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class MonsterPouchValidation
{
    const string PendingRunKey = "MonsterPouch.Validation.PendingRun";
    static TestRunnerApi runner;
    static readonly Results callbacks = new Results();

    static MonsterPouchValidation()
    {
        // Entering and leaving Play Mode reloads this assembly. The Test Framework's
        // callback list is not serialized, so register again on every domain reload.
        TestRunnerApi.RegisterTestCallback(callbacks);
    }

    public static void EditTests()
    {
        Execute("edit-mode", TestMode.EditMode, "MonsterPouch.Gameplay.Tests.EditMode");
    }
    public static void PlayTests()
    {
        Execute("play-mode", TestMode.PlayMode, "MonsterPouch.Local.Tests.PlayMode");
    }

    public static void FocusedTests(string names)
    {
        SessionState.SetString(PendingRunKey, "focused-edit");
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter { testMode=TestMode.EditMode, testNames=names.Split('|') }));
    }

    static void Execute(string name, TestMode mode, string assembly)
    {
        SessionState.SetString(PendingRunKey, name);
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        try
        {
            runner.Execute(new ExecutionSettings(new Filter { testMode = mode, assemblyNames = new[] { assembly } }));
        }
        catch
        {
            SessionState.EraseString(PendingRunKey);
            throw;
        }
    }

    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { ReportProgress("started",test.FullName); }
        public void TestFinished(ITestResultAdaptor result) { ReportProgress(result.ResultState,result.FullName); }
        static void ReportProgress(string state,string name)
        {
            string run=SessionState.GetString(PendingRunKey,string.Empty);
            if(!string.IsNullOrEmpty(run))MonsterPouchLocalBridge.Reply(run+"-progress",System.DateTime.UtcNow.ToString("O")+" "+state+" "+name);
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            string name = SessionState.GetString(PendingRunKey, string.Empty);
            if (string.IsNullOrEmpty(name)) return;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/local-validation"));
            Directory.CreateDirectory(dir);
            TestRunnerApi.SaveResultToFile(result, Path.Combine(dir, name + ".xml"));
            MonsterPouchLocalBridge.Reply(name, "passed=" + result.PassCount + "; failed=" + result.FailCount + "; skipped=" + result.SkipCount + "; " + result.ResultState);
            SessionState.EraseString(PendingRunKey);
        }
    }
}
