using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
public static class MonsterPouchPreparationReview
{
    public static void RunPlayTests()
        =>Run(new[]{"NewWhelpPreparationPlayTests","PokrakPlayTests"},"play");
    public static void RunCollectionTest()
        =>Run(new[]{"PokrakPlayTests.CollectionShowsPokrakWithPixelIconsDescriptionsAndAllAnimations"},"collection");
    public static void RunPreparationTest()
        =>Run(new[]{"NewWhelpPreparationPlayTests"},"preparation");
    static void Run(string[] tests,string report)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||MonsterPouchLunarPreview.TestState().Contains("testsRunning=True"))
            throw new System.InvalidOperationException("Wait for current test run.");
        Directory.CreateDirectory("Logs/local-validation/preparation-idles-20261017");
        SessionState.SetString("MonsterPouch.Validation.PendingRun","preparation-idles-20261017/"+report);
        var runner=ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter {testMode=TestMode.PlayMode,testNames=tests}));
    }
}

