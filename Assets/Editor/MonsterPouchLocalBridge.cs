using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

// Local, file-based editor commands. Never starts another editor or uses batch mode.
[InitializeOnLoad]
public static class MonsterPouchLocalBridge
{
    [Serializable] public class Command { public string id; public string action; public string type; public string method; public string argument; }
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string Queue => Path.Combine(Root, "Library", "MonsterPouchCommand.json");
    const string CompileState = "MonsterPouchLocalBridge.Compile.";
    static double nextPoll;
    static MonsterPouchLocalBridge()
    {
        EditorApplication.update += Poll;
        CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
        CompilationPipeline.compilationFinished += OnCompilationFinished;
        AssemblyReloadEvents.afterAssemblyReload += OnAssembliesReloaded;
    }

    static bool HasPendingCompilation => !string.IsNullOrEmpty(SessionState.GetString(CompileState + "id", ""));

    static void OnAssemblyCompiled(string assembly, CompilerMessage[] messages)
    {
        if (!HasPendingCompilation) return;
        int errors = messages.Count(message => message.type == CompilerMessageType.Error);
        SessionState.SetInt(CompileState + "errors", SessionState.GetInt(CompileState + "errors", 0) + errors);
    }

    static void OnCompilationFinished(object context)
    {
        if (HasPendingCompilation) SessionState.SetBool(CompileState + "finished", true);
    }

    static void OnAssembliesReloaded()
    {
        if (HasPendingCompilation) SessionState.SetBool(CompileState + "reloaded", true);
    }

    static void ClearPendingCompilation()
    {
        SessionState.EraseString(CompileState + "id");
        SessionState.EraseInt(CompileState + "errors");
        SessionState.EraseBool(CompileState + "finished");
        SessionState.EraseBool(CompileState + "reloaded");
        SessionState.EraseFloat(CompileState + "deadline");
    }

    static bool FinishPendingCompilation()
    {
        string id = SessionState.GetString(CompileState + "id", "");
        if (string.IsNullOrEmpty(id)) return false;
        int errors = SessionState.GetInt(CompileState + "errors", 0);
        bool finished = SessionState.GetBool(CompileState + "finished", false);
        bool reloaded = SessionState.GetBool(CompileState + "reloaded", false);
        if (finished && (errors > 0 || EditorUtility.scriptCompilationFailed))
            Reply(id, "ERROR compilation failed; errors=" + Math.Max(errors, 1) + "; reloaded=" + reloaded);
        else if (finished && reloaded)
            Reply(id, "compiled; errors=0; reloaded=True");
        else if (EditorApplication.timeSinceStartup > SessionState.GetFloat(CompileState + "deadline", float.MaxValue))
            Reply(id, "ERROR timed out waiting for compilation and assembly reload; finished=" + finished + "; reloaded=" + reloaded);
        else
            return true;
        ClearPendingCompilation();
        return true;
    }

    static void RequestCompilation(string id)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode before compiling.");
        if (string.IsNullOrEmpty(id)) throw new Exception("A command id is required for compilation.");
        SessionState.SetString(CompileState + "id", id);
        SessionState.SetInt(CompileState + "errors", 0);
        SessionState.SetBool(CompileState + "finished", false);
        SessionState.SetBool(CompileState + "reloaded", false);
        SessionState.SetFloat(CompileState + "deadline", (float)(EditorApplication.timeSinceStartup + 300));
        try
        {
            AssetDatabase.Refresh();
            // Force a real compilation so an unchanged project also produces a reload handshake.
            CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
        }
        catch
        {
            ClearPendingCompilation();
            throw;
        }
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + .5;
        // Complete on a later idle update, never within compilation or reload callbacks.
        if (FinishPendingCompilation()) return;
        if (!File.Exists(Queue)) return;
        Command c = null;
        try
        {
            c = JsonUtility.FromJson<Command>(File.ReadAllText(Queue));
            File.Delete(Queue);
            switch (c.action)
            {
                case "status": Reply(c.id, "ready; playing=" + EditorApplication.isPlaying + "; compilationFailed=" + EditorUtility.scriptCompilationFailed + "; scene=" + EditorSceneManager.GetActiveScene().path); break;
                case "refresh": AssetDatabase.Refresh(); Reply(c.id, "refreshed"); break;
                case "compile": RequestCompilation(c.id); break;
                case "play": EditorApplication.isPlaying = true; Reply(c.id, "play requested"); break;
                case "stop": EditorApplication.isPlaying = false; Reply(c.id, "stop requested"); break;
                case "open": EditorSceneManager.OpenScene(c.argument); Reply(c.id, "opened"); break;
                case "invoke":
                    var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(c.type)).FirstOrDefault(t => t != null);
                    if (type == null) throw new Exception("Type unavailable: " + c.type);
                    var method = type.GetMethod(c.method, BindingFlags.Public | BindingFlags.Static);
                    if (method == null) throw new Exception("Method unavailable: " + c.method);
                    var result = method.Invoke(null, method.GetParameters().Length == 0 ? null : new object[] { c.argument });
                    Reply(c.id, result == null ? "completed" : result.ToString()); break;
                case "build":
                    if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode before building.");
                    if (EditorUtility.scriptCompilationFailed) throw new Exception("Resolve script compilation errors before building.");
                    string outputFolder = c.argument == "coins" ? "Windows-Coins" : c.argument == "targeting" ? "Windows-Targeting" : c.argument == "tauris" ? "Windows-Tauris" : c.argument == "characters" ? "Windows-Characters" : c.argument == "roster" ? "Windows-Roster" : c.argument == "brief" ? "Windows-Brief" : "Windows";
                    string output = Path.Combine(Root, "Builds", outputFolder, "Monster Pouch.exe");
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/main-scene.unity" }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
                    Reply(c.id, report.summary.result + "; errors=" + report.summary.totalErrors + "; bytes=" + report.summary.totalSize + "; path=" + output); break;
                default: throw new Exception("Unknown local editor command.");
            }
        }
        catch (Exception ex) { Reply(c == null ? "unknown" : c.id, "ERROR " + ex); Debug.LogException(ex); }
    }
    public static void Reply(string id, string text)
    {
        string dir = Path.Combine(Root, "Logs", "local-validation"); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, id + ".txt"), text);
    }
}
