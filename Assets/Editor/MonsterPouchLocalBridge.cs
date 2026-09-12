using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Local, file-based editor commands. Never starts another editor or uses batch mode.
[InitializeOnLoad]
public static class MonsterPouchLocalBridge
{
    [Serializable] public class Command { public string id; public string action; public string type; public string method; public string argument; }
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string Queue => Path.Combine(Root, "Library", "MonsterPouchCommand.json");
    static double nextPoll;
    static MonsterPouchLocalBridge() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextPoll = EditorApplication.timeSinceStartup + .5;
        if (!File.Exists(Queue)) return;
        Command c = null;
        try
        {
            c = JsonUtility.FromJson<Command>(File.ReadAllText(Queue));
            File.Delete(Queue);
            switch (c.action)
            {
                case "status": Reply(c.id, "ready; playing=" + EditorApplication.isPlaying + "; scene=" + EditorSceneManager.GetActiveScene().path); break;
                case "refresh": AssetDatabase.Refresh(); Reply(c.id, "refreshed"); break;
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
                    if (EditorApplication.isPlaying) throw new Exception("Exit Play Mode before building.");
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
