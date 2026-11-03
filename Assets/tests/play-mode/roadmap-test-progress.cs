using System;
using System.IO;
using System.Reflection;
using MonsterPouch.Local;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(RoadmapTestProgress))]

// Only test infrastructure: identify a stalled case without attaching to the editor.
public sealed class RoadmapTestProgress : ITestRunCallback
{
    static readonly PropertyInfo ProgressDirectory=typeof(LocalGameUI).GetProperty("ProgressionDirectoryOverride",BindingFlags.Static|BindingFlags.NonPublic);
    static readonly PropertyInfo DeckDirectory=typeof(LocalGameUI).GetProperty("DeckDirectoryOverride",BindingFlags.Static|BindingFlags.NonPublic);
    static readonly PropertyInfo GuestDirectory=typeof(LocalGameUI).GetProperty("GuestProfileDirectoryOverride",BindingFlags.Static|BindingFlags.NonPublic);
    string previousDirectory,previousDeckDirectory,previousGuestDirectory;
    static string PathForRun()
    {
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++)
            if(args[i].Equals("-logFile",StringComparison.OrdinalIgnoreCase))
                return System.IO.Path.GetFullPath(args[i+1])+".progress.txt";
        return null;
    }
    static void Write(string value)
    {
        if(!Application.isBatchMode)return;
        string path=PathForRun();
        if(path!=null)File.AppendAllText(path,DateTime.UtcNow.ToString("O")+" "+value+Environment.NewLine);
    }
    public void RunStarted(ITest test)
    {
        previousDirectory=(string)ProgressDirectory.GetValue(null);
        previousDeckDirectory=(string)DeckDirectory.GetValue(null);
        previousGuestDirectory=(string)GuestDirectory.GetValue(null);
        ProgressDirectory.SetValue(null,Path.Combine(Path.GetTempPath(),"MonsterPouchPlayProgress",Guid.NewGuid().ToString("N")));
        DeckDirectory.SetValue(null,Path.Combine(Path.GetTempPath(),"MonsterPouchPlayDecks",Guid.NewGuid().ToString("N")));
        GuestDirectory.SetValue(null,Path.Combine(Path.GetTempPath(),"MonsterPouchPlayGuests",Guid.NewGuid().ToString("N")));
        Write("RUN "+test.FullName);
    }
    public static void Step(string value)=>Write("STEP "+value+" frame="+Time.frameCount+" background="+Application.runInBackground);
    public void RunFinished(ITestResult result)
    {
        ProgressDirectory.SetValue(null,previousDirectory);
        DeckDirectory.SetValue(null,previousDeckDirectory);
        GuestDirectory.SetValue(null,previousGuestDirectory);
        Write("COMPLETE "+result.ResultState);
    }
    public void TestStarted(ITest test)
    {
        if(test.IsSuite)return;
        // Each case can import its own legacy choices; reloads within a case keep
        // the same directory. Neither batch nor interactive tests touch real decks.
        DeckDirectory.SetValue(null,Path.Combine(Path.GetTempPath(),"MonsterPouchPlayDecks",Guid.NewGuid().ToString("N")));
        GuestDirectory.SetValue(null,Path.Combine(Path.GetTempPath(),"MonsterPouchPlayGuests",Guid.NewGuid().ToString("N")));
        Write("START "+test.FullName);
    }
    public void TestFinished(ITestResult result)
    {
        if(!result.Test.IsSuite)Write("END "+result.ResultState+" "+result.Test.FullName+" "+result.Message);
    }
}
