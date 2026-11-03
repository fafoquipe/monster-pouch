using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MonsterPouch.Local;
using NUnit.Framework;

public sealed class DuelLobbyRequestTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static UnityDuelDirectory Directory() => (UnityDuelDirectory)Activator.CreateInstance(typeof(UnityDuelDirectory), new object[] { null });
    static Exception RateLimited()
    {
        Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
        return (Exception)Activator.CreateInstance(Find("Unity.Services.Lobbies.LobbyServiceException"),
            Enum.Parse(Find("Unity.Services.Lobbies.LobbyExceptionReason"), "RateLimited"), "Rejected by service");
    }
    static Task<int> Request(UnityDuelDirectory directory, Func<Task<int>> operation, CancellationToken token = default) =>
        (Task<int>)typeof(UnityDuelDirectory).GetMethods(Hidden).Single(m => m.Name == "Service" && m.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(int)).Invoke(directory, new object[] { "QueryLobbiesAsync", operation, token });

    [Test] public async Task Rejected429RetriesAndReturnsTheSuccessfulResponse()
    {
        int attempts = 0;
        int result = await Request(Directory(), () => ++attempts == 1 ? Task.FromException<int>(RateLimited()) : Task.FromResult(42));
        Assert.AreEqual(42, result); Assert.AreEqual(2, attempts);
    }
    [Test] public async Task AmbiguousTimeoutIsNeverReplayed()
    {
        int attempts = 0;
        Exception failure = null;
        try { await Request(Directory(), () => { attempts++; return Task.FromException<int>(new TimeoutException()); }); }
        catch (Exception error) { failure = error; }
        Assert.IsInstanceOf<TimeoutException>(failure);
        Assert.AreEqual(1, attempts);
    }
    [Test] public async Task CancellationDuringThrottleStopsBeforeAnotherRequest()
    {
        int attempts = 0;
        Exception failure = null;
        using (var stop = new CancellationTokenSource())
        {
            try { await Request(Directory(), () => { attempts++; stop.Cancel(); return Task.FromException<int>(RateLimited()); }, stop.Token); }
            catch (Exception error) { failure = error; }
        }
        Assert.IsInstanceOf<OperationCanceledException>(failure);
        Assert.AreEqual(1, attempts);
    }
    [Test] public async Task LateSuccessIsReturnedSoTheLeaseCanBeCleaned()
    {
        using (var stop = new CancellationTokenSource())
            Assert.AreEqual(42, await Request(Directory(), () => { stop.Cancel(); return Task.FromResult(42); }, stop.Token));
    }
    [Test] public async Task PacingStartsAfterTheResponseEvenWhenItWasSlow()
    {
        var directory = Directory();
        DateTime responseAt = default;
        await Request(directory, async () => { await Task.Delay(150); responseAt = DateTime.UtcNow; return 42; });
        var next = (Dictionary<string, DateTime>)typeof(UnityDuelDirectory).GetField("nextRequest", Hidden).GetValue(directory);
        Assert.GreaterOrEqual((next["QueryLobbiesAsync"] - responseAt).TotalMilliseconds, 2990);
    }
    [Test] public async Task PersistentThrottleIsBoundedToFourAttempts()
    {
        int attempts = 0;
        Exception failure = null;
        try { await Request(Directory(), () => { attempts++; return Task.FromException<int>(RateLimited()); }); }
        catch (Exception error) { failure = error; }
        Assert.IsNotNull(failure);
        Assert.AreEqual(4, attempts);
    }
    [Test] public async Task OverlappingDiscoveryCallsRunOneAtATimeAtThreeSecondCadence()
    {
        var directory = Directory();
        var firstResponse = new TaskCompletionSource<int>();
        int started = 0; DateTime secondStarted = default;
        var first = Request(directory, () => { started++; return firstResponse.Task; });
        var second = Request(directory, () => { started++; secondStarted = DateTime.UtcNow; return Task.FromResult(2); });
        await Task.Delay(100); Assert.AreEqual(1, started);
        DateTime firstFinished = DateTime.UtcNow; firstResponse.SetResult(1);
        Assert.AreEqual(1, await first); Assert.AreEqual(2, await second);
        Assert.GreaterOrEqual((secondStarted - firstFinished).TotalMilliseconds, 2990);
    }
}
