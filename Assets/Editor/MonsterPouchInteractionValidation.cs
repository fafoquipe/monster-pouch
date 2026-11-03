using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Focused tests in the existing editor. This never calls BuildPipeline or changes build targets.
[InitializeOnLoad]
public static class MonsterPouchInteractionValidation
{
    const string Pending = "MonsterPouch.InteractionValidation.Result";
    static readonly Results Callback = new Results();
    static MonsterPouchInteractionValidation() => TestRunnerApi.RegisterTestCallback(Callback);
    public static string Run(string mode)
    {
        bool running=(bool)typeof(TestRunnerApi).GetMethod("IsRunActive",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        if(running||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)
            throw new InvalidOperationException("El editor está ocupado; espera sin interrumpir la tarea activa.");
        if(mode!="edit"&&mode!="play"&&mode!="bank-hold")throw new ArgumentException("Usa edit, play o bank-hold.");
        string path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/local-validation",
            "interaction-devbuild-"+mode+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".xml"));
        SessionState.SetString(Pending,path);
        try
        {
            var names=mode=="edit"?new[]{"AndroidDeveloperContentTests","AndroidDeveloperBuildRecipeTests",
                "CombatTargetSnapshotTests","UnitHoldProgressTests","PointerGestureFlowTests","AbilityPurchaseEligibilityTests"}:
                mode=="bank-hold"?new[]{"UnitHoldFeedbackPlayTests.BankHoldAndCancelledDragPreserveEconomyWhileCompletedDragPaysExactlyOnce"}:
                new[]{"AbilityAvailabilityVisualPlayTests","UnitHoldFeedbackPlayTests","AttackRangePreviewPlayTests"};
            var runner=ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.Execute(new ExecutionSettings(new Filter { testMode=mode=="edit"?TestMode.EditMode:TestMode.PlayMode,testNames=names }));
            return path;
        }
        catch { SessionState.EraseString(Pending);throw; }
    }
    sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string path=SessionState.GetString(Pending,"");if(string.IsNullOrEmpty(path))return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            TestRunnerApi.SaveResultToFile(result,path);
            Debug.Log("Verificación de interacción y DEV: "+result.ResultState+"; passed="+result.PassCount+"; failed="+result.FailCount+"; "+path);
            SessionState.EraseString(Pending);
        }
    }
}
