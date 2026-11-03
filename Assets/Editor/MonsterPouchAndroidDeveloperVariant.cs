using System;
using System.Globalization;
using System.IO;
using MonsterPouch.Gameplay.Profiles;
using UnityEditor;

// Pure recipe helpers: tests may inspect the variant without enqueueing or running a build.
public static class MonsterPouchAndroidDeveloperVariant
{
    public const string MenuPath = "Monster Pouch/Build/Android Developer Build (todo desbloqueado)";
    public const string FolderName = "ANDROID DEVBUILD";
    public const string TimeZone = "America/La_Paz";

    public static string Timestamp(DateTimeOffset instant)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone); }
        catch (TimeZoneNotFoundException) { zone = TimeZoneInfo.FindSystemTimeZoneById("SA Western Standard Time"); }
        return TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyy-MM-dd_HH-mm-ss", CultureInfo.InvariantCulture);
    }

    public static string OutputDirectory(string projectRoot, DateTimeOffset instant, string uniqueId) =>
        Path.Combine(projectRoot, "Builds", FolderName, Timestamp(instant) + "_" + uniqueId);

    public static string ApplicationIdentifier(string normalIdentifier) =>
        normalIdentifier.EndsWith(AndroidDeveloperContent.IdentifierSuffix, StringComparison.Ordinal)
            ? normalIdentifier : normalIdentifier + AndroidDeveloperContent.IdentifierSuffix;

    public static BuildPlayerOptions Options(string scene, string apkPath, bool development, bool unlockAllContent)
    {
        if (unlockAllContent && !development)
            throw new ArgumentException("Los desbloqueos requieren una Developer Build.");
        return new BuildPlayerOptions {
            scenes = new[] { scene }, locationPathName = apkPath, target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = (development ? BuildOptions.Development : BuildOptions.None) | BuildOptions.DetailedBuildReport,
            extraScriptingDefines = development ? new[] { AndroidDeveloperContent.Define } : Array.Empty<string>()
        };
    }
}
