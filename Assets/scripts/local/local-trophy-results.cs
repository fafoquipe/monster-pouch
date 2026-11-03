using System;
using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;

namespace MonsterPouch.Local
{
    public sealed class VerifiedOnlineMatchResult
    {
        public string MatchId,LocalSessionId,OpponentId,Digest;
        public int PlayerWins,OpponentWins;
    }

    public sealed partial class LocalGameUI
    {
        sealed class PendingTrophyResult
        {
            public string ProfileId;
            public GuestTrophyResultRequest Request;
        }
        readonly List<PendingTrophyResult> pendingTrophyResults=new List<PendingTrophyResult>();
        readonly HashSet<string> observedTrophyMatches=new HashSet<string>(StringComparer.Ordinal);
        public bool TrophySavePending=>pendingTrophyResults.Count>0;

        void RecordVerifiedTrophyMatch(VerifiedOnlineMatchResult result)
        {
            if(result==null||online==null||Match==null||!Match.IsOnline||Match.IsTraining||
                Match.Phase!=MatchPhase.MatchResult||Match.WinsToWin!=3||result.LocalSessionId!=Match.SessionId||
                result.PlayerWins!=Match.PlayerWins||result.OpponentWins!=Match.BotWins||
                online.LastVerifiedRound!=Match.Round||online.LastVerifiedDigest!=result.Digest||
                !Guid.TryParseExact(result.MatchId,"N",out _)||guestProfile==null||
                !observedTrophyMatches.Add(result.MatchId))return;
            pendingTrophyResults.Add(new PendingTrophyResult{
                ProfileId=guestProfile.ProfileId,
                Request=new GuestTrophyResultRequest{
                    MatchId=result.MatchId,OpponentId=result.OpponentId,SourceModeId="online",
                    VerifiedDigest=result.Digest,PlayerWins=result.PlayerWins,OpponentWins=result.OpponentWins}});
            RetryTrophyResults();
        }

        // A failed write remains queued when returning to the lobby. Never infer
        // awards from XP history, bot wins or a single round, and never import QA.
        public void RetryTrophyResults()
        {
            if(guestProfileStore==null||pendingTrophyResults.Count==0)return;
            while(pendingTrophyResults.Count>0)
            {
                try
                {
                    var profile=guestProfileStore.Reload();
                    if(guestProfileStore.NeedsRecovery||profile.ProfileId!=pendingTrophyResults[0].ProfileId)return;
                    var result=guestProfileStore.RecordTrophyResult(pendingTrophyResults[0].Request);
                    if(result.Status!=GuestTrophyResultStatus.Applied&&result.Status!=GuestTrophyResultStatus.AlreadyApplied)return;
                    if(result.Profile!=null)guestProfile=result.Profile;
                    pendingTrophyResults.RemoveAt(0);
                    RefreshTrophyBoardAccess();
                    if(Match.Phase==MatchPhase.Menu)rebuild=true;
                }
                catch(Exception error) when(VendingStorageError(error)){return;}
            }
        }
    }
}
