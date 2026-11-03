#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Opt-in development harness; dormant unless explicitly invoked by validation tools.
    public sealed class OnlineSmokeDriver : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public string status,code,digest,error;
            public int verifiedRound;
            public bool passed;
        }
        OnlineRoom room;MatchController match;string reportPath;float deadline,writeAt;int prepared;
        bool started,host,finished;string joinCode;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FromCommandLine()
        {
            string[] args=Environment.GetCommandLineArgs();
            int code=Array.IndexOf(args,"-mp-code"),report=Array.IndexOf(args,"-mp-report");
            if(code<0||report<0||code+1>=args.Length||report+1>=args.Length)return;
            Begin(false,args[code+1],args[report+1]);
        }
        public static void Begin(bool host,string code,string reportPath)
        {
            var go=new GameObject("Online validation driver");var driver=go.AddComponent<OnlineSmokeDriver>();
            driver.host=host;driver.joinCode=code;driver.reportPath=reportPath;driver.deadline=Time.realtimeSinceStartup+240;
        }
        async void Update()
        {
            if(finished)return;
            if(!started)
            {
                var ui=FindFirstObjectByType<LocalGameUI>();if(ui?.Match==null)return;
                started=true;match=ui.Match;room=ui.GetComponent<OnlineRoom>();
                if(host)await room.Host("bugaloo",new[]{"blotan","tsu"});
                else await room.Join(joinCode,"sepora",new[]{"blotan","tsu"});
                if(!room.Active&&!match.IsOnline){Write(room.Status);finished=true;}
                return;
            }
            if(room==null)return;
            if(room.IsHost&&room.HasOpponent&&!match.IsOnline)room.StartRoom();
            if(Time.realtimeSinceStartup>deadline){Write("Validation timed out.");finished=true;room.Leave();return;}
            if(match.IsOnline&&match.Phase==MatchPhase.Preparation&&prepared!=match.Round)
            {
                prepared=match.Round;
                for(int i=0;i<match.Player.Offers.Count;i++)
                {
                    var offer=match.Player.Offers[i];if(offer==null||match.Player.GetOfferCost(i)>match.Player.Coins)continue;
                    var cell=match.Config.FormationCells(match.Config.Get(offer.UnitId),match.LocalSide)
                        .Select(p=>match.Board.GetCell(p.x,p.y)).FirstOrDefault(c=>c!=null&&c.OccupiedBy==null);
                    if(cell!=null)match.BuyAndPlace(i,offer.Token,cell);
                }
                match.Ready();
            }
            if(Time.realtimeSinceStartup>=writeAt){writeAt=Time.realtimeSinceStartup+1;Write(null);}
            if(room.LastVerifiedRound>=2){Write(null);finished=true;}
        }
        void Write(string error)
        {
            string directory=Path.GetDirectoryName(reportPath);if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
            var report=new Report{status=room?.Status,code=room?.Code,digest=room?.LastVerifiedDigest,
                verifiedRound=room?.LastVerifiedRound??0,passed=room!=null&&room.LastVerifiedRound>=2,error=error};
            File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
        }
    }
}
#endif
