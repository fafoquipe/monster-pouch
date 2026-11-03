using System;
using System.Reflection;
using System.Linq;
using MonsterPouch.Local;
using NUnit.Framework;

public sealed class DuelDiagnosticTests
{
    static Type ServiceType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    static string Describe(Exception exception, string phase) => (string)typeof(OnlineRoom)
        .GetMethod("SafeDuelFailure", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { exception, phase });

    [Test] public void ServiceFailureReportsCodeAndOperationWithoutRequestSecrets()
    {
        var error = (Exception)Activator.CreateInstance(ServiceType("Unity.Services.Lobbies.LobbyServiceException"),
            Enum.Parse(ServiceType("Unity.Services.Lobbies.LobbyExceptionReason"), "RateLimited"), "Bearer secret; ticket=private; relay=ABCDEF");
        error.Data["duel.phase"] = "lobby.QueryLobbiesAsync";
        string diagnostic = Describe(error, "cleanup");
        StringAssert.Contains("phase=lobby.QueryLobbiesAsync", diagnostic);
        StringAssert.Contains("reason=RateLimited", diagnostic);
        StringAssert.DoesNotContain("secret", diagnostic);
        StringAssert.DoesNotContain("private", diagnostic);
        StringAssert.DoesNotContain("ABCDEF", diagnostic);
    }
    [Test] public void AuthenticationFailureRetainsNumericCodeWithoutMessage()
    {
        var error = (Exception)Activator.CreateInstance(ServiceType("Unity.Services.Core.RequestFailedException"), 10000, "private service payload");
        string diagnostic = Describe(error, "authentication.sign-in");
        Assert.AreEqual("phase=authentication.sign-in code=10000", diagnostic);
    }
    [Test] public void LocalFailureReportsTypeWithoutProfileData()
    {
        Assert.AreEqual("phase=profile.validate code=InvalidOperationException",
            Describe(new InvalidOperationException("private profile path"), "profile.validate"));
    }
}
