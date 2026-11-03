using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using MonsterPouch.Local;
using System;

public static class MonsterPouchRoyalValidation
{
    const string PreviewPreference="MonsterPouch.BorderPreview.SavedStyle";
    public static string Preview(string style)
    {
        if(!EditorApplication.isPlaying)return "Enter Play Mode first.";
        if(!SessionState.GetBool(PreviewPreference+".active",false))
        {
            SessionState.SetBool(PreviewPreference+".active",true);
            SessionState.SetBool(PreviewPreference+".exists",PlayerPrefs.HasKey(CardTableTheme.PreferenceKey));
            SessionState.SetString(PreviewPreference,PlayerPrefs.GetString(CardTableTheme.PreferenceKey));
        }
        var ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        ui.Match.Menu();ui.SelectTableSkin((TableStyle)Enum.Parse(typeof(TableStyle),style));
        MonsterPouchLayoutPreview.Preparation();
        var arena=UnityEngine.Object.FindFirstObjectByType<CardTableArena>();
        return style+": grid="+arena.BoardPortraitBounds+"; frame="+arena.FramePortraitBounds;
    }
    public static string RestorePreview()
    {
        if(!SessionState.GetBool(PreviewPreference+".active",false))return "No preview preference to restore.";
        if(SessionState.GetBool(PreviewPreference+".exists",false))PlayerPrefs.SetString(CardTableTheme.PreferenceKey,SessionState.GetString(PreviewPreference,""));
        else PlayerPrefs.DeleteKey(CardTableTheme.PreferenceKey);
        PlayerPrefs.Save();SessionState.EraseBool(PreviewPreference+".active");
        var ui=UnityEngine.Object.FindFirstObjectByType<LocalGameUI>();
        if(ui!=null&&Application.isPlaying){ui.Match.Menu();ui.SelectTableSkin(CardTableTheme.Current);ui.CloseModal();ui.OpenHome();}
        return "Restored "+CardTableTheme.Current;
    }
    public static string Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||!string.IsNullOrEmpty(SessionState.GetString("MonsterPouch.Validation.PendingRun","")))return "Busy: current editor run was left untouched.";
        if(EditorUtility.scriptCompilationFailed)return "Compilation errors must be resolved before testing.";
        SessionState.SetString("MonsterPouch.Validation.PendingRun","royal-styles-play");
        var runner=ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.Execute(new ExecutionSettings(new Filter{testMode=TestMode.PlayMode,testNames=new[]{"RoyalStylePlayTests","TableSkinsPlayTests"}}));
        return "Campo Real and existing styles validation started; no build requested.";
    }
}
