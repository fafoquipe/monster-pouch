using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Two-player casual sessions: canonical simulation on both peers, validated
    // private preparation journals, host round barriers and round-end checksums.
    // This is not a trusted dedicated server or a ranked anti-cheat service.
    public sealed partial class OnlineRoom : MonoBehaviour
    {
        const string Channel="monster-pouch.room.v1", Protocol="mp-2026-10-27-v20-eronya-rules";
        const int MaxBytes=60000;
        [Serializable] sealed class Packet
        {
            public string kind, protocol, balance, monster, digest, map, matchId;
            public string[] whelps;
            public AbilityAccessData abilityAccess;
            public int seed, round, revision, peerRevision, lockId;
            public bool ready;
            public string playerName;
            public PreparationCommand[] commands;
        }
        // Runtime timing/occupancy rules are not part of MatchConfig: peers
        // must agree on the protocol as well as the catalogue balance.
        internal static bool AcceptsConnectionVersion(byte[] payload,string expectedBalance)=>
            payload!=null&&Encoding.UTF8.GetString(payload)==Protocol+":"+expectedBalance;
        public string Code { get; private set; }="";
        public string Status { get; private set; }="Crea una sala o escribe el código de tu amigo.";
        public bool Busy { get; private set; }
        public int LastVerifiedRound { get; private set; }
        public string LastVerifiedDigest { get; private set; }
        public bool Active => manager!=null;
        public bool IsHost=>manager!=null&&manager.IsHost;
        public bool HasOpponent=>opponent!=null;
        public string OpponentMonster=>opponent?.monster;
        public string OpponentName=>DisplayName(opponent?.playerName,"RIVAL");
        public static string DisplayName(string value,string fallback="Jugador")
        {
            if(string.IsNullOrWhiteSpace(value))return fallback;
            var clean=new StringBuilder(20);
            foreach(char c in value.Trim()){if(!char.IsControl(c))clean.Append(c);if(clean.Length>=20)break;}
            return clean.Length==0?fallback:clean.ToString();
        }
        static string LocalName=>DisplayName(PlayerPrefs.GetString("monster-pouch.player-name","Jugador"));
        Packet opponent;
        bool lockPending;
        int readyRevision,peerReadyRevision,lockId;
        public event Action Changed, Failed;
        public event Action<VerifiedOnlineMatchResult> MatchCompletedVerified;
        public event Action<string,bool> ErrorReported;
        public bool StatusIsError { get; private set; }
        // Device-local entry validation only; remote ownership is not trusted or
        // inferred by this casual room. The UI supplies the guest inventory rule.
        public Func<string,IEnumerable<string>,bool> LocalTeamValidator { get; set; }
        public Func<AbilityAccessSnapshot> LocalAbilitySnapshotProvider { get; set; }
        public Func<string,string> LocalMapSelector { get; set; }
        AbilityAccessSnapshot localAbilityAccess = AbilityAccessSnapshot.Unrestricted;
        MatchController match;
        NetworkManager manager;
        GameObject networkObject;
        string monster, balance, peerDigest, localDigest, selectedMap;
        string sharedMatchId, publishedMatchId;
        string[] whelps;
        ulong peer;
        int generation;
        bool closing, peerConnected, revealSent, peerRevealed, startSent, oldBackground;
        float deadline, heartbeatAt;

        public void Configure(MatchController owner)
        {
            match=owner;match.OnlineReadyRequested+=Ready;match.OnlineRoundFinished+=RoundFinished;match.OnlineExited+=Leave;
        }
        public Task Host(string selectedMonster,string[] selectedWhelps)=>ConnectManual(true,"",selectedMonster,selectedWhelps);
        public Task Join(string code,string selectedMonster,string[] selectedWhelps)=>ConnectManual(false,code,selectedMonster,selectedWhelps);
        Task ConnectManual(bool host,string code,string selectedMonster,string[] selectedWhelps)
        {
            if(DuelPending)return Task.CompletedTask;
            AutomaticDuel=false;DuelState=AutomaticDuelState.Idle;
            return Connect(host,code,selectedMonster,selectedWhelps);
        }
        public static bool ValidCode(string code)
        {
            if(string.IsNullOrWhiteSpace(code)||code.Trim().Length!=6)return false;
            foreach(char c in code.Trim().ToUpperInvariant())if(c<'A'||c>'Z')if(c<'0'||c>'9')return false;
            return true;
        }
        async Task Connect(bool host,string code,string selectedMonster,string[] selectedWhelps,string duelTicket=null)
        {
            if(Busy||Active)return;
            if(LocalTeamValidator!=null&&!LocalTeamValidator(selectedMonster,selectedWhelps))
            {SetErrorStatus("Actualiza el Brief con tus Whelps disponibles antes de entrar.",false);return;}
            try
            {
                localAbilityAccess = LocalAbilitySnapshotProvider != null ? LocalAbilitySnapshotProvider() : AbilityAccessSnapshot.Unrestricted;
                if (localAbilityAccess == null) throw new InvalidOperationException();
                AbilityAccessSnapshot.FromData(match.Config, localAbilityAccess.ToData());
            }
            catch (Exception error)
            {
                Debug.LogWarning("Online ability access unavailable: " + error.GetType().Name);
                SetErrorStatus("No se pudo leer tu colección. Inténtalo de nuevo.",false); return;
            }
            if(!host&&!ValidCode(code)){SetErrorStatus("El código debe tener 6 letras o números.",false);return;}
            if(string.IsNullOrWhiteSpace(Application.cloudProjectId))
            {SetErrorStatus("Las salas todavía no están habilitadas en esta versión.",false);return;}
            int attempt=++generation;LastVerifiedRound=0;LastVerifiedDigest=null;sharedMatchId=publishedMatchId=null;Busy=true;SetStatus("Conectando…");
            monster=selectedMonster;whelps=(string[])selectedWhelps.Clone();selectedMap=CardTableTheme.MapId(CardTableTheme.Current);
            if(LocalMapSelector!=null)selectedMap=LocalMapSelector(selectedMap);
            if(!MatchController.ValidOnlineMap(selectedMap)){Busy=false;SetErrorStatus("No se pudo seleccionar tu tablero.",false);return;}
            balance=ConnectionBalance();
            string connectionPhase="services.initialize";
            try
            {
                await UnityServices.InitializeAsync();if(attempt!=generation)return;
                connectionPhase="authentication.sign-in";
                if(!AuthenticationService.Instance.IsSignedIn)await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if(attempt!=generation)return;
                networkObject=new GameObject("Monster Pouch online connection");
                var transport=networkObject.AddComponent<UnityTransport>();transport.MaxPayloadSize=MaxBytes+1024;
                manager=networkObject.AddComponent<NetworkManager>();
                manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false,ConnectionApproval=true};
                string connectionVersion=Protocol+":"+balance+(duelTicket==null?"":":"+duelTicket);
                manager.NetworkConfig.ConnectionData=Encoding.UTF8.GetBytes(connectionVersion);
                manager.ConnectionApprovalCallback=(request,response)=>
                {
                    bool valid=request.ClientNetworkId==NetworkManager.ServerClientId||
                        !peerConnected&&request.Payload!=null&&Encoding.UTF8.GetString(request.Payload)==connectionVersion;
                    response.Approved=valid;response.CreatePlayerObject=false;response.Pending=false;
                    if(!valid)response.Reason="La sala está llena o las versiones y el balance no coinciden.";
                };
                manager.OnClientConnectedCallback+=Connected;
                manager.OnClientDisconnectCallback+=Disconnected;
                manager.OnTransportFailure+=TransportFailed;
                oldBackground=Application.runInBackground;Application.runInBackground=true;
                if(host)
                {
                    connectionPhase="relay.allocate";
                    var allocation=await RelayService.Instance.CreateAllocationAsync(1);
                    if(attempt!=generation)return;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation,"dtls"));
                    connectionPhase="relay.join-code";
                    string roomCode=await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                    if(attempt!=generation)return;
                    connectionPhase="transport.start-host";
                    if(!manager.StartHost())throw new InvalidOperationException("No se pudo crear la sala.");
                    Code=roomCode;Register();deadline=0;SetStatus("Sala creada. Comparte el código con tu amigo.");
                }
                else
                {
                    connectionPhase="relay.join";
                    var allocation=await RelayService.Instance.JoinAllocationAsync(code.Trim().ToUpperInvariant());
                    if(attempt!=generation)return;
                    transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation,"dtls"));
                    connectionPhase="transport.start-client";
                    if(!manager.StartClient())throw new InvalidOperationException("No se pudo conectar.");
                    Code=code.Trim().ToUpperInvariant();Register();deadline=Time.realtimeSinceStartup+30;
                    SetStatus("Entrando a la sala…");
                }
            }
            catch(Exception error)
            {
                if(attempt==generation)
                {
                    if(duelTicket!=null){error.Data["duel.phase"]=connectionPhase;duelConnectionError=error;Debug.LogWarning("[Duel] connection-failed "+SafeDuelFailure(error,DuelPhase));}
                    else Debug.LogWarning("Online room connection: "+error.GetType().Name+": "+error.Message);
                    Fail("No se pudo abrir la sala. Comprueba el código y tu conexión e inténtalo de nuevo.");
                }
            }
            finally{if(attempt==generation){Busy=false;Changed?.Invoke();}}
        }
        void Register()=>manager.CustomMessagingManager.RegisterNamedMessageHandler(Channel,Receive);
        void Connected(ulong id)
        {
            if(manager.IsHost)
            {
                if(id==NetworkManager.ServerClientId)return;
                if(peerConnected){manager.DisconnectClient(id);return;}
                peer=id;peerConnected=true;deadline=Time.realtimeSinceStartup+30;
            }
            else
            {
                peer=NetworkManager.ServerClientId;peerConnected=true;
                Send(new Packet{kind="hello",protocol=Protocol,balance=balance,monster=monster,whelps=whelps,map=selectedMap,playerName=LocalName,abilityAccess=localAbilityAccess.ToData()});
            }
        }
        void Send(Packet packet)
        {
            if(manager==null||!peerConnected)return;
            byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(packet));
            if(bytes.Length>MaxBytes)throw new InvalidOperationException("Mensaje demasiado grande.");
            using(var writer=new FastBufferWriter(bytes.Length+4,Allocator.Temp))
            {
                writer.WriteValueSafe(bytes.Length);writer.WriteBytesSafe(bytes);
                manager.CustomMessagingManager.SendNamedMessage(Channel,peer,writer,NetworkDelivery.ReliableFragmentedSequenced);
            }
        }
        void Receive(ulong sender,FastBufferReader reader)
        {
            if(closing||manager==null||sender!=peer||!peerConnected)return;
            try
            {
                if(reader.Length<4||reader.Length>MaxBytes+4)throw new InvalidOperationException();
                reader.ReadValueSafe(out int count);
                if(count<=0||count>MaxBytes||reader.Length-reader.Position!=count)throw new InvalidOperationException();
                var bytes=new byte[count];reader.ReadBytesSafe(ref bytes,count);
                var p=JsonUtility.FromJson<Packet>(Encoding.UTF8.GetString(bytes));
                if(p==null)throw new InvalidOperationException();
                if(p.kind=="ping")return;
                switch(p.kind)
                {
                    case "hello":
                        Require(manager.IsHost&&!match.IsOnline&&p.protocol==Protocol&&p.balance==balance);
                        Require(opponent==null);ValidateTeam(p);opponent=p;deadline=0;
                        Send(new Packet{kind="lobby",protocol=Protocol,balance=balance,monster=monster,whelps=whelps,map=selectedMap,playerName=LocalName,abilityAccess=localAbilityAccess.ToData()});
                        SetStatus("Tu rival está aquí. Pulsa Iniciar cuando estén preparados.");break;
                    case "lobby":
                        Require(!manager.IsHost&&!match.IsOnline&&opponent==null&&p.protocol==Protocol&&p.balance==balance);
                        ValidateTeam(p);opponent=p;deadline=0;SetStatus("Esperando a que el anfitrión inicie la partida.");break;
                    case "welcome":
                        Require(!manager.IsHost&&!match.IsOnline&&p.protocol==Protocol&&p.balance==balance);
                        ValidateTeam(p);Require(Guid.TryParseExact(p.matchId,"N",out _));Require(opponent!=null&&SameTeam(opponent,p));Require(LocalTeamValidator==null||LocalTeamValidator(monster,whelps));sharedMatchId=p.matchId;SharedMatchSeed=p.seed;publishedMatchId=null;if(!string.IsNullOrWhiteSpace(p.playerName)&&opponent!=null)opponent.playerName=p.playerName;Require(match.StartOnlineMatch(BoardSide.Red,monster,whelps,p.monster,p.whelps,p.seed,selectedMap,p.map,localAbilityAccess,AbilityAccessSnapshot.FromData(match.Config,p.abilityAccess)));
                        Started();break;
                    case "ready":
                        Require(match.IsOnline&&p.round==match.Round&&match.Phase==MatchPhase.Preparation&&p.revision>peerReadyRevision&&!revealSent);
                        peerReadyRevision=p.revision;match.SetOpponentReady(p.ready);
                        if(manager.IsHost&&lockPending){lockPending=false;deadline=0;match.LockPreparation(false);Send(new Packet{kind="unlock",round=match.Round});}
                        TryReveal();break;
                    case "lock":
                        Require(!manager.IsHost&&match.IsOnline&&p.round==match.Round&&!revealSent);
                        bool accept=match.OnlineReady&&match.OpponentReady&&p.peerRevision==readyRevision&&p.revision==peerReadyRevision;
                        if(accept)match.LockPreparation(true);
                        Send(new Packet{kind="locked",round=match.Round,ready=accept,lockId=p.lockId,revision=readyRevision,peerRevision=peerReadyRevision});break;
                    case "unlock":
                        Require(!manager.IsHost&&p.round==match.Round&&!revealSent);match.LockPreparation(false);break;
                    case "locked":
                        Require(manager.IsHost&&p.round==match.Round&&!revealSent);
                        if(!lockPending||p.lockId!=lockId)break;
                        lockPending=false;
                        if(!p.ready||p.revision!=peerReadyRevision||p.peerRevision!=readyRevision)
                        {match.LockPreparation(false);Send(new Packet{kind="unlock",round=match.Round});deadline=0;break;}
                        revealSent=true;deadline=Time.realtimeSinceStartup+20;
                        Send(new Packet{kind="reveal",round=match.Round,commands=match.PreparationCommands});break;
                    case "reveal":
                        Require(match.IsOnline&&p.round==match.Round&&!peerRevealed&&match.PreparationLocked);
                        Require(match.RevealOpponent(p.commands,p.round));peerRevealed=true;
                        if(manager.IsHost)
                        {
                            Require(revealSent);Send(new Packet{kind="start",round=match.Round});startSent=true;
                            Require(match.StartOnlineCombat(match.Round));deadline=0;
                        }
                        else
                        {
                            Require(!revealSent);revealSent=true;
                            Send(new Packet{kind="reveal",round=match.Round,commands=match.PreparationCommands});
                        }
                        break;
                    case "start":
                        Require(!manager.IsHost&&!startSent&&peerRevealed&&revealSent);startSent=true;
                        Require(match.StartOnlineCombat(p.round));deadline=0;break;
                    case "result":
                        Require(match.IsOnline&&p.matchId==sharedMatchId&&p.round==match.Round&&peerDigest==null&&ValidResultDigest(p.digest));
                        peerDigest=p.digest;CompareResults();break;
                    case "next":
                        Require(!manager.IsHost&&localDigest!=null&&peerDigest==localDigest&&p.round==match.Round);
                        Require(match.NextOnlineRound(p.round));ResetRound();break;
                    default:throw new InvalidOperationException("Mensaje desconocido.");
                }
            }
            catch(Exception error)
            {
                Debug.LogWarning("Online protocol rejected: "+error.Message);
                Fail("La partida no se pudo sincronizar. Vuelve a crear la sala.");
            }
        }
        void ValidateTeam(Packet p)
        {
            Require(MatchController.ValidOnlineMap(p.map));
            AbilityAccessSnapshot.FromData(match.Config,p.abilityAccess);
            Require(p.playerName==null||p.playerName.Length<=20);
            Require(p.monster!=null&&p.monster.Length<=64&&p.whelps!=null&&p.whelps.Length<=MatchConfig.MaxWhelpTypes);
            Require(match.Config.Get(p.monster)?.IsMonster==true&&match.Config.TryResolveWhelpSelection(p.whelps,out _,out _));
        }
        static bool SameTeam(Packet first,Packet second)
        {
            if(first.monster!=second.monster||first.map!=second.map||first.whelps.Length!=second.whelps.Length||
                JsonUtility.ToJson(first.abilityAccess)!=JsonUtility.ToJson(second.abilityAccess))return false;
            for(int i=0;i<first.whelps.Length;i++)if(first.whelps[i]!=second.whelps[i])return false;
            return true;
        }
        static void Require(bool condition){if(!condition)throw new InvalidOperationException("Estado o decisión no válidos.");}
        void Started(){Busy=false;deadline=0;ResetRound();SetStatus("Conectado. Prepara tu equipo.");if(AutomaticDuel)Debug.Log("[Duel] matched match="+sharedMatchId+" seed="+SharedMatchSeed+" side="+match.LocalSide);}
        void ResetRound(){peerDigest=localDigest=null;peerRevealed=revealSent=startSent=lockPending=false;readyRevision=peerReadyRevision=0;deadline=0;}
        void Ready(){Send(new Packet{kind="ready",round=match.Round,ready=match.OnlineReady,revision=++readyRevision});TryReveal();}
        void TryReveal()
        {
            if(manager==null||!manager.IsHost||!match.OnlineReady||!match.OpponentReady||revealSent||lockPending)return;
            lockPending=true;match.LockPreparation(true);deadline=Time.realtimeSinceStartup+20;
            Send(new Packet{kind="lock",round=match.Round,lockId=++lockId,revision=readyRevision,peerRevision=peerReadyRevision});
        }
        public void StartRoom()
        {
            if(!IsHost||!HasOpponent||match.IsOnline||Busy)return;
            if(LocalTeamValidator!=null&&!LocalTeamValidator(monster,whelps))
            {SetErrorStatus("Tu Brief cambió. Vuelve a entrar a la sala con el equipo actualizado.",false);return;}
            int seed=Guid.NewGuid().GetHashCode();if(seed==0)seed=1;
            SharedMatchSeed=seed;
            sharedMatchId=Guid.NewGuid().ToString("N");publishedMatchId=null;
            if(!match.StartOnlineMatch(BoardSide.Blue,monster,whelps,opponent.monster,opponent.whelps,seed,selectedMap,opponent.map,localAbilityAccess,AbilityAccessSnapshot.FromData(match.Config,opponent.abilityAccess)))return;
            Send(new Packet{kind="welcome",protocol=Protocol,balance=balance,matchId=sharedMatchId,seed=seed,monster=monster,whelps=whelps,map=selectedMap,playerName=LocalName,abilityAccess=localAbilityAccess.ToData()});
            Started();
        }
        void RoundFinished()
        {
            localDigest=match.OnlineDigest();Send(new Packet{kind="result",matchId=sharedMatchId,round=match.Round,digest=localDigest});
            deadline=Time.realtimeSinceStartup+60;CompareResults();
        }
        static bool ValidResultDigest(string value)
        {
            if(value==null||value.Length!=64)return false;
            foreach(char c in value)if(!(c>='0'&&c<='9'||c>='A'&&c<='F'||c>='a'&&c<='f'))return false;
            return true;
        }
        void CompareResults()
        {
            if(localDigest==null||peerDigest==null||!ValidResultDigest(localDigest)||!ValidResultDigest(peerDigest))return;
            if(localDigest!=peerDigest){Fail("Los resultados no coinciden. La partida se ha detenido.");return;}
            LastVerifiedRound=match.Round;LastVerifiedDigest=localDigest;deadline=0;
            // Both peers agree on the final BO5. This confirms local simulation,
            // not server authority; no partial rounds or disconnects are scored.
            bool finalScore=match.PlayerWins==3&&match.BotWins>=0&&match.BotWins<3||
                match.BotWins==3&&match.PlayerWins>=0&&match.PlayerWins<3;
            if(!match.IsOnline||match.IsTraining||match.Phase!=MatchPhase.MatchResult||match.WinsToWin!=3||
                !finalScore||!Guid.TryParseExact(sharedMatchId,"N",out _)||string.IsNullOrWhiteSpace(match.SessionId)||
                localDigest.Length!=64||publishedMatchId==sharedMatchId)return;
            publishedMatchId=sharedMatchId;
            MatchCompletedVerified?.Invoke(new VerifiedOnlineMatchResult{
                MatchId=sharedMatchId,LocalSessionId=match.SessionId,
                OpponentId="online-peer:"+sharedMatchId+":"+(match.LocalSide==BoardSide.Blue?"red":"blue"),
                PlayerWins=match.PlayerWins,OpponentWins=match.BotWins,Digest=localDigest});
        }
        void Update()
        {
            if(manager==null||closing)return;
            if(deadline>0&&Time.realtimeSinceStartup>deadline){Fail("El rival no respondió. Intenta entrar otra vez.");return;}
            if(peerConnected&&Time.realtimeSinceStartup>heartbeatAt){heartbeatAt=Time.realtimeSinceStartup+3;Send(new Packet{kind="ping"});}
            if(manager.IsHost&&match.IsOnline&&localDigest!=null&&localDigest==peerDigest&&
               match.Phase==MatchPhase.RoundResult&&match.RoundResultElapsed>=match.RoundTransitionDuration)
            {
                int round=match.Round;Send(new Packet{kind="next",round=round});
                if(!match.NextOnlineRound(round)){Fail("No se pudo iniciar la siguiente ronda.");return;}ResetRound();
            }
        }
        void SetStatus(string text){StatusIsError=false;Status=text;if(!destroyed)Changed?.Invoke();}
        void SetErrorStatus(string text,bool connection){StatusIsError=true;Status=text;if(!destroyed){Changed?.Invoke();ErrorReported?.Invoke(text,connection);}}
        void Disconnected(ulong id){if(!closing&&peerConnected&&(id==peer||manager!=null&&!manager.IsHost))Fail("Se perdió la conexión con el rival.");}
        void TransportFailed(){if(!closing)Fail("Se perdió la conexión. Comprueba tu acceso a Internet.");}
        void Fail(string message)
        {
            if(DuelPending&&!match.IsOnline){duelConnectionFailed=true;DisconnectRoom();return;}
            Leave();SetErrorStatus(message,true);Failed?.Invoke();
        }
        public void Leave()
        {
            CancelAutomaticDuel();
            DisconnectRoom();
        }
        void DisconnectRoom()
        {
            if(closing)return;closing=true;++generation;Busy=false;
            if(manager!=null)
            {
                manager.OnClientConnectedCallback-=Connected;manager.OnClientDisconnectCallback-=Disconnected;
                manager.OnTransportFailure-=TransportFailed;
                manager.Shutdown(true);
                // Transport must remain alive until Netcode finishes its shutdown tick.
                networkObject.AddComponent<OnlineConnectionCleanup>().Manager=manager;
                Application.runInBackground=oldBackground;
            }
            manager=null;networkObject=null;Code="";peerConnected=false;opponent=null;ResetRound();
            if(match!=null&&match.IsOnline)match.Menu();
            closing=false;SetStatus("Crea una sala o escribe el código de tu amigo.");
        }
        void OnApplicationPause(bool paused)
        {
            if(paused&&DuelPending){Leave();return;}
            if(paused&&(Active||Busy))Fail("Se cerró la sala al salir del juego. Puedes volver a crearla.");
        }
        void OnDestroy()
        {
            destroyed=true;
            if(match!=null){match.OnlineReadyRequested-=Ready;match.OnlineRoundFinished-=RoundFinished;match.OnlineExited-=Leave;}
            Leave();
        }
    }

    sealed class OnlineConnectionCleanup : MonoBehaviour
    {
        public NetworkManager Manager;
        void LateUpdate()
        {
            if(Manager==null||!Manager.IsListening&&!Manager.ShutdownInProgress)Destroy(gameObject);
        }
    }
}
