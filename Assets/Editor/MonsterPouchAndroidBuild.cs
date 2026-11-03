using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// The open-editor path survives the assembly reload required to switch platforms.
[InitializeOnLoad]
public static class MonsterPouchAndroidBuild
{
    const string PendingKey="MonsterPouch.AndroidBuild.Pending";
    const string PhaseKey="MonsterPouch.AndroidBuild.Phase";
    const string TargetKey="MonsterPouch.AndroidBuild.OriginalTarget";
    const string ReadyKey="MonsterPouch.AndroidBuild.ReadyAfter";
    const string Scene="Assets/Scenes/main-scene.unity";
    static bool busy;
    static string Root=>Path.GetFullPath(Path.Combine(Application.dataPath,".."));

    [Serializable] public sealed class Request
    {
        public string outputDirectory;
        public string applicationIdentifier;
        public string architectures="Auto";
        public bool development=true;
        public bool unlockAllContent;
    }
    [Serializable] public sealed class Result
    {
        public string status="Pending",startedUtc,completedUtc,unityVersion,applicationIdentifier;
        public string apkPath,sha256,architectures,backend="IL2CPP",orientation="Portrait",signing="Android debug key";
        public string sdk,ndk,jdk,error,originalBuildTarget;
        public bool development=true,settingsRestored,platformRestored;
        public bool unlockAllContent;
        public string savedLocalTime,timeZone;
        public long bytes;
        public double seconds;
        public string[] warnings;
    }

    static MonsterPouchAndroidBuild(){EditorApplication.update+=ContinueEditorBuild;}

    [MenuItem("Monster Pouch/Build/Android APK")]
    public static void BuildReleaseFromMenu()=>RequestFromJson("{\"development\":false}");

    [MenuItem("Monster Pouch/Build/Android APK",true)]
    static bool CanBuildRelease()=>CanBuild();

    [MenuItem("Monster Pouch/Build/Android APK (desarrollo)")]
    public static void BuildFromMenu()=>RequestFromJson("{}");

    [MenuItem(MonsterPouchAndroidDeveloperVariant.MenuPath)]
    public static void BuildDeveloperFromMenu()=>RequestFromJson("{\"development\":true,\"unlockAllContent\":true}");

    [MenuItem(MonsterPouchAndroidDeveloperVariant.MenuPath,true)]
    static bool CanBuildDeveloper()=>CanBuild();

    [MenuItem("Monster Pouch/Build/Android APK (desarrollo)",true)]
    static bool CanBuild()=>!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!TestsRunning&&
        !BuildPipeline.isBuildingPlayer&&string.IsNullOrEmpty(SessionState.GetString(PendingKey,""));

    static bool TestsRunning=>(bool)typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi)
        .GetMethod("IsRunActive",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);

    // Callable through MonsterPouchLocalBridge without opening a second editor.
    public static string RequestFromJson(string json)
    {
        EnsureIdle();
        if(!string.IsNullOrEmpty(SessionState.GetString(PendingKey,"")))throw new InvalidOperationException("Ya hay una compilación Android pendiente.");
        var request=Normalize(JsonUtility.FromJson<Request>(string.IsNullOrEmpty(json)?"{}":json));
        ValidateModule();
        Directory.CreateDirectory(request.outputDirectory);
        SessionState.SetInt(TargetKey,(int)EditorUserBuildSettings.activeBuildTarget);
        SessionState.SetString(PendingKey,JsonUtility.ToJson(request));
        SessionState.SetString(PhaseKey,"switch");
        return "Compilación Android encolada: "+request.outputDirectory;
    }

    // -buildTarget Android must be supplied before Unity loads/compiles the project.
    public static void BuildBatch()
    {
        Request request=null;Result result=null;
        try
        {
            string[] args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-monsterPouchBuildRequest");
            if(index<0||index+1>=args.Length)throw new ArgumentException("Falta -monsterPouchBuildRequest <archivo.json>.");
            request=Normalize(JsonUtility.FromJson<Request>(File.ReadAllText(args[index+1])));
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)throw new InvalidOperationException("Inicia Unity con -buildTarget Android.");
            EnsureIdle();result=RunBuild(request);result.platformRestored=true;Complete(request,result);
        }
        catch(Exception ex)
        {
            Debug.LogException(ex);
            if(request!=null){result=result??NewResult();result.status="Failed";result.error=ex.ToString();Complete(request,result);}
        }
        EditorApplication.Exit(result!=null&&result.status=="Succeeded"?0:1);
    }

    static void ContinueEditorBuild()
    {
        if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer||TestsRunning)return;
        string json=SessionState.GetString(PendingKey,"");if(string.IsNullOrEmpty(json))return;
        var request=JsonUtility.FromJson<Request>(json);
        var original=(BuildTarget)SessionState.GetInt(TargetKey,(int)BuildTarget.StandaloneWindows64);
        try
        {
            string phase=SessionState.GetString(PhaseKey,"switch");
            if(phase=="switch")
            {
                EnsureIdle();SessionState.SetString(PhaseKey,"build");
                SessionState.SetFloat(ReadyKey,(float)EditorApplication.timeSinceStartup+2);
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android&&
                    !EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android,BuildTarget.Android))
                    throw new InvalidOperationException("Unity no pudo activar Android.");
                return;
            }
            if(EditorApplication.timeSinceStartup<SessionState.GetFloat(ReadyKey,0))return;
            if(phase=="build")
            {
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)return;
                EnsureIdle();busy=true;Result result;
                try{result=RunBuild(request);}finally{busy=false;}
                result.originalBuildTarget=original.ToString();WriteResult(request,result);
                SessionState.SetString(PhaseKey,"restore");
                SessionState.SetFloat(ReadyKey,(float)EditorApplication.timeSinceStartup+2);
                if(original!=BuildTarget.Android&&
                    !EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildPipeline.GetBuildTargetGroup(original),original))
                    throw new InvalidOperationException("El APK terminó, pero Unity no pudo restaurar la plataforma "+original+".");
                return;
            }
            if(phase=="restore"&&EditorUserBuildSettings.activeBuildTarget==original)
            {
                var result=JsonUtility.FromJson<Result>(File.ReadAllText(ReportPath(request)));
                result.platformRestored=true;ClearPending();Complete(request,result);
            }
        }
        catch(Exception ex)
        {
            var result=File.Exists(ReportPath(request))?JsonUtility.FromJson<Result>(File.ReadAllText(ReportPath(request))):NewResult();
            result.status="Failed";result.error=ex.ToString();result.originalBuildTarget=original.ToString();
            WriteResult(request,result);Debug.LogException(ex);
            // Switching platforms can itself expose platform-specific compile errors.
            // Restore the prior target even if BuildPlayer was never reached.
            bool alreadyRestoring=SessionState.GetString(PhaseKey,"")=="restore";
            if(!alreadyRestoring&&EditorUserBuildSettings.activeBuildTarget!=original)
            {
                SessionState.SetString(PhaseKey,"restore");
                SessionState.SetFloat(ReadyKey,(float)EditorApplication.timeSinceStartup+2);
                if(EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildPipeline.GetBuildTargetGroup(original),original))return;
            }
            result.platformRestored=EditorUserBuildSettings.activeBuildTarget==original;
            ClearPending();Complete(request,result);
        }
    }

    static Result RunBuild(Request request)
    {
        var result=NewResult();result.applicationIdentifier=request.applicationIdentifier;result.development=request.development;
        result.unlockAllContent=request.unlockAllContent;
        if(request.unlockAllContent)result.timeZone=MonsterPouchAndroidDeveloperVariant.TimeZone;
        result.apkPath=Path.Combine(request.outputDirectory,"MonsterPouch_"+DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")+".apk");
        Directory.CreateDirectory(request.outputDirectory);
        var log=new StringBuilder();
        Application.LogCallback capture=(message,stack,type)=>{log.AppendLine("["+type+"] "+message);if(type==LogType.Exception||type==LogType.Error)log.AppendLine(stack);};
        Application.logMessageReceived+=capture;
        SettingsSnapshot saved=null; IDisposable developerIcon=null;
        string previousJavaOptions=Environment.GetEnvironmentVariable("JAVA_TOOL_OPTIONS");
        try
        {
            // Recheck after a platform reload, before Unity can write an APK.
            if(File.Exists(result.apkPath)||Directory.GetFiles(request.outputDirectory,"*.apk").Length>0)
                throw new IOException("La carpeta ya contiene un APK; se conserva el resultado existente.");
            ValidateModule();ValidateTools(result);
            if(Application.platform==RuntimePlatform.WindowsEditor)
            {
                // Windows can remap the default TEMP socket path. Give both Java and
                // its Gradle daemon the same explicit directory for this build only.
                string sockets=Path.Combine(Root,"Library","mp-sockets").Replace('\\','/');
                Directory.CreateDirectory(sockets);
                Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS",(previousJavaOptions??"")+" -Djdk.net.unixdomain.tmpdir=\""+sockets+"\"");
            }
            if(!File.Exists(Path.Combine(Root,Scene)))throw new FileNotFoundException("Falta la escena de inicio.",Scene);
            saved=new SettingsSnapshot();
            developerIcon=MonsterPouchDeveloperBuildIcon.UseForBuild(request.unlockAllContent);
            var architectures=SelectArchitectures(request.architectures);
            result.architectures=architectures.ToString();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,request.applicationIdentifier);
            if(request.unlockAllContent)PlayerSettings.productName=saved.ProductName+" DEV";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=architectures;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait=true;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.allowedAutorotateToLandscapeLeft=false;PlayerSettings.allowedAutorotateToLandscapeRight=false;
            if(request.unlockAllContent)PlayerSettings.Android.useCustomKeystore=false;
            result.signing=PlayerSettings.Android.useCustomKeystore?"Configured custom keystore":"Android debug key";
            PlayerSettings.Android.buildApkPerCpuArchitecture=false;
            EditorUserBuildSettings.buildAppBundle=false;EditorUserBuildSettings.exportAsGoogleAndroidProject=false;
            var report=BuildPipeline.BuildPlayer(MonsterPouchAndroidDeveloperVariant.Options(
                Scene,result.apkPath,request.development,request.unlockAllContent));
            result.seconds=report.summary.totalTime.TotalSeconds;
            result.warnings=report.steps.SelectMany(s=>s.messages).Where(m=>m.type==LogType.Warning).Select(m=>m.content).Distinct().ToArray();
            if(report.summary.result!=BuildResult.Succeeded||!File.Exists(result.apkPath))
                throw new InvalidOperationException("Unity: "+report.summary.result+"; errores="+report.summary.totalErrors+". Consulta build.log.");
            result.bytes=new FileInfo(result.apkPath).Length;
            if(result.bytes==0)throw new InvalidDataException("Unity produjo un APK vacío.");
            if(request.unlockAllContent)
            {
                // Stamp the actual save time, after compilation, not the time the button was clicked.
                result.savedLocalTime=MonsterPouchAndroidDeveloperVariant.Timestamp(DateTimeOffset.UtcNow);
                string savedApk=Path.Combine(request.outputDirectory,"MonsterPouch_DEV_"+result.savedLocalTime+".apk");
                File.Move(result.apkPath,savedApk); // Never overwrite an existing file.
                result.apkPath=savedApk;
            }
            using(var hash=SHA256.Create())using(var file=File.OpenRead(result.apkPath))
                result.sha256=BitConverter.ToString(hash.ComputeHash(file)).Replace("-","").ToLowerInvariant();
            result.status="Succeeded";
        }
        catch(Exception ex){result.status="Failed";result.error=ex.ToString();Debug.LogException(ex);}
        finally
        {
            Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS",previousJavaOptions);
            try{try{developerIcon?.Dispose();}finally{if(saved!=null)saved.Restore();}result.settingsRestored=true;}
            catch(Exception ex){result.status="Failed";result.error+="\nRestauración de ajustes: "+ex;}
            Application.logMessageReceived-=capture;
            // Unity may remove the target directory while cleaning a failed
            // Gradle/IL2CPP build. Recreate it before persisting diagnostics.
            Directory.CreateDirectory(request.outputDirectory);
            File.WriteAllText(Path.Combine(request.outputDirectory,"build.log"),log.ToString(),Encoding.UTF8);
            WriteResult(request,result);
        }
        return result;
    }

    static Request Normalize(Request request)
    {
        request=request??new Request();
        if(string.IsNullOrWhiteSpace(request.applicationIdentifier))request.applicationIdentifier=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        if(request.development)request.unlockAllContent=true;
        if(request.unlockAllContent)
        {
            if(!request.development)throw new ArgumentException("Los desbloqueos requieren una Developer Build.");
            request.applicationIdentifier=MonsterPouchAndroidDeveloperVariant.ApplicationIdentifier(request.applicationIdentifier);
            // The dev variant always has its own fresh directory, including callers of the JSON bridge.
            request.outputDirectory=MonsterPouchAndroidDeveloperVariant.OutputDirectory(Root,DateTimeOffset.UtcNow,Guid.NewGuid().ToString("N"));
        }
        if(!Regex.IsMatch(request.applicationIdentifier,@"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$"))
            throw new ArgumentException("Identificador Android inválido: "+request.applicationIdentifier);
        if(string.IsNullOrEmpty(request.architectures))request.architectures="Auto";
        if(string.IsNullOrEmpty(request.outputDirectory))request.outputDirectory=Path.Combine(Root,"Builds","Android",DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
        request.outputDirectory=Path.GetFullPath(request.outputDirectory);
        if(File.Exists(Path.Combine(request.outputDirectory,"build-report.json"))||Directory.Exists(request.outputDirectory)&&Directory.GetFiles(request.outputDirectory,"*.apk").Length>0)
            throw new IOException("La carpeta ya contiene un resultado; elige una carpeta nueva para conservarlo.");
        return request;
    }
    static void EnsureIdle()
    {
        if(TestsRunning)throw new InvalidOperationException("Espera a que terminen las pruebas antes de compilar Android.");
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Sal de Play Mode antes de compilar Android.");
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||BuildPipeline.isBuildingPlayer)throw new InvalidOperationException("Unity todavía está ocupado.");
        if(EditorUtility.scriptCompilationFailed)throw new InvalidOperationException("Corrige los errores de compilación C# antes de crear el APK.");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Guarda las escenas modificadas antes de compilar; se compilan los archivos guardados.");
    }
    static string AndroidRoot=>Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines","AndroidPlayer");
    static void ValidateModule()
    {
        if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))
            throw new InvalidOperationException("Instala Android Build Support, Android SDK & NDK Tools y OpenJDK para Unity "+Application.unityVersion+" desde Unity Hub.");
        if(!Directory.Exists(Path.Combine(AndroidRoot,"Variations","il2cpp")))throw new DirectoryNotFoundException("Falta IL2CPP para Android en esta instalación de Unity.");
    }
    static AndroidArchitecture SelectArchitectures(string requested)
    {
        string libs=Path.Combine(AndroidRoot,"Variations","il2cpp","Development","Libs");
        bool arm64=Directory.Exists(Path.Combine(libs,"arm64-v8a")),armv7=Directory.Exists(Path.Combine(libs,"armeabi-v7a"));
        if(!arm64)throw new DirectoryNotFoundException("Faltan las bibliotecas Android IL2CPP ARM64.");
        if(requested.Equals("ARM64",StringComparison.OrdinalIgnoreCase))return AndroidArchitecture.ARM64;
        if(!requested.Equals("Auto",StringComparison.OrdinalIgnoreCase)&&!requested.Equals("Universal",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Arquitecturas: Auto, ARM64 o Universal.");
        if(requested.Equals("Universal",StringComparison.OrdinalIgnoreCase)&&!armv7)throw new DirectoryNotFoundException("Esta instalación no incluye ARMv7; usa ARM64.");
        return armv7?AndroidArchitecture.ARM64|AndroidArchitecture.ARMv7:AndroidArchitecture.ARM64;
    }
    static string ToolPath(string property,string bundled)
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEditor.Android.AndroidExternalToolsSettings")).FirstOrDefault(t=>t!=null);
        string configured=type?.GetProperty(property,BindingFlags.Public|BindingFlags.Static)?.GetValue(null) as string;
        return string.IsNullOrEmpty(configured)?Path.Combine(AndroidRoot,bundled):configured;
    }
    static void ValidateTools(Result result)
    {
        result.sdk=ToolPath("sdkRootPath","SDK");result.ndk=ToolPath("ndkRootPath","NDK");result.jdk=ToolPath("jdkRootPath","OpenJDK");
        string[] files={Path.Combine(result.sdk,"platform-tools","adb.exe"),Path.Combine(result.ndk,"source.properties"),Path.Combine(result.jdk,"bin","java.exe")};
        foreach(string path in files)if(!File.Exists(path))throw new FileNotFoundException("Herramienta Android incompleta. Revisa Unity > Preferences > External Tools.",path);
        if(!Directory.Exists(Path.Combine(result.sdk,"platforms"))||!Directory.GetDirectories(Path.Combine(result.sdk,"platforms"),"android-*").Any())throw new DirectoryNotFoundException("El SDK no tiene plataformas Android instaladas.");
    }
    static Result NewResult()=>new Result {startedUtc=DateTime.UtcNow.ToString("O"),unityVersion=Application.unityVersion};
    static string ReportPath(Request request)=>Path.Combine(request.outputDirectory,"build-report.json");
    static void WriteResult(Request request,Result result){Directory.CreateDirectory(request.outputDirectory);File.WriteAllText(ReportPath(request),JsonUtility.ToJson(result,true),Encoding.UTF8);}
    static void Complete(Request request,Result result)
    {
        result.completedUtc=DateTime.UtcNow.ToString("O");
        if(result.status=="Succeeded")
        {
            File.WriteAllText(Path.Combine(request.outputDirectory,"SHA256.txt"),result.sha256+"  "+Path.GetFileName(result.apkPath)+"\n",Encoding.UTF8);
            Debug.Log("APK Android listo: "+result.apkPath);
            // Both normal and DEV menu builds need a visible completion result.
            // Previously the normal APK was only announced in the Console.
            if(!Application.isBatchMode)
            {
                string title=request.unlockAllContent?"Android Developer Build lista":"APK Android listo";
                if(EditorUtility.DisplayDialog(title,result.apkPath,"Mostrar carpeta","Cerrar"))
                    EditorUtility.RevealInFinder(result.apkPath);
            }
        }
        else
        {
            Debug.LogError("No se pudo crear el APK. Informe: "+ReportPath(request));
            if(!Application.isBatchMode&&EditorUtility.DisplayDialog("No se pudo crear el APK",
                "Consulta el informe y build.log en:\n"+request.outputDirectory,"Mostrar carpeta","Cerrar"))
                EditorUtility.RevealInFinder(request.outputDirectory);
        }
        // Completion is published last, after settings and platform restoration finish.
        WriteResult(request,result);
    }
    static void ClearPending(){SessionState.EraseString(PendingKey);SessionState.EraseString(PhaseKey);SessionState.EraseInt(TargetKey);SessionState.EraseFloat(ReadyKey);}

    sealed class SettingsSnapshot
    {
        public readonly string ProductName=PlayerSettings.productName;
        readonly string id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        readonly ScriptingImplementation backend=PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
        readonly AndroidArchitecture architectures=PlayerSettings.Android.targetArchitectures;
        readonly UIOrientation orientation=PlayerSettings.defaultInterfaceOrientation;
        readonly bool portrait=PlayerSettings.allowedAutorotateToPortrait,upside=PlayerSettings.allowedAutorotateToPortraitUpsideDown,
            left=PlayerSettings.allowedAutorotateToLandscapeLeft,right=PlayerSettings.allowedAutorotateToLandscapeRight,
            customKey=PlayerSettings.Android.useCustomKeystore,split=PlayerSettings.Android.buildApkPerCpuArchitecture,
            bundle=EditorUserBuildSettings.buildAppBundle,export=EditorUserBuildSettings.exportAsGoogleAndroidProject;
        public void Restore()
        {
            PlayerSettings.productName=ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,id);PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,backend);
            PlayerSettings.Android.targetArchitectures=architectures;PlayerSettings.defaultInterfaceOrientation=orientation;
            PlayerSettings.allowedAutorotateToPortrait=portrait;PlayerSettings.allowedAutorotateToPortraitUpsideDown=upside;
            PlayerSettings.allowedAutorotateToLandscapeLeft=left;PlayerSettings.allowedAutorotateToLandscapeRight=right;
            PlayerSettings.Android.useCustomKeystore=customKey;PlayerSettings.Android.buildApkPerCpuArchitecture=split;
            EditorUserBuildSettings.buildAppBundle=bundle;EditorUserBuildSettings.exportAsGoogleAndroidProject=export;
        }
    }
}
