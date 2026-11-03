using System;
using System.IO;
using MonsterPouch.Gameplay.Progression;

namespace MonsterPouch.Gameplay.Profiles
{
    public sealed partial class LocalGuestProfile
    {
        public TrophyLadderState TrophyLadder;
        public long Trophies=>TrophyLadder?.CurrentTrophies??0;
        public long HighestTrophies=>TrophyLadder?.HighestTrophies??0;

        internal void InitializeTrophyLadder()=>TrophyLadder=GuestTrophyLadder.CreateEngine().CreateState(ProfileId);

        internal void UpgradeTrophyLadder()
        {
            var migration=GuestTrophyLadder.CreateEngine().UpgradeState(TrophyLadder);
            if(migration.Status!=TrophyLadderStatus.Ready)
                throw new InvalidDataException("Invalid trophy migration: "+migration.Reason);
            TrophyLadder=migration.State;
        }

        void ValidateTrophyLadder()
        {
            if(TrophyLadder==null||TrophyLadder.PlayerId!=ProfileId)
                throw new InvalidDataException("Missing trophy state or a different account identity.");
            var result=GuestTrophyLadder.CreateEngine().PreviewStage(TrophyLadder);
            if(result.Status!=TrophyLadderStatus.Ready)
                throw new InvalidDataException("Invalid trophy history: "+result.Reason);
            foreach(var match in TrophyLadder.Matches)
            {
                var receipt=match.Receipt;
                if(receipt.SourceModeId!=GuestTrophyRules.OnlineMode||
                    !GuestTrophyRules.ValidMatchId(receipt.MatchId)||
                    !GuestTrophyRules.ValidOpponent(receipt.OpponentPlayerId)||
                    receipt.OpponentTrophiesBefore!=0||
                    !GuestTrophyRules.ValidEvidence(receipt.EligibilityEvidenceId,receipt.MatchId,receipt.Outcome))
                    throw new InvalidDataException("Invalid local online trophy receipt.");
            }
        }
    }
}
