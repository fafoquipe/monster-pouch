using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MonsterPouch.Local
{
    // No Unity dependency: exercise the actual orchestration with an in-memory
    // atomic directory, including responses that arrive after cancellation.
    public sealed class DuelLobby
    {
        public string Id, HostId, Compatibility, RelayCode, Ticket;
        public DateTime Created;
        public long Trophies;
        public int Players;
        public bool Locked, Owned;
    }

    public sealed class DuelUnavailableException : Exception
    {
        public DuelUnavailableException() : base("Duel reservation is no longer available.") { }
    }

    public interface IDuelDirectory
    {
        Task RecoverAsync(CancellationToken token);
        Task<IReadOnlyList<DuelLobby>> FindAsync(long trophies, string compatibility, DuelLobby olderThan, CancellationToken token);
        Task<DuelLobby> CreateAsync(long trophies, string compatibility, CancellationToken token);
        Task<DuelLobby> JoinAsync(DuelLobby lobby, CancellationToken token);
        Task<DuelLobby> ReadAsync(DuelLobby lobby, CancellationToken token);
        Task<DuelLobby> LockAsync(DuelLobby lobby, bool locked, CancellationToken token);
        Task PublishAsync(DuelLobby lobby, string relayCode, CancellationToken token);
        Task HeartbeatAsync(DuelLobby lobby, CancellationToken token);
        Task CloseAsync(DuelLobby lobby);
        Task DelayAsync(int milliseconds, CancellationToken token);
    }

    public interface IDuelConnection
    {
        Task<string> HostAsync(string ticket, CancellationToken token);
        Task JoinAsync(string code, string ticket, CancellationToken token);
        Task WaitForMatchAsync(CancellationToken token);
        void Stop();
    }

    public static class DuelCandidateOrder
    {
        public static bool IsOlder(DuelLobby candidate, DuelLobby own) => own == null ||
            candidate.Created < own.Created || candidate.Created == own.Created &&
            string.CompareOrdinal(candidate.Id, own.Id) < 0;

        public static IReadOnlyList<DuelLobby> Rank(IEnumerable<DuelLobby> candidates, long trophies, string compatibility, DuelLobby own = null) =>
            candidates.Where(c => c != null && !c.Owned && !c.Locked && c.Players == 1 &&
                c.Trophies >= 0 && c.Compatibility == compatibility && IsOlder(c, own))
            .GroupBy(c => c.Id).Select(g => g.First())
            // Decimal keeps full Int64 profile precision and cannot overflow.
            .OrderBy(c => Math.Abs((decimal)c.Trophies - trophies))
            .ThenBy(c => c.Created).ThenBy(c => c.Id, StringComparer.Ordinal).ToArray();
    }

    public sealed class AutomaticDuelSearch
    {
        readonly IDuelDirectory directory;
        readonly IDuelConnection connection;
        readonly Action<string> phase;
        readonly List<DuelLobby> leases = new List<DuelLobby>();
        CancellationTokenSource heartbeatStop;
        Task heartbeat;
        Exception heartbeatFailure;
        string currentPhase;
        void Phase(string value) { currentPhase = value; phase?.Invoke(value); }

        public AutomaticDuelSearch(IDuelDirectory directory, IDuelConnection connection, Action<string> phase = null)
        { this.directory = directory; this.connection = connection; this.phase = phase; }

        public async Task RunAsync(long trophies, string compatibility, CancellationToken token)
        {
            if (trophies < 0 || string.IsNullOrEmpty(compatibility)) throw new ArgumentException();
            bool matched = false;
            try
            {
                Phase("lobby.recover");
                await directory.RecoverAsync(token);
                DuelLobby current = null;
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (current == null)
                        {
                            Phase("lobby.query");
                            var candidates = DuelCandidateOrder.Rank(await directory.FindAsync(trophies, compatibility, null, token), trophies, compatibility);
                            token.ThrowIfCancellationRequested();
                            if (candidates.Count > 0)
                            {
                                Phase("lobby.reserve");
                                current = await AcquireAsync(directory.JoinAsync(candidates[0], token), compatibility, false, token);
                            }
                            else
                            {
                                Phase("lobby.create");
                                current = await AcquireAsync(directory.CreateAsync(trophies, compatibility, token), compatibility, true, token);
                                StartHeartbeat(current, token);
                                Phase("lobby.wait-opponent");
                            }
                        }
                        if (heartbeatFailure != null) throw heartbeatFailure;
                        current = await directory.ReadAsync(current, token);
                        token.ThrowIfCancellationRequested();
                        if (!current.Owned)
                        {
                            Phase("lobby.wait-relay");
                            // This deadline bounds a broken reservation, never trophy eligibility.
                            var until = DateTime.UtcNow.AddSeconds(40);
                            while (string.IsNullOrEmpty(current.RelayCode))
                            {
                                if (DateTime.UtcNow >= until || current.Players != 2) throw new DuelUnavailableException();
                                await directory.DelayAsync(1100, token);
                                current = await directory.ReadAsync(current, token);
                            }
                            token.ThrowIfCancellationRequested();
                            Phase("relay.join");
                            await connection.JoinAsync(current.RelayCode, current.Ticket, token);
                            Phase("match.handshake");
                            await connection.WaitForMatchAsync(token);
                            matched = true;
                            return;
                        }
                        if (current.Players == 2)
                        {
                            Phase("lobby.lock-pair");
                            // The update response is the service snapshot AFTER locking.
                            current = await directory.LockAsync(current, true, token);
                            if (current.Players != 2 || string.IsNullOrEmpty(current.Ticket)) throw new DuelUnavailableException();
                            Phase("relay.host");
                            string code = await connection.HostAsync(current.Ticket, token);
                            token.ThrowIfCancellationRequested();
                            Phase("lobby.publish-relay");
                            await directory.PublishAsync(current, code, token);
                            Phase("match.handshake");
                            await connection.WaitForMatchAsync(token);
                            matched = true;
                            return;
                        }
                        var older = DuelCandidateOrder.Rank(await directory.FindAsync(trophies, compatibility, current, token), trophies, compatibility, current);
                        token.ThrowIfCancellationRequested();
                        if (older.Count > 0)
                        {
                            Phase("lobby.migrate-older");
                            // A host may only migrate to an older lobby: no A<->B cycle.
                            // Lock first, then inspect the returned membership. A join that
                            // won the race is kept; a join after the lock is rejected by UGS.
                            current = await directory.LockAsync(current, true, token);
                            if (current.Players == 2) continue;
                            DuelLobby joined;
                            try { joined = await AcquireAsync(directory.JoinAsync(older[0], token), compatibility, false, token); }
                            catch (DuelUnavailableException)
                            {
                                current = await directory.LockAsync(current, false, token);
                                continue;
                            }
                            await StopHeartbeatAsync();
                            await CloseAsync(current);
                            current = joined;
                            continue;
                        }
                        await directory.DelayAsync(1100, token);
                    }
                    catch (DuelUnavailableException)
                    {
                        Phase("lobby.retry-contention");
                        token.ThrowIfCancellationRequested();
                        connection.Stop();
                        await StopHeartbeatAsync();
                        await CloseAllAsync();
                        current = null;
                        // API pacing only, not a matchmaking window or trophy range.
                        await directory.DelayAsync(1100, token);
                    }
                }
            }
            catch (Exception error)
            {
                if (!error.Data.Contains("duel.phase")) error.Data["duel.phase"] = currentPhase;
                throw;
            }
            finally
            {
                Phase(matched ? "lobby.release-matched" : "lobby.release-cancelled");
                if (!matched) connection.Stop();
                await StopHeartbeatAsync();
                await CloseAllAsync();
            }
        }

        async Task<DuelLobby> AcquireAsync(Task<DuelLobby> operation, string compatibility, bool owned, CancellationToken token)
        {
            // Record even a late successful create/join before observing cancellation.
            var lobby = await operation;
            leases.Add(lobby);
            token.ThrowIfCancellationRequested();
            if (lobby.Compatibility != compatibility || lobby.Owned != owned || lobby.Trophies < 0)
            {
                await CloseAsync(lobby);
                throw new DuelUnavailableException();
            }
            return lobby;
        }
        async Task CloseAsync(DuelLobby lobby)
        { await directory.CloseAsync(lobby); leases.RemoveAll(l => l.Id == lobby.Id); }
        async Task CloseAllAsync()
        {
            Exception failure = null;
            foreach (var lobby in leases.ToArray())
                try { await CloseAsync(lobby); } catch (Exception e) { failure = e; }
            if (failure != null) throw failure;
        }
        void StartHeartbeat(DuelLobby lobby, CancellationToken token)
        {
            heartbeatFailure = null;
            heartbeatStop = CancellationTokenSource.CreateLinkedTokenSource(token);
            heartbeat = KeepAliveAsync(lobby, heartbeatStop.Token);
        }
        async Task KeepAliveAsync(DuelLobby lobby, CancellationToken token)
        {
            try
            {
                while (true)
                {
                    await directory.DelayAsync(10000, token);
                    await directory.HeartbeatAsync(lobby, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { heartbeatFailure = e; }
        }
        async Task StopHeartbeatAsync()
        {
            if (heartbeatStop == null) return;
            heartbeatStop.Cancel();
            await heartbeat;
            heartbeatStop.Dispose(); heartbeatStop = null; heartbeat = null; heartbeatFailure = null;
        }
    }
}
