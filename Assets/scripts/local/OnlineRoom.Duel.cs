using System;
using System.Threading;
using System.Threading.Tasks;
using MonsterPouch.Gameplay.Match;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using UnityEngine;

namespace MonsterPouch.Local
{
    public enum AutomaticDuelState { Idle, Searching, Cancelling, Failed, Matched }

    public sealed partial class OnlineRoom : IDuelConnection
    {
        public AutomaticDuelState DuelState { get; private set; }
        public bool DuelPending { get; private set; }
        public bool AutomaticDuel { get; private set; }
        public Func<long> LocalTrophyProvider { get; set; }
        CancellationTokenSource duelCancellation;
        UnityDuelDirectory duelDirectory;
        string duelMonster;
        string[] duelWhelps;
        bool duelConnectionFailed, destroyed;
        int duelConnectionFailures;
        Exception duelConnectionError;
        public string DuelPhase { get; private set; } = "idle";
        public string DuelFailureCode { get; private set; }
        public string SharedMatchId => sharedMatchId;
        public int SharedMatchSeed { get; private set; }

        void TraceDuelPhase(string phase)
        {
            if (DuelPhase == phase) return;
            DuelPhase = phase;
            Debug.Log("[Duel] phase=" + phase);
        }
        internal static string SafeDuelFailure(Exception error, string phase)
        {
            if (error.Data["duel.phase"] is string operation) phase = operation;
            string code = error is RequestFailedException request ? request.ErrorCode.ToString() : error.GetType().Name;
            string reason = error is LobbyServiceException lobby ? " reason=" + lobby.Reason : "";
            return "phase=" + phase + " code=" + code + reason;
        }

        string ConnectionBalance() => MatchController.Hash(Protocol + JsonUtility.ToJson(match.Config) +
            MonsterPouch.Gameplay.Status.StatusRules.Signature()+EronyaMechanics.Version+EronyaAbilityParameters.Signature+MartioFanPattern.RulesSignature);

        public async Task SearchDuel(string selectedMonster, string[] selectedWhelps)
        {
            if (destroyed || DuelPending || Active || Busy || match.IsOnline) return;
            AutomaticDuel = true; DuelPending = true; DuelState = AutomaticDuelState.Searching;
            duelCancellation = new CancellationTokenSource();
            var token = duelCancellation.Token;
            duelMonster = selectedMonster;
            duelWhelps = selectedWhelps == null ? null : (string[])selectedWhelps.Clone();
            duelConnectionFailures = 0;
            duelConnectionError = null; DuelFailureCode = null;
            SetStatus("Buscando");
            try
            {
                TraceDuelPhase("profile.validate");
                if (LocalTrophyProvider == null || duelWhelps == null ||
                    LocalTeamValidator != null && !LocalTeamValidator(duelMonster, duelWhelps))
                    throw new InvalidOperationException("Local duel profile unavailable.");
                long trophies = LocalTrophyProvider(); // Real loaded v10 profile; no preference or placeholder score.
                if (trophies < 0 || string.IsNullOrWhiteSpace(Application.cloudProjectId)) throw new InvalidOperationException();
                TraceDuelPhase("services.initialize");
                await UnityServices.InitializeAsync();
                token.ThrowIfCancellationRequested();
                TraceDuelPhase("authentication.sign-in");
                if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
                token.ThrowIfCancellationRequested();
                if (duelDirectory == null) duelDirectory = new UnityDuelDirectory();
                string compatibility = ConnectionBalance();
#if MONSTER_POUCH_DUEL_QA
                // QA clients exercise production matchmaking in a distinct pool.
                // They must never reserve a real player's lobby.
                compatibility = MatchController.Hash(compatibility + "|qa-duels-20261026");
#endif
                Debug.Log("[Duel] compatibility=" + compatibility);
                await new AutomaticDuelSearch(duelDirectory, this, TraceDuelPhase).RunAsync(trophies, compatibility, token);
                if (match.IsOnline) DuelState = AutomaticDuelState.Matched;
            }
            catch (OperationCanceledException) { DuelState = AutomaticDuelState.Idle; }
            catch (Exception error)
            {
                // Never expose lobby identifiers, member tokens or service payloads.
                DuelFailureCode = SafeDuelFailure(duelConnectionError ?? error, DuelPhase);
                Debug.LogWarning("[Duel] failed " + DuelFailureCode);
                if (match.IsOnline) DuelState = AutomaticDuelState.Matched;
                else if (token.IsCancellationRequested) DuelState = AutomaticDuelState.Idle;
                else DuelState = AutomaticDuelState.Failed;
            }
            finally
            {
                // An SDK mutation can commit remotely yet return a timeout. Reconcile
                // memberships before allowing any new search/manual connection.
                if (DuelState != AutomaticDuelState.Matched && duelDirectory != null)
                {
                    try { await duelDirectory.RecoverAsync(CancellationToken.None); }
                    catch (Exception error) { Debug.LogWarning("[Duel] cleanup-pending " + SafeDuelFailure(error, "lobby.recover")); }
                }
                if (DuelState != AutomaticDuelState.Matched) DisconnectRoom();
                duelCancellation.Dispose(); duelCancellation = null;
                DuelPending = false;
                if (DuelState == AutomaticDuelState.Failed)
                    SetErrorStatus("No se pudo buscar un rival. Comprueba tu conexión e inténtalo de nuevo.", true);
                if (!destroyed) Changed?.Invoke();
            }
        }

        public void CancelAutomaticDuel()
        {
            if (duelCancellation != null && !duelCancellation.IsCancellationRequested)
            {
                DuelState = AutomaticDuelState.Cancelling;
                duelCancellation.Cancel();
            }
        }
        void CheckDuelConnection(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (duelConnectionFailed || !Active)
            {
                if (++duelConnectionFailures >= 3) throw new InvalidOperationException("Duel connection failed repeatedly.");
                throw new DuelUnavailableException();
            }
            if (StatusIsError) throw new InvalidOperationException("Local team is no longer available.");
        }
        async Task<string> IDuelConnection.HostAsync(string ticket, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); duelConnectionFailed = false; duelConnectionError = null;
            await WaitForNetworkShutdown(token);
            await Connect(true, "", duelMonster, duelWhelps, ticket);
            CheckDuelConnection(token);
            return Code;
        }
        async Task IDuelConnection.JoinAsync(string code, string ticket, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); duelConnectionFailed = false; duelConnectionError = null;
            if (string.IsNullOrEmpty(ticket)) throw new DuelUnavailableException();
            await WaitForNetworkShutdown(token);
            await Connect(false, code, duelMonster, duelWhelps, ticket);
            CheckDuelConnection(token);
        }
        async Task IDuelConnection.WaitForMatchAsync(CancellationToken token)
        {
            var until = DateTime.UtcNow.AddSeconds(40);
            while (!match.IsOnline)
            {
                CheckDuelConnection(token);
                if (DateTime.UtcNow >= until) throw new DuelUnavailableException();
                if (IsHost && HasOpponent && !Busy)
                {
                    StartRoom();
                    if (!match.IsOnline) throw new InvalidOperationException("Could not start automatic duel.");
                }
                if (!match.IsOnline) await Task.Delay(100, token);
            }
            token.ThrowIfCancellationRequested();
        }
        void IDuelConnection.Stop() => DisconnectRoom();
        static async Task WaitForNetworkShutdown(CancellationToken token)
        {
            // Leave lets NGO finish its shutdown tick before another manager starts.
            var until = DateTime.UtcNow.AddSeconds(5);
            while (Unity.Netcode.NetworkManager.Singleton != null)
            {
                if (DateTime.UtcNow >= until) throw new InvalidOperationException("Network shutdown did not complete.");
                await Task.Delay(50, token);
            }
            token.ThrowIfCancellationRequested();
        }
    }
}
