using System;
using System.Collections;
using System.Reflection;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Local;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class October8NetworkPlayTests
{
    OnlineRoom host,guest;MatchController hm,gm;MatchConfig config;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static void Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
    OnlineRoom Make(bool isHost)
    {
        var root=new GameObject(isHost?"test host":"test guest");var board=root.AddComponent<BoardManager>();board.BuildBoard();
        var mapper=root.AddComponent<BoardWorldMapper>();mapper.Configure(board,root.transform,new Vector2(1,-1),Vector2.zero);
        var match=root.AddComponent<MatchController>();match.Configure(config,board,mapper);match.enabled=false;
        var room=root.AddComponent<OnlineRoom>();room.Configure(match);
        var go=new GameObject(isHost?"host transport":"guest transport");var transport=go.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1",23891,"127.0.0.1");
        var manager=go.AddComponent<NetworkManager>();manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,EnableSceneManagement=false};
        Set(room,"manager",manager);Set(room,"networkObject",go);Set(room,"monster",isHost?"popow":"bugaloo");
        Set(room,"whelps",new[]{"aky","tsu"});Set(room,"balance","local-test");Set(room,"selectedMap",isHost?"classic":"royal");
        Set(room,"oldBackground",Application.runInBackground);
        manager.OnClientConnectedCallback+=id=>Call(room,"Connected",id);
        manager.OnClientDisconnectCallback+=id=>Call(room,"Disconnected",id);
        Assert.IsTrue(isHost?manager.StartHost():manager.StartClient());Call(room,"Register");
        if(isHost)hm=match;else gm=match;
        return room;
    }
    IEnumerator WaitUntil(Func<bool> condition)
    {
        float end=Time.realtimeSinceStartup+8;
        while(!condition()&&Time.realtimeSinceStartup<end)yield return null;
        Assert.IsTrue(condition(),"Timed out: "+host?.Status+" / "+guest?.Status);
    }
    [UnityTest] public IEnumerator LobbyWaitsForHostAndCancellingReadyDoesNotBreakRoundHandshake()
    {
        config=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig").CreateRuntimeConfig();
        host=Make(true);guest=Make(false);
        yield return WaitUntil(()=>host.HasOpponent&&guest.HasOpponent);
        Assert.IsFalse(hm.IsOnline);Assert.IsFalse(gm.IsOnline);
        host.StartRoom();yield return WaitUntil(()=>hm.IsOnline&&gm.IsOnline);
        hm.ToggleReady();yield return WaitUntil(()=>gm.OpponentReady);
        hm.ToggleReady();yield return WaitUntil(()=>!gm.OpponentReady);
        Assert.AreEqual(MatchPhase.Preparation,hm.Phase);
        // Cancel in the same frame as the host receives the other ready: the
        // revision barrier must reject the stale lock and reveal no orders.
        hm.ToggleReady();gm.ToggleReady();gm.ToggleReady();
        yield return new WaitForSecondsRealtime(.35f);
        Assert.IsFalse(gm.OnlineReady);Assert.IsFalse(hm.OpponentReady);
        Assert.IsFalse(hm.PreparationLocked);Assert.AreEqual(MatchPhase.Preparation,hm.Phase);
        Assert.AreEqual(0f,typeof(OnlineRoom).GetField("deadline",Private).GetValue(host),"A cancelled lock must not leave a disconnect timeout running.");
        gm.ToggleReady();yield return WaitUntil(()=>hm.Phase==MatchPhase.Combat&&gm.Phase==MatchPhase.Combat);
        for(int i=0;i<405;i++){hm.Advance(.1f);gm.Advance(.1f);}
        yield return WaitUntil(()=>host.LastVerifiedRound==1&&guest.LastVerifiedRound==1);
        Assert.AreEqual(host.LastVerifiedDigest,guest.LastVerifiedDigest);
        // The same exit used by the battle menu must close the real transport
        // and return both peers to the menu without disposed-transport errors.
        hm.Menu();yield return WaitUntil(()=>!host.Active&&!guest.Active);
        Assert.IsFalse(hm.IsOnline);Assert.IsFalse(gm.IsOnline);
        Assert.AreEqual(MatchPhase.Menu,hm.Phase);Assert.AreEqual(MatchPhase.Menu,gm.Phase);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        // Shut down callbacks before their peers disappear.
        if(host!=null)host.enabled=false;if(guest!=null)guest.enabled=false;
        if(host!=null){host.Leave();UnityEngine.Object.Destroy(host.gameObject);}
        if(guest!=null){guest.Leave();UnityEngine.Object.Destroy(guest.gameObject);}
        if(config!=null)UnityEngine.Object.Destroy(config);yield return null;yield return null;
    }
}
