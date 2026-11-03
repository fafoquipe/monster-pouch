using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

namespace MonsterPouch.Local
{
    // Uses the Lobby API already shipped inside Multiplayer Services 2.3.3.
    // No Matchmaker queue, Cloud Code, dashboard mutation or new package.
    public sealed class UnityDuelDirectory : IDuelDirectory
    {
        public const string Mode = "monster-pouch-auto-duel-1";
        readonly Dictionary<string, DateTime> nextRequest = new Dictionary<string, DateTime>();
        readonly SemaphoreSlim discoveryGate = new SemaphoreSlim(1, 1);
        string PlayerId => AuthenticationService.Instance.PlayerId;
        readonly ILobbyService service;
        public UnityDuelDirectory() : this(LobbyService.Instance) { }
        public UnityDuelDirectory(ILobbyService service) { this.service = service; }

        async Task<T> Service<T>(string operation, Func<Task<T>> request, CancellationToken token)
        {
            bool discovery = operation == "QueryLobbiesAsync";
            if (discovery) await discoveryGate.WaitAsync(token);
            try
            {
            int interval = discovery ? 3000 : operation == "CreateLobbyAsync" ? 3100 :
                operation == "DeleteLobbyAsync" || operation == "RemovePlayerAsync" ? 550 : 1100;
            for (int attempt = 0; ; attempt++)
            {
                await Pace(operation, interval, token);
                try
                {
                    if (discovery) UnityEngine.Debug.Log("[Duel] discovery sent utc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    // No cancellation check after a successful mutation: AcquireAsync
                    // must first record the returned lease, even if cancellation raced it.
                    return await request();
                }
                catch (LobbyServiceException error) when (error.Reason == LobbyExceptionReason.RateLimited && attempt < 3)
                {
                    // A 429 explicitly rejected this operation. Timeouts/5xx are
                    // ambiguous and must NOT replay Create/Join here.
                    int backoff = (1500 << attempt) + (Guid.NewGuid().GetHashCode() & 511);
                    nextRequest[operation] = DateTime.UtcNow.AddMilliseconds(backoff);
                    UnityEngine.Debug.Log("[Duel] throttled phase=lobby." + operation + " retry=" + (attempt + 1));
                }
                catch (Exception error) { error.Data["duel.phase"] = "lobby." + operation; throw; }
                finally
                {
                    // Pace from the response, not from the send. Different network
                    // latencies can otherwise bunch the next arrivals inside 1 second.
                    var next = DateTime.UtcNow.AddMilliseconds(interval);
                    if (!nextRequest.TryGetValue(operation, out var existing) || existing < next) nextRequest[operation] = next;
                }
            }
            }
            finally { if (discovery) discoveryGate.Release(); }
        }
        async Task Service(string operation, Func<Task> request, CancellationToken token)
        {
            await Service(operation, async () => { await request(); return true; }, token);
        }

        public Task DelayAsync(int milliseconds, CancellationToken token) => Task.Delay(milliseconds, token);
        async Task Pace(string operation, int milliseconds, CancellationToken token)
        {
            if (nextRequest.TryGetValue(operation, out var next))
            {
                int wait = (int)Math.Ceiling((next - DateTime.UtcNow).TotalMilliseconds);
                if (wait > 0) await DelayAsync(wait, token);
            }
            token.ThrowIfCancellationRequested();
            nextRequest[operation] = DateTime.UtcNow.AddMilliseconds(milliseconds);
        }
        static string Value(Lobby lobby, string key) => lobby.Data != null && lobby.Data.TryGetValue(key, out var value) ? value.Value : null;
        static bool Gone(LobbyServiceException e) => e.Reason == LobbyExceptionReason.LobbyNotFound ||
            e.Reason == LobbyExceptionReason.PlayerNotFound || e.Reason == LobbyExceptionReason.EntityNotFound;
        static bool Contended(LobbyServiceException e) => Gone(e) || e.Reason == LobbyExceptionReason.LobbyFull ||
            e.Reason == LobbyExceptionReason.LobbyLocked || e.Reason == LobbyExceptionReason.LobbyConflict ||
            e.Reason == LobbyExceptionReason.Conflict;
        static bool VersionChanged(LobbyServiceException e) => e.Reason == LobbyExceptionReason.LobbyVersionDoesNotMatch ||
            e.Reason == LobbyExceptionReason.PreconditionFailed;
        DuelLobby Snapshot(Lobby lobby)
        {
            long trophies;
            if (!long.TryParse(Value(lobby, "trophies"), NumberStyles.None, CultureInfo.InvariantCulture, out trophies)) trophies = -1;
            var guest = lobby.Players?.FirstOrDefault(p => p.Id != lobby.HostId);
            string ticket = guest?.Data != null && guest.Data.TryGetValue("ticket", out var data) ? data.Value : null;
            return new DuelLobby { Id = lobby.Id, HostId = lobby.HostId, Created = lobby.Created,
                Trophies = trophies, Compatibility = Value(lobby, "compatibility"),
                RelayCode = Value(lobby, "relay"), Ticket = ticket, Players = lobby.MaxPlayers - lobby.AvailableSlots,
                Locked = lobby.IsLocked, Owned = lobby.HostId == PlayerId };
        }
        static QueryFilter Filter(QueryFilter.FieldOptions field, string value, QueryFilter.OpOptions op = QueryFilter.OpOptions.EQ) => new QueryFilter(field, value, op);

        public async Task<IReadOnlyList<DuelLobby>> FindAsync(long trophies, string compatibility, DuelLobby olderThan, CancellationToken token)
        {
            var candidates = new List<DuelLobby>();
            // Nearest <= score and nearest > score. Server ordering avoids the
            // random-sample bias of QuickJoin or sorting one arbitrary page.
            foreach (bool above in new[] { false, true })
            {
                string continuation = null;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                do
                {
                    var filters = new List<QueryFilter> {
                        Filter(QueryFilter.FieldOptions.S1, Mode),
                        Filter(QueryFilter.FieldOptions.S2, compatibility),
                        Filter(QueryFilter.FieldOptions.S3, PlayerId, QueryFilter.OpOptions.NE),
                        Filter(QueryFilter.FieldOptions.MaxPlayers, "2"),
                        Filter(QueryFilter.FieldOptions.AvailableSlots, "1"),
                        Filter(QueryFilter.FieldOptions.IsLocked, "false"),
                        Filter(QueryFilter.FieldOptions.HasPassword, "false"),
                        Filter(QueryFilter.FieldOptions.N1, trophies.ToString(CultureInfo.InvariantCulture), above ? QueryFilter.OpOptions.GT : QueryFilter.OpOptions.LE)
                    };
                    if (olderThan != null) filters.Add(Filter(QueryFilter.FieldOptions.Created,
                        olderThan.Created.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture), QueryFilter.OpOptions.LE));
                    var page = await Service("QueryLobbiesAsync", () => service.QueryLobbiesAsync(new QueryLobbiesOptions {
                        Count = 100, SampleResults = false, ContinuationToken = continuation, Filters = filters,
                        Order = new List<QueryOrder> { new QueryOrder(above, QueryOrder.FieldOptions.N1), new QueryOrder(true, QueryOrder.FieldOptions.Created) }
                    }), token);
                    token.ThrowIfCancellationRequested();
                    var ranked = DuelCandidateOrder.Rank(page.Results.Select(Snapshot), trophies, compatibility, olderThan);
                    if (ranked.Count > 0) { candidates.Add(ranked[0]); break; }
                    // A page can contain same-timestamp younger hosts. Keep paging
                    // rather than concluding that no eligible older room exists.
                    continuation = page.ContinuationToken;
                    if (page.Results.Count == 0 || string.IsNullOrEmpty(continuation) || !seen.Add(continuation)) break;
                } while (true);
            }
            return DuelCandidateOrder.Rank(candidates, trophies, compatibility, olderThan);
        }

        public async Task<DuelLobby> CreateAsync(long trophies, string compatibility, CancellationToken token)
        {
            var lobby = await Service("CreateLobbyAsync", () => service.CreateLobbyAsync("Monster Pouch Duel", 2, new CreateLobbyOptions {
                IsPrivate = false, IsLocked = false, Player = new Player(PlayerId),
                Data = new Dictionary<string, DataObject> {
                    ["mode"] = new DataObject(DataObject.VisibilityOptions.Public, Mode, DataObject.IndexOptions.S1),
                    ["compatibility"] = new DataObject(DataObject.VisibilityOptions.Public, compatibility, DataObject.IndexOptions.S2),
                    ["owner"] = new DataObject(DataObject.VisibilityOptions.Public, PlayerId, DataObject.IndexOptions.S3),
                    ["trophies"] = new DataObject(DataObject.VisibilityOptions.Public, trophies.ToString(CultureInfo.InvariantCulture), DataObject.IndexOptions.N1)
                }
            }), token);
            // Do not throw for cancellation here: the caller must record the lease.
            return Snapshot(lobby);
        }
        public async Task<DuelLobby> JoinAsync(DuelLobby lobby, CancellationToken token)
        {
            try
            {
                return Snapshot(await Service("JoinLobbyByIdAsync", () => service.JoinLobbyByIdAsync(lobby.Id, new JoinLobbyByIdOptions {
                    Player = new Player(PlayerId, data: new Dictionary<string, PlayerDataObject> {
                        ["ticket"] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, Guid.NewGuid().ToString("N"))
                    })
                }), token));
            }
            catch (LobbyServiceException e) when (Contended(e)) { throw new DuelUnavailableException(); }
        }
        public async Task<DuelLobby> ReadAsync(DuelLobby lobby, CancellationToken token)
        {
            try
            {
                var current = await Service("GetLobbyAsync", () => service.GetLobbyAsync(lobby.Id), token);
                token.ThrowIfCancellationRequested();
                if (Value(current, "mode") != Mode || Value(current, "compatibility") != lobby.Compatibility ||
                    current.HostId != lobby.HostId || current.Players == null || !current.Players.Any(p => p.Id == PlayerId))
                    throw new DuelUnavailableException();
                return Snapshot(current);
            }
            catch (LobbyServiceException e) when (Gone(e)) { throw new DuelUnavailableException(); }
        }
        public async Task<DuelLobby> LockAsync(DuelLobby lobby, bool locked, CancellationToken token)
        {
            return Snapshot(await UpdateAsync(lobby, new UpdateLobbyOptions { IsLocked = locked }, token));
        }
        public async Task PublishAsync(DuelLobby lobby, string relayCode, CancellationToken token)
        {
            await UpdateAsync(lobby, new UpdateLobbyOptions {
                IsLocked = true, Data = new Dictionary<string, DataObject> {
                    ["relay"] = new DataObject(DataObject.VisibilityOptions.Member, relayCode)
                }
            }, token);
        }
        async Task<Lobby> UpdateAsync(DuelLobby lobby, UpdateLobbyOptions options, CancellationToken token)
        {
            for (int attempt = 0; ; attempt++)
            {
                try { return await Service("UpdateLobbyAsync", () => service.UpdateLobbyAsync(lobby.Id, options), token); }
                catch (LobbyServiceException e) when (VersionChanged(e) && attempt < 2)
                {
                    // Respect the SDK's optional If-Match mode without changing
                    // its global setting. Re-read a winning join, then lock it.
                    await ReadAsync(lobby, token);
                }
                catch (LobbyServiceException e) when (Contended(e)) { throw new DuelUnavailableException(); }
            }
        }
        public async Task HeartbeatAsync(DuelLobby lobby, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            try { await Service("SendHeartbeatPingAsync", () => service.SendHeartbeatPingAsync(lobby.Id), token); }
            catch (LobbyServiceException e) when (Gone(e)) { throw new DuelUnavailableException(); }
        }
        public async Task CloseAsync(DuelLobby lobby)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (lobby.Owned) await Service("DeleteLobbyAsync", () => service.DeleteLobbyAsync(lobby.Id), CancellationToken.None);
                    else await Service("RemovePlayerAsync", () => service.RemovePlayerAsync(lobby.Id, PlayerId), CancellationToken.None);
                    return;
                }
                catch (LobbyServiceException e) when (Gone(e)) { return; }
                catch (LobbyServiceException e) when (VersionChanged(e) && attempt < 2)
                {
                    try { await Service("GetLobbyAsync", () => service.GetLobbyAsync(lobby.Id), CancellationToken.None); }
                    catch (LobbyServiceException missing) when (Gone(missing)) { return; }
                }
                catch when (attempt < 2) { await DelayAsync(1100 * (attempt + 1), CancellationToken.None); }
            }
        }
        public async Task RecoverAsync(CancellationToken token)
        {
            // Also resolves an ambiguous create/join (server committed, response
            // lost), process restart, and cleanup that failed while offline.
            var ids = await Service("GetJoinedLobbiesAsync", () => service.GetJoinedLobbiesAsync(), token);
            foreach (string id in ids)
            {
                Lobby lobby;
                try { lobby = await Service("GetLobbyAsync", () => service.GetLobbyAsync(id), token); }
                catch (LobbyServiceException e) when (Gone(e)) { continue; }
                if (Value(lobby, "mode") == Mode) await CloseAsync(Snapshot(lobby));
            }
            token.ThrowIfCancellationRequested();
        }
    }
}
