using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonsterPouch.Local;
using NUnit.Framework;

public sealed class AutomaticDuelContractTests
{
    static int passed;
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static DuelLobby Room(string id = "a", long score = 100, bool owned = false, int players = 1) => new DuelLobby {
        Id = id, HostId = owned ? "self" : "other", Trophies = score, Owned = owned, Players = players,
        Compatibility = "v17-balance", Created = new DateTime(2026, 10, 25).AddSeconds(id[0]), Ticket = "reservation", RelayCode = "ABCDEF"
    };
    static async Task ExpectCancel(Task work)
    { try { await work; throw new Exception("Expected cancellation"); } catch (OperationCanceledException) { } }

    sealed class Directory : IDuelDirectory
    {
        public List<DuelLobby> Candidates = new List<DuelLobby>();
        public Func<DuelLobby> Create = () => Room("z", owned: true, players: 2);
        public Func<DuelLobby, DuelLobby> Read = l => l;
        public Func<DuelLobby, DuelLobby> Lock = l => { l.Locked = true; return l; };
        public TaskCompletionSource<DuelLobby> PendingCreate, PendingJoin;
        public TaskCompletionSource<bool> PendingPublish;
        public Action OnCreate;
        public int Creates, Joins, Closes, Locks, Finds, Heartbeats;
        public bool RejectFirstJoin, FailClose;
        public readonly List<string> Closed = new List<string>();
        public Task RecoverAsync(CancellationToken t) { t.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public Task<IReadOnlyList<DuelLobby>> FindAsync(long score, string compatibility, DuelLobby own, CancellationToken t)
        { t.ThrowIfCancellationRequested(); Finds++; return Task.FromResult((IReadOnlyList<DuelLobby>)Candidates.ToArray()); }
        public Task<DuelLobby> CreateAsync(long score, string compatibility, CancellationToken t)
        { t.ThrowIfCancellationRequested(); Creates++; OnCreate?.Invoke(); return PendingCreate?.Task ?? Task.FromResult(Create()); }
        public Task<DuelLobby> JoinAsync(DuelLobby l, CancellationToken t)
        {
            t.ThrowIfCancellationRequested(); Joins++;
            if (RejectFirstJoin && Joins == 1) { Candidates.Clear(); throw new DuelUnavailableException(); }
            return PendingJoin?.Task ?? Task.FromResult(l);
        }
        public Task<DuelLobby> ReadAsync(DuelLobby l, CancellationToken t)
        { t.ThrowIfCancellationRequested(); return Task.FromResult(Read(l)); }
        public Task<DuelLobby> LockAsync(DuelLobby l, bool locked, CancellationToken t)
        { t.ThrowIfCancellationRequested(); Locks++; l.Locked = locked; return Task.FromResult(Lock(l)); }
        public Task PublishAsync(DuelLobby l, string code, CancellationToken t)
        { t.ThrowIfCancellationRequested(); return PendingPublish?.Task ?? Task.CompletedTask; }
        public Task HeartbeatAsync(DuelLobby l, CancellationToken t) { Heartbeats++; return Task.CompletedTask; }
        public Task CloseAsync(DuelLobby l)
        { Closes++; if (FailClose) throw new InvalidOperationException("offline"); Closed.Add(l.Id); return Task.CompletedTask; }
        public Task DelayAsync(int ms, CancellationToken t) => Task.Delay(ms >= 10000 ? 25 : 1, t);
    }
    sealed class Connection : IDuelConnection
    {
        public int Hosts, Joins, Stops, Waits;
        public bool FailOnce;
        public TaskCompletionSource<bool> PendingMatch;
        public Task<string> HostAsync(string ticket, CancellationToken t) { t.ThrowIfCancellationRequested(); Hosts++; return Task.FromResult("ABCDEF"); }
        public Task JoinAsync(string code, string ticket, CancellationToken t) { t.ThrowIfCancellationRequested(); Joins++; return Task.CompletedTask; }
        public async Task WaitForMatchAsync(CancellationToken t)
        {
            t.ThrowIfCancellationRequested(); Waits++;
            if (FailOnce && Waits == 1) throw new DuelUnavailableException();
            if (PendingMatch != null) await PendingMatch.Task;
            t.ThrowIfCancellationRequested();
        }
        public void Stop() { Stops++; }
    }
    static Task Run(Directory d, Connection c, CancellationToken t = default) => new AutomaticDuelSearch(d, c).RunAsync(105, "v17-balance", t);

    [TestCase(0, "nearest on either side, no trophy bands or board filter")]
    [TestCase(1, "Int64 trophies retain precision and cannot overflow")]
    [TestCase(2, "filter incompatible, full, locked, own and duplicate candidates")]
    [TestCase(3, "deterministic older rule prevents symmetric host migration")]
    [TestCase(4, "client reserves before Relay and removes membership on match")]
    [TestCase(5, "empty queue creates once and automatically starts reserved host")]
    [TestCase(6, "full or expired candidate re-queries instead of getting stuck")]
    [TestCase(7, "cancel before async creates no resources")]
    [TestCase(8, "cancel during create cleans the late successful response")]
    [TestCase(9, "cancel during atomic join releases the late reservation")]
    [TestCase(10, "cancel during relay publication cannot start a match")]
    [TestCase(11, "cancel during handshake then re-enter uses fresh resources")]
    [TestCase(12, "incompatible response after reservation is released and retried")]
    [TestCase(13, "reservation disconnect retries without reusing the old room")]
    [TestCase(14, "join that wins migration lock is honored without leaving host")]
    [TestCase(15, "simultaneous empty-room hosts converge by migrating once")]
    [TestCase(16, "cleanup failure prevents creating another room blindly")]
    [TestCase(17, "waiting stays cancellable and heartbeats without duplicate rooms")]
    [TestCase(18, "two simultaneous players with an empty shared directory")]
    [TestCase(19, "three simultaneous players cannot overbook a reservation")]
    [TestCase(20, "four simultaneous players make exactly two matches")]
    public async Task ApprovedMatchmakingContracts(int index, string contract)
    {
        var operation=Cases()[index]();
        Assert.AreSame(operation,await Task.WhenAny(operation,Task.Delay(10000)),"Timed out: "+contract);
        await operation;
    }
    static Func<Task>[] Cases() => new Func<Task>[] {
        () => {
                var rooms = new[] { Room("a", 2000), Room("b", 99), Room("c", 104), Room("d", 50000) };
                Check(DuelCandidateOrder.Rank(rooms, 105, "v17-balance")[0].Id == "c", "nearest");
                Check(DuelCandidateOrder.Rank(new[] { rooms[3] }, 0, "v17-balance").Count == 1, "distant opponent excluded");
                return Task.CompletedTask;
            },
        () => {
                Check(DuelCandidateOrder.Rank(new[] { Room("a", 0), Room("b", long.MaxValue - 1) }, long.MaxValue, "v17-balance")[0].Id == "b", "overflow");
                return Task.CompletedTask;
            },
        () => {
                var wrong = Room("w"); wrong.Compatibility = "v16";
                var locked = Room("l"); locked.Locked = true;
                var valid = Room("v");
                Check(DuelCandidateOrder.Rank(new[] { wrong, locked, Room("o", owned: true), Room("f", players: 2), valid, valid }, 105, "v17-balance").Count == 1, "bad candidate included");
                return Task.CompletedTask;
            },
        () => {
                var a = Room("a"); var b = Room("b"); b.Created = a.Created;
                Check(DuelCandidateOrder.IsOlder(a, b) && !DuelCandidateOrder.IsOlder(b, a), "cycle"); return Task.CompletedTask;
            },
        async () => {
                var d = new Directory(); d.Candidates.Add(Room(players: 1));
                d.Read = l => { l.Players = 2; return l; };
                var c = new Connection(); await Run(d, c);
                Check(d.Joins == 1 && d.Creates == 0 && c.Joins == 1 && c.Hosts == 0 && d.Closes == 1 && c.Stops == 0, "client lifecycle");
            },
        async () => {
                var d = new Directory(); var c = new Connection(); await Run(d, c);
                Check(d.Creates == 1 && d.Locks == 1 && c.Hosts == 1 && d.Closes == 1, "host lifecycle");
            },
        async () => {
                var d = new Directory { RejectFirstJoin = true }; d.Candidates.Add(Room()); var c = new Connection();
                await Run(d, c); Check(d.Joins == 1 && d.Creates == 1 && c.Hosts == 1, "no recovery");
            },
        async () => {
                var d = new Directory(); var c = new Connection(); var cancel = new CancellationTokenSource(); cancel.Cancel();
                await ExpectCancel(Run(d, c, cancel.Token)); Check(d.Creates == 0 && d.Joins == 0 && c.Hosts == 0, "late mutation");
            },
        async () => {
                var d = new Directory { PendingCreate = new TaskCompletionSource<DuelLobby>() }; var c = new Connection(); var cancel = new CancellationTokenSource();
                var task = Run(d, c, cancel.Token); cancel.Cancel(); d.PendingCreate.SetResult(Room("z", owned: true)); await ExpectCancel(task);
                Check(d.Closes == 1 && c.Hosts == 0 && c.Joins == 0, "late create leaked");
            },
        async () => {
                var d = new Directory { PendingJoin = new TaskCompletionSource<DuelLobby>() }; d.Candidates.Add(Room());
                var c = new Connection(); var cancel = new CancellationTokenSource(); var task = Run(d, c, cancel.Token);
                cancel.Cancel(); d.PendingJoin.SetResult(Room(players: 2)); await ExpectCancel(task);
                Check(d.Closes == 1 && c.Joins == 0, "late join leaked");
            },
        async () => {
                var d = new Directory { PendingPublish = new TaskCompletionSource<bool>() }; var c = new Connection(); var cancel = new CancellationTokenSource();
                var task = Run(d, c, cancel.Token); cancel.Cancel(); d.PendingPublish.SetResult(true); await ExpectCancel(task);
                Check(d.Closes == 1 && c.Waits == 0 && c.Stops > 0, "started after cancel");
            },
        async () => {
                var d = new Directory(); var c = new Connection { PendingMatch = new TaskCompletionSource<bool>() }; var cancel = new CancellationTokenSource();
                var task = Run(d, c, cancel.Token); cancel.Cancel(); c.PendingMatch.SetResult(true); await ExpectCancel(task);
                c.PendingMatch = null; await Run(d, c);
                Check(d.Creates == 2 && d.Closes == 2 && c.Hosts == 2, "stale entry reused");
            },
        async () => {
                var d = new Directory { PendingJoin = new TaskCompletionSource<DuelLobby>() }; d.Candidates.Add(Room()); var c = new Connection();
                var task = Run(d, c); d.Candidates.Clear();
                var changed = Room(); changed.Compatibility = "v16"; d.PendingJoin.SetResult(changed); await task;
                Check(c.Joins == 0 && c.Hosts == 1 && d.Closes == 2, "incompatible response used");
            },
        async () => {
                var d = new Directory(); var c = new Connection { FailOnce = true }; await Run(d, c);
                Check(d.Creates == 2 && d.Closes == 2 && c.Stops == 1, "retry leaked");
            },
        async () => {
                var d = new Directory { Create = () => Room("z", owned: true) }; var c = new Connection();
                d.OnCreate = () => d.Candidates.Add(Room("a"));
                d.Lock = l => { l.Players = 2; return l; };
                await Run(d, c); Check(d.Joins == 0 && c.Hosts == 1 && d.Closes == 1, "abandoned incoming player");
            },
        async () => {
                var d = new Directory { Create = () => Room("z", owned: true) }; var c = new Connection();
                d.OnCreate = () => d.Candidates.Add(Room("a"));
                d.Read = l => { if (!l.Owned) l.Players = 2; return l; };
                await Run(d, c);
                Check(d.Creates == 1 && d.Joins == 1 && c.Joins == 1 && d.Closes == 2 && d.Closed[0] == "z", "split queue");
            },
        async () => {
                var d = new Directory { FailClose = true }; var c = new Connection { FailOnce = true };
                try { await Run(d, c); throw new Exception("Expected failure"); } catch (InvalidOperationException) { }
                Check(d.Creates == 1, "created despite ambiguous cleanup");
            },
        async () => {
                var d = new Directory { Create = () => Room("z", owned: true) }; var c = new Connection(); var cancel = new CancellationTokenSource();
                var task = Run(d, c, cancel.Token); await Task.Delay(65); cancel.Cancel(); await ExpectCancel(task);
                Check(d.Creates == 1 && d.Heartbeats >= 1 && d.Closes == 1 && c.Hosts == 0, "waiting lifecycle");
            },
        () => AutomaticDuelAtomicTestDirectory.Run(2),
        () => AutomaticDuelAtomicTestDirectory.Run(3),
        () => AutomaticDuelAtomicTestDirectory.Run(4),
    };
}
static class AutomaticDuelAtomicTestDirectory
{
    sealed class Row
    {
        public string Id, Host, Guest, Ticket, Code;
        public long Score;
        public bool Locked;
        public DateTime Created;
    }
    sealed class Pair
    {
        public int Ready;
        public TaskCompletionSource<bool> Both = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    sealed class Server
    {
        public object Gate = new object();
        public Dictionary<string, Row> Rows = new Dictionary<string, Row>();
        public Dictionary<string, Pair> Pairs = new Dictionary<string, Pair>();
        public TaskCompletionSource<bool> InitialQueries = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public int QueryCount, Sequence, Size, Reservations;
        public DuelLobby View(Row row, string user) => new DuelLobby {
            Id = row.Id, HostId = row.Host, Owned = row.Host == user, Players = row.Guest == null ? 1 : 2,
            Ticket = row.Ticket, RelayCode = row.Code, Locked = row.Locked, Trophies = row.Score,
            Compatibility = "v17", Created = row.Created
        };
    }
    sealed class Client : IDuelDirectory, IDuelConnection
    {
        readonly Server server;
        readonly string user;
        bool firstQuery = true;
        string ticket;
        public bool Matched;
        public Client(Server server, string user) { this.server = server; this.user = user; }
        public Task RecoverAsync(CancellationToken t) { t.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public async Task<IReadOnlyList<DuelLobby>> FindAsync(long score, string compatibility, DuelLobby own, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            if (firstQuery)
            {
                firstQuery = false;
                lock (server.Gate) { if (++server.QueryCount == server.Size) server.InitialQueries.TrySetResult(true); }
                await server.InitialQueries.Task;
                return Array.Empty<DuelLobby>(); // All clients observed the same empty queue.
            }
            lock (server.Gate) return server.Rows.Values.Select(r => server.View(r, user)).ToArray();
        }
        public Task<DuelLobby> CreateAsync(long score, string compatibility, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            lock (server.Gate)
            {
                if (server.Rows.Values.Any(r => r.Host == user)) throw new Exception("duplicate owned room");
                int seq = ++server.Sequence;
                var row = new Row { Id = seq.ToString("D4"), Host = user, Score = score, Created = new DateTime(2026, 10, 25).AddMilliseconds(seq) };
                server.Rows.Add(row.Id, row); return Task.FromResult(server.View(row, user));
            }
        }
        public Task<DuelLobby> JoinAsync(DuelLobby lobby, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            lock (server.Gate)
            {
                if (!server.Rows.TryGetValue(lobby.Id, out var row) || row.Locked || row.Guest != null) throw new DuelUnavailableException();
                row.Guest = user; row.Ticket = "ticket-" + user; server.Reservations++;
                return Task.FromResult(server.View(row, user));
            }
        }
        public Task<DuelLobby> ReadAsync(DuelLobby lobby, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            lock (server.Gate)
            {
                if (!server.Rows.TryGetValue(lobby.Id, out var row)) throw new DuelUnavailableException();
                return Task.FromResult(server.View(row, user));
            }
        }
        public Task<DuelLobby> LockAsync(DuelLobby lobby, bool locked, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            lock (server.Gate)
            {
                if (!server.Rows.TryGetValue(lobby.Id, out var row)) throw new DuelUnavailableException();
                row.Locked = locked; return Task.FromResult(server.View(row, user));
            }
        }
        public Task PublishAsync(DuelLobby lobby, string code, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            lock (server.Gate)
            {
                if (!server.Rows.TryGetValue(lobby.Id, out var row)) throw new DuelUnavailableException();
                if (!row.Locked || row.Guest == null) throw new Exception("published unreserved room");
                row.Code = code;
            }
            return Task.CompletedTask;
        }
        public Task HeartbeatAsync(DuelLobby l, CancellationToken t) => Task.CompletedTask;
        public Task CloseAsync(DuelLobby lobby)
        {
            lock (server.Gate)
            {
                if (server.Rows.TryGetValue(lobby.Id, out var row))
                {
                    if (row.Host == user) server.Rows.Remove(row.Id);
                    else if (row.Guest == user) row.Guest = null;
                }
            }
            return Task.CompletedTask;
        }
        public Task DelayAsync(int ms, CancellationToken t) => Task.Delay(ms >= 10000 ? 100 : 2, t);
        public Task<string> HostAsync(string reservation, CancellationToken t)
        {
            t.ThrowIfCancellationRequested(); ticket = reservation;
            lock (server.Gate) server.Pairs.Add(ticket, new Pair());
            return Task.FromResult(ticket);
        }
        public Task JoinAsync(string code, string reservation, CancellationToken t)
        {
            t.ThrowIfCancellationRequested();
            if (code != reservation) throw new Exception("wrong reservation connected");
            ticket = reservation; return Task.CompletedTask;
        }
        public async Task WaitForMatchAsync(CancellationToken t)
        {
            Task both;
            lock (server.Gate)
            {
                var pair = server.Pairs[ticket];
                if (++pair.Ready > 2) throw new Exception("third player entered match");
                if (pair.Ready == 2) pair.Both.TrySetResult(true);
                both = pair.Both.Task;
            }
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (t.Register(() => cancelled.TrySetCanceled()))
            {
                await Task.WhenAny(both, cancelled.Task);
                t.ThrowIfCancellationRequested();
                await both;
            }
            Matched = true;
        }
        public void Stop() { }
    }

    public static async Task Run(int count)
    {
        var server = new Server { Size = count };
        var clients = Enumerable.Range(0, count).Select(i => new Client(server, "player-" + i)).ToArray();
        using var cancel = new CancellationTokenSource();
        var tasks = clients.Select((c, i) => Task.Run(() => new AutomaticDuelSearch(c, c).RunAsync(i * 9999, "v17", cancel.Token))).ToArray();
        int expected = count / 2 * 2;
        try
        {
            for (int i = 0; clients.Count(c => c.Matched) < expected && i < 1000; i++) await Task.Delay(2);
            if (clients.Count(c => c.Matched) != expected) throw new Exception("concurrent seekers did not converge");
            // Give successful reservations time to close before cancelling the odd seeker.
            await Task.Delay(20);
        }
        finally
        {
            // Unlike the one-shot console runner, Unity survives a failed assertion.
            // Always stop every waiting seeker and heartbeat before the next test.
            cancel.Cancel();
            foreach (var task in tasks)
                try { await task; } catch (OperationCanceledException) { }
        }
        if (server.Rows.Count != 0 || server.Pairs.Count != count / 2 || server.Pairs.Values.Any(p => p.Ready != 2))
            throw new Exception("concurrent reservation leaked or duplicated a match");
    }
}
