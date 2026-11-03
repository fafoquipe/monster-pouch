#if UNITY_EDITOR || MONSTER_POUCH_DUEL_QA
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Explicitly compiled only into the local QA build. Normal players never run this driver.
    public sealed class DuelQaClient : MonoBehaviour
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        static string root, label;
        LocalGameUI ui; OnlineRoom room;
        int preparedRound, verifiedEvents, starts;
        bool accelerating, sawOnline;
        [Serializable] sealed class Report
        {
            public string client, status, phase, failure, playerHash, matchId, side, digest;
            public string localMap, hostMap, guestMap;
            public System.Collections.Generic.List<string> roundMaps = new System.Collections.Generic.List<string>();
            public int seed, round, playerWins, rivalWins, verifiedEvents, starts;
            public int receiptsBefore, receiptsAfter, receiptsAfterRepeat;
            public int remainingLobbies = -1;
            public int cancellationRemainingLobbies = -1;
            public long trophiesBefore, trophiesAfter, trophiesAfterRepeat;
            public bool cancelled, pending;
        }
        Report report;
        static string Arg(string name, string fallback = "")
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            label = Arg("-duelQaClient"); if (label == "") return;
            root = Path.GetFullPath(Arg("-duelQaRoot")); Directory.CreateDirectory(root);
            LocalGameUI.GuestProfileDirectoryOverride = Path.Combine(root, "Guest");
            LocalGameUI.DeckDirectoryOverride = Path.Combine(root, "Decks");
            LocalGameUI.ProgressionDirectoryOverride = Path.Combine(root, "Progress");
            new LocalGuestProfileStore(LocalGameUI.GuestProfileDirectoryOverride).OpenOrCreate(true);
            Application.runInBackground = true;
            var driver = new GameObject("Explicit Duel QA client").AddComponent<DuelQaClient>();
            DontDestroyOnLoad(driver.gameObject);
        }
        async void Start()
        {
            report = new Report { client = label, status = "starting" };
            try
            {
                await UnityServices.InitializeAsync(new InitializationOptions().SetProfile("duelqa" + Guid.NewGuid().ToString("N").Substring(0, 24)));
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                report.playerHash = MatchController.Hash(AuthenticationService.Instance.PlayerId);
                await Until(() => FindFirstObjectByType<LocalGameUI>() != null && !LoadingArtworkScreen.IsBlocking, 30);
                ui = FindFirstObjectByType<LocalGameUI>();
                room = (OnlineRoom)typeof(LocalGameUI).GetField("online", Hidden).GetValue(ui);
                room.MatchCompletedVerified += _ => verifiedEvents++;
                var profile = (LocalGuestProfile)typeof(LocalGameUI).GetField("guestProfile", Hidden).GetValue(ui);
                PlayerPrefs.SetString("monster-pouch.lobby.mode." + profile.ProfileId, "duel");
                report.trophiesBefore = ui.AccountTrophies;
                report.receiptsBefore = profile.TrophyLadder.Matches.Length;
                string requestedMap = Arg("-duelQaMap");
                if (requestedMap != "")
                {
                    if (!CardTableTheme.KnownMap(requestedMap) || !MatchController.ValidOnlineMap(requestedMap))
                        throw new InvalidOperationException("Unknown QA map: " + requestedMap);
                    var style = CardTableTheme.FromMapId(requestedMap);
                    if (!ui.CanSelectTrophyBoard(style)) throw new InvalidOperationException("QA profile cannot select map: " + requestedMap);
                    ui.SelectTableSkin(style);
                    await Until(() => !ui.MapTransitionActive, 15);
                    if (CardTableTheme.MapId(CardTableTheme.Current) != requestedMap)
                        throw new InvalidOperationException("QA map selection did not persist.");
                }
                report.localMap = CardTableTheme.MapId(CardTableTheme.Current);
                await Task.Delay(int.Parse(Arg("-duelQaDelay", "0")) * 1000);
                PressPlay();
                if (Arg("-duelQaCancel") == "true")
                {
                    await Until(() => room.DuelPhase == "lobby.create" || room.DuelState == AutomaticDuelState.Failed, 45);
                    await Task.Delay(2000);
                    typeof(LocalGameUI).GetMethod("CancelDuelSearch", Hidden).Invoke(ui, null);
                    await Until(() => !room.DuelPending, 30);
                    if (room.Active || ui.Match.IsOnline) throw new InvalidOperationException("Cancellation left an active connection.");
                    await VerifyCleanup(); report.cancellationRemainingLobbies = report.remainingLobbies;
                    report.cancelled = true; Save("cancelled");
                    await Task.Delay(1200);
                    PressPlay();
                }
                try { await Until(() => ui.Match.IsOnline || room.DuelState == AutomaticDuelState.Failed, Arg("-duelQaAllowUnmatched") == "true" ? 50 : 120); }
                catch (TimeoutException) when (Arg("-duelQaAllowUnmatched") == "true" && !ui.Match.IsOnline && room.DuelState == AutomaticDuelState.Searching)
                {
                    room.Leave(); await Until(() => !room.DuelPending, 30);
                    report.cancelled = true; await VerifyCleanup(); Save("unmatched-cancelled"); Application.Quit(0); return;
                }
                if (!ui.Match.IsOnline) throw new InvalidOperationException("Search failed: " + room.DuelFailureCode);
                report.matchId = room.SharedMatchId; report.seed = room.SharedMatchSeed; report.side = ui.Match.LocalSide.ToString();
                report.hostMap = ui.Match.HostMap; report.guestMap = ui.Match.GuestMap;
                if (ui.Match.MapForSide(ui.Match.LocalSide) != report.localMap)
                    throw new InvalidOperationException("Negotiated local map differs from the selected map.");
                string expectedRivalMap = Arg("-duelQaRivalMap");
                if (expectedRivalMap != "" && (!MatchController.ValidOnlineMap(expectedRivalMap) ||
                    ui.Match.MapForSide(ui.Match.RivalSide) != expectedRivalMap))
                    throw new InvalidOperationException("Negotiated rival map differs from the expected map.");
                Save("matched");
                ScreenCapture.CaptureScreenshot(Path.Combine(root, "matched.png"));
                ui.Match.enabled = false; accelerating = true;
                await Until(() => verifiedEvents > 0 || !ui.Match.IsOnline, 150);
                accelerating = false;
                if (verifiedEvents != 1) throw new InvalidOperationException("BO5 did not produce one verified result.");
                report.round = ui.Match.Round; report.playerWins = ui.Match.PlayerWins; report.rivalWins = ui.Match.BotWins;
                report.digest = room.LastVerifiedDigest; report.trophiesAfter = ui.AccountTrophies;
                report.receiptsAfter = new LocalGuestProfileStore(LocalGameUI.GuestProfileDirectoryOverride).Reload().TrophyLadder.Matches.Length;
                // Repeat the real idempotency entry points after both peers agreed on the result.
                typeof(OnlineRoom).GetMethod("CompareResults", Hidden).Invoke(room, null);
                ui.RetryTrophyResults(); ui.RetryTrophyResults();
                report.trophiesAfterRepeat = ui.AccountTrophies;
                report.receiptsAfterRepeat = new LocalGuestProfileStore(LocalGameUI.GuestProfileDirectoryOverride).Reload().TrophyLadder.Matches.Length;
                if (report.trophiesAfterRepeat != report.trophiesAfter || verifiedEvents != 1 || report.receiptsAfter != report.receiptsBefore + 1 || report.receiptsAfterRepeat != report.receiptsAfter) throw new InvalidOperationException("Incorrect or duplicate trophy application.");
                long expected = report.playerWins == 3 ? report.trophiesBefore + 30 : Math.Max(0, report.trophiesBefore - 5);
                if (report.trophiesAfter != expected) throw new InvalidOperationException("Trophy delta differs from the Naturalis rules.");
                await VerifyCleanup(); Save("passed"); await Task.Delay(3000); room.Leave(); Application.Quit(0);
            }
            catch (Exception error)
            {
                report.failure = error.GetType().Name + ": " + error.Message;
                Save("failed"); Debug.LogError("[DuelQA] " + report.failure);
                room?.Leave();
                if (room != null) try { await Until(() => !room.DuelPending, 30); } catch (TimeoutException) { }
                Save("failed"); await Task.Delay(1000); Application.Quit(1);
            }
        }
        void PressPlay()
        {
            ui.CloseModal(); ui.RenderPage();
            var play = ui.CanvasRoot.GetComponentsInChildren<Button>().Single(b => b.name == "BATALLA");
            play.onClick.Invoke(); Save("searching");
        }
        async Task VerifyCleanup()
        {
            await Until(() => !room.DuelPending, 30);
            await Task.Delay(1200); // Stay within GetJoinedLobbies player rate limits after recovery.
            report.remainingLobbies = (await LobbyService.Instance.GetJoinedLobbiesAsync()).Count;
            Debug.Log("[DuelQA] cleanup remaining-lobbies=" + report.remainingLobbies);
            if (report.remainingLobbies != 0) throw new InvalidOperationException("QA identity retained lobby membership.");
        }
        async Task Until(Func<bool> condition, int seconds)
        {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition()) { if (DateTime.UtcNow >= deadline) throw new TimeoutException("QA deadline; phase=" + room?.DuelPhase); await Task.Delay(100); }
        }
        void Update()
        {
            if (ui == null) return;
            if (ui.Match.IsOnline && !sawOnline) { starts++; sawOnline = true; }
            if (!accelerating || !ui.Match.IsOnline) return;
            var match = ui.Match;
            if (match.Phase == MatchPhase.Preparation && match.Round != preparedRound)
            {
                preparedRound = match.Round;
                report.roundMaps.Add(match.Round + "|" + match.RoundMap);
                // Legal preparation commands only. Blue gets extra units, making the BO5 deterministic.
                if (match.LocalSide == BoardSide.Blue)
                    for (int slot = 0; slot < match.Player.Offers.Count; slot++)
                    {
                        var offer = match.Player.Offers[slot]; if (offer == null) continue;
                        bool bought = false;
                        for (int y = 0; y < 10 && !bought; y++) for (int x = 0; x < 6 && !bought; x++)
                        {
                            var cell = match.Board.GetCell(x, y);
                            if (cell != null && !cell.IsBlocked && cell.OccupiedBy == null && match.Config.IsDeploymentLegal(match.LocalSide, cell.Coordinates))
                                bought = match.BuyAndPlace(slot, offer.Token, cell);
                        }
                    }
                match.SetReady(true);
            }
            match.Advance(Time.unscaledDeltaTime * 8);
        }
        void Save(string state)
        {
            report.status = state; report.phase = room?.DuelPhase; report.pending = room?.DuelPending ?? false;
            report.verifiedEvents = verifiedEvents; report.starts = starts;
            File.WriteAllText(Path.Combine(root, "report.json"), JsonUtility.ToJson(report, true));
            Debug.Log("[DuelQA] state=" + state + " client=" + label);
        }
    }
}
#endif
